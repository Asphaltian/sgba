using System.Diagnostics;
using System.Threading;
using Sandbox.Rendering;
using Emulotl;

namespace sGBA;

public sealed partial class EmulatorComponent : Component
{
	[Property, Title( "ROM Path" ), FilePath( Extension = "gba" )]
	public string RomPath { get; set; }

	public string GameCode { get; private set; }

	public static EmulatorComponent Current { get; private set; }
	public IEmulatorCore Core => _coreThread?.Core;
	public Texture ScreenTexture { get; private set; }
	public bool IsReady { get; private set; }
	public string ErrorMessage { get; private set; }

	private SoundStream _audioStream;
	private SoundHandle _soundHandle;
	private string _savePath;
	private CameraComponent _camera;
	private object _coreLock = new();
	private EmulatorCoreThread _coreThread;
	private SystemProfile _profile;
	private double _frameTime;
	private Stopwatch _videoClock;
	private double _nextVideoFrameDue;

	private const int AudioPrefillFrames = 2;
	private const int AudioHighWaterFrames = 3;
	private const int MaxPendingFrames = 3;
	private const int SyncWaitMilliseconds = 50;
	private const float StickDeadzone = 0.3f;

	private int _inputKeys;
	private bool _videoFramePending;
	private bool _paused;
	private bool _coreHalted;
	private int _inputCooldown;
	private bool _initCoreOnUpdate;
	private int _initDeferFrames;
	private string _stateBasePath;
	private bool _appliedReproduceClassicFeel;
	private RealTimeSince _sessionTime;

	protected override void OnStart()
	{
		Current = this;
		EmulatorSystems.EnsureRegistered();
		GbaLog.SetBackend( LogBackend );
		_camera = Scene.Camera;
		if ( !string.IsNullOrEmpty( RomPath ) )
			_initCoreOnUpdate = true;
	}

	private bool EnsureProfile()
	{
		EmulatorSystems.EnsureRegistered();
		_profile = SystemRegistry.ResolveByPath( RomPath ) ?? _profile;
		if ( _profile != null )
			_frameTime = 1.0 / _profile.NativeFps;
		return _profile != null;
	}

	public void Restart( string romPath )
	{
		if ( _linkedHost || _linkedClient )
			EndLinkedMode();

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
		GameCode = null;
		_initCoreOnUpdate = false;
		_initDeferFrames = 0;
	}

	private void TearDownCore()
	{
		EmulatorCoreThread coreThread = _coreThread;
		_coreThread = null;
		coreThread?.End();

		lock ( CoreLock )
		{
			IEmulatorCore core = coreThread?.Core;
			if ( core?.SaveData != null && core.SaveData.Data.Length > 0 && _savePath != null )
				FileSystem.Data.WriteAllBytes( _savePath, core.SaveData.Data );

			if ( core != null )
			{
				foreach ( IVideoOutput screen in core.Screens )
				{
					if ( _camera.IsValid() && screen.RenderCommandList != null )
						_camera.RemoveCommandList( screen.RenderCommandList );
					screen.DisposeGpu();
				}
			}

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
		_coreHalted = false;
		Interlocked.Exchange( ref _inputKeys, 0 );
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

			if ( !EnsureProfile() )
			{
				ErrorMessage = $"No emulator system handles ROM: {RomPath}";
				GbaLog.Write( LogCategory.GBA, LogLevel.Error, ErrorMessage );
				return;
			}

			BaseFileSystem romFs = FileSystem.Mounted.FileExists( RomPath ) ? FileSystem.Mounted : FileSystem.Data;
			byte[] romData = romFs.ReadAllBytes( RomPath ).ToArray();
			if ( romData.Length < 192 )
			{
				ErrorMessage = $"ROM file is too small to be a valid {_profile.DisplayName} ROM.";
				GbaLog.Write( LogCategory.GBA, LogLevel.Error, ErrorMessage );
				return;
			}

			GameCode = _profile.ReadGameId( romData );

			IEmulatorCore core = _profile.CreateCore();
			core.LoadRom( romData );

			_savePath = "saves/" + System.IO.Path.GetFileNameWithoutExtension( RomPath ) + ".sav";
			if ( FileSystem.Data.FileExists( _savePath ) )
			{
				byte[] saveData = FileSystem.Data.ReadAllBytes( _savePath ).ToArray();
				core.SaveData.Load( saveData );
			}

			core.Reset();

			int scale = ComputeAutoScale();
			DisplayOptions display = CurrentDisplayOptions();
			foreach ( IVideoOutput screen in core.Screens )
			{
				screen.InitGpu( scale );
				screen.ApplyDisplayOptions( display );
				if ( _camera.IsValid() && screen.RenderCommandList != null )
					_camera.AddCommandList( screen.RenderCommandList, Stage.AfterOpaque, 0 );
			}
			_appliedReproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
			ScreenTexture = core.Screens[0].OutputTexture;

			_stateBasePath = "states/" + System.IO.Path.GetFileNameWithoutExtension( RomPath );

			try { InitAudioStream(); }
			catch ( Exception audioEx ) { GbaLog.Write( LogCategory.GBAAudio, LogLevel.Warn, $"Audio init failed: {audioEx.Message}" ); }

			_coreThread = new EmulatorCoreThread( core, CoreLock, ReadInputKeysActive, LogCoreThreadError, LogCoreThreadReset );
			_coreThread.Sync.LoadCoreOptions( audioSync: true, videoSync: true, fpsTarget: (float)_profile.NativeFps );
			_coreThread.Sync.AudioHighWater = _profile.AudioSamplesPerFrame * AudioHighWaterFrames;
			ResetVideoClock();
			_coreThread.Start();

			_sessionTime = 0;
			AchievementManager.OnSessionStarted();
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

		int sampleRate = _profile?.AudioSampleRate ?? GbaAudio.SampleRate;
		int channels = _profile?.AudioChannels ?? 2;
		int samplesPerFrame = _profile?.AudioSamplesPerFrame ?? GbaAudio.SamplesPerFrame;

		_audioStream = new SoundStream( sampleRate, channels );
		_audioStream.WriteData( new short[samplesPerFrame * channels * AudioPrefillFrames] );
		_soundHandle = _audioStream.Play( volume: _paused ? 0f : 1.0f );
		_soundHandle.SpacialBlend = 0f;
		_soundHandle.OcclusionEnabled = false;
		_soundHandle.DistanceAttenuation = false;
		_soundHandle.AirAbsorption = false;
		_soundHandle.Transmission = false;
		_soundHandle.Stop( float.MaxValue );
	}

	private DisplayOptions CurrentDisplayOptions() => new( GamePreferences.ReproduceClassicFeel, GamePreferences.DisplayWithSmallScreen );

	private int ComputeAutoScale()
	{
		int screenWidth = Screen.Width > 0 ? (int)Screen.Width : 1920;
		int screenHeight = Screen.Height > 0 ? (int)Screen.Height : 1080;
		ScreenSpec native = _profile?.Screens is { Length: > 0 } screens ? screens[0] : new ScreenSpec( 240, 160 );
		return Math.Clamp( Math.Min( screenWidth / native.Width, screenHeight / native.Height ), 1, 8 );
	}

	protected override void OnUpdate()
	{
		ReconcileLinkedMode();

		if ( _coreThread != null )
			AchievementManager.OnSessionTime( _sessionTime );

		if ( _linkedClient )
		{
			if ( _timeSinceClientPacket > ClientHostTimeoutSeconds )
			{
				ResumeSoloAfterHostLost();
				return;
			}

			if ( _appliedReproduceClassicFeel != GamePreferences.ReproduceClassicFeel )
			{
				_clientVideo?.SetReproduceClassicFeel( GamePreferences.ReproduceClassicFeel );
				_appliedReproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
			}

			PollInput();
			TickLinkedClient();
			RestoreAudioStreamIfNeeded();
			return;
		}

		if ( _linkedHost )
		{
			RescaleGpuIfNeeded();
			if ( _appliedReproduceClassicFeel != GamePreferences.ReproduceClassicFeel )
				ApplyDisplaySettings();
			PollInput();
			TickLinkedHost();
			RestoreAudioStreamIfNeeded();
			return;
		}

		if ( !StartCoreWhenReady() )
			return;

		ReconcileCorePause();
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
			var net = NetworkManager.Current;
			if ( net != null && net.Mode == SessionMode.LinkCable && !Networking.IsHost && net.State != SessionState.Solo )
			{
				_initCoreOnUpdate = false;
				_initDeferFrames = 0;
				return false;
			}

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
		return (ushort)Interlocked.CompareExchange( ref _inputKeys, 0, 0 );
	}

	private void ResetVideoClock()
	{
		_videoClock ??= new Stopwatch();
		_videoClock.Restart();
		_nextVideoFrameDue = 0;
	}

	private bool IsVideoFrameDue( EmulatorCoreSync sync )
	{
		if ( _videoClock == null )
			ResetVideoClock();

		if ( sync == null || _nextVideoFrameDue <= 0 )
			return true;

		return _videoClock.Elapsed.TotalSeconds >= _nextVideoFrameDue;
	}

	private void AdvanceVideoClock( EmulatorCoreSync sync )
	{
		double frameTime = sync?.FpsTarget > 0 ? 1.0 / sync.FpsTarget : _frameTime;
		double now = _videoClock?.Elapsed.TotalSeconds ?? 0;

		if ( _nextVideoFrameDue <= 0 || now - _nextVideoFrameDue > frameTime * MaxPendingFrames )
			_nextVideoFrameDue = now + frameTime;
		else
			_nextVideoFrameDue += frameTime;
	}

	private void RunOnCoreThread( Action<IEmulatorCore> action )
	{
		_coreThread?.RunFunction( action );
	}

	private void RescaleGpuIfNeeded()
	{
		if ( _paused )
			return;

		IEmulatorCore core = Core;
		if ( core == null || core.Screens.Count == 0 )
			return;

		int desiredScale = ComputeAutoScale();
		if ( core.Screens[0].GpuScale == desiredScale )
			return;

		lock ( CoreLock )
		{
			if ( Core != core || core.Screens.Count == 0 )
				return;

			if ( core.Screens[0].GpuScale == desiredScale )
				return;

			DisplayOptions display = CurrentDisplayOptions();
			foreach ( IVideoOutput screen in core.Screens )
			{
				if ( _camera.IsValid() && screen.RenderCommandList != null )
					_camera.RemoveCommandList( screen.RenderCommandList );

				screen.InitGpu( desiredScale );
				screen.ApplyDisplayOptions( display );

				if ( _camera.IsValid() && screen.RenderCommandList != null )
					_camera.AddCommandList( screen.RenderCommandList, Stage.AfterOpaque, 0 );
			}

			_appliedReproduceClassicFeel = GamePreferences.ReproduceClassicFeel;
			ScreenTexture = core.Screens[0].OutputTexture;

			_videoFramePending = true;
			_coreThread?.Sync.ForceFrame();
		}
	}

	protected override void OnPreRender()
	{
		if ( _linkedHost )
		{
			CaptureAndSendHostVideo();
			return;
		}

		if ( _linkedClient )
		{
			PresentClientVideo();
			return;
		}

		WaitForPostedCoreFrame();
		UploadPendingVideoFrame();
	}

	private void WaitForPostedCoreFrame()
	{
		EmulatorCoreThread coreThread = _coreThread;
		EmulatorCoreSync sync = coreThread?.Sync;
		if ( sync == null || !IsVideoFrameDue( sync ) )
			return;

		if ( !sync.WaitFrameStart() )
			return;

		DrainPostedCoreFrames( coreThread );
		sync.WaitFrameEnd();
		AdvanceVideoClock( sync );
	}

	private void DrainPostedCoreFrames( EmulatorCoreThread coreThread )
	{
		bool hasFrame = false;
		int channels = _profile?.AudioChannels ?? 2;
		int highWater = (_profile?.AudioSamplesPerFrame ?? GbaAudio.SamplesPerFrame) * AudioHighWaterFrames;

		while ( coreThread.PostedFrames.TryDequeue( out FramePacket frame ) )
		{
			if ( _audioStream != null && frame.AudioSamples > 0 && _audioStream.QueuedSampleCount <= highWater )
				_audioStream.WriteData( frame.Audio.AsSpan( 0, frame.AudioSamples * channels ) );

			coreThread.Sync.ConsumeAudio( frame.AudioSamples );

			if ( frame.SaveData != null )
				FileSystem.Data.WriteAllBytes( _savePath, frame.SaveData );

			hasFrame = true;
		}

		if ( hasFrame )
			_videoFramePending = true;
	}

	private void UploadPendingVideoFrame()
	{
		IEmulatorCore core = Core;
		if ( core == null || !_videoFramePending )
			return;

		bool uploaded = false;
		foreach ( IVideoOutput screen in core.Screens )
		{
			if ( screen.RenderCommandList == null )
				continue;
			if ( screen.UploadAndBuildCommandList() )
				uploaded = true;
		}

		if ( uploaded )
			_videoFramePending = false;
	}

	private void PollInput()
	{
		if ( _paused )
			return;

		InputButton[] buttons = _profile?.Buttons;
		if ( buttons == null )
			return;

		float stickX = Input.GetAnalog( InputAnalog.LeftStickX );
		float stickY = Input.GetAnalog( InputAnalog.LeftStickY );

		if ( _inputCooldown > 0 )
		{
			if ( AnyButtonHeld( buttons, stickX, stickY ) )
				return;

			_inputCooldown = 0;
		}

		ulong mask = 0;
		for ( int i = 0; i < buttons.Length; i++ )
		{
			if ( IsButtonPressed( buttons[i], stickX, stickY ) )
				mask |= 1UL << i;
		}

		Interlocked.Exchange( ref _inputKeys, (int)mask );
	}

	private static bool IsButtonPressed( in InputButton button, float stickX, float stickY )
	{
		if ( Input.Down( button.ActionName ) )
			return true;

		return button.Dpad switch
		{
			DpadDirection.Up => stickY < -StickDeadzone,
			DpadDirection.Down => stickY > StickDeadzone,
			DpadDirection.Left => stickX < -StickDeadzone,
			DpadDirection.Right => stickX > StickDeadzone,
			_ => false,
		};
	}

	private static bool AnyButtonHeld( InputButton[] buttons, float stickX, float stickY )
	{
		for ( int i = 0; i < buttons.Length; i++ )
		{
			if ( IsButtonPressed( buttons[i], stickX, stickY ) )
				return true;
		}

		return false;
	}

	public void SetPaused( bool paused )
	{
		_paused = paused;

		if ( paused )
			Interlocked.Exchange( ref _inputKeys, 0 );
		else
			_inputCooldown = 2;

		if ( _soundHandle is { IsValid: true } )
			_soundHandle.Volume = paused ? 0 : 1.0f;

		ReconcileCorePause();
	}

	private void ReconcileCorePause()
	{
		bool networked = NetworkManager.Current?.IsActive == true;
		bool shouldHalt = _paused && !networked;
		if ( shouldHalt == _coreHalted )
			return;

		_coreHalted = shouldHalt;
		_coreThread?.SetPaused( shouldHalt );
		if ( !shouldHalt )
			ResetVideoClock();
	}

	public string GetStatePath( int slot ) => $"{_stateBasePath}.ss{slot}";

	public void CreateSuspendPoint( int slot )
	{
		IEmulatorCore core = Core;
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

				byte[] screenshot = core.Screens[0].CaptureScreenshot();
				data = core.SaveState( screenshot );
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
			core.LoadState( data );
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
		IEmulatorCore core = Core;
		if ( core != null && core.Screens.Count > 0 )
		{
			lock ( CoreLock )
			{
				if ( Core != core || core.Screens.Count == 0 )
					return;

				DisplayOptions display = CurrentDisplayOptions();
				foreach ( IVideoOutput screen in core.Screens )
					screen.ApplyDisplayOptions( display );
			}
		}
		_appliedReproduceClassicFeel = reproduceClassicFeel;
	}

	protected override void OnDestroy()
	{
		if ( _linkedHost || _linkedClient )
			EndLinkedMode();
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
}
