namespace Emulotl.Nds;

public sealed partial class GPU
{
	private uint RdBank( int bank, uint addr, int bytes )
	{
		if ( bank < 4 )
			SyncVRAMCaptureBlock( (uint)((bank << 2) | (int)((addr & VRAMMask[bank]) >> 15)), false );

		byte[] m = VRAM[bank];
		uint a = addr & VRAMMask[bank];
		uint v = m[a];
		if ( bytes >= 2 ) v |= (uint)m[a + 1] << 8;
		if ( bytes == 4 ) v |= ((uint)m[a + 2] << 16) | ((uint)m[a + 3] << 24);
		return v;
	}

	private void WrBank( int bank, uint addr, uint val, int bytes )
	{
		if ( bank < 4 )
			SyncVRAMCaptureBlock( (uint)((bank << 2) | (int)((addr & VRAMMask[bank]) >> 15)), true );

		if ( (TexBankMask & (1u << bank)) != 0 )
			TexVRAMVersion++;

		byte[] m = VRAM[bank];
		uint a = addr & VRAMMask[bank];
		m[a] = (byte)val;
		if ( bytes >= 2 ) m[a + 1] = (byte)(val >> 8);
		if ( bytes == 4 ) { m[a + 2] = (byte)(val >> 16); m[a + 3] = (byte)(val >> 24); }
	}

	public uint ReadVRAM_ABG( uint addr, int bytes )
	{
		uint mask = VRAMMap_ABG[(addr >> 14) & 0x1F];
		uint ret = 0;
		if ( (mask & (1 << 0)) != 0 ) ret |= RdBank( 0, addr, bytes );
		if ( (mask & (1 << 1)) != 0 ) ret |= RdBank( 1, addr, bytes );
		if ( (mask & (1 << 2)) != 0 ) ret |= RdBank( 2, addr, bytes );
		if ( (mask & (1 << 3)) != 0 ) ret |= RdBank( 3, addr, bytes );
		if ( (mask & (1 << 4)) != 0 ) ret |= RdBank( 4, addr, bytes );
		if ( (mask & (1 << 5)) != 0 ) ret |= RdBank( 5, addr, bytes );
		if ( (mask & (1 << 6)) != 0 ) ret |= RdBank( 6, addr, bytes );
		return ret;
	}

	public void WriteVRAM_ABG( uint addr, uint val, int bytes )
	{
		uint mask = VRAMMap_ABG[(addr >> 14) & 0x1F];
		if ( (mask & (1 << 0)) != 0 ) WrBank( 0, addr, val, bytes );
		if ( (mask & (1 << 1)) != 0 ) WrBank( 1, addr, val, bytes );
		if ( (mask & (1 << 2)) != 0 ) WrBank( 2, addr, val, bytes );
		if ( (mask & (1 << 3)) != 0 ) WrBank( 3, addr, val, bytes );
		if ( (mask & (1 << 4)) != 0 ) WrBank( 4, addr, val, bytes );
		if ( (mask & (1 << 5)) != 0 ) WrBank( 5, addr, val, bytes );
		if ( (mask & (1 << 6)) != 0 ) WrBank( 6, addr, val, bytes );
	}

	public uint ReadVRAM_AOBJ( uint addr, int bytes )
	{
		uint mask = VRAMMap_AOBJ[(addr >> 14) & 0xF];
		uint ret = 0;
		if ( (mask & (1 << 0)) != 0 ) ret |= RdBank( 0, addr, bytes );
		if ( (mask & (1 << 1)) != 0 ) ret |= RdBank( 1, addr, bytes );
		if ( (mask & (1 << 4)) != 0 ) ret |= RdBank( 4, addr, bytes );
		if ( (mask & (1 << 5)) != 0 ) ret |= RdBank( 5, addr, bytes );
		if ( (mask & (1 << 6)) != 0 ) ret |= RdBank( 6, addr, bytes );
		return ret;
	}

	public void WriteVRAM_AOBJ( uint addr, uint val, int bytes )
	{
		uint mask = VRAMMap_AOBJ[(addr >> 14) & 0xF];
		if ( (mask & (1 << 0)) != 0 ) WrBank( 0, addr, val, bytes );
		if ( (mask & (1 << 1)) != 0 ) WrBank( 1, addr, val, bytes );
		if ( (mask & (1 << 4)) != 0 ) WrBank( 4, addr, val, bytes );
		if ( (mask & (1 << 5)) != 0 ) WrBank( 5, addr, val, bytes );
		if ( (mask & (1 << 6)) != 0 ) WrBank( 6, addr, val, bytes );
	}

	public uint ReadVRAM_BBG( uint addr, int bytes )
	{
		uint mask = VRAMMap_BBG[(addr >> 14) & 0x7];
		uint ret = 0;
		if ( (mask & (1 << 2)) != 0 ) ret |= RdBank( 2, addr, bytes );
		if ( (mask & (1 << 7)) != 0 ) ret |= RdBank( 7, addr, bytes );
		if ( (mask & (1 << 8)) != 0 ) ret |= RdBank( 8, addr, bytes );
		return ret;
	}

	public void WriteVRAM_BBG( uint addr, uint val, int bytes )
	{
		uint mask = VRAMMap_BBG[(addr >> 14) & 0x7];
		if ( (mask & (1 << 2)) != 0 ) WrBank( 2, addr, val, bytes );
		if ( (mask & (1 << 7)) != 0 ) WrBank( 7, addr, val, bytes );
		if ( (mask & (1 << 8)) != 0 ) WrBank( 8, addr, val, bytes );
	}

	public uint ReadVRAM_BOBJ( uint addr, int bytes )
	{
		uint mask = VRAMMap_BOBJ[(addr >> 14) & 0x7];
		uint ret = 0;
		if ( (mask & (1 << 3)) != 0 ) ret |= RdBank( 3, addr, bytes );
		if ( (mask & (1 << 8)) != 0 ) ret |= RdBank( 8, addr, bytes );
		return ret;
	}

	public void WriteVRAM_BOBJ( uint addr, uint val, int bytes )
	{
		uint mask = VRAMMap_BOBJ[(addr >> 14) & 0x7];
		if ( (mask & (1 << 3)) != 0 ) WrBank( 3, addr, val, bytes );
		if ( (mask & (1 << 8)) != 0 ) WrBank( 8, addr, val, bytes );
	}

	public uint ReadVRAM_ARM7( uint addr, int bytes )
	{
		uint mask = VRAMMap_ARM7[(addr >> 17) & 0x1];
		uint ret = 0;
		if ( (mask & (1 << 2)) != 0 ) ret |= RdBank( 2, addr, bytes );
		if ( (mask & (1 << 3)) != 0 ) ret |= RdBank( 3, addr, bytes );
		return ret;
	}

	public void WriteVRAM_ARM7( uint addr, uint val, int bytes )
	{
		uint mask = VRAMMap_ARM7[(addr >> 17) & 0x1];
		if ( (mask & (1 << 2)) != 0 ) WrBank( 2, addr, val, bytes );
		if ( (mask & (1 << 3)) != 0 ) WrBank( 3, addr, val, bytes );
	}

	public uint ReadVRAM_Texture( uint addr, int bytes )
	{
		uint mask = VRAMMap_Texture[(addr >> 17) & 0x3];
		uint ret = 0;
		if ( (mask & (1 << 0)) != 0 ) ret |= RdBank( 0, addr, bytes );
		if ( (mask & (1 << 1)) != 0 ) ret |= RdBank( 1, addr, bytes );
		if ( (mask & (1 << 2)) != 0 ) ret |= RdBank( 2, addr, bytes );
		if ( (mask & (1 << 3)) != 0 ) ret |= RdBank( 3, addr, bytes );
		return ret;
	}

	public uint ReadVRAM_TexPal( uint addr, int bytes )
	{
		uint mask = VRAMMap_TexPal[(addr >> 14) & 0x7];
		uint ret = 0;
		if ( (mask & (1 << 4)) != 0 ) ret |= RdBank( 4, addr, bytes );
		if ( (mask & (1 << 5)) != 0 ) ret |= RdBank( 5, addr, bytes );
		if ( (mask & (1 << 6)) != 0 ) ret |= RdBank( 6, addr, bytes );
		return ret;
	}

	public ushort ReadVRAM_ABGExtPal( uint addr )
	{
		uint mask = VRAMMap_ABGExtPal[(addr >> 13) & 0x3];
		uint ret = 0;
		if ( (mask & (1 << 4)) != 0 ) ret |= RdBank( 4, addr, 2 );
		if ( (mask & (1 << 5)) != 0 ) ret |= RdBank( 5, addr, 2 );
		if ( (mask & (1 << 6)) != 0 ) ret |= RdBank( 6, addr, 2 );
		return (ushort)ret;
	}

	public ushort ReadVRAM_AOBJExtPal( uint addr )
	{
		uint mask = VRAMMap_AOBJExtPal;
		uint ret = 0;
		if ( (mask & (1 << 4)) != 0 ) ret |= RdBank( 4, addr, 2 );
		if ( (mask & (1 << 5)) != 0 ) ret |= RdBank( 5, addr, 2 );
		if ( (mask & (1 << 6)) != 0 ) ret |= RdBank( 6, addr, 2 );
		return (ushort)ret;
	}

	public ushort ReadVRAM_BBGExtPal( uint addr )
	{
		uint mask = VRAMMap_BBGExtPal[(addr >> 13) & 0x3];
		uint ret = 0;
		if ( (mask & (1 << 7)) != 0 ) ret |= RdBank( 7, addr, 2 );
		return (ushort)ret;
	}

	public ushort ReadVRAM_BOBJExtPal( uint addr )
	{
		uint mask = VRAMMap_BOBJExtPal;
		uint ret = 0;
		if ( (mask & (1 << 8)) != 0 ) ret |= RdBank( 8, addr, 2 );
		return (ushort)ret;
	}

	private static int LcdcBank( uint addr )
	{
		int blk = (int)((addr >> 14) & 0x3F);
		if ( blk < 8 ) return 0;
		if ( blk < 16 ) return 1;
		if ( blk < 24 ) return 2;
		if ( blk < 32 ) return 3;
		if ( blk < 36 ) return 4;
		if ( blk == 36 ) return 5;
		if ( blk == 37 ) return 6;
		if ( blk < 40 ) return 7;
		if ( blk == 40 ) return 8;
		return -1;
	}

	public uint ReadVRAM_LCDC( uint addr, int bytes )
	{
		int bank = LcdcBank( addr );
		if ( bank < 0 ) return 0;
		if ( (VRAMMap_LCDC & (1 << bank)) != 0 ) return RdBank( bank, addr, bytes );
		return 0;
	}

	public void WriteVRAM_LCDC( uint addr, uint val, int bytes )
	{
		int bank = LcdcBank( addr );
		if ( bank < 0 ) return;
		if ( (VRAMMap_LCDC & (1 << bank)) != 0 ) WrBank( bank, addr, val, bytes );
	}

	public uint ReadVRAM_BG( uint addr, int bytes )
	{
		if ( (addr & 0xFFE00000) == 0x06000000 ) return ReadVRAM_ABG( addr, bytes );
		return ReadVRAM_BBG( addr, bytes );
	}

	public uint ReadVRAM_OBJ( uint addr, int bytes )
	{
		if ( (addr & 0xFFE00000) == 0x06400000 ) return ReadVRAM_AOBJ( addr, bytes );
		return ReadVRAM_BOBJ( addr, bytes );
	}

	public uint ReadPalette( uint addr, int bytes )
	{
		addr &= 0x7FF;
		uint v = Palette[addr];
		if ( bytes >= 2 ) v |= (uint)Palette[addr + 1] << 8;
		if ( bytes == 4 ) v |= ((uint)Palette[addr + 2] << 16) | ((uint)Palette[addr + 3] << 24);
		return v;
	}

	public void WritePalette( uint addr, uint val, int bytes )
	{
		addr &= 0x7FF;
		Palette[addr] = (byte)val;
		if ( bytes >= 2 ) Palette[addr + 1] = (byte)(val >> 8);
		if ( bytes == 4 ) { Palette[addr + 2] = (byte)(val >> 16); Palette[addr + 3] = (byte)(val >> 24); }
	}

	public uint ReadOAM( uint addr, int bytes )
	{
		addr &= 0x7FF;
		uint v = OAM[addr];
		if ( bytes >= 2 ) v |= (uint)OAM[addr + 1] << 8;
		if ( bytes == 4 ) v |= ((uint)OAM[addr + 2] << 16) | ((uint)OAM[addr + 3] << 24);
		return v;
	}

	public void WriteOAM( uint addr, uint val, int bytes )
	{
		addr &= 0x7FF;
		OAM[addr] = (byte)val;
		if ( bytes >= 2 ) OAM[addr + 1] = (byte)(val >> 8);
		if ( bytes == 4 ) { OAM[addr + 2] = (byte)(val >> 16); OAM[addr + 3] = (byte)(val >> 24); }
	}
}
