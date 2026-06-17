namespace Emulotl.Nds;

public sealed partial class NDS
{
	public long ARM9Timestamp, ARM9Target;
	public long ARM7Timestamp, ARM7Target;

	public uint[] IME = new uint[2];
	public uint[] IE = new uint[2];
	public uint[] IF = new uint[2];

	public byte[,] ARM7MemTimings = new byte[0x20000, 4];
	public byte[,] ARM9MemTimings = new byte[0x40000, 4];

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

	private void SetARM9RegionTimings( uint addrstart, uint addrend, int buswidth, int nonseq, int seq, bool mainram )
	{
		addrstart >>= 2;
		addrend >>= 2;

		int n16 = nonseq, s16 = seq, n32, s32;
		if ( buswidth == 16 ) { n32 = n16 + s16; s32 = s16 + s16; }
		else { n32 = n16; s32 = s16; }

		int cpuN = mainram ? 0 : 3;

		for ( uint i = addrstart; i < addrend; i++ )
		{
			ARM9MemTimings[i, 0] = (byte)(n16 + cpuN);
			ARM9MemTimings[i, 1] = (byte)s16;
			ARM9MemTimings[i, 2] = (byte)(n32 + cpuN);
			ARM9MemTimings[i, 3] = (byte)s32;
		}
	}

	private void SetARM7RegionTimings( uint addrstart, uint addrend, int buswidth, int nonseq, int seq )
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
		}
	}

	private void ResetTimings()
	{
		SetARM9RegionTimings( 0x00000, 0x100000, 32, 1, 1, false ); // void

		SetARM9RegionTimings( 0xFFFF0, 0x100000, 32, 1, 1, false ); // BIOS
		SetARM9RegionTimings( 0x02000, 0x03000, 16, 8, 1, true );   // main RAM
		SetARM9RegionTimings( 0x03000, 0x04000, 32, 1, 1, false );  // ARM9/shared WRAM
		SetARM9RegionTimings( 0x04000, 0x05000, 32, 1, 1, false );  // IO
		SetARM9RegionTimings( 0x05000, 0x06000, 16, 1, 1, false );  // palette
		SetARM9RegionTimings( 0x06000, 0x07000, 16, 1, 1, false );  // VRAM
		SetARM9RegionTimings( 0x07000, 0x08000, 32, 1, 1, false );  // OAM

		SetARM7RegionTimings( 0x00000, 0x100000, 32, 1, 1 ); // void

		SetARM7RegionTimings( 0x00000, 0x00010, 32, 1, 1 ); // BIOS
		SetARM7RegionTimings( 0x02000, 0x03000, 16, 8, 1 ); // main RAM
		SetARM7RegionTimings( 0x03000, 0x04000, 32, 1, 1 ); // ARM7/shared WRAM
		SetARM7RegionTimings( 0x04000, 0x04800, 32, 1, 1 ); // IO
		SetARM7RegionTimings( 0x06000, 0x07000, 16, 1, 1 ); // ARM7 VRAM
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
