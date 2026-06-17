namespace Emulotl.Nds;

public sealed class ARMv4 : ARM
{
	public ARMv4( NDS nds ) : base( 1, nds ) { }

	public override void FillPipeline()
	{
		SetupCodeMem( R[15] );

		if ( (CPSR & 0x20) != 0 )
		{
			NextInstr[0] = CodeRead16( R[15] - 2 );
			NextInstr[1] = CodeRead16( R[15] );
		}
		else
		{
			NextInstr[0] = CodeRead32( R[15] - 4 );
			NextInstr[1] = CodeRead32( R[15] );
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

		CodeRegion = addr >> 24;
		CodeCycles = (int)(addr >> 15);
		SetupCodeMem( addr );

		if ( (addr & 0x1) != 0 )
		{
			addr &= ~0x1u;
			R[15] = addr + 2;

			NextInstr[0] = CodeRead16( addr );
			NextInstr[1] = CodeRead16( addr + 2 );
			Cycles += NDS.ARM7MemTimings[CodeCycles, 0] + NDS.ARM7MemTimings[CodeCycles, 1];

			CPSR |= 0x20;
		}
		else
		{
			addr &= ~0x3u;
			R[15] = addr + 4;

			NextInstr[0] = CodeRead32( addr );
			NextInstr[1] = CodeRead32( addr + 4 );
			Cycles += NDS.ARM7MemTimings[CodeCycles, 2] + NDS.ARM7MemTimings[CodeCycles, 3];

			CPSR &= ~0x20u;
		}
	}

	public void Execute()
	{
		if ( Halted != 0 )
		{
			if ( Halted == 2 )
			{
				Halted = 0;
			}
			else if ( NDS.HaltInterrupted( 1 ) )
			{
				Halted = 0;
				if ( (NDS.IME[1] & 0x1) != 0 )
					TriggerIRQ();
			}
			else
			{
				NDS.ARM7Timestamp = NDS.ARM7Target;
				return;
			}
		}

		while ( NDS.ARM7Timestamp < NDS.ARM7Target )
		{
			if ( (CPSR & 0x20) != 0 )
			{
				R[15] += 2;
				CurInstr = NextInstr[0];
				NextInstr[0] = NextInstr[1];
				NextInstr[1] = CodeRead16( R[15] );

				uint icode = CurInstr >> 6;
				ARMInterpreter.THUMBInstrTable[(int)icode]( this );
			}
			else
			{
				R[15] += 4;
				CurInstr = NextInstr[0];
				NextInstr[0] = NextInstr[1];
				NextInstr[1] = CodeRead32( R[15] );

				if ( CheckCondition( CurInstr >> 28 ) )
				{
					uint icode = ((CurInstr >> 4) & 0xF) | ((CurInstr >> 16) & 0xFF0);
					ARMInterpreter.ARMInstrTable[(int)icode]( this );
				}
				else
				{
					AddCycles_C();
				}
			}

			if ( Halted != 0 )
			{
				if ( Halted == 1 && NDS.ARM7Timestamp < NDS.ARM7Target )
					NDS.ARM7Timestamp = NDS.ARM7Target;
				break;
			}

			if ( IRQ != 0 )
				TriggerIRQ();

			NDS.ARM7Timestamp += Cycles;
			Cycles = 0;
		}

		if ( Halted == 2 )
			Halted = 0;
	}

	public ushort CodeRead16( uint addr )
	{
		if ( (addr & 0xFF800000) != CodeMemBase )
			SetupCodeMem( addr );
		if ( CodeMem.Mem != null )
		{
			uint o = CodeMem.Offset + (addr & CodeMem.Mask);
			return (ushort)(CodeMem.Mem[o] | (CodeMem.Mem[o + 1] << 8));
		}
		return BusRead16( addr );
	}

	public uint CodeRead32( uint addr )
	{
		if ( (addr & 0xFF800000) != CodeMemBase )
			SetupCodeMem( addr );
		if ( CodeMem.Mem != null )
		{
			uint o = CodeMem.Offset + (addr & CodeMem.Mask);
			return (uint)(CodeMem.Mem[o] | (CodeMem.Mem[o + 1] << 8) | (CodeMem.Mem[o + 2] << 16) | (CodeMem.Mem[o + 3] << 24));
		}
		return BusRead32( addr );
	}

	public override void DataRead8( uint addr, ref uint val )
	{
		val = BusRead8( addr );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 0];
	}

	private static bool IsMainRam( uint addr ) => (addr & 0xFF000000) == 0x02000000;
	private static ushort RdRam16( byte[] m, uint o ) => (ushort)(m[o] | (m[o + 1] << 8));
	private static uint RdRam32( byte[] m, uint o ) => (uint)(m[o] | (m[o + 1] << 8) | (m[o + 2] << 16) | (m[o + 3] << 24));
	private static void WrRam16( byte[] m, uint o, ushort v ) { m[o] = (byte)v; m[o + 1] = (byte)(v >> 8); }
	private static void WrRam32( byte[] m, uint o, uint v ) { m[o] = (byte)v; m[o + 1] = (byte)(v >> 8); m[o + 2] = (byte)(v >> 16); m[o + 3] = (byte)(v >> 24); }

	public override void DataRead16( uint addr, ref uint val )
	{
		addr &= ~1u;
		if ( IsMainRam( addr ) ) val = RdRam16( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead16( addr );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 0];
	}

	public override void DataRead32( uint addr, ref uint val )
	{
		addr &= ~3u;
		if ( IsMainRam( addr ) ) val = RdRam32( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead32( addr );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 2];
	}

	public override void DataRead32S( uint addr, ref uint val )
	{
		addr &= ~3u;
		if ( IsMainRam( addr ) ) val = RdRam32( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead32( addr );
		DataCycles += NDS.ARM7MemTimings[addr >> 15, 3];
	}

	public override void DataWrite8( uint addr, byte val )
	{
		BusWrite8( addr, val );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 0];
	}

	public override void DataWrite16( uint addr, ushort val )
	{
		addr &= ~1u;
		if ( IsMainRam( addr ) ) WrRam16( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite16( addr, val );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 0];
	}

	public override void DataWrite32( uint addr, uint val )
	{
		addr &= ~3u;
		if ( IsMainRam( addr ) ) WrRam32( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite32( addr, val );
		DataRegion = addr;
		DataCycles = NDS.ARM7MemTimings[addr >> 15, 2];
	}

	public override void DataWrite32S( uint addr, uint val )
	{
		addr &= ~3u;
		if ( IsMainRam( addr ) ) WrRam32( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite32( addr, val );
		DataCycles += NDS.ARM7MemTimings[addr >> 15, 3];
	}

	public override void AddCycles_C()
	{
		Cycles += NDS.ARM7MemTimings[CodeCycles, (CPSR & 0x20) != 0 ? 1 : 3];
	}

	public override void AddCycles_CI( int num )
	{
		Cycles += NDS.ARM7MemTimings[CodeCycles, (CPSR & 0x20) != 0 ? 0 : 2] + num;
	}

	public override void AddCycles_CDI()
	{
		int numC = NDS.ARM7MemTimings[CodeCycles, (CPSR & 0x20) != 0 ? 0 : 2];
		int numD = DataCycles;

		if ( (DataRegion >> 24) == 0x02 )
		{
			if ( CodeRegion == 0x02 )
				Cycles += numC + numD;
			else
			{
				numC++;
				Cycles += Math.Max( numC + numD - 3, Math.Max( numC, numD ) );
			}
		}
		else if ( CodeRegion == 0x02 )
		{
			numD++;
			Cycles += Math.Max( numC + numD - 3, Math.Max( numC, numD ) );
		}
		else
		{
			Cycles += numC + numD + 1;
		}
	}

	public override void AddCycles_CD()
	{
		int numC = NDS.ARM7MemTimings[CodeCycles, (CPSR & 0x20) != 0 ? 0 : 2];
		int numD = DataCycles;

		if ( (DataRegion >> 24) == 0x02 )
		{
			if ( CodeRegion == 0x02 )
				Cycles += numC + numD;
			else
				Cycles += Math.Max( numC + numD - 3, Math.Max( numC, numD ) );
		}
		else if ( CodeRegion == 0x02 )
		{
			Cycles += Math.Max( numC + numD - 3, Math.Max( numC, numD ) );
		}
		else
		{
			Cycles += numC + numD;
		}
	}

	protected override byte BusRead8( uint addr ) => NDS.ARM7Read8( addr );
	protected override ushort BusRead16( uint addr ) => NDS.ARM7Read16( addr );
	protected override uint BusRead32( uint addr ) => NDS.ARM7Read32( addr );
	protected override void BusWrite8( uint addr, byte val ) => NDS.ARM7Write8( addr, val );
	protected override void BusWrite16( uint addr, ushort val ) => NDS.ARM7Write16( addr, val );
	protected override void BusWrite32( uint addr, uint val ) => NDS.ARM7Write32( addr, val );
}
