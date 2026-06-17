namespace Emulotl.Nds;

public static partial class ARMInterpreter
{
	public static void A_B( ARM cpu )
	{
		int offset = (int)(cpu.CurInstr << 8) >> 6;
		cpu.JumpTo( cpu.R[15] + (uint)offset );
	}

	public static void A_BL( ARM cpu )
	{
		int offset = (int)(cpu.CurInstr << 8) >> 6;
		cpu.R[14] = cpu.R[15] - 4;
		cpu.JumpTo( cpu.R[15] + (uint)offset );
	}

	public static void A_BLX_IMM( ARM cpu )
	{
		int offset = (int)(cpu.CurInstr << 8) >> 6;
		if ( (cpu.CurInstr & 0x01000000) != 0 ) offset += 2;
		cpu.R[14] = cpu.R[15] - 4;
		cpu.JumpTo( cpu.R[15] + (uint)offset + 1 );
	}

	public static void A_BX( ARM cpu )
	{
		cpu.JumpTo( cpu.R[cpu.CurInstr & 0xF] );
	}

	public static void A_BLX_REG( ARM cpu )
	{
		uint lr = cpu.R[15] - 4;
		cpu.JumpTo( cpu.R[cpu.CurInstr & 0xF] );
		cpu.R[14] = lr;
	}

	public static void T_BCOND( ARM cpu )
	{
		if ( cpu.CheckCondition( (cpu.CurInstr >> 8) & 0xF ) )
		{
			int offset = (int)(cpu.CurInstr << 24) >> 23;
			cpu.JumpTo( cpu.R[15] + (uint)offset + 1 );
		}
		else
		{
			cpu.AddCycles_C();
		}
	}

	public static void T_BX( ARM cpu )
	{
		cpu.JumpTo( cpu.R[(cpu.CurInstr >> 3) & 0xF] );
	}

	public static void T_BLX_REG( ARM cpu )
	{
		if ( cpu.Num == 1 )
		{
			Platform.Log( LogLevel.Warn, "!! THUMB BLX_REG ON ARM7" );
			return;
		}

		uint lr = cpu.R[15] - 1;
		cpu.JumpTo( cpu.R[(cpu.CurInstr >> 3) & 0xF] );
		cpu.R[14] = lr;
	}

	public static void T_B( ARM cpu )
	{
		int offset = (int)((cpu.CurInstr & 0x7FF) << 21) >> 20;
		cpu.JumpTo( cpu.R[15] + (uint)offset + 1 );
	}

	public static void T_BL_LONG_1( ARM cpu )
	{
		int offset = (int)((cpu.CurInstr & 0x7FF) << 21) >> 9;
		cpu.R[14] = cpu.R[15] + (uint)offset;
		cpu.AddCycles_C();
	}

	public static void T_BL_LONG_2( ARM cpu )
	{
		uint offset = (cpu.CurInstr & 0x7FF) << 1;
		uint pc = cpu.R[14] + offset;

		if ( cpu.Num == 1 || (cpu.CurInstr & (1 << 12)) != 0 )
		{
			pc |= 1;
		}
		else
		{
			if ( (cpu.CurInstr & 1) != 0 )
			{
				T_UNK( cpu );
				return;
			}

			pc &= ~1u;
		}

		cpu.R[14] = (cpu.R[15] - 2) | 1;
		cpu.JumpTo( pc );
	}

	private static void BuildBranchTable()
	{
		for ( int i = 0; i < 256; i++ )
		{
			ARMInstrTable[0xA00 | i] = A_B;
			ARMInstrTable[0xB00 | i] = A_BL;
		}

		ARMInstrTable[0x121] = A_BX;
		ARMInstrTable[0x123] = A_BLX_REG;
	}
}
