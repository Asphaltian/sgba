using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Emulotl;

public sealed partial class EmulatorComponent
{
	private readonly struct FramePacket( short[] audio, int audioSamples, byte[] saveData )
	{
		public readonly short[] Audio = audio;
		public readonly int AudioSamples = audioSamples;
		public readonly byte[] SaveData = saveData;
	}

	private enum EmulatorCoreThreadState
	{
		Initialized = -1,
		Running = 0,
		Request,
		Interrupted,
		Paused,
		Crashed,
		Interrupting,
		Exiting,
		Shutdown
	}

	[Flags]
	private enum EmulatorCoreThreadRequest
	{
		None = 0,
		Pause = 1,
		Wait = 2,
		Reset = 4,
		RunOn = 8,
		Crashed = 16,
		RewindEmpty = 32
	}

	private sealed class EmulatorCoreThread
	{
		public IEmulatorCore Core { get; }
		public EmulatorCoreSync Sync { get; } = new();
		public ConcurrentQueue<FramePacket> PostedFrames { get; } = new();

		private readonly ConcurrentQueue<Action<IEmulatorCore>> _runQueue = new();
		private readonly SemaphoreSlim _stateOnThreadSignal = new( 0, 1 );
		private readonly object _stateLock = new();
		private readonly object _coreLock;
		private readonly Func<ushort> _readInputKeysActive;
		private readonly Func<int> _readTouchState;
		private readonly Action<string> _logError;
		private readonly Action _resetCallback;
		private readonly short[][] _audioBuffers;
		private CancellationTokenSource _cts;
		private Task _workerTask;
		private EmulatorCoreThreadState _state = EmulatorCoreThreadState.Initialized;
		private EmulatorCoreThreadRequest _requested;
		private int _audioBufferIndex;
		private bool _waitPrologueActive;
		private bool _waitPrologueVideoFrameWait;
		private bool _waitPrologueAudioWait;

		public EmulatorCoreThread( IEmulatorCore core, object coreLock, Func<ushort> readInputKeysActive, Func<int> readTouchState, Action<string> logError, Action resetCallback )
		{
			Core = core;
			_coreLock = coreLock;
			_readInputKeysActive = readInputKeysActive;
			_readTouchState = readTouchState;
			_logError = logError;
			_resetCallback = resetCallback;
			_audioBuffers = new short[4][];

			int audioBufferSize = core.Profile.AudioSamplesPerFrame * core.Profile.AudioChannels;
			for ( int i = 0; i < _audioBuffers.Length; i++ )
				_audioBuffers[i] = new short[audioBufferSize];
		}

		public void Start()
		{
			if ( _cts != null )
				return;

			_cts = new CancellationTokenSource();
			ChangeState( EmulatorCoreThreadState.Running );
			_workerTask = GameTask.RunInThreadAsync( Run );
			_ = ObserveWorkerTaskAsync( _workerTask );
		}

		public void End()
		{
			ChangeState( EmulatorCoreThreadState.Exiting );
			_cts?.Cancel();
			Sync.SetAudioSync( false );
			Sync.SetVideoSync( false );
			Sync.ForceFrame();
			SignalStateOnThread();
			_cts = null;
		}

		public void Reset()
		{
			SendRequest( EmulatorCoreThreadRequest.Reset );
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		public void Pause()
		{
			SendRequest( EmulatorCoreThreadRequest.Pause );
			WaitPrologue();
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		public void Unpause()
		{
			CancelRequest( EmulatorCoreThreadRequest.Pause );
			WaitEpilogue();
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		public void SetPaused( bool paused )
		{
			if ( paused )
				Pause();
			else
				Unpause();
		}

		public void RunFunction( Action<IEmulatorCore> run )
		{
			if ( run == null || _cts == null )
				return;

			_runQueue.Enqueue( run );
			SendRequest( EmulatorCoreThreadRequest.RunOn );
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		private async Task ObserveWorkerTaskAsync( Task workerTask )
		{
			try
			{
				await workerTask;
			}
			catch ( OperationCanceledException ) { }
			catch ( ObjectDisposedException ) { }
			catch ( Exception ex )
			{
				try
				{
					_logError?.Invoke( $"Emulation worker task error: {ex.Message}\n{ex.StackTrace}" );
				}
				catch { }
			}
			finally
			{
				if ( _workerTask == workerTask )
					_workerTask = null;
			}
		}

		private async Task Run()
		{
			CancellationTokenSource cts = _cts;
			if ( cts == null )
				return;

			CancellationToken token = cts.Token;

			try
			{
				while ( !token.IsCancellationRequested )
				{
					EmulatorCoreThreadRequest pendingRequests = TakePendingRequests();
					RunPendingRequests( pendingRequests );

					if ( IsRequested( EmulatorCoreThreadRequest.Pause | EmulatorCoreThreadRequest.Wait | EmulatorCoreThreadRequest.Crashed | EmulatorCoreThreadRequest.RewindEmpty ) )
					{
						ChangeState( EmulatorCoreThreadState.Paused );
						await _stateOnThreadSignal.WaitAsync( SyncWaitMilliseconds, token );
						continue;
					}

					ChangeState( EmulatorCoreThreadState.Running );
					FramePacket frame = RunFrame( token );
					PostedFrames.Enqueue( frame );
					await Sync.ProduceAudioAsync( frame.AudioSamples, token );
					await Sync.PostFrameAsync( token );
					await GameTask.Yield();
				}
			}
			catch ( OperationCanceledException ) { }
			catch ( ObjectDisposedException ) { }
			catch ( Exception ex )
			{
				SendRequest( EmulatorCoreThreadRequest.Crashed );
				ChangeState( EmulatorCoreThreadState.Crashed );
				try
				{
					_logError?.Invoke( $"Emulation worker error: {ex.Message}\n{ex.StackTrace}" );
				}
				catch { }
			}
			finally
			{
				ChangeState( EmulatorCoreThreadState.Shutdown );
			}
		}

		private FramePacket RunFrame( CancellationToken token )
		{
			short[] audio;
			int audioSamples;
			byte[] saveData = null;

			lock ( _coreLock )
			{
				Core.SetButtons( 0, _readInputKeysActive() );

				int ts = _readTouchState();
				Core.SetTouch( (ts & unchecked((int)0x80000000)) != 0, (ts >> 8) & 0x1FF, ts & 0xFF );

				Core.RunFrame();

				if ( token.IsCancellationRequested )
					return new FramePacket( null, 0, null );

				int bufferIndex = _audioBufferIndex;
				_audioBufferIndex = (bufferIndex + 1) & 3;
				audio = _audioBuffers[bufferIndex];

				audioSamples = Core.Audio.SamplesWritten;
				if ( audioSamples > 0 )
					Buffer.BlockCopy( Core.Audio.OutputBuffer, 0, audio, 0, audioSamples * Core.Profile.AudioChannels * sizeof( short ) );

				if ( Core.SaveData.ConsumeDirty() && Core.SaveData.Data.Length > 0 )
					saveData = [.. Core.SaveData.Data];
			}

			return new FramePacket( audio, audioSamples, saveData );
		}

		private EmulatorCoreThreadRequest TakePendingRequests()
		{
			lock ( _stateLock )
			{
				EmulatorCoreThreadRequest pendingRequests = _requested;
				_requested &= EmulatorCoreThreadRequest.Pause | EmulatorCoreThreadRequest.Wait | EmulatorCoreThreadRequest.Crashed | EmulatorCoreThreadRequest.RewindEmpty;
				return pendingRequests;
			}
		}

		private void RunPendingRequests( EmulatorCoreThreadRequest pendingRequests )
		{
			if ( (pendingRequests & EmulatorCoreThreadRequest.Reset) != 0 )
			{
				lock ( _coreLock )
				{
					Core.Reset();
				}
				_resetCallback?.Invoke();
			}

			if ( (pendingRequests & EmulatorCoreThreadRequest.RunOn) != 0 )
				RunPendingFunctions();
		}

		private void RunPendingFunctions()
		{
			while ( _runQueue.TryDequeue( out Action<IEmulatorCore> run ) )
			{
				try
				{
					lock ( _coreLock )
					{
						run( Core );
					}
				}
				catch ( Exception ex )
				{
					EmuLog.Write( LogCategory.Core, LogLevel.Error, ex.Message );
				}
			}
		}

		private void WaitPrologue()
		{
			if ( _waitPrologueActive )
				return;

			Sync.WaitPrologue( out _waitPrologueVideoFrameWait, out _waitPrologueAudioWait );
			_waitPrologueActive = true;
		}

		private void WaitEpilogue()
		{
			if ( !_waitPrologueActive )
				return;

			Sync.WaitEpilogue( _waitPrologueVideoFrameWait, _waitPrologueAudioWait );
			_waitPrologueActive = false;
		}

		private bool IsRequested( EmulatorCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				return (_requested & request) != 0;
			}
		}

		private void SendRequest( EmulatorCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				_requested |= request;
				if ( _state is EmulatorCoreThreadState.Running or EmulatorCoreThreadState.Paused or EmulatorCoreThreadState.Crashed )
					_state = EmulatorCoreThreadState.Request;
			}
		}

		private void CancelRequest( EmulatorCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				_requested &= ~request;
				if ( _state == EmulatorCoreThreadState.Request && _requested == EmulatorCoreThreadRequest.None )
					_state = EmulatorCoreThreadState.Running;
			}
		}

		private void ChangeState( EmulatorCoreThreadState state )
		{
			lock ( _stateLock )
			{
				_state = state;
			}
		}

		private void SignalStateOnThread()
		{
			try { _stateOnThreadSignal.Release(); }
			catch ( SemaphoreFullException ) { }
		}
	}
}
