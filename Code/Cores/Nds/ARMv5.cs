namespace Emulotl.Nds;

public sealed partial class ARMv5 : ARM
{
	public uint CP15Control;

	public uint RNGSeed;
	public uint TraceProcessID;

	public uint DTCMSetting, ITCMSetting;

	public uint ITCMSize;
	public uint DTCMBase, DTCMMask;
	public int RegionCodeCycles;

	public byte[] ITCM = new byte[NdsConstants.Arm9ItcmSize];
	public byte[] DTCM = new byte[NdsConstants.Arm9DtcmSize];

	public byte[] ICache = new byte[0x2000];
	public uint[] ICacheTags = new uint[64 * 4];
	public byte[] ICacheCount = new byte[64];

	public uint PU_CodeCacheable;
	public uint PU_DataCacheable;
	public uint PU_DataCacheWrite;

	public uint PU_CodeRW;
	public uint PU_DataRW;

	public uint[] PU_Region = new uint[8];

	public byte[] PU_PrivMap = new byte[0x100000];
	public byte[] PU_UserMap = new byte[0x100000];
	public byte[] PU_Map;

	public byte[,] MemTimings = new byte[0x100000, 4];

	public ARMv5( NDS nds ) : base( 0, nds )
	{
		PU_Map = PU_PrivMap;
	}

	public override void Reset()
	{
		PU_Map = PU_PrivMap;
		base.Reset();
	}

	public override void FillPipeline()
	{
		SetupCodeMem( R[15] );

		if ( (CPSR & 0x20) != 0 )
		{
			if ( ((R[15] - 2) & 0x2) != 0 )
			{
				NextInstr[0] = CodeRead32( R[15] - 4, false ) >> 16;
				NextInstr[1] = CodeRead32( R[15], false );
			}
			else
			{
				NextInstr[0] = CodeRead32( R[15] - 2, false );
				NextInstr[1] = NextInstr[0] >> 16;
			}
		}
		else
		{
			NextInstr[0] = CodeRead32( R[15] - 4, false );
			NextInstr[1] = CodeRead32( R[15], false );
		}
	}

	public override void JumpTo( uint addr, bool restorecpsr = false )
	{
		if ( restorecpsr )
		{
			RestoreCPSR();

			if ( (CPSR & 0x20) != 0 ) addr |= 0x1;
			else addr &= ~0x1u;
		}

		uint oldregion = R[15] >> 24;
		uint newregion = addr >> 24;

		RegionCodeCycles = MemTimings[addr >> 12, 0];

		if ( (addr & 0x1) != 0 )
		{
			addr &= ~0x1u;
			R[15] = addr + 2;

			if ( newregion != oldregion ) SetupCodeMem( addr );

			if ( (addr & 0x2) != 0 )
			{
				NextInstr[0] = CodeRead32( addr - 2, true ) >> 16;
				Cycles += CodeCycles;
				NextInstr[1] = CodeRead32( addr + 2, false );
				Cycles += CodeCycles;
			}
			else
			{
				NextInstr[0] = CodeRead32( addr, true );
				NextInstr[1] = NextInstr[0] >> 16;
				Cycles += CodeCycles;
			}

			CPSR |= 0x20;
		}
		else
		{
			addr &= ~0x3u;
			R[15] = addr + 4;

			if ( newregion != oldregion ) SetupCodeMem( addr );

			NextInstr[0] = CodeRead32( addr, true );
			Cycles += CodeCycles;
			NextInstr[1] = CodeRead32( addr + 4, false );
			Cycles += CodeCycles;

			CPSR &= ~0x20u;
		}

		if ( (PU_Map[addr >> 12] & 0x04) == 0 )
		{
			PrefetchAbort();
			return;
		}

		NDS.MonitorARM9Jump( addr );
	}

	public void PrefetchAbort()
	{
		Platform.Log( LogLevel.Warn, $"ARM9: prefetch abort ({R[15]:X8})" );

		uint oldcpsr = CPSR;
		CPSR &= ~0xBFu;
		CPSR |= 0x97;
		UpdateMode( oldcpsr, CPSR );

		if ( (PU_Map[ExceptionBase >> 12] & 0x04) == 0 )
		{
			Platform.Log( LogLevel.Error, "!!!!! EXCEPTION REGION NOT EXECUTABLE. THIS IS VERY BAD!!" );
			Halt( 1 );
			return;
		}

		R_ABT[2] = oldcpsr;
		R[14] = R[15] + ((oldcpsr & 0x20) != 0 ? 4u : 0u);
		JumpTo( ExceptionBase + 0x0C );
	}

	public void DataAbort()
	{
		Platform.Log( LogLevel.Warn, $"ARM9: data abort ({R[15]:X8})" );

		uint oldcpsr = CPSR;
		CPSR &= ~0xBFu;
		CPSR |= 0x97;
		UpdateMode( oldcpsr, CPSR );

		R_ABT[2] = oldcpsr;
		R[14] = R[15] + ((oldcpsr & 0x20) != 0 ? 4u : 0u);
		JumpTo( ExceptionBase + 0x10 );
	}

	public void Execute()
	{
		if ( Halted != 0 )
		{
			if ( Halted == 2 )
			{
				Halted = 0;
			}
			else if ( NDS.HaltInterrupted( 0 ) )
			{
				Halted = 0;
				if ( (NDS.IME[0] & 0x1) != 0 )
					TriggerIRQ();
			}
			else
			{
				NDS.ARM9Timestamp = NDS.ARM9Target;
				return;
			}
		}

		while ( NDS.ARM9Timestamp < NDS.ARM9Target )
		{
			if ( (CPSR & 0x20) != 0 )
			{
				R[15] += 2;
				CurInstr = NextInstr[0];
				NextInstr[0] = NextInstr[1];
				if ( (R[15] & 0x2) != 0 ) { NextInstr[1] >>= 16; CodeCycles = 0; }
				else NextInstr[1] = CodeRead32( R[15], false );

				uint icode = (CurInstr >> 6) & 0x3FF;
				ARMInterpreter.THUMBInstrTable[(int)icode]( this );
			}
			else
			{
				R[15] += 4;
				CurInstr = NextInstr[0];
				NextInstr[0] = NextInstr[1];
				NextInstr[1] = CodeRead32( R[15], false );

				if ( CheckCondition( CurInstr >> 28 ) )
				{
					uint icode = ((CurInstr >> 4) & 0xF) | ((CurInstr >> 16) & 0xFF0);
					ARMInterpreter.ARMInstrTable[(int)icode]( this );
				}
				else if ( (CurInstr & 0xFE000000) == 0xFA000000 )
				{
					ARMInterpreter.A_BLX_IMM( this );
				}
				else
				{
					AddCycles_C();
				}
			}

			if ( Halted != 0 )
			{
				if ( Halted == 1 && NDS.ARM9Timestamp < NDS.ARM9Target )
					NDS.ARM9Timestamp = NDS.ARM9Target;
				break;
			}

			if ( IRQ != 0 )
				TriggerIRQ();

			NDS.ARM9Timestamp += Cycles;
			Cycles = 0;
		}

		if ( Halted == 2 )
			Halted = 0;
	}

	public override void AddCycles_C()
	{
		int numC = (R[15] & 0x2) != 0 ? 0 : CodeCycles;
		Cycles += numC;
	}

	public override void AddCycles_CI( int numI )
	{
		int numC = (R[15] & 0x2) != 0 ? 0 : CodeCycles;
		Cycles += numC + numI;
	}

	public override void AddCycles_CDI()
	{
		int numC = (R[15] & 0x2) != 0 ? 0 : CodeCycles;
		int numD = DataCycles;
		Cycles += Math.Max( numC + numD - 6, Math.Max( numC, numD ) );
	}

	public override void AddCycles_CD()
	{
		int numC = (R[15] & 0x2) != 0 ? 0 : CodeCycles;
		int numD = DataCycles;
		Cycles += Math.Max( numC + numD - 6, Math.Max( numC, numD ) );
	}

	protected override byte BusRead8( uint addr ) => NDS.ARM9Read8( addr );
	protected override ushort BusRead16( uint addr ) => NDS.ARM9Read16( addr );
	protected override uint BusRead32( uint addr ) => NDS.ARM9Read32( addr );
	protected override void BusWrite8( uint addr, byte val ) => NDS.ARM9Write8( addr, val );
	protected override void BusWrite16( uint addr, ushort val ) => NDS.ARM9Write16( addr, val );
	protected override void BusWrite32( uint addr, uint val ) => NDS.ARM9Write32( addr, val );
}
