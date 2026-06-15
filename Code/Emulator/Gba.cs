using System.Collections.Generic;
using Emulotl;

namespace sGBA;

public class Gba : IEmulatorCore
{
	public GbaTiming Timing { get; } = new();
	public ArmCore Cpu { get; private set; }
	public GbaMemory Memory { get; private set; }
	public GbaVideo Video { get; private set; }
	public GbaIo Io { get; private set; }
	public GbaDmaController Dma { get; private set; }
	public GbaTimerController Timers { get; private set; }
	public GbaBios Bios { get; private set; }
	public GbaAudio Audio { get; private set; }
	public GbaSavedata Savedata { get; private set; }
	public GbaCartridgeHardware Hardware { get; private set; }

	public bool IsRunning { get; set; }
	public int CyclesThisFrame { get; set; }
	public long FrameCounter { get; set; }
	public long TotalCycles { get; set; }
	public ushort KeysActive { get; set; }
	public ushort KeysLast { get; set; } = 0x400;
	public bool AllowOpposingDirections { get; set; } = true;
	public bool HaltPending { get; set; }

	private readonly IVideoOutput[] _screens;

	public Gba()
	{
		Memory = new GbaMemory( this );
		Cpu = new ArmCore( this );
		Video = new GbaVideo( this );
		Io = new GbaIo( this );
		Dma = new GbaDmaController( this );
		Timers = new GbaTimerController( this );
		Bios = new GbaBios( this );
		Audio = new GbaAudio( this );
		Savedata = new GbaSavedata( this );
		Hardware = new GbaCartridgeHardware( this );

		_screens = [Video];
	}

	public SystemProfile Profile => GbaSystem.Profile;

	IReadOnlyList<IVideoOutput> IEmulatorCore.Screens => _screens;
	IAudioOutput IEmulatorCore.Audio => Audio;
	ISaveData IEmulatorCore.SaveData => Savedata;

	public void SetButtons( int player, ulong pressedMask )
	{
		KeysActive = (ushort)(pressedMask & 0x03FF);
		Io.TestKeypadIrq();
	}

	public byte[] SaveState( byte[] screenshot ) => GbaSerialize.Save( this, screenshot );

	public void LoadState( byte[] data ) => GbaSerialize.Load( this, data );

	public void LoadRom( byte[] romData )
	{
		Memory.LoadRom( romData );
		Savedata.ForceType( romData );
		Hardware.InitRtc( romData );
	}

	public void Reset()
	{
		Timing.Clear();
		Memory.Reset();
		Cpu.Reset();
		Video.Reset();
		Io.Reset();
		Dma.Reset();
		Timers.Reset();
		Audio.Reset();
		Savedata.Reset();
		Hardware.Reset();
		Memory.InstallHleBios();
		Cpu.SkipBios();

		Io.ApplySkipBiosState();

		KeysLast = 0x400;
		HaltPending = false;

		CyclesThisFrame = 0;
		FrameCounter = 0;
		TotalCycles = 0;
		IsRunning = true;
		_frameInProgress = false;
	}

	public void RunFrame()
	{
		if ( !IsRunning ) return;

		Audio.BeginFrame();
		Io.TestKeypadIrq();

		long startCounter = FrameCounter;
		while ( IsRunning && FrameCounter == startCounter )
		{
			long next = Timing.NextEvent;
			if ( next == long.MaxValue )
				break;

			if ( next <= Cpu.Cycles )
			{
				DrainDueEvents();
				continue;
			}

			RunCpuTo( next );
			if ( Cpu.Cycles < next )
				break;
		}
	}

	private void DrainDueEvents()
	{
		Io.BeginEventProcessing();
		Timing.Tick( Cpu.Cycles );
		Io.EndEventProcessing();
	}

	private bool _frameInProgress;
	private long _frameStartCounter;

	private const int StepFrameEventBudget = 4096;

	public bool FrameInProgress => _frameInProgress;

	public bool StepFrame()
	{
		if ( !IsRunning ) return false;
		if ( Io.LockstepBlocked ) return false;

		if ( !_frameInProgress )
		{
			Audio.BeginFrame();
			Io.TestKeypadIrq();
			_frameStartCounter = FrameCounter;
			_frameInProgress = true;
		}

		int budget = 0;
		while ( FrameCounter == _frameStartCounter )
		{
			long next = Timing.NextEvent;
			if ( next == long.MaxValue )
				break;

			if ( next <= Cpu.Cycles )
			{
				DrainDueEvents();
				continue;
			}

			RunCpuTo( next );
			if ( Io.LockstepBlocked || Cpu.Cycles < next )
				return false;

			if ( ++budget >= StepFrameEventBudget )
				return false;
		}

		_frameInProgress = false;
		return true;
	}

	private void RunCpuTo( long target )
	{
		while ( Cpu.Cycles < target )
		{
			if ( Io.LockstepBlocked )
			{
				if ( Io.NextDriverEvent <= Cpu.Cycles )
					Io.ProcessDriverEvent();
				if ( Io.LockstepBlocked )
					return;
			}

			if ( Timing.NextEvent <= Cpu.Cycles )
			{
				DrainDueEvents();
				continue;
			}

			if ( Cpu.Halted )
			{
				long next = Math.Min( Timing.NextEvent, target );
				if ( next > Cpu.Cycles )
					Cpu.Cycles = next;
				continue;
			}

			Cpu.Run( target );
		}
	}

	public void SetKeyState( GbaKey key, bool pressed )
	{
		if ( pressed )
			KeysActive |= (ushort)key;
		else
			KeysActive &= (ushort)~(ushort)key;
		Io.TestKeypadIrq();
	}
}

[Flags]
public enum GbaKey : ushort
{
	A = 1 << 0,
	B = 1 << 1,
	Select = 1 << 2,
	Start = 1 << 3,
	Right = 1 << 4,
	Left = 1 << 5,
	Up = 1 << 6,
	Down = 1 << 7,
	R = 1 << 8,
	L = 1 << 9,
}
