using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Rendering;

namespace sGBA;

public sealed partial class EmulatorComponent : Component
{
	[Property, Title( "ROM Path" ), FilePath( Extension = "gba" )]
	public string RomPath { get; set; }

	public static EmulatorComponent Current { get; private set; }
	public Gba Core { get; private set; }
	public Texture ScreenTexture { get; private set; }
	public bool IsReady { get; private set; }
	public string ErrorMessage { get; private set; }

	private SoundStream _audioStream;
	private SoundHandle _soundHandle;
	private string _savePath;
	private CameraComponent _camera;
	private object _coreSync = new();

	private const int AllKeysReleased = 0x03FF;
	private const int AudioPrefillFrames = 2;
	private const int AudioHighWaterFrames = 3;
	private const double GbaFrameTime = 1.0 / 59.7275;
	private const float StickDeadzone = 0.3f;

	private CancellationTokenSource _emulationCts;
	private ConcurrentQueue<FramePacket> _postedFrames;
	private ConcurrentQueue<Action<Gba>> _coreThreadActions;
	private SemaphoreSlim _frameSignal;
	private int _inputKeys = AllKeysReleased;
	private short[][] _audBufs;
	private int _workerBufIdx;
	private double _frameBudget;
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
		_emulationCts?.Cancel();

		_emulationCts = null;

		lock ( CoreSync )
		{
			if ( Core?.Savedata != null && Core.Savedata.Data.Length > 0 && _savePath != null )
				FileSystem.Data.WriteAllBytes( _savePath, Core.Savedata.Data );

			if ( _camera.IsValid() && Core?.Video?.RenderCommandList != null )
				_camera.RemoveCommandList( Core.Video.RenderCommandList );

			Core?.Video?.DisposeGpu();
			Core = null;
			ScreenTexture = null;
		}

		if ( _soundHandle is { IsValid: true } )
			_soundHandle.Volume = 0;
		_audioStream?.Dispose();
		_audioStream = null;
		_postedFrames = null;
		_coreThreadActions = null;
		_frameSignal?.Dispose();
		_frameSignal = null;
		_workerBufIdx = 0;
		_frameBudget = 0;
		_videoFramePending = false;
		_inputCooldown = 0;
		_initDeferFrames = 0;
		_paused = false;
		_inputKeys = AllKeysReleased;
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

			Core = new Gba();
			Core.LoadRom( romData );

			_savePath = "saves/" + System.IO.Path.GetFileNameWithoutExtension( RomPath ) + ".sav";
			if ( FileSystem.Data.FileExists( _savePath ) )
			{
				byte[] saveData = FileSystem.Data.ReadAllBytes( _savePath ).ToArray();
				Core.Savedata.Load( saveData );
			}

			Core.Reset();
			IsReady = true;

			Core.Video.InitGpu( scale: ComputeAutoScale() );
			ApplyDisplaySettings();
			ScreenTexture = Core.Video.OutputTexture;

			if ( _camera.IsValid() && Core.Video.RenderCommandList != null )
				_camera.AddCommandList( Core.Video.RenderCommandList, Stage.AfterOpaque, 0 );

			_stateBasePath = "states/" + System.IO.Path.GetFileNameWithoutExtension( RomPath );

			try { InitAudioStream(); }
			catch ( Exception audioEx ) { GbaLog.Write( LogCategory.GBAAudio, LogLevel.Warn, $"Audio init failed: {audioEx.Message}" ); }

			int audioBufferSize = GbaAudio.SamplesPerFrame * 2;
			_audBufs = new short[4][];
			for ( int i = 0; i < 4; i++ )
				_audBufs[i] = new short[audioBufferSize];

			_postedFrames = new ConcurrentQueue<FramePacket>();
			_coreThreadActions = new ConcurrentQueue<Action<Gba>>();
			_frameSignal = new SemaphoreSlim( 0, 1 );
			_emulationCts = new CancellationTokenSource();
			GameTask.RunInThreadAsync( EmulationLoop );
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

	private async Task EmulationLoop()
	{
		CancellationToken token = _emulationCts.Token;

		try
		{
			while ( !token.IsCancellationRequested )
			{
				await EnsureFrameSignal().WaitAsync( token );

				Gba core = Core;
				if ( core == null )
					break;

				int audioSamples;
				short[] audio;
				byte[] saveData = null;

				lock ( CoreSync )
				{
					RunPendingCoreActions( core );
					if ( _paused )
						continue;

					core.KeysActive = (ushort)(AllKeysReleased ^ Interlocked.CompareExchange( ref _inputKeys, 0, 0 ));
					core.RunFrame();

					if ( token.IsCancellationRequested )
						break;

					int bufferIndex = _workerBufIdx;
					_workerBufIdx = (bufferIndex + 1) & 3;
					audio = _audBufs[bufferIndex];

					audioSamples = core.Audio.SamplesWritten;
					if ( audioSamples > 0 )
						Buffer.BlockCopy( core.Audio.OutputBuffer, 0, audio, 0, audioSamples * 2 * sizeof( short ) );

					if ( core.Savedata.Clean() && core.Savedata.Data.Length > 0 )
						saveData = core.Savedata.Data.ToArray();
				}

				_postedFrames?.Enqueue( new FramePacket( audio, audioSamples, saveData ) );

				await Task.Yield();
			}
		}
		catch ( OperationCanceledException ) { }
		catch ( ObjectDisposedException ) { }
		catch ( Exception ex )
		{
			GbaLog.Write( LogCategory.GBA, LogLevel.Fatal, $"Emulation worker error: {ex.Message}\n{ex.StackTrace}" );
		}
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
		ReleaseFrameBudget();
		DrainPostedFrames();
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

		return IsReady && Core != null;
	}

	private void RestoreAudioStreamIfNeeded()
	{
		if ( _audioStream == null || _soundHandle is { IsValid: true } )
			return;

		try { InitAudioStream(); }
		catch { _audioStream = null; }
	}

	private void ReleaseFrameBudget()
	{
		if ( !_paused )
		{
			_frameBudget += RealTime.Delta;

			if ( _frameBudget > GbaFrameTime * 3 )
				_frameBudget = GbaFrameTime * 3;

			while ( _frameBudget >= GbaFrameTime )
			{
				_frameBudget -= GbaFrameTime;

				SemaphoreSlim frameSignal = EnsureFrameSignal();
				if ( frameSignal.CurrentCount < 1 )
					frameSignal.Release();
			}
		}
	}

	private void DrainPostedFrames()
	{
		bool hasFrame = false;

		while ( _postedFrames != null && _postedFrames.TryDequeue( out FramePacket frame ) )
		{
			if ( _audioStream != null && frame.AudioSamples > 0 && _audioStream.QueuedSampleCount <= GbaAudio.SamplesPerFrame * AudioHighWaterFrames )
				_audioStream.WriteData( frame.Audio.AsSpan( 0, frame.AudioSamples * 2 ) );

			if ( frame.SaveData != null )
				FileSystem.Data.WriteAllBytes( _savePath, frame.SaveData );

			hasFrame = true;
		}

		if ( hasFrame )
			_videoFramePending = true;
	}

	private bool ShouldDeferInitialCore()
	{
		if ( (int)Screen.Width != 1024 || (int)Screen.Height != 1024 )
			return false;

		if ( _initDeferFrames++ >= 5 )
			return false;

		return true;
	}

	private object CoreSync => _coreSync ??= new object();

	private SemaphoreSlim EnsureFrameSignal()
	{
		SemaphoreSlim frameSignal = _frameSignal;
		if ( frameSignal != null )
			return frameSignal;

		frameSignal = new SemaphoreSlim( 0, 1 );
		SemaphoreSlim existing = Interlocked.CompareExchange( ref _frameSignal, frameSignal, null );
		if ( existing != null )
		{
			frameSignal.Dispose();
			return existing;
		}

		return frameSignal;
	}

	private void RunOnCoreThread( Action<Gba> action )
	{
		if ( Core == null || _coreThreadActions == null )
			return;

		_coreThreadActions.Enqueue( action );

		SemaphoreSlim frameSignal = EnsureFrameSignal();
		if ( frameSignal.CurrentCount < 1 )
			frameSignal.Release();
	}

	private void RunPendingCoreActions( Gba core )
	{
		while ( _coreThreadActions != null && _coreThreadActions.TryDequeue( out Action<Gba> action ) )
		{
			try { action( core ); }
			catch ( Exception ex ) { GbaLog.Write( LogCategory.GBA, LogLevel.Error, ex.Message ); }
		}
	}

	private void RescaleGpuIfNeeded()
	{
		Gba core = Core;
		if ( core?.Video == null )
			return;

		int desiredScale = ComputeAutoScale();
		if ( core.Video.GpuScale == desiredScale )
			return;

		lock ( CoreSync )
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
		}
	}

	protected override void OnPreRender()
	{
		PresentVideoFrame();
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
		if ( paused )
		{
			_frameBudget = 0;
			if ( _soundHandle is { IsValid: true } )
				_soundHandle.Volume = 0;
		}
		else
		{
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
			lock ( CoreSync )
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
		RunOnCoreThread( core =>
		{
			core.Reset();
			GbaLog.Write( LogCategory.GBA, LogLevel.Info, "Emulator reset" );
		} );
	}

	public void ApplyDisplaySettings()
	{
		bool reproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
		Gba core = Core;
		if ( core?.Video != null )
		{
			lock ( CoreSync )
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
}
