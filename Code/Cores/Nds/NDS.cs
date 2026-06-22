namespace Emulotl.Nds;

public sealed partial class NDS : IEmulatorCore
{
	public ARMv5 ARM9 { get; }
	public ARMv4 ARM7 { get; }
	public GPU GPU { get; }
	public GPU3D GPU3D { get; }
	public SPU SPU { get; }
	public Wifi Wifi { get; }

	public byte[] MainRAM = new byte[NdsConstants.MainRamSize];
	public byte[] SharedWRAM = new byte[NdsConstants.SharedWramSize];
	public byte[] ARM7WRAM = new byte[NdsConstants.Arm7WramSize];

	public ushort KeyInput = 0x03FF;
	public ushort ExtKeyIn = 0x007F;

	private readonly NdsScreen _topScreen;
	private readonly NdsScreen _bottomScreen;
	private readonly ComputeRenderer3D _renderer3D;
	private readonly ComputeRenderer2D _render2D_A;
	private readonly ComputeRenderer2D _render2D_B;
	private readonly NdsAudio _audio = new();
	private readonly NdsSaveData _save = new();
	private readonly IVideoOutput[] _screens;

	public ComputeRenderer2D Render2DA => _render2D_A;
	public ComputeRenderer2D Render2DB => _render2D_B;

	private byte[] _rom;
	private bool _running;

	public NDS()
	{
		ARM9 = new ARMv5( this );
		ARM7 = new ARMv4( this );
		GPU = new GPU( this );
		GPU3D = new GPU3D( this );
		SPU = new SPU( this, _audio );
		Wifi = new Wifi( this );
		_renderer3D = new ComputeRenderer3D( GPU3D, GPU );
		_render2D_A = new ComputeRenderer2D( 0, GPU.GPU2D_A, GPU, GPU3D );
		_render2D_B = new ComputeRenderer2D( 1, GPU.GPU2D_B, GPU, GPU3D );
		_render2D_A.SetRenderer3D( _renderer3D );
		_render2D_B.SetRenderer3D( _renderer3D );
		_render2D_B.SetCaptureOwner( _render2D_A );
		_renderer3D.SetCaptureSource( _render2D_A );
		_render2D_A.SetSwapPeer( _render2D_B );
		_render2D_B.SetSwapPeer( _render2D_A );
		_topScreen = new NdsScreen( _render2D_B, _renderer3D );
		_bottomScreen = new NdsScreen( _render2D_A, _renderer3D );
		_screens = [_topScreen, _bottomScreen];

		InitDmas();
	}

	public SystemProfile Profile => NdsSystem.Profile;

	IReadOnlyList<IVideoOutput> IEmulatorCore.Screens => _screens;
	IAudioOutput IEmulatorCore.Audio => _audio;
	ISaveData IEmulatorCore.SaveData => _save;

	public void LoadRom( byte[] romData )
	{
		_rom = romData;
		_secureAreaProcessed = false;
	}

	public void Reset()
	{
		Array.Clear( MainRAM );
		Array.Clear( SharedWRAM );
		Array.Clear( ARM7WRAM );

		ResetMemory();
		ResetScheduler();
		ResetInterrupts();
		ResetTimings();
		ResetTimers();
		ResetDmas();
		ResetIpc();
		ResetMath();
		ResetCart();
		ResetCartSave();
		ResetSpi();
		Wifi.Reset();
		ResetRtc();
		GPU.Reset();
		GPU3D.Reset();
		ARM9.CP15Reset();
		ARM9.Reset();
		ARM7.Reset();
		SPU.Reset();
		SPU.SetBias( 0x200 );

		KeyInput = 0x03FF;
		ExtKeyIn = 0x007F;
		_running = true;

		DirectBoot();
	}

	public void RunFrame()
	{
		if ( !_running )
			return;

		_audio.BeginFrame();
		GPU.StartFrame();

		long frameEnd = SysTimestamp + NdsConstants.CyclesPerFrame;
		while ( SysTimestamp < frameEnd )
		{
			long minEvent = NextEventTimestamp();
			long max = SysTimestamp + 64;
			long target = minEvent < max + 8 ? minEvent : max;
			if ( target > frameEnd )
				target = frameEnd;

			ARM9Target = target << 1;
			CurCPU = 0;
			if ( (CPUStop & CPUStop_GXStall) != 0 )
			{
				ARM9Timestamp = Math.Min( ARM9Target, ARM9Timestamp + (GPU3D.CyclesToRunFor() << 1) );
			}
			else if ( (CPUStop & CPUStop_DMA9) != 0 )
			{
				DMAs[0].Run();
				if ( (CPUStop & CPUStop_GXStall) == 0 ) DMAs[1].Run();
				if ( (CPUStop & CPUStop_GXStall) == 0 ) DMAs[2].Run();
				if ( (CPUStop & CPUStop_GXStall) == 0 ) DMAs[3].Run();
			}
			else
			{
				ARM9.Execute();
			}
			RunTimers( 0 );
			GPU3D.Run();

			target = ARM9Timestamp >> 1;
			CurCPU = 1;
			while ( ARM7Timestamp < target )
			{
				ARM7Target = target;
				if ( (CPUStop & CPUStop_DMA7) != 0 )
					RunDMAs( 1 );
				else
					ARM7.Execute();
				RunTimers( 1 );
			}

			RunSystem( target );
		}

		_audio.EndFrame();
		PresentFrame();

		_renderer3D.Snapshot();
	}

	private void PresentFrame()
	{
		_render2D_A.SetOutput3D( _renderer3D.OutputTexture );
		_render2D_B.SetOutput3D( _renderer3D.OutputTexture );
	}

	public bool StepFrame()
	{
		RunFrame();
		return true;
	}

	public void SetButtons( int player, ulong pressedMask )
	{
		KeyInput = (ushort)(~pressedMask & 0x03FF);
		int btns = 0x003F & ~(int)((pressedMask >> 10) & 0x3);
		ExtKeyIn = (ushort)((ExtKeyIn & 0x40) | btns);
	}

	public void SetTouch( bool down, int x, int y )
	{
		if ( down )
		{
			if ( x < 0 ) x = 0; else if ( x > 255 ) x = 255;
			if ( y < 0 ) y = 0; else if ( y > 191 ) y = 191;
			TouchScreen( x, y );
		}
		else
		{
			ReleaseScreen();
		}
	}

	public byte[] SaveState( byte[] screenshot ) => [];

	public void LoadState( byte[] data ) { }
}
