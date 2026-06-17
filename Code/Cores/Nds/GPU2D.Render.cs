namespace Emulotl.Nds;

public sealed partial class GPU2D
{
	private const uint OBJ_StandardPal = 1 << 12;
	private const uint OBJ_DirectColor = 1 << 15;
	private const uint OBJ_BGPrioMask = 0x3 << 16;
	private const uint OBJ_IsOpaque = 1 << 18;
	private const uint OBJ_OpaPrioMask = OBJ_BGPrioMask | OBJ_IsOpaque;
	private const uint OBJ_IsSprite = 1 << 19;
	private const uint OBJ_Mosaic = 1 << 20;

	public uint[] Output2D = new uint[256];

	private readonly uint[] BGOBJLine = new uint[256 * 2];
	private readonly byte[] WindowMask = new byte[256];
	private readonly uint[] OBJLine = new uint[256];
	private readonly byte[] OBJWindow = new byte[256];

	private uint NumSprites;

	private static readonly byte[][] MosaicTable = BuildMosaicTable();
	private byte[] CurBGXMosaicTable;

	private static byte[][] BuildMosaicTable()
	{
		byte[][] table = new byte[16][];
		for ( int m = 0; m < 16; m++ )
		{
			table[m] = new byte[256];
			for ( int x = 0; x < 256; x++ )
				table[m][x] = (byte)(x % (m + 1));
		}
		return table;
	}

	private byte[] _bgFlat;
	private uint _bgFlatMask;
	private byte[] _objFlat;
	private uint _objFlatMask;
	private byte[] _bgExtPalFlat;
	private byte[] _objExtPalFlat;
	private byte[] _palette;
	private byte[] _oam;

	public void BindVramFlats()
	{
		_bgFlat = Num == 0 ? GPU.VRAMFlat_ABG : GPU.VRAMFlat_BBG;
		_bgFlatMask = Num == 0 ? 0x7FFFFu : 0x1FFFFu;
		_objFlat = Num == 0 ? GPU.VRAMFlat_AOBJ : GPU.VRAMFlat_BOBJ;
		_objFlatMask = Num == 0 ? 0x3FFFFu : 0x1FFFFu;
		_bgExtPalFlat = Num == 0 ? GPU.VRAMFlat_ABGExtPal : GPU.VRAMFlat_BBGExtPal;
		_objExtPalFlat = Num == 0 ? GPU.VRAMFlat_AOBJExtPal : GPU.VRAMFlat_BOBJExtPal;
		_palette = GPU.Palette;
		_oam = GPU.OAM;
	}

	private byte BgRead8( uint addr )
	{
		return _bgFlat[addr & _bgFlatMask];
	}

	private ushort BgRead16( uint addr )
	{
		uint a = addr & _bgFlatMask;
		return (ushort)(_bgFlat[a] | (_bgFlat[a + 1] << 8));
	}

	private byte ObjRead8( uint addr )
	{
		return _objFlat[addr & _objFlatMask];
	}

	private ushort ObjRead16( uint addr )
	{
		uint a = addr & _objFlatMask;
		return (ushort)(_objFlat[a] | (_objFlat[a + 1] << 8));
	}

	private uint BgPalByte => Num == 0 ? 0u : 0x400u;
	private uint ObjPalByte => Num == 0 ? 0x200u : 0x600u;

	private ushort PalRead( uint byteoff )
	{
		uint a = byteoff & 0x7FF;
		return (ushort)(_palette[a] | (_palette[a + 1] << 8));
	}

	private ushort BgExtPalRead( uint slot, uint palnum, uint color )
	{
		uint a = (slot * 0x2000 + palnum * 0x200 + color * 2) & 0x7FFF;
		return (ushort)(_bgExtPalFlat[a] | (_bgExtPalFlat[a + 1] << 8));
	}

	private ushort ObjExtPalRead( uint idx )
	{
		uint a = (idx * 2) & 0x1FFF;
		return (ushort)(_objExtPalFlat[a] | (_objExtPalFlat[a + 1] << 8));
	}

	private void DrawPixel( int i, ushort color, uint flag )
	{
		BGOBJLine[256 + i] = BGOBJLine[i];
		uint r = (uint)((color & 0x001F) << 1);
		uint g = (uint)(((color & 0x03E0) >> 4) | ((color & 0x8000) >> 15));
		uint b = (uint)((color & 0x7C00) >> 9);
		BGOBJLine[i] = r | (g << 8) | (b << 16) | flag;
	}

	public void DrawScanline( uint line )
	{
		uint[] dst = Output2D;

		if ( !Enabled )
		{
			uint fillcolor = Num == 0 ? 0xFF000000 : 0xFF3F3F3F;
			for ( int i = 0; i < 256; i++ )
				dst[i] = fillcolor;
			return;
		}

		if ( ForcedBlank != 0 )
		{
			for ( int i = 0; i < 256; i++ )
				dst[i] = 0xFF3F3F3F;
			return;
		}

		DrawScanline_BGOBJ( line );

		for ( int i = 0; i < 256; i++ )
			dst[i] = ColorComposite( i, BGOBJLine[i], BGOBJLine[256 + i] );
	}

	private void DrawScanline_BGOBJ( uint line )
	{
		ushort bdcolor = Num != 0 ? PalRead( 0x400 ) : PalRead( 0 );
		uint r = (uint)((bdcolor & 0x001F) << 1);
		uint g = (uint)(((bdcolor & 0x03E0) >> 4) | ((bdcolor & 0x8000) >> 15));
		uint b = (uint)((bdcolor & 0x7C00) >> 9);
		uint backdrop = r | (g << 8) | (b << 16) | 0x20000000;

		for ( int i = 0; i < 256; i++ )
		{
			BGOBJLine[i] = backdrop;
			BGOBJLine[256 + i] = 0;
		}

		if ( (DispCnt & 0xE000) != 0 )
			CalculateWindowMask( WindowMask, OBJWindow );
		else
			for ( int i = 0; i < 256; i++ )
				WindowMask[i] = 0xFF;

		ApplySpriteMosaicX();
		CurBGXMosaicTable = MosaicTable[BGMosaicSize[0]];

		switch ( DispCnt & 0x7 )
		{
			case 0: DrawScanlineBGMode( line, 0 ); break;
			case 1: DrawScanlineBGMode( line, 1 ); break;
			case 2: DrawScanlineBGMode( line, 2 ); break;
			case 3: DrawScanlineBGMode( line, 3 ); break;
			case 4: DrawScanlineBGMode( line, 4 ); break;
			case 5: DrawScanlineBGMode( line, 5 ); break;
			case 6: DrawScanlineBGMode6( line ); break;
			case 7: DrawScanlineBGMode7( line ); break;
		}
	}

	private void DoDrawBGText( uint line, uint num )
	{
		bool mosaic = (BGCnt[num] & (1 << 6)) != 0 && BGMosaicSize[0] > 0;
		DrawBG_Text( line, num, mosaic );
	}

	private void DoDrawBGAffine( uint line, uint num )
	{
		bool mosaic = (BGCnt[num] & (1 << 6)) != 0 && BGMosaicSize[0] > 0;
		DrawBG_Affine( line, num, mosaic );
	}

	private void DoDrawBGExtended( uint line, uint num )
	{
		bool mosaic = (BGCnt[num] & (1 << 6)) != 0 && BGMosaicSize[0] > 0;
		DrawBG_Extended( line, num, mosaic );
	}

	private void DoDrawBGLarge( uint line )
	{
		bool mosaic = (BGCnt[2] & (1 << 6)) != 0 && BGMosaicSize[0] > 0;
		DrawBG_Large( line, mosaic );
	}

	private void DrawScanlineBGMode( uint line, uint bgmode )
	{
		for ( int i = 3; i >= 0; i-- )
		{
			if ( (BGCnt[3] & 0x3) == i && (LayerEnable & (1 << 3)) != 0 )
			{
				if ( bgmode >= 3 ) DoDrawBGExtended( line, 3 );
				else if ( bgmode >= 1 ) DoDrawBGAffine( line, 3 );
				else DoDrawBGText( line, 3 );
			}
			if ( (BGCnt[2] & 0x3) == i && (LayerEnable & (1 << 2)) != 0 )
			{
				if ( bgmode == 5 ) DoDrawBGExtended( line, 2 );
				else if ( bgmode == 4 || bgmode == 2 ) DoDrawBGAffine( line, 2 );
				else DoDrawBGText( line, 2 );
			}
			if ( (BGCnt[1] & 0x3) == i && (LayerEnable & (1 << 1)) != 0 )
				DoDrawBGText( line, 1 );
			if ( (BGCnt[0] & 0x3) == i && (LayerEnable & (1 << 0)) != 0 )
			{
				if ( Num == 0 && (DispCnt & 0x8) != 0 ) { /* 3D layer (not ported) */ }
				else DoDrawBGText( line, 0 );
			}
			if ( (LayerEnable & (1 << 4)) != 0 && NumSprites != 0 )
				InterleaveSprites( (uint)i );
		}
	}

	private void DrawScanlineBGMode6( uint line )
	{
		for ( int i = 3; i >= 0; i-- )
		{
			if ( (BGCnt[2] & 0x3) == i && (LayerEnable & (1 << 2)) != 0 )
				DoDrawBGLarge( line );
			if ( (BGCnt[0] & 0x3) == i && (LayerEnable & (1 << 0)) != 0 )
			{
				if ( Num == 0 && (DispCnt & 0x8) != 0 ) { /* 3D layer */ }
			}
			if ( (LayerEnable & (1 << 4)) != 0 && NumSprites != 0 )
				InterleaveSprites( (uint)i );
		}
	}

	private void DrawScanlineBGMode7( uint line )
	{
		for ( int i = 3; i >= 0; i-- )
		{
			if ( (BGCnt[1] & 0x3) == i && (LayerEnable & (1 << 1)) != 0 )
				DoDrawBGText( line, 1 );
			if ( (BGCnt[0] & 0x3) == i && (LayerEnable & (1 << 0)) != 0 )
			{
				if ( Num == 0 && (DispCnt & 0x8) != 0 ) { /* 3D layer */ }
				else DoDrawBGText( line, 0 );
			}
			if ( (LayerEnable & (1 << 4)) != 0 && NumSprites != 0 )
				InterleaveSprites( (uint)i );
		}
	}

	private uint ColorComposite( int i, uint val1, uint val2 )
	{
		uint coloreffect = 0;
		uint eva = 0, evb = 0;

		uint flag1 = val1 >> 24;
		uint flag2 = val2 >> 24;

		uint blendCnt = BlendCnt;

		uint target2;
		if ( (flag2 & 0x80) != 0 ) target2 = 0x1000;
		else if ( (flag2 & 0x40) != 0 ) target2 = 0x0100;
		else target2 = flag2 << 8;

		if ( (flag1 & 0x80) != 0 && (blendCnt & target2) != 0 )
		{
			coloreffect = 1;
			if ( (flag1 & 0x40) != 0 )
			{
				eva = flag1 & 0x1F;
				evb = 16 - eva;
			}
			else
			{
				eva = EVA;
				evb = EVB;
			}
		}
		else if ( (flag1 & 0x40) != 0 && (blendCnt & target2) != 0 )
		{
			coloreffect = 4;
		}
		else
		{
			if ( (flag1 & 0x80) != 0 ) flag1 = 0x10;
			else if ( (flag1 & 0x40) != 0 ) flag1 = 0x01;

			if ( (blendCnt & flag1) != 0 && (WindowMask[i] & 0x20) != 0 )
			{
				coloreffect = (blendCnt >> 6) & 0x3;
				if ( coloreffect == 1 )
				{
					if ( (blendCnt & target2) != 0 )
					{
						eva = EVA;
						evb = EVB;
					}
					else
						coloreffect = 0;
				}
			}
		}

		switch ( coloreffect )
		{
			case 0: return val1;
			case 1: return ColorOp.ColorBlend4( val1, val2, eva, evb );
			case 2: return ColorOp.ColorBrightnessUp( val1, EVY, 0x8 );
			case 3: return ColorOp.ColorBrightnessDown( val1, EVY, 0x7 );
			case 4: return ColorOp.ColorBlend5( val1, val2 );
		}
		return val1;
	}
}
