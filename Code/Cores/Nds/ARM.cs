namespace Emulotl.Nds;

public abstract class ARM
{
	public static uint ROR( uint x, uint n )
	{
		return (x >> (int)(n & 0x1F)) | (x << (int)((32 - n) & 0x1F));
	}

	public static readonly uint[] ConditionTable =
	[
		0xF0F0,
		0x0F0F,
		0xCCCC,
		0x3333,
		0xFF00,
		0x00FF,
		0xAAAA,
		0x5555,
		0x0C0C,
		0xF3F3,
		0xAA55,
		0x55AA,
		0x0A05,
		0xF5FA,
		0xFFFF,
		0x0000,
	];

	public readonly NDS NDS;
	public uint Num;

	public int Cycles;

	public uint StopExecution;

	public byte Halted
	{
		get => (byte)(StopExecution & 0xFF);
		set => StopExecution = (StopExecution & 0xFFFFFF00) | value;
	}

	public byte IRQ
	{
		get => (byte)((StopExecution >> 8) & 0xFF);
		set => StopExecution = (StopExecution & 0xFFFF00FF) | ((uint)value << 8);
	}

	public byte IdleLoop
	{
		get => (byte)((StopExecution >> 16) & 0xFF);
		set => StopExecution = (StopExecution & 0xFF00FFFF) | ((uint)value << 16);
	}

	public uint CodeRegion;
	public int CodeCycles;

	public uint DataRegion;
	public int DataCycles;

	public uint[] R = new uint[16];
	public uint CPSR;
	public uint[] R_FIQ = new uint[8];
	public uint[] R_SVC = new uint[3];
	public uint[] R_ABT = new uint[3];
	public uint[] R_IRQ = new uint[3];
	public uint[] R_UND = new uint[3];
	public uint CurInstr;
	public uint[] NextInstr = new uint[2];

	public uint ExceptionBase;

	public MemRegion CodeMem;

	protected ARM( uint num, NDS nds )
	{
		Num = num;
		NDS = nds;
	}

	public virtual void Reset()
	{
		Cycles = 0;
		Halted = 0;
		IRQ = 0;

		for ( int i = 0; i < 16; i++ )
			R[i] = 0;

		CPSR = 0x000000D3;

		for ( int i = 0; i < 7; i++ )
			R_FIQ[i] = 0;
		for ( int i = 0; i < 2; i++ )
		{
			R_SVC[i] = 0;
			R_ABT[i] = 0;
			R_IRQ[i] = 0;
			R_UND[i] = 0;
		}

		R_FIQ[7] = 0x00000010;
		R_SVC[2] = 0x00000010;
		R_ABT[2] = 0x00000010;
		R_IRQ[2] = 0x00000010;
		R_UND[2] = 0x00000010;

		ExceptionBase = Num != 0 ? 0x00000000 : 0xFFFF0000;

		CodeMem.Mem = null;

		JumpTo( ExceptionBase );
	}

	public abstract void FillPipeline();

	public abstract void JumpTo( uint addr, bool restorecpsr = false );

	public void RestoreCPSR()
	{
		uint oldcpsr = CPSR;

		switch ( CPSR & 0x1F )
		{
			case 0x11: CPSR = R_FIQ[7]; break;
			case 0x12: CPSR = R_IRQ[2]; break;
			case 0x13: CPSR = R_SVC[2]; break;
			case 0x14:
			case 0x15:
			case 0x16:
			case 0x17: CPSR = R_ABT[2]; break;
			case 0x18:
			case 0x19:
			case 0x1A:
			case 0x1B: CPSR = R_UND[2]; break;
			default:
				Platform.Log( LogLevel.Warn, $"!! restore CPSR bad mode {CPSR & 0x1F:X2}" );
				break;
		}

		CPSR |= 0x00000010;

		UpdateMode( oldcpsr, CPSR );
	}

	public void Halt( uint halt )
	{
		if ( halt == 2 && Halted == 1 )
			return;
		Halted = (byte)halt;
	}

	public bool CheckCondition( uint code )
	{
		if ( code == 0xE )
			return true;
		return (ConditionTable[code] & (1 << (int)(CPSR >> 28))) != 0;
	}

	public void SetC( bool c )
	{
		if ( c ) CPSR |= 0x20000000;
		else CPSR &= ~0x20000000u;
	}

	public void SetNZ( bool n, bool z )
	{
		CPSR &= ~0xC0000000u;
		if ( n ) CPSR |= 0x80000000;
		if ( z ) CPSR |= 0x40000000;
	}

	public void SetNZCV( bool n, bool z, bool c, bool v )
	{
		CPSR &= ~0xF0000000u;
		if ( n ) CPSR |= 0x80000000;
		if ( z ) CPSR |= 0x40000000;
		if ( c ) CPSR |= 0x20000000;
		if ( v ) CPSR |= 0x10000000;
	}

	public bool ModeIs( uint mode )
	{
		uint cm = CPSR & 0x1F;
		mode &= 0x1F;

		if ( mode == cm ) return true;
		if ( mode == 0x17 ) return cm >= 0x14 && cm <= 0x17;
		if ( mode == 0x1B ) return cm >= 0x18 && cm <= 0x1B;

		return false;
	}

	public void UpdateMode( uint oldmode, uint newmode, bool phony = false )
	{
		if ( (oldmode & 0x1F) == (newmode & 0x1F) )
			return;

		switch ( oldmode & 0x1F )
		{
			case 0x11:
				Swap( ref R[8], ref R_FIQ[0] );
				Swap( ref R[9], ref R_FIQ[1] );
				Swap( ref R[10], ref R_FIQ[2] );
				Swap( ref R[11], ref R_FIQ[3] );
				Swap( ref R[12], ref R_FIQ[4] );
				Swap( ref R[13], ref R_FIQ[5] );
				Swap( ref R[14], ref R_FIQ[6] );
				break;
			case 0x12:
				Swap( ref R[13], ref R_IRQ[0] );
				Swap( ref R[14], ref R_IRQ[1] );
				break;
			case 0x13:
				Swap( ref R[13], ref R_SVC[0] );
				Swap( ref R[14], ref R_SVC[1] );
				break;
			case 0x17:
				Swap( ref R[13], ref R_ABT[0] );
				Swap( ref R[14], ref R_ABT[1] );
				break;
			case 0x1B:
				Swap( ref R[13], ref R_UND[0] );
				Swap( ref R[14], ref R_UND[1] );
				break;
		}

		switch ( newmode & 0x1F )
		{
			case 0x11:
				Swap( ref R[8], ref R_FIQ[0] );
				Swap( ref R[9], ref R_FIQ[1] );
				Swap( ref R[10], ref R_FIQ[2] );
				Swap( ref R[11], ref R_FIQ[3] );
				Swap( ref R[12], ref R_FIQ[4] );
				Swap( ref R[13], ref R_FIQ[5] );
				Swap( ref R[14], ref R_FIQ[6] );
				break;
			case 0x12:
				Swap( ref R[13], ref R_IRQ[0] );
				Swap( ref R[14], ref R_IRQ[1] );
				break;
			case 0x13:
				Swap( ref R[13], ref R_SVC[0] );
				Swap( ref R[14], ref R_SVC[1] );
				break;
			case 0x17:
				Swap( ref R[13], ref R_ABT[0] );
				Swap( ref R[14], ref R_ABT[1] );
				break;
			case 0x1B:
				Swap( ref R[13], ref R_UND[0] );
				Swap( ref R[14], ref R_UND[1] );
				break;
		}

		if ( !phony && Num == 0 )
		{
			ARMv5 cpu = (ARMv5)this;
			cpu.PU_Map = (newmode & 0x1F) == 0x10 ? cpu.PU_UserMap : cpu.PU_PrivMap;
		}
	}

	public void TriggerIRQ()
	{
		if ( (CPSR & 0x80) != 0 )
			return;

		uint oldcpsr = CPSR;
		CPSR &= ~0xFFu;
		CPSR |= 0xD2;
		UpdateMode( oldcpsr, CPSR );

		R_IRQ[2] = oldcpsr;
		R[14] = R[15] + ((oldcpsr & 0x20) != 0 ? 2u : 0u);
		JumpTo( ExceptionBase + 0x18 );
	}

	public uint CodeMemBase = 0xFFFFFFFF;

	public void SetupCodeMem( uint addr )
	{
		if ( Num == 0 )
			((ARMv5)this).GetCodeMemRegion( addr, ref CodeMem );
		else
			NDS.GetARM7CodeMem( addr, ref CodeMem );

		CodeMemBase = addr & 0xFF800000;
	}

	public void NocashPrint( uint addr ) { }

	public abstract void DataRead8( uint addr, ref uint val );
	public abstract void DataRead16( uint addr, ref uint val );
	public abstract void DataRead32( uint addr, ref uint val );
	public abstract void DataRead32S( uint addr, ref uint val );
	public abstract void DataWrite8( uint addr, byte val );
	public abstract void DataWrite16( uint addr, ushort val );
	public abstract void DataWrite32( uint addr, uint val );
	public abstract void DataWrite32S( uint addr, uint val );

	public abstract void AddCycles_C();
	public abstract void AddCycles_CI( int numI );
	public abstract void AddCycles_CDI();
	public abstract void AddCycles_CD();

	protected abstract byte BusRead8( uint addr );
	protected abstract ushort BusRead16( uint addr );
	protected abstract uint BusRead32( uint addr );
	protected abstract void BusWrite8( uint addr, byte val );
	protected abstract void BusWrite16( uint addr, ushort val );
	protected abstract void BusWrite32( uint addr, uint val );

	private static void Swap( ref uint a, ref uint b )
	{
		(a, b) = (b, a);
	}
}
