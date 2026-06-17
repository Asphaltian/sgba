namespace Emulotl.Nds;

public sealed class DMA
{
	public uint SrcAddr;
	public uint DstAddr;
	public uint Cnt;

	private readonly NDS NDS;
	private readonly uint CPU;
	private readonly uint Num;

	private uint StartMode;
	private uint CurSrcAddr;
	private uint CurDstAddr;
	private uint RemCount;
	private uint IterCount;
	private int SrcAddrInc;
	private int DstAddrInc;
	private uint CountMask;

	private uint Running;
	private bool InProgress;
	private bool Executing;
	private bool Stall;
	private bool IsGXFIFODMA;

	public DMA( uint cpu, uint num, NDS nds )
	{
		CPU = cpu;
		Num = num;
		NDS = nds;

		if ( cpu == 0 )
			CountMask = 0x001FFFFF;
		else
			CountMask = num == 3 ? 0x0000FFFFu : 0x00003FFFu;
	}

	public void Reset()
	{
		SrcAddr = 0;
		DstAddr = 0;
		Cnt = 0;

		StartMode = 0;
		CurSrcAddr = 0;
		CurDstAddr = 0;
		RemCount = 0;
		IterCount = 0;
		SrcAddrInc = 0;
		DstAddrInc = 0;

		Stall = false;

		Running = 0;
		Executing = false;
		InProgress = false;
	}

	public bool IsInMode( uint mode ) => mode == StartMode && (Cnt & 0x80000000) != 0;

	public bool IsRunning() => Running != 0;

	public void StartIfNeeded( uint mode )
	{
		if ( mode == StartMode && (Cnt & 0x80000000) != 0 )
			Start();
	}

	public void StopIfNeeded( uint mode )
	{
		if ( mode == StartMode )
			Cnt &= ~0x80000000;
	}

	public void StallIfRunning()
	{
		if ( Executing ) Stall = true;
	}

	public void WriteCnt( uint val )
	{
		uint oldcnt = Cnt;
		Cnt = val;

		if ( (oldcnt & 0x80000000) == 0 && (val & 0x80000000) != 0 )
		{
			CurSrcAddr = SrcAddr;
			CurDstAddr = DstAddr;

			switch ( Cnt & 0x00600000 )
			{
				case 0x00000000: DstAddrInc = 1; break;
				case 0x00200000: DstAddrInc = -1; break;
				case 0x00400000: DstAddrInc = 0; break;
				case 0x00600000: DstAddrInc = 1; break;
			}

			switch ( Cnt & 0x01800000 )
			{
				case 0x00000000: SrcAddrInc = 1; break;
				case 0x00800000: SrcAddrInc = -1; break;
				case 0x01000000: SrcAddrInc = 0; break;
				case 0x01800000: SrcAddrInc = 1; break;
			}

			if ( CPU == 0 )
				StartMode = (Cnt >> 27) & 0x7;
			else
				StartMode = ((Cnt >> 28) & 0x3) | 0x10;

			if ( (StartMode & 0x7) == 0 )
				Start();
			else if ( StartMode == 0x05 || StartMode == 0x12 )
				NDS.CartCheckDMA();

			if ( StartMode == 0x06 || StartMode == 0x13 )
				Platform.Log( LogLevel.Warn, $"UNIMPLEMENTED ARM{(CPU != 0 ? 7 : 9)} DMA{Num} START MODE {StartMode:X2}, {SrcAddr:X8}->{DstAddr:X8}" );
		}
	}

	public void Start()
	{
		if ( Running != 0 ) return;

		if ( !InProgress )
		{
			uint countmask;
			if ( CPU == 0 )
				countmask = 0x001FFFFF;
			else
				countmask = Num == 3 ? 0x0000FFFFu : 0x00003FFFu;

			RemCount = Cnt & countmask;
			if ( RemCount == 0 )
				RemCount = countmask + 1;
		}

		if ( StartMode == 0x07 && RemCount > 112 )
			IterCount = 112;
		else
			IterCount = RemCount;

		if ( (Cnt & 0x01800000) == 0x01800000 )
			CurSrcAddr = SrcAddr;

		if ( (Cnt & 0x00600000) == 0x00600000 )
			CurDstAddr = DstAddr;

		IsGXFIFODMA = CPU == 0 && (CurSrcAddr >> 24) == 0x02 && CurDstAddr == 0x04000400 && DstAddrInc == 0;

		Running = 2;

		InProgress = true;
		NDS.StopCPU( CPU, 1u << (int)Num );
	}

	public void Run()
	{
		if ( Running == 0 ) return;
		if ( CPU == 0 ) Run9();
		else Run7();
	}

	private void Run9()
	{
		if ( NDS.ARM9Timestamp >= NDS.ARM9Target ) return;

		Executing = true;
		Running = 1;

		if ( (Cnt & (1 << 26)) == 0 )
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM9Timestamp += 1 << 1;

				NDS.ARM9Write16( CurDstAddr, NDS.ARM9Read16( CurSrcAddr ) );

				CurSrcAddr = (uint)(CurSrcAddr + (SrcAddrInc << 1));
				CurDstAddr = (uint)(CurDstAddr + (DstAddrInc << 1));
				IterCount--;
				RemCount--;

				if ( NDS.ARM9Timestamp >= NDS.ARM9Target ) break;
			}
		}
		else
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM9Timestamp += 1 << 1;

				NDS.ARM9Write32( CurDstAddr, NDS.ARM9Read32( CurSrcAddr ) );

				CurSrcAddr = (uint)(CurSrcAddr + (SrcAddrInc << 2));
				CurDstAddr = (uint)(CurDstAddr + (DstAddrInc << 2));
				IterCount--;
				RemCount--;

				if ( NDS.ARM9Timestamp >= NDS.ARM9Target ) break;
			}
		}

		Executing = false;
		Stall = false;

		if ( RemCount != 0 )
		{
			if ( IterCount == 0 )
			{
				Running = 0;
				NDS.ResumeCPU( 0, 1u << (int)Num );
			}

			return;
		}

		if ( (Cnt & (1 << 25)) == 0 )
			Cnt &= ~(1u << 31);

		if ( (Cnt & (1 << 30)) != 0 )
			NDS.SetIRQ( 0, IRQ.DMA0 + (int)Num );

		Running = 0;
		InProgress = false;
		NDS.ResumeCPU( 0, 1u << (int)Num );

		if ( StartMode == 0x05 )
			NDS.CartCheckDMA();
	}

	private void Run7()
	{
		if ( NDS.ARM7Timestamp >= NDS.ARM7Target ) return;

		Executing = true;
		Running = 1;

		if ( (Cnt & (1 << 26)) == 0 )
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM7Timestamp += 1;

				NDS.ARM7Write16( CurDstAddr, NDS.ARM7Read16( CurSrcAddr ) );

				CurSrcAddr = (uint)(CurSrcAddr + (SrcAddrInc << 1));
				CurDstAddr = (uint)(CurDstAddr + (DstAddrInc << 1));
				IterCount--;
				RemCount--;

				if ( NDS.ARM7Timestamp >= NDS.ARM7Target ) break;
			}
		}
		else
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM7Timestamp += 1;

				NDS.ARM7Write32( CurDstAddr, NDS.ARM7Read32( CurSrcAddr ) );

				CurSrcAddr = (uint)(CurSrcAddr + (SrcAddrInc << 2));
				CurDstAddr = (uint)(CurDstAddr + (DstAddrInc << 2));
				IterCount--;
				RemCount--;

				if ( NDS.ARM7Timestamp >= NDS.ARM7Target ) break;
			}
		}

		Executing = false;
		Stall = false;

		if ( RemCount != 0 )
		{
			if ( IterCount == 0 )
			{
				Running = 0;
				NDS.ResumeCPU( 1, 1u << (int)Num );
			}

			return;
		}

		if ( (Cnt & (1 << 25)) == 0 )
			Cnt &= ~(1u << 31);

		if ( (Cnt & (1 << 30)) != 0 )
			NDS.SetIRQ( 1, IRQ.DMA0 + (int)Num );

		Running = 0;
		InProgress = false;
		NDS.ResumeCPU( 1, 1u << (int)Num );

		if ( StartMode == 0x12 )
			NDS.CartCheckDMA();
	}
}
