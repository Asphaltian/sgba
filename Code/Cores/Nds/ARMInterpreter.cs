namespace Emulotl.Nds;

public delegate void InstrFunc( ARM cpu );

public static partial class ARMInterpreter
{
	public static readonly InstrFunc[] ARMInstrTable = new InstrFunc[4096];
	public static readonly InstrFunc[] THUMBInstrTable = new InstrFunc[1024];

	static ARMInterpreter()
	{
		for ( int i = 0; i < ARMInstrTable.Length; i++ )
			ARMInstrTable[i] = A_UNK;
		for ( int i = 0; i < THUMBInstrTable.Length; i++ )
			THUMBInstrTable[i] = T_UNK;

		BuildAluTable();
		BuildMultiplyTable();
		BuildBranchTable();
		BuildLoadStoreTable();
		BuildSpecialsTable();
		BuildThumbTable();
	}

	private static void BuildSpecialsTable()
	{
		for ( int low = 0; low < 16; low++ )
		{
			ARMInstrTable[(0x32 << 4) | low] = A_MSR_IMM;
			ARMInstrTable[(0x36 << 4) | low] = A_MSR_IMM;
		}

		for ( int h = 0xE0; h <= 0xEF; h++ )
		{
			InstrFunc fn = (h & 1) == 0 ? A_MCR : A_MRC;
			for ( int low = 1; low < 16; low += 2 )
				ARMInstrTable[(h << 4) | low] = fn;
		}

		for ( int h = 0xF0; h <= 0xFF; h++ )
		{
			for ( int low = 0; low < 16; low++ )
				ARMInstrTable[(h << 4) | low] = A_SVC;
		}
	}

	public static void A_UNK( ARM cpu )
	{
		Platform.Log( LogLevel.Warn, $"undefined ARM{(cpu.Num != 0 ? 7 : 9)} instruction {cpu.CurInstr:X8} @ {cpu.R[15] - 8:X8}" );

		uint oldcpsr = cpu.CPSR;
		cpu.CPSR &= ~0xBFu;
		cpu.CPSR |= 0x9B;
		cpu.UpdateMode( oldcpsr, cpu.CPSR );

		cpu.R_UND[2] = oldcpsr;
		cpu.R[14] = cpu.R[15] - 4;
		cpu.JumpTo( cpu.ExceptionBase + 0x04 );
	}

	public static void T_UNK( ARM cpu )
	{
		Platform.Log( LogLevel.Warn, $"undefined THUMB{(cpu.Num != 0 ? 7 : 9)} instruction {cpu.CurInstr:X4} @ {cpu.R[15] - 4:X8}" );

		uint oldcpsr = cpu.CPSR;
		cpu.CPSR &= ~0xBFu;
		cpu.CPSR |= 0x9B;
		cpu.UpdateMode( oldcpsr, cpu.CPSR );

		cpu.R_UND[2] = oldcpsr;
		cpu.R[14] = cpu.R[15] - 2;
		cpu.JumpTo( cpu.ExceptionBase + 0x04 );
	}

	private static uint MsrMask( ARM cpu, bool spsr )
	{
		uint mask = 0;
		if ( (cpu.CurInstr & (1 << 16)) != 0 ) mask |= 0x000000FF;
		if ( (cpu.CurInstr & (1 << 17)) != 0 ) mask |= 0x0000FF00;
		if ( (cpu.CurInstr & (1 << 18)) != 0 ) mask |= 0x00FF0000;
		if ( (cpu.CurInstr & (1 << 19)) != 0 ) mask |= 0xFF000000;

		if ( !spsr )
			mask &= 0xFFFFFFDF;

		if ( (cpu.CPSR & 0x1F) == 0x10 )
			mask &= 0xFFFFFF00;

		return mask;
	}

	private static void WritePsr( ARM cpu, ref uint psr, uint mask, uint val, bool isCpsr )
	{
		uint oldpsr = psr;
		psr = (psr & ~mask) | (val & mask);
		if ( isCpsr )
			cpu.UpdateMode( oldpsr, cpu.CPSR );
	}

	private static void DoMsr( ARM cpu, uint val )
	{
		bool spsr = (cpu.CurInstr & (1 << 22)) != 0;
		uint mask = MsrMask( cpu, spsr );
		val |= 0x00000010;

		if ( !spsr )
		{
			WritePsr( cpu, ref cpu.CPSR, mask, val, true );
		}
		else
		{
			switch ( cpu.CPSR & 0x1F )
			{
				case 0x11: WritePsr( cpu, ref cpu.R_FIQ[7], mask, val, false ); break;
				case 0x12: WritePsr( cpu, ref cpu.R_IRQ[2], mask, val, false ); break;
				case 0x13: WritePsr( cpu, ref cpu.R_SVC[2], mask, val, false ); break;
				case 0x14:
				case 0x15:
				case 0x16:
				case 0x17: WritePsr( cpu, ref cpu.R_ABT[2], mask, val, false ); break;
				case 0x18:
				case 0x19:
				case 0x1A:
				case 0x1B: WritePsr( cpu, ref cpu.R_UND[2], mask, val, false ); break;
				default: cpu.AddCycles_C(); return;
			}
		}

		cpu.AddCycles_C();
	}

	public static void A_MSR_IMM( ARM cpu )
	{
		uint val = ARM.ROR( cpu.CurInstr & 0xFF, (cpu.CurInstr >> 7) & 0x1E );
		DoMsr( cpu, val );
	}

	public static void A_MSR_REG( ARM cpu )
	{
		uint val = cpu.R[cpu.CurInstr & 0xF];
		DoMsr( cpu, val );
	}

	public static void A_MRS( ARM cpu )
	{
		uint psr;
		if ( (cpu.CurInstr & (1 << 22)) != 0 )
		{
			switch ( cpu.CPSR & 0x1F )
			{
				case 0x11: psr = cpu.R_FIQ[7]; break;
				case 0x12: psr = cpu.R_IRQ[2]; break;
				case 0x13: psr = cpu.R_SVC[2]; break;
				case 0x14:
				case 0x15:
				case 0x16:
				case 0x17: psr = cpu.R_ABT[2]; break;
				case 0x18:
				case 0x19:
				case 0x1A:
				case 0x1B: psr = cpu.R_UND[2]; break;
				default: psr = cpu.CPSR; break;
			}
		}
		else
		{
			psr = cpu.CPSR;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = psr;
		cpu.AddCycles_C();
	}

	public static void A_MCR( ARM cpu )
	{
		if ( (cpu.CPSR & 0x1F) == 0x10 )
		{
			A_UNK( cpu );
			return;
		}

		uint cp = (cpu.CurInstr >> 8) & 0xF;
		uint cn = (cpu.CurInstr >> 16) & 0xF;
		uint cm = cpu.CurInstr & 0xF;
		uint cpinfo = (cpu.CurInstr >> 5) & 0x7;

		if ( cpu.Num == 0 && cp == 15 )
		{
			((ARMv5)cpu).CP15Write( (cn << 8) | (cm << 4) | cpinfo, cpu.R[(cpu.CurInstr >> 12) & 0xF] );
		}
		else if ( cpu.Num == 1 && cp == 14 )
		{
			Platform.Log( LogLevel.Debug, $"MCR p14,{cn},{cm},{cpinfo} on ARM7" );
		}
		else
		{
			Platform.Log( LogLevel.Warn, $"bad MCR opcode p{cp},{cn},{cm},{cpinfo} on ARM{(cpu.Num != 0 ? 7 : 9)}" );
			A_UNK( cpu );
			return;
		}

		cpu.AddCycles_CI( 1 + 1 );
	}

	public static void A_MRC( ARM cpu )
	{
		if ( (cpu.CPSR & 0x1F) == 0x10 )
		{
			A_UNK( cpu );
			return;
		}

		uint cp = (cpu.CurInstr >> 8) & 0xF;
		uint cn = (cpu.CurInstr >> 16) & 0xF;
		uint cm = cpu.CurInstr & 0xF;
		uint cpinfo = (cpu.CurInstr >> 5) & 0x7;

		if ( cpu.Num == 0 && cp == 15 )
		{
			cpu.R[(cpu.CurInstr >> 12) & 0xF] = ((ARMv5)cpu).CP15Read( (cn << 8) | (cm << 4) | cpinfo );
		}
		else if ( cpu.Num == 1 && cp == 14 )
		{
			Platform.Log( LogLevel.Debug, $"MRC p14,{cn},{cm},{cpinfo} on ARM7" );
		}
		else
		{
			Platform.Log( LogLevel.Warn, $"bad MRC opcode p{cp},{cn},{cm},{cpinfo} on ARM{(cpu.Num != 0 ? 7 : 9)}" );
			A_UNK( cpu );
			return;
		}

		cpu.AddCycles_CI( 2 + 1 );
	}

	public static void A_SVC( ARM cpu )
	{
		uint oldcpsr = cpu.CPSR;
		cpu.CPSR &= ~0xBFu;
		cpu.CPSR |= 0x93;
		cpu.UpdateMode( oldcpsr, cpu.CPSR );

		cpu.R_SVC[2] = oldcpsr;
		cpu.R[14] = cpu.R[15] - 4;
		cpu.JumpTo( cpu.ExceptionBase + 0x08 );
	}

	public static void T_SVC( ARM cpu )
	{
		uint oldcpsr = cpu.CPSR;
		cpu.CPSR &= ~0xBFu;
		cpu.CPSR |= 0x93;
		cpu.UpdateMode( oldcpsr, cpu.CPSR );

		cpu.R_SVC[2] = oldcpsr;
		cpu.R[14] = cpu.R[15] - 2;
		cpu.JumpTo( cpu.ExceptionBase + 0x08 );
	}
}
