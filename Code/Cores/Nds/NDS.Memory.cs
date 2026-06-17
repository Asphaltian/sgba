namespace Emulotl.Nds;

public sealed partial class NDS
{
	public byte[] ARM9BIOS = new byte[0x1000];
	public byte[] ARM7BIOS = new byte[NdsConstants.Arm7BiosSize];

	public uint MainRAMMask = NdsConstants.MainRamSize - 1;
	public byte WRAMCnt;
	private MemRegion SWRAM_ARM9;
	private MemRegion SWRAM_ARM7;

	public uint[] ExMemCnt = new uint[2];
	public uint PowerControl9;
	public uint PowerControl7;
	public uint ARM7BIOSProt;
	public byte PostFlag9;
	public byte PostFlag7;

	private void ResetMemory()
	{
		MainRAMMask = NdsConstants.MainRamSize - 1;

		Array.Clear( ARM9BIOS );
		Array.Clear( ARM7BIOS );

		if ( _nativeArm9Bios != null )
			Array.Copy( _nativeArm9Bios, ARM9BIOS, Math.Min( _nativeArm9Bios.Length, ARM9BIOS.Length ) );
		else
			Array.Copy( FreeBIOS.NtrArm9, ARM9BIOS, FreeBIOS.NtrArm9.Length );

		if ( _nativeArm7Bios != null )
		{
			Array.Copy( _nativeArm7Bios, ARM7BIOS, Math.Min( _nativeArm7Bios.Length, ARM7BIOS.Length ) );
			IsNativeArm7Bios = true;
		}
		else
		{
			Array.Copy( FreeBIOS.NtrArm7, ARM7BIOS, FreeBIOS.NtrArm7.Length );
			IsNativeArm7Bios = false;
		}

		ExMemCnt[0] = ExMemCnt[1] = 0;
		PowerControl9 = 0;
		PowerControl7 = 0;
		ARM7BIOSProt = 0;
		PostFlag9 = 0;
		PostFlag7 = 0;

		WRAMCnt = 0xFF;
		MapSharedWRAM( 0 );
	}

	public void MapSharedWRAM( byte val )
	{
		if ( val == WRAMCnt )
			return;

		WRAMCnt = val;

		switch ( WRAMCnt & 0x3 )
		{
			case 0:
				SWRAM_ARM9 = new MemRegion { Mem = SharedWRAM, Offset = 0, Mask = 0x7FFF };
				SWRAM_ARM7 = default;
				break;
			case 1:
				SWRAM_ARM9 = new MemRegion { Mem = SharedWRAM, Offset = 0x4000, Mask = 0x3FFF };
				SWRAM_ARM7 = new MemRegion { Mem = SharedWRAM, Offset = 0, Mask = 0x3FFF };
				break;
			case 2:
				SWRAM_ARM9 = new MemRegion { Mem = SharedWRAM, Offset = 0, Mask = 0x3FFF };
				SWRAM_ARM7 = new MemRegion { Mem = SharedWRAM, Offset = 0x4000, Mask = 0x3FFF };
				break;
			case 3:
				SWRAM_ARM9 = default;
				SWRAM_ARM7 = new MemRegion { Mem = SharedWRAM, Offset = 0, Mask = 0x7FFF };
				break;
		}
	}

	public void GetARM9CodeMem( uint addr, ref MemRegion region )
	{
		if ( (addr & 0xFFFFF000) == 0xFFFF0000 )
		{
			region.Mem = ARM9BIOS;
			region.Offset = 0;
			region.Mask = 0xFFF;
			return;
		}

		switch ( addr & 0xFF000000 )
		{
			case 0x02000000:
				region.Mem = MainRAM;
				region.Offset = 0;
				region.Mask = MainRAMMask;
				return;
			case 0x03000000:
				region = SWRAM_ARM9;
				return;
			default:
				region.Mem = null;
				return;
		}
	}

	public void GetARM7CodeMem( uint addr, ref MemRegion region )
	{
		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000:
				region.Mem = MainRAM;
				region.Offset = 0;
				region.Mask = MainRAMMask;
				return;
			case 0x03000000:
				if ( SWRAM_ARM7.Mem != null )
				{
					region = SWRAM_ARM7;
				}
				else
				{
					region.Mem = ARM7WRAM;
					region.Offset = 0;
					region.Mask = NdsConstants.Arm7WramSize - 1;
				}
				return;
			case 0x03800000:
				region.Mem = ARM7WRAM;
				region.Offset = 0;
				region.Mask = NdsConstants.Arm7WramSize - 1;
				return;
			default:
				region.Mem = null;
				return;
		}
	}

	private static ushort Read16( byte[] mem, uint offset ) => (ushort)(mem[offset] | (mem[offset + 1] << 8));
	private static uint Read32( byte[] mem, uint offset ) => (uint)(mem[offset] | (mem[offset + 1] << 8) | (mem[offset + 2] << 16) | (mem[offset + 3] << 24));
	private static void Write16( byte[] mem, uint offset, ushort val ) { mem[offset] = (byte)val; mem[offset + 1] = (byte)(val >> 8); }
	private static void Write32( byte[] mem, uint offset, uint val ) { mem[offset] = (byte)val; mem[offset + 1] = (byte)(val >> 8); mem[offset + 2] = (byte)(val >> 16); mem[offset + 3] = (byte)(val >> 24); }

	public byte ARM9Read8( uint addr )
	{
		if ( (addr & 0xFFFFF000) == 0xFFFF0000 )
			return ARM9BIOS[addr & 0xFFF];

		switch ( addr & 0xFF000000 )
		{
			case 0x02000000: return MainRAM[addr & MainRAMMask];
			case 0x03000000: return SWRAM_ARM9.Mem != null ? SWRAM_ARM9.Mem[SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask)] : (byte)0;
			case 0x04000000: return ARM9IORead8( addr );
			case 0x05000000: return PaletteEnabled9( addr ) ? (byte)GPU.ReadPalette( addr, 1 ) : (byte)0;
			case 0x06000000: return (byte)ReadVRAM9( addr, 1 );
			case 0x07000000: return PaletteEnabled9( addr ) ? (byte)GPU.ReadOAM( addr, 1 ) : (byte)0;
		}
		return 0;
	}

	private uint ReadVRAM9( uint addr, int bytes )
	{
		return (addr & 0x00E00000) switch
		{
			0x00000000 => GPU.ReadVRAM_ABG( addr, bytes ),
			0x00200000 => GPU.ReadVRAM_BBG( addr, bytes ),
			0x00400000 => GPU.ReadVRAM_AOBJ( addr, bytes ),
			0x00600000 => GPU.ReadVRAM_BOBJ( addr, bytes ),
			_ => GPU.ReadVRAM_LCDC( addr, bytes ),
		};
	}

	private void WriteVRAM9( uint addr, uint val, int bytes )
	{
		switch ( addr & 0x00E00000 )
		{
			case 0x00000000: GPU.WriteVRAM_ABG( addr, val, bytes ); return;
			case 0x00200000: GPU.WriteVRAM_BBG( addr, val, bytes ); return;
			case 0x00400000: GPU.WriteVRAM_AOBJ( addr, val, bytes ); return;
			case 0x00600000: GPU.WriteVRAM_BOBJ( addr, val, bytes ); return;
			default: GPU.WriteVRAM_LCDC( addr, val, bytes ); return;
		}
	}

	public ushort ARM9Read16( uint addr )
	{
		addr &= ~0x1u;
		if ( (addr & 0xFFFFF000) == 0xFFFF0000 )
			return Read16( ARM9BIOS, addr & 0xFFF );

		switch ( addr & 0xFF000000 )
		{
			case 0x02000000: return Read16( MainRAM, addr & MainRAMMask );
			case 0x03000000: return SWRAM_ARM9.Mem != null ? Read16( SWRAM_ARM9.Mem, SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask) ) : (ushort)0;
			case 0x04000000: return ARM9IORead16( addr );
			case 0x05000000: return PaletteEnabled9( addr ) ? (ushort)GPU.ReadPalette( addr, 2 ) : (ushort)0;
			case 0x06000000: return (ushort)ReadVRAM9( addr, 2 );
			case 0x07000000: return PaletteEnabled9( addr ) ? (ushort)GPU.ReadOAM( addr, 2 ) : (ushort)0;
		}
		return 0;
	}

	public uint ARM9Read32( uint addr )
	{
		addr &= ~0x3u;
		if ( (addr & 0xFFFFF000) == 0xFFFF0000 )
			return Read32( ARM9BIOS, addr & 0xFFF );

		switch ( addr & 0xFF000000 )
		{
			case 0x02000000: return Read32( MainRAM, addr & MainRAMMask );
			case 0x03000000: return SWRAM_ARM9.Mem != null ? Read32( SWRAM_ARM9.Mem, SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask) ) : 0;
			case 0x04000000: return ARM9IORead32( addr );
			case 0x05000000: return PaletteEnabled9( addr ) ? GPU.ReadPalette( addr, 4 ) : 0;
			case 0x06000000: return ReadVRAM9( addr, 4 );
			case 0x07000000: return PaletteEnabled9( addr ) ? GPU.ReadOAM( addr, 4 ) : 0;
		}
		return 0;
	}

	public void ARM9Write8( uint addr, byte val )
	{
		switch ( addr & 0xFF000000 )
		{
			case 0x02000000: MainRAM[addr & MainRAMMask] = val; return;
			case 0x03000000: if ( SWRAM_ARM9.Mem != null ) SWRAM_ARM9.Mem[SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask)] = val; return;
			case 0x04000000: ARM9IOWrite8( addr, val ); return;
		}
	}

	public void ARM9Write16( uint addr, ushort val )
	{
		addr &= ~0x1u;
		switch ( addr & 0xFF000000 )
		{
			case 0x02000000: Write16( MainRAM, addr & MainRAMMask, val ); return;
			case 0x03000000: if ( SWRAM_ARM9.Mem != null ) Write16( SWRAM_ARM9.Mem, SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask), val ); return;
			case 0x04000000: ARM9IOWrite16( addr, val ); return;
			case 0x05000000: if ( PaletteEnabled9( addr ) ) GPU.WritePalette( addr, val, 2 ); return;
			case 0x06000000: WriteVRAM9( addr, val, 2 ); return;
			case 0x07000000: if ( PaletteEnabled9( addr ) ) GPU.WriteOAM( addr, val, 2 ); return;
		}
	}

	public void ARM9Write32( uint addr, uint val )
	{
		addr &= ~0x3u;
		switch ( addr & 0xFF000000 )
		{
			case 0x02000000:
				Write32( MainRAM, addr & MainRAMMask, val ); return;
			case 0x03000000: if ( SWRAM_ARM9.Mem != null ) Write32( SWRAM_ARM9.Mem, SWRAM_ARM9.Offset + (addr & SWRAM_ARM9.Mask), val ); return;
			case 0x04000000: ARM9IOWrite32( addr, val ); return;
			case 0x05000000: if ( PaletteEnabled9( addr ) ) GPU.WritePalette( addr, val, 4 ); return;
			case 0x06000000: WriteVRAM9( addr, val, 4 ); return;
			case 0x07000000: if ( PaletteEnabled9( addr ) ) GPU.WriteOAM( addr, val, 4 ); return;
		}
	}

	private bool PaletteEnabled9( uint addr ) => (PowerControl9 & ((addr & 0x400) != 0 ? (1u << 9) : (1u << 1))) != 0;

	public byte ARM7Read8( uint addr )
	{
		if ( addr < 0x00004000 )
		{
			if ( ARM7.R[15] >= 0x00004000 ) return 0xFF;
			if ( addr < ARM7BIOSProt && ARM7.R[15] >= ARM7BIOSProt ) return 0xFF;
			return ARM7BIOS[addr];
		}

		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000: return MainRAM[addr & MainRAMMask];
			case 0x03000000: return SWRAM_ARM7.Mem != null ? SWRAM_ARM7.Mem[SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask)] : ARM7WRAM[addr & (NdsConstants.Arm7WramSize - 1)];
			case 0x03800000: return ARM7WRAM[addr & (NdsConstants.Arm7WramSize - 1)];
			case 0x04000000: return ARM7IORead8( addr );
			case 0x06000000:
			case 0x06800000: return (byte)GPU.ReadVRAM_ARM7( addr, 1 );
		}
		return 0;
	}

	public ushort ARM7Read16( uint addr )
	{
		addr &= ~0x1u;
		if ( addr < 0x00004000 )
		{
			if ( ARM7.R[15] >= 0x00004000 ) return 0xFFFF;
			if ( addr < ARM7BIOSProt && ARM7.R[15] >= ARM7BIOSProt ) return 0xFFFF;
			return Read16( ARM7BIOS, addr );
		}

		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000: return Read16( MainRAM, addr & MainRAMMask );
			case 0x03000000: return SWRAM_ARM7.Mem != null ? Read16( SWRAM_ARM7.Mem, SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask) ) : Read16( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1) );
			case 0x03800000: return Read16( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1) );
			case 0x04000000: return ARM7IORead16( addr );
			case 0x06000000:
			case 0x06800000: return (ushort)GPU.ReadVRAM_ARM7( addr, 2 );
		}
		return 0;
	}

	public uint ARM7Read32( uint addr )
	{
		addr &= ~0x3u;
		if ( addr < 0x00004000 )
		{
			if ( ARM7.R[15] >= 0x00004000 ) return 0xFFFFFFFF;
			if ( addr < ARM7BIOSProt && ARM7.R[15] >= ARM7BIOSProt ) return 0xFFFFFFFF;
			return Read32( ARM7BIOS, addr );
		}

		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000: return Read32( MainRAM, addr & MainRAMMask );
			case 0x03000000: return SWRAM_ARM7.Mem != null ? Read32( SWRAM_ARM7.Mem, SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask) ) : Read32( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1) );
			case 0x03800000: return Read32( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1) );
			case 0x04000000: return ARM7IORead32( addr );
			case 0x06000000:
			case 0x06800000: return GPU.ReadVRAM_ARM7( addr, 4 );
		}
		return 0;
	}

	public void ARM7Write8( uint addr, byte val )
	{
		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000: MainRAM[addr & MainRAMMask] = val; return;
			case 0x03000000:
				if ( SWRAM_ARM7.Mem != null ) SWRAM_ARM7.Mem[SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask)] = val;
				else ARM7WRAM[addr & (NdsConstants.Arm7WramSize - 1)] = val;
				return;
			case 0x03800000: ARM7WRAM[addr & (NdsConstants.Arm7WramSize - 1)] = val; return;
			case 0x04000000: ARM7IOWrite8( addr, val ); return;
			case 0x06000000:
			case 0x06800000: GPU.WriteVRAM_ARM7( addr, val, 1 ); return;
		}
	}

	public void ARM7Write16( uint addr, ushort val )
	{
		addr &= ~0x1u;
		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000: Write16( MainRAM, addr & MainRAMMask, val ); return;
			case 0x03000000:
				if ( SWRAM_ARM7.Mem != null ) Write16( SWRAM_ARM7.Mem, SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask), val );
				else Write16( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1), val );
				return;
			case 0x03800000: Write16( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1), val ); return;
			case 0x04000000: ARM7IOWrite16( addr, val ); return;
			case 0x06000000:
			case 0x06800000: GPU.WriteVRAM_ARM7( addr, val, 2 ); return;
		}
	}

	public void ARM7Write32( uint addr, uint val )
	{
		addr &= ~0x3u;
		switch ( addr & 0xFF800000 )
		{
			case 0x02000000:
			case 0x02800000:
				Write32( MainRAM, addr & MainRAMMask, val ); return;
			case 0x03000000:
				if ( SWRAM_ARM7.Mem != null ) Write32( SWRAM_ARM7.Mem, SWRAM_ARM7.Offset + (addr & SWRAM_ARM7.Mask), val );
				else Write32( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1), val );
				return;
			case 0x03800000:
				Write32( ARM7WRAM, addr & (NdsConstants.Arm7WramSize - 1), val ); return;
			case 0x04000000: ARM7IOWrite32( addr, val ); return;
			case 0x06000000:
			case 0x06800000: GPU.WriteVRAM_ARM7( addr, val, 4 ); return;
		}
	}
}
