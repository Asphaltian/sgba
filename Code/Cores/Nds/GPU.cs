namespace Emulotl.Nds;

public sealed partial class GPU
{
	private readonly NDS NDS;

	public byte[] VRAM_A = new byte[128 * 1024];
	public byte[] VRAM_B = new byte[128 * 1024];
	public byte[] VRAM_C = new byte[128 * 1024];
	public byte[] VRAM_D = new byte[128 * 1024];
	public byte[] VRAM_E = new byte[64 * 1024];
	public byte[] VRAM_F = new byte[16 * 1024];
	public byte[] VRAM_G = new byte[16 * 1024];
	public byte[] VRAM_H = new byte[32 * 1024];
	public byte[] VRAM_I = new byte[16 * 1024];

	public readonly byte[][] VRAM;
	public static readonly uint[] VRAMMask = [0x1FFFF, 0x1FFFF, 0x1FFFF, 0x1FFFF, 0xFFFF, 0x3FFF, 0x3FFF, 0x7FFF, 0x3FFF];

	public byte[] VRAMCNT = new byte[9];
	public byte VRAMSTAT;

	public uint VRAMMap_LCDC;
	public uint[] VRAMMap_ABG = new uint[0x20];
	public uint[] VRAMMap_AOBJ = new uint[0x10];
	public uint[] VRAMMap_BBG = new uint[0x8];
	public uint[] VRAMMap_BOBJ = new uint[0x8];
	public uint[] VRAMMap_ABGExtPal = new uint[4];
	public uint VRAMMap_AOBJExtPal;
	public uint[] VRAMMap_BBGExtPal = new uint[4];
	public uint VRAMMap_BOBJExtPal;
	public uint[] VRAMMap_Texture = new uint[4];
	public uint[] VRAMMap_TexPal = new uint[8];
	public uint[] VRAMMap_ARM7 = new uint[2];

	public ulong TexVRAMVersion;
	public uint TexBankMask;
	private ulong _lastTexMapSig;

	public byte[] Palette = new byte[2 * 1024];
	public byte[] OAM = new byte[2 * 1024];

	public ushort MasterBrightnessA;
	public ushort MasterBrightnessB;

	public GPU2D GPU2D_A;
	public GPU2D GPU2D_B;

	public GPU( NDS nds )
	{
		NDS = nds;
		VRAM = [VRAM_A, VRAM_B, VRAM_C, VRAM_D, VRAM_E, VRAM_F, VRAM_G, VRAM_H, VRAM_I];
		GPU2D_A = new GPU2D( 0, this );
		GPU2D_B = new GPU2D( 1, this );
	}

	public void Reset()
	{
		foreach ( byte[] bank in VRAM )
			Array.Clear( bank );
		Array.Clear( Palette );
		Array.Clear( OAM );

		Array.Clear( VRAMCNT );
		VRAMSTAT = 0;

		VRAMMap_LCDC = 0;
		Array.Clear( VRAMMap_ABG );
		Array.Clear( VRAMMap_AOBJ );
		Array.Clear( VRAMMap_BBG );
		Array.Clear( VRAMMap_BOBJ );
		Array.Clear( VRAMMap_ABGExtPal );
		VRAMMap_AOBJExtPal = 0;
		Array.Clear( VRAMMap_BBGExtPal );
		VRAMMap_BOBJExtPal = 0;
		Array.Clear( VRAMMap_Texture );
		Array.Clear( VRAMMap_TexPal );
		Array.Clear( VRAMMap_ARM7 );

		ResetCapture();

		MasterBrightnessA = 0;
		MasterBrightnessB = 0;

		GPU2D_A.Reset();
		GPU2D_B.Reset();

		ResetLcd();
	}

	public void SetPowerCnt( uint val )
	{
		GPU2D_A.SetEnabled( (val & (1 << 1)) != 0 );
		GPU2D_B.SetEnabled( (val & (1 << 9)) != 0 );
		NDS.GPU3D.SetEnabled( (val & (1 << 3)) != 0, (val & (1 << 2)) != 0 );
		ScreenSwap = (val & (1 << 15)) != 0;
	}

	private static void MapRange( uint[] map, int based, int n, uint bankmask )
	{
		for ( int i = 0; i < n; i++ )
			map[based + i] |= bankmask;
	}

	private static void UnmapRange( uint[] map, int based, int n, uint bankmask )
	{
		for ( int i = 0; i < n; i++ )
			map[based + i] &= ~bankmask;
	}

	public void MapVRAM_AB( uint bank, byte cnt )
	{
		cnt &= 0x9B;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		int oldofs = (oldcnt >> 3) & 0x3;
		int ofs = (cnt >> 3) & 0x3;
		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x3 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1: UnmapRange( VRAMMap_ABG, oldofs << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, oldofs << 3, 8 ); break;
				case 2: UnmapRange( VRAMMap_AOBJ, (oldofs & 0x1) << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, (oldofs & 0x1) << 3, 8 ); break;
				case 3: VRAMMap_Texture[oldofs] &= ~bankmask; break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x3 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1: MapRange( VRAMMap_ABG, ofs << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, ofs << 3, 8 ); break;
				case 2: MapRange( VRAMMap_AOBJ, (ofs & 0x1) << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, (ofs & 0x1) << 3, 8 ); break;
				case 3: VRAMMap_Texture[ofs] |= bankmask; break;
			}
		}
	}

	public void MapVRAM_CD( uint bank, byte cnt )
	{
		cnt &= 0x9F;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		VRAMSTAT &= (byte)~(1 << (int)(bank - 2));

		int oldofs = (oldcnt >> 3) & 0x7;
		int ofs = (cnt >> 3) & 0x7;
		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x7 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1: UnmapRange( VRAMMap_ABG, oldofs << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, oldofs << 3, 8 ); break;
				case 2: VRAMMap_ARM7[oldofs & 0x1] &= ~bankmask; break;
				case 3: VRAMMap_Texture[oldofs] &= ~bankmask; break;
				case 4:
					if ( bank == 2 ) { UnmapRange( VRAMMap_BBG, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BBG, VRAMMap_BBG, 0, 8 ); }
					else { UnmapRange( VRAMMap_BOBJ, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BOBJ, VRAMMap_BOBJ, 0, 8 ); }
					break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x7 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1: MapRange( VRAMMap_ABG, ofs << 3, 8, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, ofs << 3, 8 ); break;
				case 2:
					VRAMMap_ARM7[ofs & 0x1] |= bankmask;
					VRAMSTAT |= (byte)(1 << (int)(bank - 2));
					break;
				case 3: VRAMMap_Texture[ofs] |= bankmask; break;
				case 4:
					if ( bank == 2 ) { MapRange( VRAMMap_BBG, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BBG, VRAMMap_BBG, 0, 8 ); }
					else { MapRange( VRAMMap_BOBJ, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BOBJ, VRAMMap_BOBJ, 0, 8 ); }
					break;
			}
		}
	}

	public void MapVRAM_E( uint bank, byte cnt )
	{
		cnt &= 0x87;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x7 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1: UnmapRange( VRAMMap_ABG, 0, 4, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, 0, 8 ); break;
				case 2: UnmapRange( VRAMMap_AOBJ, 0, 4, bankmask ); SetRangeCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, 0, 8 ); break;
				case 3: UnmapRange( VRAMMap_TexPal, 0, 4, bankmask ); break;
				case 4: UnmapRange( VRAMMap_ABGExtPal, 0, 4, bankmask ); break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x7 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1: MapRange( VRAMMap_ABG, 0, 4, bankmask ); SetRangeCBF( VRAMCBF_ABG, VRAMMap_ABG, 0, 8 ); break;
				case 2: MapRange( VRAMMap_AOBJ, 0, 4, bankmask ); SetRangeCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, 0, 8 ); break;
				case 3: MapRange( VRAMMap_TexPal, 0, 4, bankmask ); break;
				case 4: MapRange( VRAMMap_ABGExtPal, 0, 4, bankmask ); break;
			}
		}
	}

	public void MapVRAM_FG( uint bank, byte cnt )
	{
		cnt &= 0x9F;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		int oldofs = (oldcnt >> 3) & 0x7;
		int ofs = (cnt >> 3) & 0x7;
		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x7 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1:
					{
						int based = (oldofs & 0x1) + ((oldofs & 0x2) << 1);
						VRAMMap_ABG[based] &= ~bankmask;
						VRAMMap_ABG[based + 2] &= ~bankmask; SetCBF( VRAMCBF_ABG, VRAMMap_ABG, based ); SetCBF( VRAMCBF_ABG, VRAMMap_ABG, based + 2 );
						break;
					}
				case 2:
					{
						int based = (oldofs & 0x1) + ((oldofs & 0x2) << 1);
						VRAMMap_AOBJ[based] &= ~bankmask;
						VRAMMap_AOBJ[based + 2] &= ~bankmask; SetCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, based ); SetCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, based + 2 );
						break;
					}
				case 3: VRAMMap_TexPal[(oldofs & 0x1) + ((oldofs & 0x2) << 1)] &= ~bankmask; break;
				case 4:
					VRAMMap_ABGExtPal[(oldofs & 0x1) << 1] &= ~bankmask;
					VRAMMap_ABGExtPal[((oldofs & 0x1) << 1) + 1] &= ~bankmask;
					break;
				case 5: VRAMMap_AOBJExtPal &= ~bankmask; break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x7 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1:
					{
						int based = (ofs & 0x1) + ((ofs & 0x2) << 1);
						VRAMMap_ABG[based] |= bankmask;
						VRAMMap_ABG[based + 2] |= bankmask; SetCBF( VRAMCBF_ABG, VRAMMap_ABG, based ); SetCBF( VRAMCBF_ABG, VRAMMap_ABG, based + 2 );
						break;
					}
				case 2:
					{
						int based = (ofs & 0x1) + ((ofs & 0x2) << 1);
						VRAMMap_AOBJ[based] |= bankmask;
						VRAMMap_AOBJ[based + 2] |= bankmask; SetCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, based ); SetCBF( VRAMCBF_AOBJ, VRAMMap_AOBJ, based + 2 );
						break;
					}
				case 3: VRAMMap_TexPal[(ofs & 0x1) + ((ofs & 0x2) << 1)] |= bankmask; break;
				case 4:
					VRAMMap_ABGExtPal[(ofs & 0x1) << 1] |= bankmask;
					VRAMMap_ABGExtPal[((ofs & 0x1) << 1) + 1] |= bankmask;
					break;
				case 5: VRAMMap_AOBJExtPal |= bankmask; break;
			}
		}
	}

	public void MapVRAM_H( uint bank, byte cnt )
	{
		cnt &= 0x83;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x3 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1:
					foreach ( int i in new[] { 0, 1, 4, 5 } )
					{ VRAMMap_BBG[i] &= ~bankmask; SetCBF( VRAMCBF_BBG, VRAMMap_BBG, i ); }
					break;
				case 2: UnmapRange( VRAMMap_BBGExtPal, 0, 4, bankmask ); break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x3 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1:
					foreach ( int i in new[] { 0, 1, 4, 5 } )
					{ VRAMMap_BBG[i] |= bankmask; SetCBF( VRAMCBF_BBG, VRAMMap_BBG, i ); }
					break;
				case 2: MapRange( VRAMMap_BBGExtPal, 0, 4, bankmask ); break;
			}
		}
	}

	public void MapVRAM_I( uint bank, byte cnt )
	{
		cnt &= 0x83;

		byte oldcnt = VRAMCNT[bank];
		VRAMCNT[bank] = cnt;

		if ( oldcnt == cnt ) return;

		uint bankmask = 1u << (int)bank;

		if ( (oldcnt & (1 << 7)) != 0 )
		{
			switch ( oldcnt & 0x3 )
			{
				case 0: VRAMMap_LCDC &= ~bankmask; break;
				case 1:
					foreach ( int i in new[] { 2, 3, 6, 7 } )
					{ VRAMMap_BBG[i] &= ~bankmask; SetCBF( VRAMCBF_BBG, VRAMMap_BBG, i ); }
					break;
				case 2: UnmapRange( VRAMMap_BOBJ, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BOBJ, VRAMMap_BOBJ, 0, 8 ); break;
				case 3: VRAMMap_BOBJExtPal &= ~bankmask; break;
			}
		}

		if ( (cnt & (1 << 7)) != 0 )
		{
			switch ( cnt & 0x3 )
			{
				case 0: VRAMMap_LCDC |= bankmask; break;
				case 1:
					foreach ( int i in new[] { 2, 3, 6, 7 } )
					{ VRAMMap_BBG[i] |= bankmask; SetCBF( VRAMCBF_BBG, VRAMMap_BBG, i ); }
					break;
				case 2: MapRange( VRAMMap_BOBJ, 0, 8, bankmask ); SetRangeCBF( VRAMCBF_BOBJ, VRAMMap_BOBJ, 0, 8 ); break;
				case 3: VRAMMap_BOBJExtPal |= bankmask; break;
			}
		}
	}
}
