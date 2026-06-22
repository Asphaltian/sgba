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

	private byte[] MRAMBurstTable;
	private uint MRAMBurstCount;

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

		MRAMBurstTable = null;
		MRAMBurstCount = 0;
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
			else if ( StartMode == 0x07 )
				NDS.GPU3D.CheckFIFODMA();

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

	private uint UnitTimings9_16( bool burststart )
	{
		uint src_id = CurSrcAddr >> 14;
		uint dst_id = CurDstAddr >> 14;

		uint src_rgn = NDS.ARM9Regions[src_id];
		uint dst_rgn = NDS.ARM9Regions[dst_id];

		uint src_n = NDS.ARM9MemTimings[src_id, 4];
		uint src_s = NDS.ARM9MemTimings[src_id, 5];
		uint dst_n = NDS.ARM9MemTimings[dst_id, 4];
		uint dst_s = NDS.ARM9MemTimings[dst_id, 5];

		if ( src_rgn == Mem9.MainRAM )
		{
			if ( dst_rgn == Mem9.MainRAM )
				return 16;

			if ( SrcAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( dst_rgn == Mem9.GBAROM )
						MRAMBurstTable = dst_s == 4 ? DMATiming.MRAMRead16Bursts[1] : DMATiming.MRAMRead16Bursts[2];
					else
						MRAMBurstTable = DMATiming.MRAMRead16Bursts[0];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			uint rbase = ((CurSrcAddr & 0x1F) == 0x1E) ? 7u : 8u;
			return rbase + (burststart ? dst_n : dst_s);
		}

		if ( dst_rgn == Mem9.MainRAM )
		{
			if ( DstAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( src_rgn == Mem9.GBAROM )
						MRAMBurstTable = src_s == 4 ? DMATiming.MRAMWrite16Bursts[1] : DMATiming.MRAMWrite16Bursts[2];
					else
						MRAMBurstTable = DMATiming.MRAMWrite16Bursts[0];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			return (burststart ? src_n : src_s) + 7;
		}

		if ( (src_rgn & dst_rgn) != 0 )
			return src_n + dst_n + 1;

		return burststart ? src_n + dst_n : src_s + dst_s;
	}

	private uint UnitTimings9_32( bool burststart )
	{
		uint src_id = CurSrcAddr >> 14;
		uint dst_id = CurDstAddr >> 14;

		uint src_rgn = NDS.ARM9Regions[src_id];
		uint dst_rgn = NDS.ARM9Regions[dst_id];

		uint src_n = NDS.ARM9MemTimings[src_id, 6];
		uint src_s = NDS.ARM9MemTimings[src_id, 7];
		uint dst_n = NDS.ARM9MemTimings[dst_id, 6];
		uint dst_s = NDS.ARM9MemTimings[dst_id, 7];

		if ( src_rgn == Mem9.MainRAM )
		{
			if ( dst_rgn == Mem9.MainRAM )
				return 18;

			if ( SrcAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( dst_rgn == Mem9.GBAROM )
						MRAMBurstTable = dst_s == 8 ? DMATiming.MRAMRead32Bursts[2] : DMATiming.MRAMRead32Bursts[3];
					else if ( dst_n == 2 )
						MRAMBurstTable = DMATiming.MRAMRead32Bursts[0];
					else
						MRAMBurstTable = DMATiming.MRAMRead32Bursts[1];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			uint rbase = ((CurSrcAddr & 0x1F) == 0x1C) ? (dst_n == 2 ? 7u : 8u) : 9u;
			return rbase + (burststart ? dst_n : dst_s);
		}

		if ( dst_rgn == Mem9.MainRAM )
		{
			if ( DstAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( src_rgn == Mem9.GBAROM )
						MRAMBurstTable = src_s == 8 ? DMATiming.MRAMWrite32Bursts[2] : DMATiming.MRAMWrite32Bursts[3];
					else if ( src_n == 2 )
						MRAMBurstTable = DMATiming.MRAMWrite32Bursts[0];
					else
						MRAMBurstTable = DMATiming.MRAMWrite32Bursts[1];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			return (burststart ? src_n : src_s) + 8;
		}

		if ( (src_rgn & dst_rgn) != 0 )
			return src_n + dst_n + 1;

		return burststart ? src_n + dst_n : src_s + dst_s;
	}

	private uint UnitTimings7_16( bool burststart )
	{
		uint src_id = CurSrcAddr >> 15;
		uint dst_id = CurDstAddr >> 15;

		uint src_rgn = NDS.ARM7Regions[src_id];
		uint dst_rgn = NDS.ARM7Regions[dst_id];

		uint src_n = NDS.ARM7MemTimings[src_id, 0];
		uint src_s = NDS.ARM7MemTimings[src_id, 1];
		uint dst_n = NDS.ARM7MemTimings[dst_id, 0];
		uint dst_s = NDS.ARM7MemTimings[dst_id, 1];

		if ( src_rgn == Mem7.MainRAM )
		{
			if ( dst_rgn == Mem7.MainRAM )
				return 16;

			if ( SrcAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( dst_rgn == Mem7.GBAROM || dst_rgn == Mem7.Wifi0 || dst_rgn == Mem7.Wifi1 )
						MRAMBurstTable = dst_s == 4 ? DMATiming.MRAMRead16Bursts[1] : DMATiming.MRAMRead16Bursts[2];
					else
						MRAMBurstTable = DMATiming.MRAMRead16Bursts[0];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			uint rbase = ((CurSrcAddr & 0x1F) == 0x1E) ? 7u : 8u;
			return rbase + (burststart ? dst_n : dst_s);
		}

		if ( dst_rgn == Mem7.MainRAM )
		{
			if ( DstAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( src_rgn == Mem7.GBAROM || src_rgn == Mem7.Wifi0 || src_rgn == Mem7.Wifi1 )
						MRAMBurstTable = src_s == 4 ? DMATiming.MRAMWrite16Bursts[1] : DMATiming.MRAMWrite16Bursts[2];
					else
						MRAMBurstTable = DMATiming.MRAMWrite16Bursts[0];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			return (burststart ? src_n : src_s) + 7;
		}

		if ( (src_rgn & dst_rgn) != 0 )
			return src_n + dst_n + 1;

		return burststart ? src_n + dst_n : src_s + dst_s;
	}

	private uint UnitTimings7_32( bool burststart )
	{
		uint src_id = CurSrcAddr >> 15;
		uint dst_id = CurDstAddr >> 15;

		uint src_rgn = NDS.ARM7Regions[src_id];
		uint dst_rgn = NDS.ARM7Regions[dst_id];

		uint src_n = NDS.ARM7MemTimings[src_id, 2];
		uint src_s = NDS.ARM7MemTimings[src_id, 3];
		uint dst_n = NDS.ARM7MemTimings[dst_id, 2];
		uint dst_s = NDS.ARM7MemTimings[dst_id, 3];

		if ( src_rgn == Mem7.MainRAM )
		{
			if ( dst_rgn == Mem7.MainRAM )
				return 18;

			if ( SrcAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( dst_rgn == Mem7.GBAROM || dst_rgn == Mem7.Wifi0 || dst_rgn == Mem7.Wifi1 )
						MRAMBurstTable = dst_s == 8 ? DMATiming.MRAMRead32Bursts[2] : DMATiming.MRAMRead32Bursts[3];
					else if ( dst_n == 2 )
						MRAMBurstTable = DMATiming.MRAMRead32Bursts[0];
					else
						MRAMBurstTable = DMATiming.MRAMRead32Bursts[1];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			uint rbase = ((CurSrcAddr & 0x1F) == 0x1C) ? (dst_n == 2 ? 7u : 8u) : 9u;
			return rbase + (burststart ? dst_n : dst_s);
		}

		if ( dst_rgn == Mem7.MainRAM )
		{
			if ( DstAddrInc > 0 )
			{
				if ( burststart || MRAMBurstTable[MRAMBurstCount] == 0 )
				{
					MRAMBurstCount = 0;

					if ( src_rgn == Mem7.GBAROM || src_rgn == Mem7.Wifi0 || src_rgn == Mem7.Wifi1 )
						MRAMBurstTable = src_s == 8 ? DMATiming.MRAMWrite32Bursts[2] : DMATiming.MRAMWrite32Bursts[3];
					else if ( src_n == 2 )
						MRAMBurstTable = DMATiming.MRAMWrite32Bursts[0];
					else
						MRAMBurstTable = DMATiming.MRAMWrite32Bursts[1];
				}

				return MRAMBurstTable[MRAMBurstCount++];
			}

			return (burststart ? src_n : src_s) + 8;
		}

		if ( (src_rgn & dst_rgn) != 0 )
			return src_n + dst_n + 1;

		return burststart ? src_n + dst_n : src_s + dst_s;
	}

	private void Run9()
	{
		if ( NDS.ARM9Timestamp >= NDS.ARM9Target ) return;

		Executing = true;

		bool burststart = Running == 2;
		Running = 1;

		if ( (Cnt & (1 << 26)) == 0 )
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM9Timestamp += UnitTimings9_16( burststart ) << NDS.ARM9ClockShift;
				burststart = false;

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
				NDS.ARM9Timestamp += UnitTimings9_32( burststart ) << NDS.ARM9ClockShift;
				burststart = false;

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

				if ( StartMode == 0x07 )
					NDS.GPU3D.CheckFIFODMA();
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

		bool burststart = Running == 2;
		Running = 1;

		if ( (Cnt & (1 << 26)) == 0 )
		{
			while ( IterCount > 0 && !Stall )
			{
				NDS.ARM7Timestamp += UnitTimings7_16( burststart );
				burststart = false;

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
				NDS.ARM7Timestamp += UnitTimings7_32( burststart );
				burststart = false;

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
