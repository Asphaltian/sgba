using System.Numerics;

namespace Emulotl.Nds;

public static partial class ARMInterpreter
{
	private static int RnIdx( ARM cpu ) => (int)((cpu.CurInstr >> 16) & 0xF);
	private static int RdIdx( ARM cpu ) => (int)((cpu.CurInstr >> 12) & 0xF);
	private static bool Wb( ARM cpu ) => (cpu.CurInstr & (1 << 21)) != 0;

	private static uint OffImm( ARM cpu )
	{
		uint offset = cpu.CurInstr & 0xFFF;
		if ( (cpu.CurInstr & (1 << 23)) == 0 ) offset = 0u - offset;
		return offset;
	}

	private static uint OffReg( ARM cpu, int mode )
	{
		uint offset = cpu.R[cpu.CurInstr & 0xF];
		uint shift = (cpu.CurInstr >> 7) & 0x1F;
		Shift( cpu, ref offset, shift, mode, false, false );
		if ( (cpu.CurInstr & (1 << 23)) == 0 ) offset = 0u - offset;
		return offset;
	}

	private static uint OffHDImm( ARM cpu )
	{
		uint offset = (cpu.CurInstr & 0xF) | ((cpu.CurInstr >> 4) & 0xF0);
		if ( (cpu.CurInstr & (1 << 23)) == 0 ) offset = 0u - offset;
		return offset;
	}

	private static uint OffHDReg( ARM cpu )
	{
		uint offset = cpu.R[cpu.CurInstr & 0xF];
		if ( (cpu.CurInstr & (1 << 23)) == 0 ) offset = 0u - offset;
		return offset;
	}

	private static void Op_STR( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		uint sv = cpu.R[RdIdx( cpu )];
		if ( RdIdx( cpu ) == 15 ) sv += 4;
		cpu.DataWrite32( offset, sv );
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		cpu.AddCycles_CD();
	}

	private static void Op_STR_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		uint sv = cpu.R[RdIdx( cpu )];
		if ( RdIdx( cpu ) == 15 ) sv += 4;
		cpu.DataWrite32( addr, sv );
		cpu.R[rn] += offset;
		cpu.AddCycles_CD();
	}

	private static void Op_STRB( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		cpu.DataWrite8( offset, (byte)cpu.R[RdIdx( cpu )] );
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		cpu.AddCycles_CD();
	}

	private static void Op_STRB_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.DataWrite8( addr, (byte)cpu.R[RdIdx( cpu )] );
		cpu.R[rn] += offset;
		cpu.AddCycles_CD();
	}

	private static void Op_LDR( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		uint val = 0;
		cpu.DataRead32( offset, ref val );
		val = ARM.ROR( val, (offset & 0x3) << 3 );
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		cpu.AddCycles_CDI();
		int rd = RdIdx( cpu );
		if ( rd == 15 )
		{
			if ( cpu.Num == 1 ) val &= ~0x1u;
			cpu.JumpTo( val );
		}
		else cpu.R[rd] = val;
	}

	private static void Op_LDR_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		uint val = 0;
		cpu.DataRead32( addr, ref val );
		val = ARM.ROR( val, (addr & 0x3) << 3 );
		cpu.R[rn] += offset;
		cpu.AddCycles_CDI();
		int rd = RdIdx( cpu );
		if ( rd == 15 )
		{
			if ( cpu.Num == 1 ) val &= ~0x1u;
			cpu.JumpTo( val );
		}
		else cpu.R[rd] = val;
	}

	private static void Op_LDRB( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		uint val = 0;
		cpu.DataRead8( offset, ref val );
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		cpu.AddCycles_CDI();
		cpu.R[RdIdx( cpu )] = val;
	}

	private static void Op_LDRB_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		uint val = 0;
		cpu.DataRead8( addr, ref val );
		cpu.R[rn] += offset;
		cpu.AddCycles_CDI();
		cpu.R[RdIdx( cpu )] = val;
	}

	private static void Op_STRH( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		cpu.DataWrite16( offset, (ushort)cpu.R[RdIdx( cpu )] );
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		cpu.AddCycles_CD();
	}

	private static void Op_STRH_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.DataWrite16( addr, (ushort)cpu.R[RdIdx( cpu )] );
		cpu.R[rn] += offset;
		cpu.AddCycles_CD();
	}

	private static void Op_LDRH( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		uint val = 0;
		cpu.DataRead16( offset, ref val );
		cpu.R[RdIdx( cpu )] = val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRH_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.R[rn] += offset;
		uint val = 0;
		cpu.DataRead16( addr, ref val );
		cpu.R[RdIdx( cpu )] = val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRSB( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		uint val = 0;
		cpu.DataRead8( offset, ref val );
		cpu.R[RdIdx( cpu )] = (uint)(sbyte)val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRSB_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.R[rn] += offset;
		uint val = 0;
		cpu.DataRead8( addr, ref val );
		cpu.R[RdIdx( cpu )] = (uint)(sbyte)val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRSH( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		uint val = 0;
		cpu.DataRead16( offset, ref val );
		cpu.R[RdIdx( cpu )] = (uint)(short)val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRSH_POST( ARM cpu, uint offset )
	{
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.R[rn] += offset;
		uint val = 0;
		cpu.DataRead16( addr, ref val );
		cpu.R[RdIdx( cpu )] = (uint)(short)val;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRD( ARM cpu, uint offset )
	{
		if ( cpu.Num != 0 ) return;
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		int r = RdIdx( cpu );
		if ( (r & 1) != 0 ) r--;
		uint v0 = 0, v1 = 0;
		cpu.DataRead32( offset, ref v0 );
		cpu.DataRead32S( offset + 4, ref v1 );
		cpu.R[r] = v0;
		cpu.R[r + 1] = v1;
		cpu.AddCycles_CDI();
	}

	private static void Op_LDRD_POST( ARM cpu, uint offset )
	{
		if ( cpu.Num != 0 ) return;
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.R[rn] += offset;
		int r = RdIdx( cpu );
		if ( (r & 1) != 0 ) r--;
		uint v0 = 0, v1 = 0;
		cpu.DataRead32( addr, ref v0 );
		cpu.DataRead32S( addr + 4, ref v1 );
		cpu.R[r] = v0;
		cpu.R[r + 1] = v1;
		cpu.AddCycles_CDI();
	}

	private static void Op_STRD( ARM cpu, uint offset )
	{
		if ( cpu.Num != 0 ) return;
		int rn = RnIdx( cpu );
		offset += cpu.R[rn];
		if ( Wb( cpu ) ) cpu.R[rn] = offset;
		int r = RdIdx( cpu );
		if ( (r & 1) != 0 ) r--;
		cpu.DataWrite32( offset, cpu.R[r] );
		cpu.DataWrite32S( offset + 4, cpu.R[r + 1] );
		cpu.AddCycles_CD();
	}

	private static void Op_STRD_POST( ARM cpu, uint offset )
	{
		if ( cpu.Num != 0 ) return;
		int rn = RnIdx( cpu );
		uint addr = cpu.R[rn];
		cpu.R[rn] += offset;
		int r = RdIdx( cpu );
		if ( (r & 1) != 0 ) r--;
		cpu.DataWrite32( addr, cpu.R[r] );
		cpu.DataWrite32S( addr + 4, cpu.R[r + 1] );
		cpu.AddCycles_CD();
	}

	public static void A_SWP( ARM cpu )
	{
		uint @base = cpu.R[(cpu.CurInstr >> 16) & 0xF];
		uint rm = cpu.R[cpu.CurInstr & 0xF];

		uint val = 0;
		cpu.DataRead32( @base, ref val );
		cpu.R[(cpu.CurInstr >> 12) & 0xF] = ARM.ROR( val, 8 * (@base & 0x3) );

		int numD = cpu.DataCycles;
		cpu.DataWrite32( @base, rm );
		cpu.DataCycles += numD;

		cpu.AddCycles_CDI();
	}

	public static void A_SWPB( ARM cpu )
	{
		uint @base = cpu.R[(cpu.CurInstr >> 16) & 0xF];
		uint rm = cpu.R[cpu.CurInstr & 0xF] & 0xFF;

		uint val = 0;
		cpu.DataRead8( @base, ref val );
		cpu.R[(cpu.CurInstr >> 12) & 0xF] = val;

		int numD = cpu.DataCycles;
		cpu.DataWrite8( @base, (byte)rm );
		cpu.DataCycles += numD;

		cpu.AddCycles_CDI();
	}

	public static void A_LDM( ARM cpu )
	{
		uint baseid = (cpu.CurInstr >> 16) & 0xF;
		uint @base = cpu.R[baseid];
		uint wbbase = 0;
		bool preinc = (cpu.CurInstr & (1 << 24)) != 0;
		bool first = true;

		if ( (cpu.CurInstr & (1 << 23)) == 0 )
		{
			@base -= 4 * (uint)BitOperations.PopCount( cpu.CurInstr & 0xFFFF );
			if ( (cpu.CurInstr & (1 << 21)) != 0 )
				wbbase = @base;
			preinc = !preinc;
		}

		if ( (cpu.CurInstr & (1 << 22)) != 0 && (cpu.CurInstr & (1 << 15)) == 0 )
			cpu.UpdateMode( cpu.CPSR, (cpu.CPSR & ~0x1Fu) | 0x10, true );

		for ( int i = 0; i < 15; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				if ( preinc ) @base += 4;
				uint v = 0;
				if ( first ) cpu.DataRead32( @base, ref v );
				else cpu.DataRead32S( @base, ref v );
				cpu.R[i] = v;
				first = false;
				if ( !preinc ) @base += 4;
			}
		}

		uint pc = 0;
		if ( (cpu.CurInstr & (1 << 15)) != 0 )
		{
			if ( preinc ) @base += 4;
			if ( first ) cpu.DataRead32( @base, ref pc );
			else cpu.DataRead32S( @base, ref pc );
			if ( !preinc ) @base += 4;

			if ( cpu.Num == 1 ) pc &= ~0x1u;
		}

		if ( (cpu.CurInstr & (1 << 21)) != 0 )
		{
			if ( (cpu.CurInstr & (1 << 23)) != 0 )
				wbbase = @base;

			if ( (cpu.CurInstr & (1 << (int)baseid)) != 0 )
			{
				if ( cpu.Num == 0 )
				{
					uint rlist = cpu.CurInstr & 0xFFFF;
					if ( (rlist & ~(1u << (int)baseid)) == 0 || (rlist & ~((2u << (int)baseid) - 1)) != 0 )
						cpu.R[baseid] = wbbase;
				}
			}
			else cpu.R[baseid] = wbbase;
		}

		if ( (cpu.CurInstr & (1 << 22)) != 0 && (cpu.CurInstr & (1 << 15)) == 0 )
			cpu.UpdateMode( (cpu.CPSR & ~0x1Fu) | 0x10, cpu.CPSR, true );

		if ( (cpu.CurInstr & (1 << 15)) != 0 )
			cpu.JumpTo( pc, (cpu.CurInstr & (1 << 22)) != 0 );

		cpu.AddCycles_CDI();
	}

	public static void A_STM( ARM cpu )
	{
		uint baseid = (cpu.CurInstr >> 16) & 0xF;
		uint @base = cpu.R[baseid];
		uint oldbase = @base;
		bool preinc = (cpu.CurInstr & (1 << 24)) != 0;
		bool first = true;

		if ( (cpu.CurInstr & (1 << 23)) == 0 )
		{
			@base -= 4 * (uint)BitOperations.PopCount( cpu.CurInstr & 0xFFFF );
			if ( (cpu.CurInstr & (1 << 21)) != 0 )
				cpu.R[baseid] = @base;
			preinc = !preinc;
		}

		bool isbanked = false;
		if ( (cpu.CurInstr & (1 << 22)) != 0 )
		{
			uint mode = cpu.CPSR & 0x1F;
			if ( mode == 0x11 )
				isbanked = baseid >= 8 && baseid < 15;
			else if ( mode != 0x10 && mode != 0x1F )
				isbanked = baseid >= 13 && baseid < 15;

			cpu.UpdateMode( cpu.CPSR, (cpu.CPSR & ~0x1Fu) | 0x10, true );
		}

		for ( int i = 0; i < 16; i++ )
		{
			if ( (cpu.CurInstr & (1 << i)) != 0 )
			{
				if ( preinc ) @base += 4;

				if ( i == baseid && !isbanked )
				{
					uint val = (cpu.Num == 0 || (cpu.CurInstr & ((1 << i) - 1)) == 0) ? oldbase : @base;
					if ( first ) cpu.DataWrite32( @base, val );
					else cpu.DataWrite32S( @base, val );
				}
				else
				{
					if ( first ) cpu.DataWrite32( @base, cpu.R[i] );
					else cpu.DataWrite32S( @base, cpu.R[i] );
				}

				first = false;

				if ( !preinc ) @base += 4;
			}
		}

		if ( (cpu.CurInstr & (1 << 22)) != 0 )
			cpu.UpdateMode( (cpu.CPSR & ~0x1Fu) | 0x10, cpu.CPSR, true );

		if ( (cpu.CurInstr & (1 << 23)) != 0 && (cpu.CurInstr & (1 << 21)) != 0 )
			cpu.R[baseid] = @base;

		cpu.AddCycles_CD();
	}

	private static Action<ARM, uint> WordBody( int l, int b, int p )
	{
		if ( l == 0 )
			return b == 0 ? (p == 1 ? Op_STR : Op_STR_POST) : (p == 1 ? Op_STRB : Op_STRB_POST);
		return b == 0 ? (p == 1 ? Op_LDR : Op_LDR_POST) : (p == 1 ? Op_LDRB : Op_LDRB_POST);
	}

	private static Action<ARM, uint> HalfwordBody( int sh, int l, int p )
	{
		if ( sh == 1 )
			return l == 0 ? (p == 1 ? Op_STRH : Op_STRH_POST) : (p == 1 ? Op_LDRH : Op_LDRH_POST);
		if ( sh == 2 )
			return l == 0 ? (p == 1 ? Op_LDRD : Op_LDRD_POST) : (p == 1 ? Op_LDRSB : Op_LDRSB_POST);
		return l == 0 ? (p == 1 ? Op_STRD : Op_STRD_POST) : (p == 1 ? Op_LDRSH : Op_LDRSH_POST);
	}

	private static void BuildLoadStoreTable()
	{
		for ( int h = 0x40; h <= 0x7F; h++ )
		{
			int i = (h >> 5) & 1;
			int p = (h >> 4) & 1;
			int b = (h >> 2) & 1;
			int l = h & 1;
			Action<ARM, uint> body = WordBody( l, b, p );

			if ( i == 0 )
			{
				for ( int low = 0; low < 16; low++ )
					ARMInstrTable[(h << 4) | low] = MakeLsImm( body );
			}
			else
			{
				for ( int low = 0; low < 16; low += 2 )
				{
					int mode = (low >> 1) & 3;
					ARMInstrTable[(h << 4) | low] = MakeLsReg( body, mode );
				}
			}
		}

		(int low, int sh)[] hd = [(0xB, 1), (0xD, 2), (0xF, 3)];
		for ( int h = 0; h <= 0x1F; h++ )
		{
			int p = (h >> 4) & 1;
			int immOff = (h >> 2) & 1;
			int l = h & 1;

			foreach ( var (low, sh) in hd )
			{
				Action<ARM, uint> body = HalfwordBody( sh, l, p );
				ARMInstrTable[(h << 4) | low] = immOff == 1 ? MakeHdImm( body ) : MakeHdReg( body );
			}
		}

		ARMInstrTable[0x109] = A_SWP;
		ARMInstrTable[0x149] = A_SWPB;

		for ( int h = 0x80; h <= 0x9F; h++ )
		{
			InstrFunc fn = (h & 1) == 1 ? A_LDM : A_STM;
			for ( int low = 0; low < 16; low++ )
				ARMInstrTable[(h << 4) | low] = fn;
		}
	}

	private static InstrFunc MakeLsImm( Action<ARM, uint> body ) => cpu => body( cpu, OffImm( cpu ) );
	private static InstrFunc MakeLsReg( Action<ARM, uint> body, int mode ) => cpu => body( cpu, OffReg( cpu, mode ) );
	private static InstrFunc MakeHdImm( Action<ARM, uint> body ) => cpu => body( cpu, OffHDImm( cpu ) );
	private static InstrFunc MakeHdReg( Action<ARM, uint> body ) => cpu => body( cpu, OffHDReg( cpu ) );
}
