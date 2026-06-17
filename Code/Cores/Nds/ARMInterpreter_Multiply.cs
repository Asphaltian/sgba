namespace Emulotl.Nds;

public static partial class ARMInterpreter
{
	private static void BuildMultiplyTable()
	{
		ARMInstrTable[0x009] = A_MUL; ARMInstrTable[0x019] = A_MUL;
		ARMInstrTable[0x029] = A_MLA; ARMInstrTable[0x039] = A_MLA;
		ARMInstrTable[0x089] = A_UMULL; ARMInstrTable[0x099] = A_UMULL;
		ARMInstrTable[0x0A9] = A_UMLAL; ARMInstrTable[0x0B9] = A_UMLAL;
		ARMInstrTable[0x0C9] = A_SMULL; ARMInstrTable[0x0D9] = A_SMULL;
		ARMInstrTable[0x0E9] = A_SMLAL; ARMInstrTable[0x0F9] = A_SMLAL;

		ARMInstrTable[0x100] = A_MRS;
		ARMInstrTable[0x105] = A_QADD;
		ARMInstrTable[0x108] = A_SMLAxy; ARMInstrTable[0x10A] = A_SMLAxy; ARMInstrTable[0x10C] = A_SMLAxy; ARMInstrTable[0x10E] = A_SMLAxy;

		ARMInstrTable[0x120] = A_MSR_REG;
		ARMInstrTable[0x125] = A_QSUB;
		ARMInstrTable[0x128] = A_SMLAWy; ARMInstrTable[0x12C] = A_SMLAWy;
		ARMInstrTable[0x12A] = A_SMULWy; ARMInstrTable[0x12E] = A_SMULWy;

		ARMInstrTable[0x140] = A_MRS;
		ARMInstrTable[0x145] = A_QDADD;
		ARMInstrTable[0x148] = A_SMLALxy; ARMInstrTable[0x14A] = A_SMLALxy; ARMInstrTable[0x14C] = A_SMLALxy; ARMInstrTable[0x14E] = A_SMLALxy;

		ARMInstrTable[0x160] = A_MSR_REG;
		ARMInstrTable[0x161] = A_CLZ;
		ARMInstrTable[0x165] = A_QDSUB;
		ARMInstrTable[0x168] = A_SMULxy; ARMInstrTable[0x16A] = A_SMULxy; ARMInstrTable[0x16C] = A_SMULxy; ARMInstrTable[0x16E] = A_SMULxy;
	}

	private static int MulCycles( ARM cpu, uint rs, int m0, int m1, int m2, int m3 )
	{
		if ( (rs & 0xFFFFFF00) == 0x00000000 || (rs & 0xFFFFFF00) == 0xFFFFFF00 ) return m0;
		if ( (rs & 0xFFFF0000) == 0x00000000 || (rs & 0xFFFF0000) == 0xFFFF0000 ) return m1;
		if ( (rs & 0xFF000000) == 0x00000000 || (rs & 0xFF000000) == 0xFF000000 ) return m2;
		return m3;
	}

	private static int MulCyclesU( uint rs, int m0, int m1, int m2, int m3 )
	{
		if ( (rs & 0xFFFFFF00) == 0x00000000 ) return m0;
		if ( (rs & 0xFFFF0000) == 0x00000000 ) return m1;
		if ( (rs & 0xFF000000) == 0x00000000 ) return m2;
		return m3;
	}

	public static void A_MUL( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		uint res = rm * rs;

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCycles( cpu, rs, 1, 2, 3, 4 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_MLA( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 12) & 0xF];

		uint res = (rm * rs) + rn;

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCycles( cpu, rs, 2, 3, 4, 5 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_UMULL( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		ulong res = (ulong)rm * rs;

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = (uint)res;
		cpu.R[(cpu.CurInstr >> 16) & 0xF] = (uint)(res >> 32);
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( (res >> 63) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCyclesU( rs, 2, 3, 4, 5 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_UMLAL( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		ulong res = (ulong)rm * rs;

		ulong rd = cpu.R[(cpu.CurInstr >> 12) & 0xF] | ((ulong)cpu.R[(cpu.CurInstr >> 16) & 0xF] << 32);
		res += rd;

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = (uint)res;
		cpu.R[(cpu.CurInstr >> 16) & 0xF] = (uint)(res >> 32);
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( (res >> 63) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCyclesU( rs, 2, 3, 4, 5 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_SMULL( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		long res = (long)(int)rm * (int)rs;

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = (uint)res;
		cpu.R[(cpu.CurInstr >> 16) & 0xF] = (uint)((ulong)res >> 32);
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( ((ulong)res >> 63) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCycles( cpu, rs, 2, 3, 4, 5 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_SMLAL( ARM cpu )
	{
		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		long res = (long)(int)rm * (int)rs;

		long rd = (long)(cpu.R[(cpu.CurInstr >> 12) & 0xF] | ((ulong)cpu.R[(cpu.CurInstr >> 16) & 0xF] << 32));
		res += rd;

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = (uint)res;
		cpu.R[(cpu.CurInstr >> 16) & 0xF] = (uint)((ulong)res >> 32);
		if ( (cpu.CurInstr & (1 << 20)) != 0 )
		{
			cpu.SetNZ( ((ulong)res >> 63) != 0, res == 0 );
			if ( cpu.Num == 1 ) cpu.SetC( false );
		}

		int cycles = cpu.Num == 0
			? ((cpu.CurInstr & (1 << 20)) != 0 ? 3 : 1)
			: MulCycles( cpu, rs, 2, 3, 4, 5 );

		cpu.AddCycles_CI( cycles );
	}

	public static void A_SMLAxy( ARM cpu )
	{
		if ( cpu.Num != 0 ) return;

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 12) & 0xF];

		if ( (cpu.CurInstr & (1 << 5)) != 0 ) rm >>= 16;
		else rm &= 0xFFFF;
		if ( (cpu.CurInstr & (1 << 6)) != 0 ) rs >>= 16;
		else rs &= 0xFFFF;

		uint resMul = (uint)((short)rm * (short)rs);
		uint res = resMul + rn;

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		if ( OverflowAdd( resMul, rn ) )
			cpu.CPSR |= 0x08000000;

		cpu.AddCycles_C();
	}

	public static void A_SMLAWy( ARM cpu )
	{
		if ( cpu.Num != 0 ) return;

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 12) & 0xF];

		if ( (cpu.CurInstr & (1 << 6)) != 0 ) rs >>= 16;
		else rs &= 0xFFFF;

		uint resMul = (uint)(((long)(int)rm * (short)rs) >> 16);
		uint res = resMul + rn;

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		if ( OverflowAdd( resMul, rn ) )
			cpu.CPSR |= 0x08000000;

		cpu.AddCycles_C();
	}

	public static void A_SMULxy( ARM cpu )
	{
		if ( cpu.Num != 0 ) return;

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		if ( (cpu.CurInstr & (1 << 5)) != 0 ) rm >>= 16;
		else rm &= 0xFFFF;
		if ( (cpu.CurInstr & (1 << 6)) != 0 ) rs >>= 16;
		else rs &= 0xFFFF;

		uint res = (uint)((short)rm * (short)rs);

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_SMULWy( ARM cpu )
	{
		if ( cpu.Num != 0 ) return;

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		if ( (cpu.CurInstr & (1 << 6)) != 0 ) rs >>= 16;
		else rs &= 0xFFFF;

		uint res = (uint)(((long)(int)rm * (short)rs) >> 16);

		cpu.R[(cpu.CurInstr >> 16) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_SMLALxy( ARM cpu )
	{
		if ( cpu.Num != 0 ) return;

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rs = cpu.R[(cpu.CurInstr >> 8) & 0xF];

		if ( (cpu.CurInstr & (1 << 5)) != 0 ) rm >>= 16;
		else rm &= 0xFFFF;
		if ( (cpu.CurInstr & (1 << 6)) != 0 ) rs >>= 16;
		else rs &= 0xFFFF;

		long res = (long)(short)rm * (short)rs;

		long rd = (long)(cpu.R[(cpu.CurInstr >> 12) & 0xF] | ((ulong)cpu.R[(cpu.CurInstr >> 16) & 0xF] << 32));
		res += rd;

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = (uint)res;
		cpu.R[(cpu.CurInstr >> 16) & 0xF] = (uint)((ulong)res >> 32);

		cpu.AddCycles_CI( 1 );
	}

	public static void A_CLZ( ARM cpu )
	{
		if ( cpu.Num != 0 ) { A_UNK( cpu ); return; }

		uint val = cpu.R[cpu.CurInstr & 0xF];

		uint res = 0;
		while ( (val & 0xFF000000) == 0 )
		{
			res += 8;
			val <<= 8;
			val |= 0xFF;
		}
		while ( (val & 0x80000000) == 0 )
		{
			res++;
			val <<= 1;
			val |= 0x1;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_QADD( ARM cpu )
	{
		if ( cpu.Num != 0 ) { A_UNK( cpu ); return; }

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 16) & 0xF];

		uint res = rm + rn;
		if ( OverflowAdd( rm, rn ) )
		{
			res = (res & 0x80000000) != 0 ? 0x7FFFFFFF : 0x80000000;
			cpu.CPSR |= 0x08000000;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_QSUB( ARM cpu )
	{
		if ( cpu.Num != 0 ) { A_UNK( cpu ); return; }

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 16) & 0xF];

		uint res = rm - rn;
		if ( OverflowSub( rm, rn ) )
		{
			res = (res & 0x80000000) != 0 ? 0x7FFFFFFF : 0x80000000;
			cpu.CPSR |= 0x08000000;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_QDADD( ARM cpu )
	{
		if ( cpu.Num != 0 ) { A_UNK( cpu ); return; }

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 16) & 0xF];

		if ( OverflowAdd( rn, rn ) )
		{
			rn = (rn & 0x80000000) != 0 ? 0x80000000 : 0x7FFFFFFF;
			cpu.CPSR |= 0x08000000;
		}
		else rn <<= 1;

		uint res = rm + rn;
		if ( OverflowAdd( rm, rn ) )
		{
			res = (res & 0x80000000) != 0 ? 0x7FFFFFFF : 0x80000000;
			cpu.CPSR |= 0x08000000;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
		cpu.AddCycles_C();
	}

	public static void A_QDSUB( ARM cpu )
	{
		if ( cpu.Num != 0 ) { A_UNK( cpu ); return; }

		uint rm = cpu.R[cpu.CurInstr & 0xF];
		uint rn = cpu.R[(cpu.CurInstr >> 16) & 0xF];

		if ( OverflowAdd( rn, rn ) )
		{
			rn = (rn & 0x80000000) != 0 ? 0x80000000 : 0x7FFFFFFF;
			cpu.CPSR |= 0x08000000;
		}
		else rn <<= 1;

		uint res = rm - rn;
		if ( OverflowSub( rm, rn ) )
		{
			res = (res & 0x80000000) != 0 ? 0x7FFFFFFF : 0x80000000;
			cpu.CPSR |= 0x08000000;
		}

		cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
		cpu.AddCycles_C();
	}
}
