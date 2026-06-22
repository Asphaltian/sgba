namespace Emulotl.Nds;

public sealed partial class NDS
{
	public long ARM9Timestamp, ARM9Target;
	public long ARM7Timestamp, ARM7Target;

	public uint[] IME = new uint[2];
	public uint[] IE = new uint[2];
	public uint[] IF = new uint[2];

	public byte[,] ARM7MemTimings = new byte[0x20000, 4];
	public byte[,] ARM9MemTimings = new byte[0x40000, 8];
	public uint[] ARM7Regions = new uint[0x20000];
	public uint[] ARM9Regions = new uint[0x40000];
	public int ARM9ClockShift = 1;

	private void ResetInterrupts()
	{
		ARM9Timestamp = 0;
		ARM9Target = 0;
		ARM7Timestamp = 0;
		ARM7Target = 0;

		IME[0] = IME[1] = 0;
		IE[0] = IE[1] = 0;
		IF[0] = IF[1] = 0;
	}

	private void SetARM9RegionTimings( uint addrstart, uint addrend, uint region, int buswidth, int nonseq, int seq )
	{
		addrstart >>= 2;
		addrend >>= 2;

		int n16 = nonseq, s16 = seq, n32, s32;
		if ( buswidth == 16 ) { n32 = n16 + s16; s32 = s16 + s16; }
		else { n32 = n16; s32 = s16; }

		int cpuN = region == Mem9.MainRAM ? 0 : 3;

		for ( uint i = addrstart; i < addrend; i++ )
		{
			ARM9MemTimings[i, 0] = (byte)(n16 + cpuN);
			ARM9MemTimings[i, 1] = (byte)s16;
			ARM9MemTimings[i, 2] = (byte)(n32 + cpuN);
			ARM9MemTimings[i, 3] = (byte)s32;
			ARM9MemTimings[i, 4] = (byte)n16;
			ARM9MemTimings[i, 5] = (byte)s16;
			ARM9MemTimings[i, 6] = (byte)n32;
			ARM9MemTimings[i, 7] = (byte)s32;
			ARM9Regions[i] = region;
		}
	}

	private void SetARM7RegionTimings( uint addrstart, uint addrend, uint region, int buswidth, int nonseq, int seq )
	{
		addrstart >>= 3;
		addrend >>= 3;

		int n16 = nonseq, s16 = seq, n32, s32;
		if ( buswidth == 16 ) { n32 = n16 + s16; s32 = s16 + s16; }
		else { n32 = n16; s32 = s16; }

		for ( uint i = addrstart; i < addrend; i++ )
		{
			ARM7MemTimings[i, 0] = (byte)n16;
			ARM7MemTimings[i, 1] = (byte)s16;
			ARM7MemTimings[i, 2] = (byte)n32;
			ARM7MemTimings[i, 3] = (byte)s32;
			ARM7Regions[i] = region;
		}
	}

	private void ResetTimings()
	{
		SetARM9RegionTimings( 0x00000, 0x100000, 0, 32, 1, 1 );

		SetARM9RegionTimings( 0xFFFF0, 0x100000, Mem9.BIOS, 32, 1, 1 );
		SetARM9RegionTimings( 0x02000, 0x03000, Mem9.MainRAM, 16, 8, 1 );
		SetARM9RegionTimings( 0x03000, 0x04000, Mem9.WRAM, 32, 1, 1 );
		SetARM9RegionTimings( 0x04000, 0x05000, Mem9.IO, 32, 1, 1 );
		SetARM9RegionTimings( 0x05000, 0x06000, Mem9.Pal, 16, 1, 1 );
		SetARM9RegionTimings( 0x06000, 0x07000, Mem9.VRAM, 16, 1, 1 );
		SetARM9RegionTimings( 0x07000, 0x08000, Mem9.OAM, 32, 1, 1 );

		SetARM7RegionTimings( 0x00000, 0x100000, 0, 32, 1, 1 );

		SetARM7RegionTimings( 0x00000, 0x00010, Mem7.BIOS, 32, 1, 1 );
		SetARM7RegionTimings( 0x02000, 0x03000, Mem7.MainRAM, 16, 8, 1 );
		SetARM7RegionTimings( 0x03000, 0x04000, Mem7.WRAM, 32, 1, 1 );
		SetARM7RegionTimings( 0x04000, 0x04800, Mem7.IO, 32, 1, 1 );
		SetARM7RegionTimings( 0x06000, 0x07000, Mem7.VRAM, 16, 1, 1 );
	}

	public bool HaltInterrupted( uint cpu )
	{
		if ( cpu == 0 )
		{
			if ( (IME[0] & 0x1) == 0 )
				return false;
		}

		if ( (IF[cpu] & IE[cpu]) != 0 )
			return true;

		return false;
	}

	public void MonitorARM9Jump( uint addr ) { }

	public void UpdateIRQ( uint cpu )
	{
		ARM arm = cpu != 0 ? ARM7 : ARM9;

		if ( (IME[cpu] & 0x1) != 0 )
			arm.IRQ = (byte)((IE[cpu] & IF[cpu]) != 0 ? 1 : 0);
		else
			arm.IRQ = 0;
	}

	public void SetIRQ( uint cpu, int irq )
	{
		IF[cpu] |= 1u << irq;
		UpdateIRQ( cpu );
	}

	public void ClearIRQ( uint cpu, int irq )
	{
		IF[cpu] &= ~(1u << irq);
		UpdateIRQ( cpu );
	}
}

public static class IRQ
{
	public const int VBlank = 0;
	public const int HBlank = 1;
	public const int VCount = 2;
	public const int Timer0 = 3;
	public const int Timer1 = 4;
	public const int Timer2 = 5;
	public const int Timer3 = 6;
	public const int Rtc = 7;
	public const int DMA0 = 8;
	public const int DMA1 = 9;
	public const int DMA2 = 10;
	public const int DMA3 = 11;
	public const int Keypad = 12;
	public const int GBASlot = 13;
	public const int IPCSync = 16;
	public const int IPCSendDone = 17;
	public const int IPCRecv = 18;
	public const int CartXferDone = 19;
	public const int GXFIFO = 21;
	public const int SPI = 23;
	public const int Wifi = 24;
}

public static class Mem9
{
	public const uint ITCM = 0x00000001;
	public const uint DTCM = 0x00000002;
	public const uint BIOS = 0x00000004;
	public const uint MainRAM = 0x00000008;
	public const uint WRAM = 0x00000010;
	public const uint IO = 0x00000020;
	public const uint Pal = 0x00000040;
	public const uint OAM = 0x00000080;
	public const uint VRAM = 0x00000100;
	public const uint GBAROM = 0x00020000;
	public const uint GBARAM = 0x00040000;
}

public static class Mem7
{
	public const uint BIOS = 0x00000001;
	public const uint MainRAM = 0x00000002;
	public const uint WRAM = 0x00000004;
	public const uint IO = 0x00000008;
	public const uint Wifi0 = 0x00000010;
	public const uint Wifi1 = 0x00000020;
	public const uint VRAM = 0x00000040;
	public const uint GBAROM = 0x00000100;
	public const uint GBARAM = 0x00000200;
}
