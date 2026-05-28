using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Rendering;

namespace sGBA;

public sealed partial class EmulatorComponent : Component
{
	[Property, Title( "ROM Path" ), FilePath( Extension = "gba" )]
	public string RomPath { get; set; }

	public static EmulatorComponent Current { get; private set; }
	public Gba Core => _coreThread?.Core;
	public Texture ScreenTexture { get; private set; }
	public bool IsReady { get; private set; }
	public string ErrorMessage { get; private set; }

	private SoundStream _audioStream;
	private SoundHandle _soundHandle;
	private string _savePath;
	private CameraComponent _camera;
	private object _coreLock = new();
	private GbaCoreThread _coreThread;
	private Stopwatch _videoClock;
	private double _nextVideoFrameDue;

	private const int AllKeysReleased = 0x03FF;
	private const int AudioPrefillFrames = 2;
	private const int AudioHighWaterFrames = 3;
	private const int GbaClockRate = 1 << 24;
	private const int GbaCyclesPerFrame = 228 * 1232;
	private const double GbaNativeFps = (double)GbaClockRate / GbaCyclesPerFrame;
	private const double GbaFrameTime = (double)GbaCyclesPerFrame / GbaClockRate;
	private const int MaxPendingFrames = 3;
	private const int SyncWaitMilliseconds = 50;
	private const int WorkerWatchdogYieldMilliseconds = 500;
	private const float StickDeadzone = 0.3f;

	private int _inputKeys = AllKeysReleased;
	private bool _videoFramePending;
	private bool _paused;
	private int _inputCooldown;
	private bool _initCoreOnUpdate;
	private int _initDeferFrames;
	private string _stateBasePath;
	private bool _appliedReproduceClassicFeel;

	private readonly struct FramePacket( short[] audio, int audioSamples, byte[] saveData )
	{
		public readonly short[] Audio = audio;
		public readonly int AudioSamples = audioSamples;
		public readonly byte[] SaveData = saveData;
	}

	private enum GbaCoreThreadState
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
	private enum GbaCoreThreadRequest
	{
		None = 0,
		Pause = 1,
		Wait = 2,
		Reset = 4,
		RunOn = 8,
		Crashed = 16,
		RewindEmpty = 32
	}

	protected override void OnStart()
	{
		Current = this;
		GbaLog.SetBackend( LogBackend );
		_camera = Scene.Camera;
		if ( !string.IsNullOrEmpty( RomPath ) )
			_initCoreOnUpdate = true;
	}

	public void Restart( string romPath )
	{
		TearDownCore();
		RomPath = romPath;
		IsReady = false;
		ErrorMessage = null;
		_initCoreOnUpdate = false;
		_initDeferFrames = 0;
		InitCore();
	}

	public void Unload()
	{
		TearDownCore();
		RomPath = null;
		_initCoreOnUpdate = false;
		_initDeferFrames = 0;
	}

	private void TearDownCore()
	{
		GbaCoreThread coreThread = _coreThread;
		_coreThread = null;
		coreThread?.End();

		lock ( CoreLock )
		{
			Gba core = coreThread?.Core;
			if ( core?.Savedata != null && core.Savedata.Data.Length > 0 && _savePath != null )
				FileSystem.Data.WriteAllBytes( _savePath, core.Savedata.Data );

			if ( _camera.IsValid() && core?.Video?.RenderCommandList != null )
				_camera.RemoveCommandList( core.Video.RenderCommandList );

			core?.Video?.DisposeGpu();
			ScreenTexture = null;
		}

		if ( _soundHandle is { IsValid: true } )
			_soundHandle.Volume = 0;
		_audioStream?.Dispose();
		_audioStream = null;
		_videoClock = null;
		_nextVideoFrameDue = 0;
		_videoFramePending = false;
		_inputCooldown = 0;
		_initDeferFrames = 0;
		_paused = false;
		Interlocked.Exchange( ref _inputKeys, AllKeysReleased );
		IsReady = false;
	}

	private void InitCore()
	{
		try
		{
			if ( !FileSystem.Mounted.FileExists( RomPath ) && !FileSystem.Data.FileExists( RomPath ) )
			{
				ErrorMessage = $"ROM not found: {RomPath}";
				GbaLog.Write( LogCategory.GBA, LogLevel.Error, ErrorMessage );
				return;
			}

			BaseFileSystem romFs = FileSystem.Mounted.FileExists( RomPath ) ? FileSystem.Mounted : FileSystem.Data;
			byte[] romData = romFs.ReadAllBytes( RomPath ).ToArray();
			if ( romData.Length < 192 )
			{
				ErrorMessage = "ROM file is too small to be a valid GBA ROM.";
				GbaLog.Write( LogCategory.GBA, LogLevel.Error, ErrorMessage );
				return;
			}

			Gba core = new();
			core.LoadRom( romData );

			_savePath = "saves/" + System.IO.Path.GetFileNameWithoutExtension( RomPath ) + ".sav";
			if ( FileSystem.Data.FileExists( _savePath ) )
			{
				byte[] saveData = FileSystem.Data.ReadAllBytes( _savePath ).ToArray();
				core.Savedata.Load( saveData );
			}

			core.Reset();
			core.Video.InitGpu( scale: ComputeAutoScale() );
			core.Video.SetReproduceClassicFeel( GamePreferences.ReproduceClassicFeel );
			_appliedReproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
			ScreenTexture = core.Video.OutputTexture;

			if ( _camera.IsValid() && core.Video.RenderCommandList != null )
				_camera.AddCommandList( core.Video.RenderCommandList, Stage.AfterOpaque, 0 );

			_stateBasePath = "states/" + System.IO.Path.GetFileNameWithoutExtension( RomPath );

			try { InitAudioStream(); }
			catch ( Exception audioEx ) { GbaLog.Write( LogCategory.GBAAudio, LogLevel.Warn, $"Audio init failed: {audioEx.Message}" ); }

			_coreThread = new GbaCoreThread( core, CoreLock, ReadInputKeysActive, LogCoreThreadError, LogCoreThreadReset );
			_coreThread.Sync.LoadCoreOptions( audioSync: true, videoSync: true, fpsTarget: (float)GbaNativeFps );
			_coreThread.Sync.AudioHighWater = GbaAudio.SamplesPerFrame * AudioHighWaterFrames;
			ResetVideoClock();
			_coreThread.Start();

			IsReady = true;
		}
		catch ( Exception ex )
		{
			ErrorMessage = $"Failed to load ROM: {ex.Message}";
			GbaLog.Write( LogCategory.GBA, LogLevel.Fatal, ErrorMessage );
		}
	}

	private void InitAudioStream()
	{
		if ( _audioStream != null )
		{
			if ( _soundHandle is { IsValid: true } )
				_soundHandle.Volume = 0;
			_audioStream.Dispose();
			_audioStream = null;
		}

		_audioStream = new SoundStream( GbaAudio.SampleRate, 2 );
		_audioStream.WriteData( new short[GbaAudio.SamplesPerFrame * 2 * AudioPrefillFrames] );
		_soundHandle = _audioStream.Play( volume: 1.0f );
		_soundHandle.SpacialBlend = 0f;
		_soundHandle.Occlusion = false;
		_soundHandle.DistanceAttenuation = false;
		_soundHandle.AirAbsorption = false;
		_soundHandle.Transmission = false;
		_soundHandle.Stop( float.MaxValue );
	}

	private static int ComputeAutoScale()
	{
		int sw = Screen.Width > 0 ? (int)Screen.Width : 1920;
		int sh = Screen.Height > 0 ? (int)Screen.Height : 1080;
		return Math.Clamp( Math.Min( sw / 240, sh / 160 ), 1, 8 );
	}

	protected override void OnUpdate()
	{
		if ( !StartCoreWhenReady() )
			return;

		RescaleGpuIfNeeded();

		if ( _appliedReproduceClassicFeel != GamePreferences.ReproduceClassicFeel )
			ApplyDisplaySettings();

		PollInput();
		RestoreAudioStreamIfNeeded();
	}

	private bool StartCoreWhenReady()
	{
		if ( _initCoreOnUpdate )
		{
			if ( ShouldDeferInitialCore() )
				return false;

			_initCoreOnUpdate = false;
			_initDeferFrames = 0;
			InitCore();
		}

		return IsReady && _coreThread?.Core != null;
	}

	private void RestoreAudioStreamIfNeeded()
	{
		if ( _audioStream == null || _soundHandle is { IsValid: true } )
			return;

		try { InitAudioStream(); }
		catch { _audioStream = null; }
	}

	private bool ShouldDeferInitialCore()
	{
		if ( (int)Screen.Width != 1024 || (int)Screen.Height != 1024 )
			return false;

		if ( _initDeferFrames++ >= 5 )
			return false;

		return true;
	}

	private object CoreLock => _coreLock ??= new object();

	private ushort ReadInputKeysActive()
	{
		return (ushort)(AllKeysReleased ^ Interlocked.CompareExchange( ref _inputKeys, 0, 0 ));
	}

	private void ResetVideoClock()
	{
		_videoClock ??= new Stopwatch();
		_videoClock.Restart();
		_nextVideoFrameDue = 0;
	}

	private bool IsVideoFrameDue( GbaCoreSync sync )
	{
		if ( _videoClock == null )
			ResetVideoClock();

		if ( sync == null || _nextVideoFrameDue <= 0 )
			return true;

		return _videoClock.Elapsed.TotalSeconds >= _nextVideoFrameDue;
	}

	private void AdvanceVideoClock( GbaCoreSync sync )
	{
		double frameTime = sync?.FpsTarget > 0 ? 1.0 / sync.FpsTarget : GbaFrameTime;
		double now = _videoClock?.Elapsed.TotalSeconds ?? 0;

		if ( _nextVideoFrameDue <= 0 || now - _nextVideoFrameDue > frameTime * MaxPendingFrames )
			_nextVideoFrameDue = now + frameTime;
		else
			_nextVideoFrameDue += frameTime;
	}

	private void RunOnCoreThread( Action<Gba> action )
	{
		_coreThread?.RunFunction( action );
	}

	private void RescaleGpuIfNeeded()
	{
		if ( _paused )
			return;

		Gba core = Core;
		if ( core?.Video == null )
			return;

		int desiredScale = ComputeAutoScale();
		if ( core.Video.GpuScale == desiredScale )
			return;

		lock ( CoreLock )
		{
			if ( Core != core || core.Video == null )
				return;

			if ( core.Video.GpuScale == desiredScale )
				return;

			if ( _camera.IsValid() && core.Video.RenderCommandList != null )
				_camera.RemoveCommandList( core.Video.RenderCommandList );

			core.Video.DisposeGpu();
			core.Video.InitGpu( desiredScale );
			core.Video.SetReproduceClassicFeel( GamePreferences.ReproduceClassicFeel );
			_appliedReproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
			ScreenTexture = core.Video.OutputTexture;

			if ( _camera.IsValid() && core.Video.RenderCommandList != null )
				_camera.AddCommandList( core.Video.RenderCommandList, Stage.AfterOpaque, 0 );

			_videoFramePending = false;
			_coreThread?.Sync.ForceFrame();
		}
	}

	protected override void OnPreRender()
	{
		WaitFrameStart();
		PresentVideoFrame();
	}

	private void WaitFrameStart()
	{
		GbaCoreThread coreThread = _coreThread;
		GbaCoreSync sync = coreThread?.Sync;
		if ( sync == null || !IsVideoFrameDue( sync ) )
			return;

		if ( !sync.WaitFrameStart() )
			return;

		DrainPostedFrames( coreThread );
		sync.WaitFrameEnd();
		AdvanceVideoClock( sync );
	}

	private void DrainPostedFrames( GbaCoreThread coreThread )
	{
		bool hasFrame = false;

		while ( coreThread.PostedFrames.TryDequeue( out FramePacket frame ) )
		{
			if ( _audioStream != null && frame.AudioSamples > 0 && _audioStream.QueuedSampleCount <= GbaAudio.SamplesPerFrame * AudioHighWaterFrames )
				_audioStream.WriteData( frame.Audio.AsSpan( 0, frame.AudioSamples * 2 ) );

			coreThread.Sync.ConsumeAudio( frame.AudioSamples );

			if ( frame.SaveData != null )
				FileSystem.Data.WriteAllBytes( _savePath, frame.SaveData );

			hasFrame = true;
		}

		if ( hasFrame )
			_videoFramePending = true;
	}

	private void PresentVideoFrame()
	{
		Gba core = Core;
		if ( core?.Video?.RenderCommandList == null )
			return;

		if ( !_videoFramePending )
		{
			core.Video.RenderCommandList.Reset();
			return;
		}

		_videoFramePending = false;
		if ( !core.Video.UploadAndBuildCommandList() )
			core.Video.RenderCommandList.Reset();
	}

	private void PollInput()
	{
		if ( _paused )
			return;

		if ( _inputCooldown > 0 )
		{
			bool anyHeld = Input.Down( "GBA_A" ) || Input.Down( "GBA_B" ) ||
				Input.Down( "GBA_Start" ) || Input.Down( "GBA_Select" ) ||
				Input.Down( "GBA_L" ) || Input.Down( "GBA_R" ) ||
				Input.Down( "GBA_Up" ) || Input.Down( "GBA_Down" ) ||
				Input.Down( "GBA_Left" ) || Input.Down( "GBA_Right" ) ||
				MathF.Abs( Input.GetAnalog( InputAnalog.LeftStickX ) ) > StickDeadzone ||
				MathF.Abs( Input.GetAnalog( InputAnalog.LeftStickY ) ) > StickDeadzone;

			if ( anyHeld )
				return;

			_inputCooldown = 0;
		}

		int keys = AllKeysReleased;
		if ( Input.Down( "GBA_A" ) ) keys &= ~(int)GbaKey.A;
		if ( Input.Down( "GBA_B" ) ) keys &= ~(int)GbaKey.B;
		if ( Input.Down( "GBA_Start" ) ) keys &= ~(int)GbaKey.Start;
		if ( Input.Down( "GBA_Select" ) ) keys &= ~(int)GbaKey.Select;
		if ( Input.Down( "GBA_L" ) ) keys &= ~(int)GbaKey.L;
		if ( Input.Down( "GBA_R" ) ) keys &= ~(int)GbaKey.R;

		float stickX = Input.GetAnalog( InputAnalog.LeftStickX );
		float stickY = Input.GetAnalog( InputAnalog.LeftStickY );
		if ( Input.Down( "GBA_Up" ) || stickY < -StickDeadzone ) keys &= ~(int)GbaKey.Up;
		if ( Input.Down( "GBA_Down" ) || stickY > StickDeadzone ) keys &= ~(int)GbaKey.Down;
		if ( Input.Down( "GBA_Left" ) || stickX < -StickDeadzone ) keys &= ~(int)GbaKey.Left;
		if ( Input.Down( "GBA_Right" ) || stickX > StickDeadzone ) keys &= ~(int)GbaKey.Right;

		Interlocked.Exchange( ref _inputKeys, keys );
	}

	public void SetPaused( bool paused )
	{
		_paused = paused;
		_coreThread?.SetPaused( paused );
		if ( paused )
		{
			if ( _soundHandle is { IsValid: true } )
				_soundHandle.Volume = 0;
		}
		else
		{
			ResetVideoClock();
			_inputCooldown = 2;
			if ( _soundHandle is { IsValid: true } )
				_soundHandle.Volume = 1.0f;
		}
	}

	public string GetStatePath( int slot ) => $"{_stateBasePath}.ss{slot}";

	public void CreateSuspendPoint( int slot )
	{
		Gba core = Core;
		if ( core == null )
			return;

		string path = GetStatePath( slot );
		try
		{
			byte[] data;
			lock ( CoreLock )
			{
				if ( Core != core )
					return;

				byte[] screenshot = core.Video.CaptureScreenshot();
				data = GbaSerialize.Save( core, screenshot );
			}

			FileSystem.Data.WriteAllBytes( path, data );
			GbaLog.Write( LogCategory.GBAState, LogLevel.Info, $"Suspend point created in slot {slot}" );
		}
		catch ( Exception ex )
		{
			GbaLog.Write( LogCategory.GBAState, LogLevel.Error, $"Failed to create suspend point {slot}: {ex.Message}" );
		}
	}

	public void LoadSuspendPoint( int slot )
	{
		string path = GetStatePath( slot );
		RunOnCoreThread( core =>
		{
			if ( !FileSystem.Data.FileExists( path ) )
			{
				GbaLog.Write( LogCategory.GBAState, LogLevel.Warn, $"No suspend point in slot {slot}" );
				return;
			}

			byte[] data = FileSystem.Data.ReadAllBytes( path ).ToArray();
			GbaSerialize.Load( core, data );
			GbaLog.Write( LogCategory.GBAState, LogLevel.Info, $"Suspend point loaded from slot {slot}" );
		} );
	}

	public void ResetEmulator()
	{
		_coreThread?.Reset();
	}

	public void ApplyDisplaySettings()
	{
		bool reproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
		Gba core = Core;
		if ( core?.Video != null )
		{
			lock ( CoreLock )
			{
				if ( Core != core || core.Video == null )
					return;

				core.Video.SetReproduceClassicFeel( reproduceClassicFeel );
			}
		}
		_appliedReproduceClassicFeel = reproduceClassicFeel;
	}

	protected override void OnDestroy()
	{
		TearDownCore();
		_camera = null;
		if ( Current == this )
			Current = null;
	}

	private void LogCoreThreadError( string message )
	{
		GbaLog.Write( LogCategory.GBA, LogLevel.Fatal, message );
	}

	private void LogCoreThreadReset()
	{
		GbaLog.Write( LogCategory.GBA, LogLevel.Info, "Emulator reset" );
	}

	private static void LogBackend( LogCategory category, LogLevel level, string message )
	{
		string formatted = $"{GbaLog.GetCategoryName( category )}: {message}";

		if ( (level & (LogLevel.Fatal | LogLevel.Error)) != 0 )
			Log.Error( formatted );
		else if ( (level & (LogLevel.Warn | LogLevel.GameError)) != 0 )
			Log.Warning( formatted );
		else
			Log.Info( formatted );
	}

	private sealed class GbaCoreThread
	{
		public Gba Core { get; }
		public GbaCoreSync Sync { get; } = new();
		public ConcurrentQueue<FramePacket> PostedFrames { get; } = new();

		private readonly ConcurrentQueue<Action<Gba>> _runQueue = new();
		private readonly SemaphoreSlim _stateOnThreadSignal = new( 0, 1 );
		private readonly object _stateLock = new();
		private readonly object _coreLock;
		private readonly Func<ushort> _readInputKeysActive;
		private readonly Action<string> _logError;
		private readonly Action _resetCallback;
		private readonly short[][] _audioBuffers;
		private CancellationTokenSource _cts;
		private GbaCoreThreadState _state = GbaCoreThreadState.Initialized;
		private GbaCoreThreadRequest _requested;
		private int _audioBufferIndex;

		public GbaCoreThread( Gba core, object coreLock, Func<ushort> readInputKeysActive, Action<string> logError, Action resetCallback )
		{
			Core = core;
			_coreLock = coreLock;
			_readInputKeysActive = readInputKeysActive;
			_logError = logError;
			_resetCallback = resetCallback;
			_audioBuffers = new short[4][];

			int audioBufferSize = GbaAudio.SamplesPerFrame * 2;
			for ( int i = 0; i < _audioBuffers.Length; i++ )
				_audioBuffers[i] = new short[audioBufferSize];
		}

		public void Start()
		{
			if ( _cts != null )
				return;

			_cts = new CancellationTokenSource();
			ChangeState( GbaCoreThreadState.Running );
			_ = GameTask.RunInThreadAsync( Run );
		}

		public void End()
		{
			ChangeState( GbaCoreThreadState.Exiting );
			_cts?.Cancel();
			Sync.SetAudioSync( false );
			Sync.SetVideoSync( false );
			Sync.ForceFrame();
			SignalStateOnThread();
			_cts = null;
		}

		public void Reset()
		{
			SendRequest( GbaCoreThreadRequest.Reset );
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		public void Pause()
		{
			SendRequest( GbaCoreThreadRequest.Pause );
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		public void Unpause()
		{
			CancelRequest( GbaCoreThreadRequest.Pause );
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

		public void RunFunction( Action<Gba> run )
		{
			if ( run == null || _cts == null )
				return;

			_runQueue.Enqueue( run );
			SendRequest( GbaCoreThreadRequest.RunOn );
			SignalStateOnThread();
			Sync.ForceFrame();
		}

		private async Task Run()
		{
			CancellationTokenSource cts = _cts;
			if ( cts == null )
				return;

			CancellationToken token = cts.Token;
			Stopwatch watchdogYield = Stopwatch.StartNew();

			try
			{
				while ( !token.IsCancellationRequested )
				{
					GbaCoreThreadRequest pendingRequests = TakePendingRequests();
					RunPendingRequests( pendingRequests );

					if ( IsRequested( GbaCoreThreadRequest.Pause | GbaCoreThreadRequest.Wait | GbaCoreThreadRequest.Crashed | GbaCoreThreadRequest.RewindEmpty ) )
					{
						ChangeState( GbaCoreThreadState.Paused );
						await _stateOnThreadSignal.WaitAsync( SyncWaitMilliseconds, token );
						continue;
					}

					ChangeState( GbaCoreThreadState.Running );
					FramePacket frame = RunFrame( token );
					PostedFrames.Enqueue( frame );
					await Sync.ProduceAudio( frame.AudioSamples, token );
					await Sync.PostFrame( token );

					if ( watchdogYield.ElapsedMilliseconds >= WorkerWatchdogYieldMilliseconds )
					{
						await System.Threading.Tasks.Task.Yield();
						watchdogYield.Restart();
					}
				}
			}
			catch ( OperationCanceledException ) { }
			catch ( ObjectDisposedException ) { }
			catch ( Exception ex )
			{
				SendRequest( GbaCoreThreadRequest.Crashed );
				ChangeState( GbaCoreThreadState.Crashed );
				_logError?.Invoke( $"Emulation worker error: {ex.Message}\n{ex.StackTrace}" );
			}
			finally
			{
				ChangeState( GbaCoreThreadState.Shutdown );
			}
		}

		private FramePacket RunFrame( CancellationToken token )
		{
			short[] audio;
			int audioSamples;
			byte[] saveData = null;

			lock ( _coreLock )
			{
				Core.KeysActive = _readInputKeysActive();
				Core.RunFrame();

				if ( token.IsCancellationRequested )
					return new FramePacket( null, 0, null );

				int bufferIndex = _audioBufferIndex;
				_audioBufferIndex = (bufferIndex + 1) & 3;
				audio = _audioBuffers[bufferIndex];

				audioSamples = Core.Audio.SamplesWritten;
				if ( audioSamples > 0 )
					Buffer.BlockCopy( Core.Audio.OutputBuffer, 0, audio, 0, audioSamples * 2 * sizeof( short ) );

				if ( Core.Savedata.Clean() && Core.Savedata.Data.Length > 0 )
					saveData = Core.Savedata.Data.ToArray();
			}

			return new FramePacket( audio, audioSamples, saveData );
		}

		private GbaCoreThreadRequest TakePendingRequests()
		{
			lock ( _stateLock )
			{
				GbaCoreThreadRequest pendingRequests = _requested;
				_requested &= GbaCoreThreadRequest.Pause | GbaCoreThreadRequest.Wait | GbaCoreThreadRequest.Crashed | GbaCoreThreadRequest.RewindEmpty;
				return pendingRequests;
			}
		}

		private void RunPendingRequests( GbaCoreThreadRequest pendingRequests )
		{
			if ( (pendingRequests & GbaCoreThreadRequest.Reset) != 0 )
			{
				lock ( _coreLock )
				{
					Core.Reset();
				}
				_resetCallback?.Invoke();
			}

			if ( (pendingRequests & GbaCoreThreadRequest.RunOn) != 0 )
				RunPendingFunctions();
		}

		private void RunPendingFunctions()
		{
			while ( _runQueue.TryDequeue( out Action<Gba> run ) )
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
					GbaLog.Write( LogCategory.GBA, LogLevel.Error, ex.Message );
				}
			}
		}

		private bool IsRequested( GbaCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				return (_requested & request) != 0;
			}
		}

		private void SendRequest( GbaCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				_requested |= request;
				if ( _state is GbaCoreThreadState.Running or GbaCoreThreadState.Paused or GbaCoreThreadState.Crashed )
					_state = GbaCoreThreadState.Request;
			}
		}

		private void CancelRequest( GbaCoreThreadRequest request )
		{
			lock ( _stateLock )
			{
				_requested &= ~request;
				if ( _state == GbaCoreThreadState.Request && _requested == GbaCoreThreadRequest.None )
					_state = GbaCoreThreadState.Running;
			}
		}

		private void ChangeState( GbaCoreThreadState state )
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

	private sealed class GbaCoreSync
	{
		public int VideoFramePending { get; private set; }
		public bool VideoFrameWait { get; private set; }
		public bool AudioWait { get; private set; }
		public int AudioHighWater { get; set; }
		public float FpsTarget { get; private set; }

		private readonly object _videoFrameLock = new();
		private readonly object _audioBufferLock = new();
		private readonly SemaphoreSlim _videoFrameAvailable = new( 0, 1 );
		private readonly SemaphoreSlim _videoFrameRequired = new( 0, 1 );
		private readonly SemaphoreSlim _audioRequired = new( 0, 1 );
		private int _audioSamplesPending;

		public void LoadCoreOptions( bool audioSync, bool videoSync, float fpsTarget )
		{
			AudioWait = audioSync;
			VideoFrameWait = videoSync;
			FpsTarget = fpsTarget > 0 ? fpsTarget : 60f;
			AudioHighWater = 512;
		}

		public async Task PostFrame( CancellationToken token )
		{
			lock ( _videoFrameLock )
			{
				VideoFramePending++;
				Signal( _videoFrameAvailable );
			}

			while ( true )
			{
				lock ( _videoFrameLock )
				{
					if ( !VideoFrameWait || VideoFramePending <= 0 )
						return;
				}

				await _videoFrameRequired.WaitAsync( SyncWaitMilliseconds, token );
			}
		}

		public void ForceFrame()
		{
			Signal( _videoFrameAvailable );
			Signal( _videoFrameRequired );
		}

		public bool WaitFrameStart()
		{
			Monitor.Enter( _videoFrameLock );
			try
			{
				if ( !VideoFrameWait && VideoFramePending <= 0 )
					return false;

				Signal( _videoFrameRequired );

				if ( VideoFrameWait && VideoFramePending <= 0 )
				{
					Drain( _videoFrameAvailable );
					Monitor.Exit( _videoFrameLock );
					try
					{
						if ( !_videoFrameAvailable.Wait( SyncWaitMilliseconds ) )
							return false;
					}
					finally
					{
						Monitor.Enter( _videoFrameLock );
					}
				}

				if ( VideoFramePending <= 0 )
					return false;

				Drain( _videoFrameAvailable );
				VideoFramePending = 0;
				return true;
			}
			finally
			{
				Monitor.Exit( _videoFrameLock );
			}
		}

		public void WaitFrameEnd()
		{
			Signal( _videoFrameRequired );
		}

		public void SetVideoSync( bool wait )
		{
			lock ( _videoFrameLock )
			{
				if ( wait == VideoFrameWait )
					return;

				VideoFrameWait = wait;
				Signal( _videoFrameAvailable );
			}
		}

		public void SetAudioSync( bool wait )
		{
			lock ( _audioBufferLock )
			{
				AudioWait = wait;
				Signal( _audioRequired );
			}
		}

		public async Task ProduceAudio( int audioSamples, CancellationToken token )
		{
			if ( audioSamples <= 0 )
				return;

			lock ( _audioBufferLock )
			{
				_audioSamplesPending += audioSamples;
			}

			while ( true )
			{
				lock ( _audioBufferLock )
				{
					if ( !AudioWait || AudioHighWater <= 0 || _audioSamplesPending < AudioHighWater )
						return;
				}

				await _audioRequired.WaitAsync( SyncWaitMilliseconds, token );
			}
		}

		public void ConsumeAudio( int audioSamples )
		{
			if ( audioSamples > 0 )
			{
				lock ( _audioBufferLock )
				{
					_audioSamplesPending = Math.Max( 0, _audioSamplesPending - audioSamples );
				}
			}

			Signal( _audioRequired );
		}

		private static void Signal( SemaphoreSlim signal )
		{
			try { signal.Release(); }
			catch ( SemaphoreFullException ) { }
		}

		private static void Drain( SemaphoreSlim signal )
		{
			while ( signal.Wait( 0 ) ) { }
		}
	}
}