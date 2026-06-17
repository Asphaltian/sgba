namespace Emulotl.Nds;

public static partial class ARMInterpreter
{
	private static bool CarryAdd( uint a, uint b ) => (0xFFFFFFFF - a) < b;

	private static bool CarrySub( uint a, uint b ) => a >= b;

	private static bool OverflowAdd( uint a, uint b )
	{
		uint res = a + b;
		return ((a ^ b) & 0x80000000) == 0 && ((a ^ res) & 0x80000000) != 0;
	}

	private static bool OverflowSub( uint a, uint b )
	{
		uint res = a - b;
		return ((a ^ b) & 0x80000000) != 0 && ((a ^ res) & 0x80000000) != 0;
	}

	private static bool OverflowAdc( uint a, uint b, uint carry )
	{
		long fullResult = (long)(int)a + (int)b + carry;
		uint res = a + b + carry;
		return (int)res != fullResult;
	}

	private static bool OverflowSbc( uint a, uint b, uint carry )
	{
		long fullResult = (long)(int)a - (int)b - carry;
		uint res = a - b - carry;
		return (int)res != fullResult;
	}

	private static void Shift( ARM cpu, ref uint x, uint s, int mode, bool isReg, bool setC )
	{
		if ( !isReg )
		{
			switch ( mode )
			{
				case 0:
					if ( setC )
					{
						if ( s > 0 ) { cpu.SetC( (x & (1u << (int)(32 - s))) != 0 ); x <<= (int)s; }
					}
					else x <<= (int)s;
					break;
				case 1:
					if ( setC )
					{
						if ( s == 0 ) { cpu.SetC( (x & 0x80000000) != 0 ); x = 0; }
						else { cpu.SetC( (x & (1u << (int)(s - 1))) != 0 ); x >>= (int)s; }
					}
					else { if ( s == 0 ) x = 0; else x >>= (int)s; }
					break;
				case 2:
					if ( setC )
					{
						if ( s == 0 ) { cpu.SetC( (x & 0x80000000) != 0 ); x = (uint)((int)x >> 31); }
						else { cpu.SetC( (x & (1u << (int)(s - 1))) != 0 ); x = (uint)((int)x >> (int)s); }
					}
					else { if ( s == 0 ) x = (uint)((int)x >> 31); else x = (uint)((int)x >> (int)s); }
					break;
				case 3:
					if ( setC )
					{
						if ( s == 0 ) { uint newc = x & 1; x = (x >> 1) | ((cpu.CPSR & 0x20000000) << 2); cpu.SetC( newc != 0 ); }
						else { cpu.SetC( (x & (1u << (int)(s - 1))) != 0 ); x = ARM.ROR( x, s ); }
					}
					else
					{
						if ( s == 0 ) x = (x >> 1) | ((cpu.CPSR & 0x20000000) << 2);
						else x = ARM.ROR( x, s );
					}
					break;
			}
		}
		else
		{
			switch ( mode )
			{
				case 0:
					if ( setC )
					{
						if ( s > 31 ) { cpu.SetC( s > 32 ? false : (x & 1) != 0 ); x = 0; }
						else if ( s > 0 ) { cpu.SetC( (x & (1u << (int)(32 - s))) != 0 ); x <<= (int)s; }
					}
					else { if ( s > 31 ) x = 0; else x <<= (int)s; }
					break;
				case 1:
					if ( setC )
					{
						if ( s > 31 ) { cpu.SetC( s > 32 ? false : (x & 0x80000000) != 0 ); x = 0; }
						else if ( s > 0 ) { cpu.SetC( (x & (1u << (int)(s - 1))) != 0 ); x >>= (int)s; }
					}
					else { if ( s > 31 ) x = 0; else x >>= (int)s; }
					break;
				case 2:
					if ( setC )
					{
						if ( s > 31 ) { cpu.SetC( (x & 0x80000000) != 0 ); x = (uint)((int)x >> 31); }
						else if ( s > 0 ) { cpu.SetC( (x & (1u << (int)(s - 1))) != 0 ); x = (uint)((int)x >> (int)s); }
					}
					else { if ( s > 31 ) x = (uint)((int)x >> 31); else x = (uint)((int)x >> (int)s); }
					break;
				case 3:
					if ( setC )
					{
						if ( s > 0 ) cpu.SetC( (x & (1u << (int)(s - 1))) != 0 );
						x = ARM.ROR( x, s & 0x1F );
					}
					else x = ARM.ROR( x, s & 0x1F );
					break;
			}
		}
	}

	private static uint Op2ImmRot( ARM cpu ) => ARM.ROR( cpu.CurInstr & 0xFF, (cpu.CurInstr >> 7) & 0x1E );

	private static uint Op2ImmRotS( ARM cpu )
	{
		uint b = ARM.ROR( cpu.CurInstr & 0xFF, (cpu.CurInstr >> 7) & 0x1E );
		if ( ((cpu.CurInstr >> 7) & 0x1E) != 0 )
			cpu.SetC( (b & 0x80000000) != 0 );
		return b;
	}

	private static uint Op2RegImm( ARM cpu, int mode, bool setC )
	{
		uint b = cpu.R[cpu.CurInstr & 0xF];
		uint s = (cpu.CurInstr >> 7) & 0x1F;
		Shift( cpu, ref b, s, mode, false, setC );
		return b;
	}

	private static uint Op2RegReg( ARM cpu, int mode, bool setC )
	{
		uint b = cpu.R[cpu.CurInstr & 0xF];
		if ( (cpu.CurInstr & 0xF) == 15 ) b += 4;
		uint s = cpu.R[(cpu.CurInstr >> 8) & 0xF] & 0xFF;
		Shift( cpu, ref b, s, mode, true, setC );
		return b;
	}

	private static void Cycles( ARM cpu, int c )
	{
		if ( c != 0 ) cpu.AddCycles_CI( c );
		else cpu.AddCycles_C();
	}

	private static void WriteAlu( ARM cpu, uint res )
	{
		if ( ((cpu.CurInstr >> 12) & 0xF) == 15 ) cpu.JumpTo( res & ~1u );
		else cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
	}

	private static void WriteAluS( ARM cpu, uint res )
	{
		if ( ((cpu.CurInstr >> 12) & 0xF) == 15 ) cpu.JumpTo( res, true );
		else cpu.R[(cpu.CurInstr >> 12) & 0xF] = res;
	}

	private static uint Rn( ARM cpu ) => cpu.R[(cpu.CurInstr >> 16) & 0xF];

	private static void Op_AND( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) & b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_AND_S( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) & b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_EOR( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) ^ b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_EOR_S( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) ^ b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_SUB( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) - b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_SUB_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint res = a - b; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_RSB( ARM cpu, uint b, int c ) { uint res = b - Rn( cpu ); Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_RSB_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint res = b - a; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( b, a ), OverflowSub( b, a ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_ADD( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) + b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_ADD_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint res = a + b; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_ADC( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) + b + ((cpu.CPSR & 0x20000000) != 0 ? 1u : 0u); Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_ADC_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint resTmp = a + b; uint carry = (cpu.CPSR & 0x20000000) != 0 ? 1u : 0u; uint res = resTmp + carry; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ) | CarryAdd( resTmp, carry ), OverflowAdc( a, b, carry ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_SBC( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) - b - ((cpu.CPSR & 0x20000000) != 0 ? 0u : 1u); Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_SBC_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint resTmp = a - b; uint carry = (cpu.CPSR & 0x20000000) != 0 ? 0u : 1u; uint res = resTmp - carry; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ) && CarrySub( resTmp, carry ), OverflowSbc( a, b, carry ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_RSC( ARM cpu, uint b, int c ) { uint res = b - Rn( cpu ) - ((cpu.CPSR & 0x20000000) != 0 ? 0u : 1u); Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_RSC_S( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint resTmp = b - a; uint carry = (cpu.CPSR & 0x20000000) != 0 ? 0u : 1u; uint res = resTmp - carry; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( b, a ) && CarrySub( resTmp, carry ), OverflowSbc( b, a, carry ) ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_TST( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) & b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); }
	private static void Op_TEQ( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) ^ b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); }
	private static void Op_CMP( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint res = a - b; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarrySub( a, b ), OverflowSub( a, b ) ); Cycles( cpu, c ); }
	private static void Op_CMN( ARM cpu, uint b, int c ) { uint a = Rn( cpu ); uint res = a + b; cpu.SetNZCV( (res & 0x80000000) != 0, res == 0, CarryAdd( a, b ), OverflowAdd( a, b ) ); Cycles( cpu, c ); }

	private static void Op_ORR( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) | b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_ORR_S( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) | b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_MOV( ARM cpu, uint b, int c ) { Cycles( cpu, c ); WriteAlu( cpu, b ); }
	private static void Op_MOV_S( ARM cpu, uint b, int c ) { cpu.SetNZ( (b & 0x80000000) != 0, b == 0 ); Cycles( cpu, c ); WriteAluS( cpu, b ); }

	private static void Op_BIC( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) & ~b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_BIC_S( ARM cpu, uint b, int c ) { uint res = Rn( cpu ) & ~b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static void Op_MVN( ARM cpu, uint b, int c ) { uint res = ~b; Cycles( cpu, c ); WriteAlu( cpu, res ); }
	private static void Op_MVN_S( ARM cpu, uint b, int c ) { uint res = ~b; cpu.SetNZ( (res & 0x80000000) != 0, res == 0 ); Cycles( cpu, c ); WriteAluS( cpu, res ); }

	private static bool IsLogical( int op ) => op is 0 or 1 or 8 or 9 or 12 or 13 or 14 or 15;

	private static Action<ARM, uint, int> AluBody( int op, int s ) => op switch
	{
		0 => s == 0 ? Op_AND : Op_AND_S,
		1 => s == 0 ? Op_EOR : Op_EOR_S,
		2 => s == 0 ? Op_SUB : Op_SUB_S,
		3 => s == 0 ? Op_RSB : Op_RSB_S,
		4 => s == 0 ? Op_ADD : Op_ADD_S,
		5 => s == 0 ? Op_ADC : Op_ADC_S,
		6 => s == 0 ? Op_SBC : Op_SBC_S,
		7 => s == 0 ? Op_RSC : Op_RSC_S,
		8 => Op_TST,
		9 => Op_TEQ,
		10 => Op_CMP,
		11 => Op_CMN,
		12 => s == 0 ? Op_ORR : Op_ORR_S,
		13 => s == 0 ? Op_MOV : Op_MOV_S,
		14 => s == 0 ? Op_BIC : Op_BIC_S,
		_ => s == 0 ? Op_MVN : Op_MVN_S,
	};

	private static void BuildAluTable()
	{
		for ( int op = 0; op < 16; op++ )
		{
			bool isTest = op is >= 8 and <= 11;
			bool logical = IsLogical( op );

			for ( int s = 0; s <= 1; s++ )
			{
				if ( isTest && s == 0 )
					continue;

				Action<ARM, uint, int> body = AluBody( op, s );
				bool carry = s == 1 && logical;

				int hiReg = (op << 1) | s;
				for ( int low = 0; low < 16; low++ )
				{
					int icode = (hiReg << 4) | low;
					int mode = (low >> 1) & 3;

					if ( (low & 1) == 0 )
						ARMInstrTable[icode] = MakeRegImm( body, mode, carry );
					else if ( low is 1 or 3 or 5 or 7 )
						ARMInstrTable[icode] = MakeRegReg( body, mode, carry );
				}

				int hiImm = 0x20 | (op << 1) | s;
				for ( int low = 0; low < 16; low++ )
					ARMInstrTable[(hiImm << 4) | low] = MakeImm( body, carry );
			}
		}
	}

	private static InstrFunc MakeImm( Action<ARM, uint, int> body, bool carry )
		=> cpu => body( cpu, carry ? Op2ImmRotS( cpu ) : Op2ImmRot( cpu ), 0 );

	private static InstrFunc MakeRegImm( Action<ARM, uint, int> body, int mode, bool carry )
		=> cpu => body( cpu, Op2RegImm( cpu, mode, carry ), 0 );

	private static InstrFunc MakeRegReg( Action<ARM, uint, int> body, int mode, bool carry )
		=> cpu => body( cpu, Op2RegReg( cpu, mode, carry ), 1 );
}
