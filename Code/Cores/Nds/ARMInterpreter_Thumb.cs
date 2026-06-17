using System.Numerics;

namespace Emulotl.Nds;

public static partial class ARMInterpreter
{
	private static void FillThumb( int start, int count, InstrFunc fn )
	{
		for ( int i = 0; i < count; i++ )
			THUMBInstrTable[start + i] = fn;
	}

	private static void BuildThumbTable()
	{
		FillThumb( 0x000, 0x20, T_LSL_IMM );
		FillThumb( 0x020, 0x20, T_LSR_IMM );
		FillThumb( 0x040, 0x20, T_ASR_IMM );
		FillThumb( 0x060, 0x08, T_ADD_REG_ ); FillThumb( 0x068, 0x08, T_SUB_REG_ );
		FillThumb( 0x070, 0x08, T_ADD_IMM_ ); FillThumb( 0x078, 0x08, T_SUB_IMM_ );
		FillThumb( 0x080, 0x20, T_MOV_IMM );
		FillThumb( 0x0A0, 0x20, T_CMP_IMM );
		FillThumb( 0x0C0, 0x20, T_ADD_IMM );
		FillThumb( 0x0E0, 0x20, T_SUB_IMM );

		THUMBInstrTable[0x100] = T_AND_REG;
		THUMBInstrTable[0x101] = T_EOR_REG;
		THUMBInstrTable[0x102] = T_LSL_REG;
		THUMBInstrTable[0x103] = T_LSR_REG;
		THUMBInstrTable[0x104] = T_ASR_REG;
		THUMBInstrTable[0x105] = T_ADC_REG;
		THUMBInstrTable[0x106] = T_SBC_REG;
		THUMBInstrTable[0x107] = T_ROR_REG;
		THUMBInstrTable[0x108] = T_TST_REG;
		THUMBInstrTable[0x109] = T_NEG_REG;
		THUMBInstrTable[0x10A] = T_CMP_REG;
		THUMBInstrTable[0x10B] = T_CMN_REG;
		THUMBInstrTable[0x10C] = T_ORR_REG;
		THUMBInstrTable[0x10D] = T_MUL_REG;
		THUMBInstrTable[0x10E] = T_BIC_REG;
		THUMBInstrTable[0x10F] = T_MVN_REG;

		FillThumb( 0x110, 4, T_ADD_HIREG );
		FillThumb( 0x114, 4, T_CMP_HIREG );
		FillThumb( 0x118, 4, T_MOV_HIREG );
		THUMBInstrTable[0x11C] = T_BX;
		THUMBInstrTable[0x11D] = T_BX;
		THUMBInstrTable[0x11E] = T_BLX_REG;
		THUMBInstrTable[0x11F] = T_BLX_REG;

		FillThumb( 0x120, 0x20, T_LDR_PCREL );

		FillThumb( 0x140, 8, T_STR_REG ); FillThumb( 0x148, 8, T_STRH_REG );
		FillThumb( 0x150, 8, T_STRB_REG ); FillThumb( 0x158, 8, T_LDRSB_REG );
		FillThumb( 0x160, 8, T_LDR_REG ); FillThumb( 0x168, 8, T_LDRH_REG );
		FillThumb( 0x170, 8, T_LDRB_REG ); FillThumb( 0x178, 8, T_LDRSH_REG );

		FillThumb( 0x180, 0x20, T_STR_IMM );
		FillThumb( 0x1A0, 0x20, T_LDR_IMM );
		FillThumb( 0x1C0, 0x20, T_STRB_IMM );
		FillThumb( 0x1E0, 0x20, T_LDRB_IMM );
		FillThumb( 0x200, 0x20, T_STRH_IMM );
		FillThumb( 0x220, 0x20, T_LDRH_IMM );
		FillThumb( 0x240, 0x20, T_STR_SPREL );
		FillThumb( 0x260, 0x20, T_LDR_SPREL );
		FillThumb( 0x280, 0x20, T_ADD_PCREL );
		FillThumb( 0x2A0, 0x20, T_ADD_SPREL );

		FillThumb( 0x2C0, 4, T_ADD_SP );
		FillThumb( 0x2D0, 8, T_PUSH );
		FillThumb( 0x2F0, 8, T_POP );

		FillThumb( 0x300, 0x20, T_STMIA );
		FillThumb( 0x320, 0x20, T_LDMIA );

		FillThumb( 0x340, 0x30, T_BCOND );
		FillThumb( 0x370, 8, T_BCOND );
		FillThumb( 0x37C, 4, T_SVC );

		FillThumb( 0x380, 0x20, T_B );
		FillThumb( 0x3A0, 0x20, T_BL_LONG_2 );
		FillThumb( 0x3C0, 0x20, T_BL_LONG_1 );
		FillThumb( 0x3E0, 0x20, T_BL_LONG_2 );
	}

	public static void T_LSL_IMM( ARM cpu )
	{
		uint op = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint s = (cpu.CurInstr >> 6) & 0x1F;
		Shift( cpu, ref op, s, 0, false, true );
		cpu.R[cpu.CurInstr & 0x7] = op;
		cpu.SetNZ( (op & 0x80000000) != 0, op == 0 );
		cpu.AddCycles_C();
	}

	public static void T_LSR_IMM( ARM cpu )
	{
		uint op = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint s = (cpu.CurInstr >> 6) & 0x1F;
		Shift( cpu, ref op, s, 1, false, true );
		cpu.R[cpu.CurInstr & 0x7] = op;
		cpu.SetNZ( (op & 0x80000000) != 0, op == 0 );
		cpu.AddCycles_C();
	}

	public static void T_ASR_IMM( ARM cpu )
	{
		uint op = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint s = (cpu.CurInstr >> 6) & 0x1F;
		Shift( cpu, ref op, s, 2, false, true );
		cpu.R[cpu.CurInstr & 0x7] = op;
		cpu.SetNZ( (op & 0x80000000) != 0, op == 0 );
		cpu.AddCycles_C();
	}

	public static void T_ADD_REG_( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint res = a + b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_SUB_REG_( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint res = a - b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_ADD_IMM_( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint b = (cpu.CurInstr >> 6) & 0x7;
		uint res = a + b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_SUB_IMM_( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint b = (cpu.CurInstr >> 6) & 0x7;
		uint res = a - b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_MOV_IMM( ARM cpu )
	{
		uint b = cpu.CurInstr & 0xFF;
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = b;
		cpu.SetNZ( false, b == 0 );
		cpu.AddCycles_C();
	}

	public static void T_CMP_IMM( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 8) & 0x7];
		uint b = cpu.CurInstr & 0xFF;
		uint res = a - b;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_ADD_IMM( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 8) & 0x7];
		uint b = cpu.CurInstr & 0xFF;
		uint res = a + b;
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_SUB_IMM( ARM cpu )
	{
		uint a = cpu.R[(cpu.CurInstr >> 8) & 0x7];
		uint b = cpu.CurInstr & 0xFF;
		uint res = a - b;
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_AND_REG( ARM cpu )
	{
		uint res = cpu.R[cpu.CurInstr & 0x7] & cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_EOR_REG( ARM cpu )
	{
		uint res = cpu.R[cpu.CurInstr & 0x7] ^ cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_LSL_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7] & 0xFF;
		Shift( cpu, ref a, b, 0, true, true );
		cpu.R[cpu.CurInstr & 0x7] = a;
		cpu.SetNZ( (a & 0x80000000) != 0, a == 0 );
		cpu.AddCycles_CI( 1 );
	}

	public static void T_LSR_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7] & 0xFF;
		Shift( cpu, ref a, b, 1, true, true );
		cpu.R[cpu.CurInstr & 0x7] = a;
		cpu.SetNZ( (a & 0x80000000) != 0, a == 0 );
		cpu.AddCycles_CI( 1 );
	}

	public static void T_ASR_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7] & 0xFF;
		Shift( cpu, ref a, b, 2, true, true );
		cpu.R[cpu.CurInstr & 0x7] = a;
		cpu.SetNZ( (a & 0x80000000) != 0, a == 0 );
		cpu.AddCycles_CI( 1 );
	}

	public static void T_ADC_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint resTmp = a + b;
		uint carry = (cpu.CPSR & 0x20000000) != 0 ? 1u : 0u;
		uint res = resTmp + carry;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ) | CarryAdd( resTmp, carry ), OverflowAdc( a, b, carry ) );
		cpu.AddCycles_C();
	}

	public static void T_SBC_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint resTmp = a - b;
		uint carry = (cpu.CPSR & 0x20000000) != 0 ? 0u : 1u;
		uint res = resTmp - carry;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ) && CarrySub( resTmp, carry ), OverflowSbc( a, b, carry ) );
		cpu.AddCycles_C();
	}

	public static void T_ROR_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7] & 0xFF;
		Shift( cpu, ref a, b, 3, true, true );
		cpu.R[cpu.CurInstr & 0x7] = a;
		cpu.SetNZ( (a & 0x80000000) != 0, a == 0 );
		cpu.AddCycles_CI( 1 );
	}

	public static void T_TST_REG( ARM cpu )
	{
		uint res = cpu.R[cpu.CurInstr & 0x7] & cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_NEG_REG( ARM cpu )
	{
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint res = 0u - b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( 0, b ), OverflowSub( 0, b ) );
		cpu.AddCycles_C();
	}

	public static void T_CMP_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint res = a - b;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_CMN_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint res = a + b;
		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_ORR_REG( ARM cpu )
	{
		uint res = cpu.R[cpu.CurInstr & 0x7] | cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_MUL_REG( ARM cpu )
	{
		uint a = cpu.R[cpu.CurInstr & 0x7];
		uint b = cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint res = a * b;
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );

		int cycles = 0;
		if ( cpu.Num == 0 )
		{
			cycles += 3;
		}
		else
		{
			cpu.SetC( false );
			if ( (a & 0xFF000000) != 0 ) cycles += 4;
			else if ( (a & 0x00FF0000) != 0 ) cycles += 3;
			else if ( (a & 0x0000FF00) != 0 ) cycles += 2;
			else cycles += 1;
		}
		cpu.AddCycles_CI( cycles );
	}

	public static void T_BIC_REG( ARM cpu )
	{
		uint res = cpu.R[cpu.CurInstr & 0x7] & ~cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_MVN_REG( ARM cpu )
	{
		uint res = ~cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.R[cpu.CurInstr & 0x7] = res;
		cpu.SetNZ( (res & 0x80000000) != 0, res == 0 );
		cpu.AddCycles_C();
	}

	public static void T_ADD_HIREG( ARM cpu )
	{
		uint rd = (cpu.CurInstr & 0x7) | ((cpu.CurInstr >> 4) & 0x8);
		uint rs = (cpu.CurInstr >> 3) & 0xF;

		uint a = cpu.R[rd];
		uint b = cpu.R[rs];

		cpu.AddCycles_C();

		if ( rd == 15 ) cpu.JumpTo( (a + b) | 1 );
		else cpu.R[rd] = a + b;
	}

	public static void T_CMP_HIREG( ARM cpu )
	{
		uint rd = (cpu.CurInstr & 0x7) | ((cpu.CurInstr >> 4) & 0x8);
		uint rs = (cpu.CurInstr >> 3) & 0xF;

		uint a = cpu.R[rd];
		uint b = cpu.R[rs];
		uint res = a - b;

		cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) );
		cpu.AddCycles_C();
	}

	public static void T_MOV_HIREG( ARM cpu )
	{
		uint rd = (cpu.CurInstr & 0x7) | ((cpu.CurInstr >> 4) & 0x8);
		uint rs = (cpu.CurInstr >> 3) & 0xF;

		cpu.AddCycles_C();

		if ( rd == 15 ) cpu.JumpTo( cpu.R[rs] | 1 );
		else cpu.R[rd] = cpu.R[rs];
	}

	public static void T_ADD_PCREL( ARM cpu )
	{
		uint val = cpu.R[15] & ~2u;
		val += (cpu.CurInstr & 0xFF) << 2;
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = val;
		cpu.AddCycles_C();
	}

	public static void T_ADD_SPREL( ARM cpu )
	{
		uint val = cpu.R[13];
		val += (cpu.CurInstr & 0xFF) << 2;
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = val;
		cpu.AddCycles_C();
	}

	public static void T_ADD_SP( ARM cpu )
	{
		uint val = cpu.R[13];
		if ( (cpu.CurInstr & (1 << 7)) != 0 )
			val -= (cpu.CurInstr & 0x7F) << 2;
		else
			val += (cpu.CurInstr & 0x7F) << 2;
		cpu.R[13] = val;
		cpu.AddCycles_C();
	}

	public static void T_LDR_PCREL( ARM cpu )
	{
		uint addr = (cpu.R[15] & ~0x2u) + ((cpu.CurInstr & 0xFF) << 2);
		uint val = 0;
		cpu.DataRead32( addr, ref val );
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_STR_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		cpu.DataWrite32( addr, cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_STRB_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		cpu.DataWrite8( addr, (byte)cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDR_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint val = 0;
		cpu.DataRead32( addr, ref val );
		cpu.R[cpu.CurInstr & 0x7] = ARM.ROR( val, 8 * (addr & 0x3) );
		cpu.AddCycles_CDI();
	}

	public static void T_LDRB_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint val = 0;
		cpu.DataRead8( addr, ref val );
		cpu.R[cpu.CurInstr & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_STRH_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		cpu.DataWrite16( addr, (ushort)cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDRSB_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint val = 0;
		cpu.DataRead8( addr, ref val );
		cpu.R[cpu.CurInstr & 0x7] = (uint)(sbyte)val;
		cpu.AddCycles_CDI();
	}

	public static void T_LDRH_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint val = 0;
		cpu.DataRead16( addr, ref val );
		cpu.R[cpu.CurInstr & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_LDRSH_REG( ARM cpu )
	{
		uint addr = cpu.R[(cpu.CurInstr >> 3) & 0x7] + cpu.R[(cpu.CurInstr >> 6) & 0x7];
		uint val = 0;
		cpu.DataRead16( addr, ref val );
		cpu.R[cpu.CurInstr & 0x7] = (uint)(short)val;
		cpu.AddCycles_CDI();
	}

	public static void T_STR_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 4) & 0x7C;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.DataWrite32( offset, cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDR_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 4) & 0x7C;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint val = 0;
		cpu.DataRead32( offset, ref val );
		cpu.R[cpu.CurInstr & 0x7] = ARM.ROR( val, 8 * (offset & 0x3) );
		cpu.AddCycles_CDI();
	}

	public static void T_STRB_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 6) & 0x1F;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.DataWrite8( offset, (byte)cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDRB_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 6) & 0x1F;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint val = 0;
		cpu.DataRead8( offset, ref val );
		cpu.R[cpu.CurInstr & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_STRH_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 5) & 0x3E;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		cpu.DataWrite16( offset, (ushort)cpu.R[cpu.CurInstr & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDRH_IMM( ARM cpu )
	{
		uint offset = (cpu.CurInstr >> 5) & 0x3E;
		offset += cpu.R[(cpu.CurInstr >> 3) & 0x7];
		uint val = 0;
		cpu.DataRead16( offset, ref val );
		cpu.R[cpu.CurInstr & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_STR_SPREL( ARM cpu )
	{
		uint offset = (cpu.CurInstr << 2) & 0x3FC;
		offset += cpu.R[13];
		cpu.DataWrite32( offset, cpu.R[(cpu.CurInstr >> 8) & 0x7] );
		cpu.AddCycles_CD();
	}

	public static void T_LDR_SPREL( ARM cpu )
	{
		uint offset = (cpu.CurInstr << 2) & 0x3FC;
		offset += cpu.R[13];
		uint val = 0;
		cpu.DataRead32( offset, ref val );
		cpu.R[(cpu.CurInstr >> 8) & 0x7] = val;
		cpu.AddCycles_CDI();
	}

	public static void T_PUSH( ARM cpu )
	{
		int nregs = BitOperations.PopCount( cpu.CurInstr & 0x1FF );
		bool first = true;

		uint @base = cpu.R[13];
		@base -= (uint)(nregs << 2);
		cpu.R[13] = @base;

		for ( int i = 0; i < 8; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				if ( first ) cpu.DataWrite32( @base, cpu.R[i] );
				else cpu.DataWrite32S( @base, cpu.R[i] );
				first = false;
				@base += 4;
			}
		}

		if ( (cpu.CurInstr & (1 << 8)) != 0 )
		{
			if ( first ) cpu.DataWrite32( @base, cpu.R[14] );
			else cpu.DataWrite32S( @base, cpu.R[14] );
		}

		cpu.AddCycles_CD();
	}

	public static void T_POP( ARM cpu )
	{
		uint @base = cpu.R[13];
		bool first = true;

		for ( int i = 0; i < 8; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				uint v = 0;
				if ( first ) cpu.DataRead32( @base, ref v );
				else cpu.DataRead32S( @base, ref v );
				cpu.R[i] = v;
				first = false;
				@base += 4;
			}
		}

		if ( (cpu.CurInstr & (1 << 8)) != 0 )
		{
			uint pc = 0;
			if ( first ) cpu.DataRead32( @base, ref pc );
			else cpu.DataRead32S( @base, ref pc );
			if ( cpu.Num == 1 ) pc |= 0x1;
			cpu.JumpTo( pc );
			@base += 4;
		}

		cpu.R[13] = @base;
		cpu.AddCycles_CDI();
	}

	public static void T_STMIA( ARM cpu )
	{
		uint @base = cpu.R[(cpu.CurInstr >> 8) & 0x7];
		bool first = true;

		for ( int i = 0; i < 8; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				if ( first ) cpu.DataWrite32( @base, cpu.R[i] );
				else cpu.DataWrite32S( @base, cpu.R[i] );
				first = false;
				@base += 4;
			}
		}

		cpu.R[(cpu.CurInstr >> 8) & 0x7] = @base;
		cpu.AddCycles_CD();
	}

	public static void T_LDMIA( ARM cpu )
	{
		uint @base = cpu.R[(cpu.CurInstr >> 8) & 0x7];
		bool first = true;

		for ( int i = 0; i < 8; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				uint v = 0;
				if ( first ) cpu.DataRead32( @base, ref v );
				else cpu.DataRead32S( @base, ref v );
				cpu.R[i] = v;
				first = false;
				@base += 4;
			}
		}

		if ( (cpu.CurInstr & (1 << (int)((cpu.CurInstr >> 8) & 0x7))) == 0 )
			cpu.R[(cpu.CurInstr >> 8) & 0x7] = @base;

		cpu.AddCycles_CDI();
	}
}
