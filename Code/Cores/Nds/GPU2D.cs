namespace Emulotl.Nds;

public sealed partial class GPU2D
{
	public readonly uint Num;
	private readonly GPU GPU;

	public bool Enabled;

	public uint DispCnt;
	public uint[] DispCntLatch = new uint[3];
	public byte LayerEnable;
	public byte OBJEnable;
	public byte ForcedBlank;
	public ushort[] BGCnt = new ushort[4];

	public ushort[] BGXPos = new ushort[4];
	public ushort[] BGYPos = new ushort[4];

	public int[] BGXRef = new int[2];
	public int[] BGYRef = new int[2];
	public int[] BGXRefInternal = new int[2];
	public int[] BGYRefInternal = new int[2];
	public int[] BGXRefReload = new int[2];
	public int[] BGYRefReload = new int[2];
	public short[] BGRotA = new short[2];
	public short[] BGRotB = new short[2];
	public short[] BGRotC = new short[2];
	public short[] BGRotD = new short[2];

	public byte[] Win0Coords = new byte[4];
	public byte[] Win1Coords = new byte[4];
	public byte[] WinCnt = new byte[4];
	public byte Win0Active;
	public byte Win1Active;

	public byte[] BGMosaicSize = new byte[2];
	public byte[] OBJMosaicSize = new byte[2];
	public byte BGMosaicY, BGMosaicYMax;
	public byte OBJMosaicY;
	public bool BGMosaicLatch;
	public bool OBJMosaicLatch;
	public uint BGMosaicLine;
	public uint OBJMosaicLine;

	public ushort BlendCnt;
	public ushort BlendAlpha;
	public byte EVA, EVB;
	public byte EVY;

	public GPU2D( uint num, GPU gpu )
	{
		Num = num;
		GPU = gpu;
	}

	public void Reset()
	{
		Enabled = false;

		DispCnt = 0;
		Array.Clear( DispCntLatch );
		LayerEnable = 0;
		OBJEnable = 0;
		ForcedBlank = 0;
		Array.Clear( BGCnt );
		Array.Clear( BGXPos );
		Array.Clear( BGYPos );
		Array.Clear( BGXRef );
		Array.Clear( BGYRef );
		Array.Clear( BGXRefInternal );
		Array.Clear( BGYRefInternal );
		Array.Clear( BGXRefReload );
		Array.Clear( BGYRefReload );
		Array.Clear( BGRotA );
		Array.Clear( BGRotB );
		Array.Clear( BGRotC );
		Array.Clear( BGRotD );

		Array.Clear( Win0Coords );
		Array.Clear( Win1Coords );
		Array.Clear( WinCnt );
		Win0Active = 0;
		Win1Active = 0;

		Array.Clear( BGMosaicSize );
		Array.Clear( OBJMosaicSize );
		BGMosaicY = 0;
		BGMosaicYMax = 0;
		OBJMosaicY = 0;
		BGMosaicLatch = true;
		OBJMosaicLatch = true;
		BGMosaicLine = 0;
		OBJMosaicLine = 0;

		BlendCnt = 0;
		BlendAlpha = 0;
		EVA = 16;
		EVB = 0;
		EVY = 0;

		BindVramFlats();
	}

	public void SetEnabled( bool enabled ) => Enabled = enabled;

	public byte Read8( uint addr )
	{
		switch ( addr & 0xFFF )
		{
			case 0x000: return (byte)DispCnt;
			case 0x001: return (byte)(DispCnt >> 8);
			case 0x002: return (byte)(DispCnt >> 16);
			case 0x003: return (byte)(DispCnt >> 24);

			case 0x008: return (byte)BGCnt[0];
			case 0x009: return (byte)(BGCnt[0] >> 8);
			case 0x00A: return (byte)BGCnt[1];
			case 0x00B: return (byte)(BGCnt[1] >> 8);
			case 0x00C: return (byte)BGCnt[2];
			case 0x00D: return (byte)(BGCnt[2] >> 8);
			case 0x00E: return (byte)BGCnt[3];
			case 0x00F: return (byte)(BGCnt[3] >> 8);

			case 0x048: return WinCnt[0];
			case 0x049: return WinCnt[1];
			case 0x04A: return WinCnt[2];
			case 0x04B: return WinCnt[3];

			case 0x050: return (byte)BlendCnt;
			case 0x051: return (byte)(BlendCnt >> 8);
			case 0x052: return (byte)BlendAlpha;
			case 0x053: return (byte)(BlendAlpha >> 8);
		}
		return 0;
	}

	public ushort Read16( uint addr )
	{
		switch ( addr & 0xFFF )
		{
			case 0x000: return (ushort)DispCnt;
			case 0x002: return (ushort)(DispCnt >> 16);

			case 0x008: return BGCnt[0];
			case 0x00A: return BGCnt[1];
			case 0x00C: return BGCnt[2];
			case 0x00E: return BGCnt[3];

			case 0x048: return (ushort)(WinCnt[0] | (WinCnt[1] << 8));
			case 0x04A: return (ushort)(WinCnt[2] | (WinCnt[3] << 8));

			case 0x050: return BlendCnt;
			case 0x052: return BlendAlpha;
		}
		return 0;
	}

	public uint Read32( uint addr )
	{
		if ( (addr & 0xFFF) == 0x000 )
			return DispCnt;
		return (uint)(Read16( addr ) | (Read16( addr + 2 ) << 16));
	}

	public void Write8( uint addr, byte val )
	{
		switch ( addr & 0xFFF )
		{
			case 0x000: DispCnt = (DispCnt & 0xFFFFFF00) | val; if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
			case 0x001: DispCnt = (DispCnt & 0xFFFF00FF) | ((uint)val << 8); if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
			case 0x002: DispCnt = (DispCnt & 0xFF00FFFF) | ((uint)val << 16); if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
			case 0x003: DispCnt = (DispCnt & 0x00FFFFFF) | ((uint)val << 24); if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
		}

		if ( !Enabled ) return;

		switch ( addr & 0xFFF )
		{
			case 0x008: BGCnt[0] = (ushort)((BGCnt[0] & 0xFF00) | val); return;
			case 0x009: BGCnt[0] = (ushort)((BGCnt[0] & 0x00FF) | (val << 8)); return;
			case 0x00A: BGCnt[1] = (ushort)((BGCnt[1] & 0xFF00) | val); return;
			case 0x00B: BGCnt[1] = (ushort)((BGCnt[1] & 0x00FF) | (val << 8)); return;
			case 0x00C: BGCnt[2] = (ushort)((BGCnt[2] & 0xFF00) | val); return;
			case 0x00D: BGCnt[2] = (ushort)((BGCnt[2] & 0x00FF) | (val << 8)); return;
			case 0x00E: BGCnt[3] = (ushort)((BGCnt[3] & 0xFF00) | val); return;
			case 0x00F: BGCnt[3] = (ushort)((BGCnt[3] & 0x00FF) | (val << 8)); return;

			case 0x010: BGXPos[0] = (ushort)((BGXPos[0] & 0xFF00) | val); return;
			case 0x011: BGXPos[0] = (ushort)((BGXPos[0] & 0x00FF) | (val << 8)); return;
			case 0x012: BGYPos[0] = (ushort)((BGYPos[0] & 0xFF00) | val); return;
			case 0x013: BGYPos[0] = (ushort)((BGYPos[0] & 0x00FF) | (val << 8)); return;
			case 0x014: BGXPos[1] = (ushort)((BGXPos[1] & 0xFF00) | val); return;
			case 0x015: BGXPos[1] = (ushort)((BGXPos[1] & 0x00FF) | (val << 8)); return;
			case 0x016: BGYPos[1] = (ushort)((BGYPos[1] & 0xFF00) | val); return;
			case 0x017: BGYPos[1] = (ushort)((BGYPos[1] & 0x00FF) | (val << 8)); return;
			case 0x018: BGXPos[2] = (ushort)((BGXPos[2] & 0xFF00) | val); return;
			case 0x019: BGXPos[2] = (ushort)((BGXPos[2] & 0x00FF) | (val << 8)); return;
			case 0x01A: BGYPos[2] = (ushort)((BGYPos[2] & 0xFF00) | val); return;
			case 0x01B: BGYPos[2] = (ushort)((BGYPos[2] & 0x00FF) | (val << 8)); return;
			case 0x01C: BGXPos[3] = (ushort)((BGXPos[3] & 0xFF00) | val); return;
			case 0x01D: BGXPos[3] = (ushort)((BGXPos[3] & 0x00FF) | (val << 8)); return;
			case 0x01E: BGYPos[3] = (ushort)((BGYPos[3] & 0xFF00) | val); return;
			case 0x01F: BGYPos[3] = (ushort)((BGYPos[3] & 0x00FF) | (val << 8)); return;

			case 0x040: Win0Coords[1] = val; return;
			case 0x041: Win0Coords[0] = val; return;
			case 0x042: Win1Coords[1] = val; return;
			case 0x043: Win1Coords[0] = val; return;
			case 0x044: Win0Coords[3] = val; return;
			case 0x045: Win0Coords[2] = val; return;
			case 0x046: Win1Coords[3] = val; return;
			case 0x047: Win1Coords[2] = val; return;

			case 0x048: WinCnt[0] = val; return;
			case 0x049: WinCnt[1] = val; return;
			case 0x04A: WinCnt[2] = val; return;
			case 0x04B: WinCnt[3] = val; return;

			case 0x04C: BGMosaicSize[0] = (byte)(val & 0xF); BGMosaicSize[1] = (byte)(val >> 4); return;
			case 0x04D: OBJMosaicSize[0] = (byte)(val & 0xF); OBJMosaicSize[1] = (byte)(val >> 4); return;

			case 0x050: BlendCnt = (ushort)((BlendCnt & 0x3F00) | val); return;
			case 0x051: BlendCnt = (ushort)((BlendCnt & 0x00FF) | (val << 8)); return;
			case 0x052: BlendAlpha = (ushort)((BlendAlpha & 0x1F00) | (val & 0x1F)); EVA = (byte)(val & 0x1F); if ( EVA > 16 ) EVA = 16; return;
			case 0x053: BlendAlpha = (ushort)((BlendAlpha & 0x001F) | ((val & 0x1F) << 8)); EVB = (byte)(val & 0x1F); if ( EVB > 16 ) EVB = 16; return;
			case 0x054: EVY = (byte)(val & 0x1F); if ( EVY > 16 ) EVY = 16; return;
		}
	}

	public void Write16( uint addr, ushort val )
	{
		switch ( addr & 0xFFF )
		{
			case 0x000: DispCnt = (DispCnt & 0xFFFF0000) | val; if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
			case 0x002: DispCnt = (DispCnt & 0x0000FFFF) | ((uint)val << 16); if ( Num != 0 ) DispCnt &= 0xC0B1FFF7; return;
		}

		if ( !Enabled ) return;

		switch ( addr & 0xFFF )
		{
			case 0x008: BGCnt[0] = val; return;
			case 0x00A: BGCnt[1] = val; return;
			case 0x00C: BGCnt[2] = val; return;
			case 0x00E: BGCnt[3] = val; return;

			case 0x010: BGXPos[0] = val; return;
			case 0x012: BGYPos[0] = val; return;
			case 0x014: BGXPos[1] = val; return;
			case 0x016: BGYPos[1] = val; return;
			case 0x018: BGXPos[2] = val; return;
			case 0x01A: BGYPos[2] = val; return;
			case 0x01C: BGXPos[3] = val; return;
			case 0x01E: BGYPos[3] = val; return;

			case 0x020: BGRotA[0] = (short)val; return;
			case 0x022: BGRotB[0] = (short)val; return;
			case 0x024: BGRotC[0] = (short)val; return;
			case 0x026: BGRotD[0] = (short)val; return;
			case 0x028: BGXRef[0] = (int)((BGXRef[0] & 0xFFFF0000) | val); BGXRefReload[0] = BGXRef[0]; return;
			case 0x02A: BGXRef[0] = (BGXRef[0] & 0xFFFF) | (SignExtend12( val ) << 16); BGXRefReload[0] = BGXRef[0]; return;
			case 0x02C: BGYRef[0] = (int)((BGYRef[0] & 0xFFFF0000) | val); BGYRefReload[0] = BGYRef[0]; return;
			case 0x02E: BGYRef[0] = (BGYRef[0] & 0xFFFF) | (SignExtend12( val ) << 16); BGYRefReload[0] = BGYRef[0]; return;

			case 0x030: BGRotA[1] = (short)val; return;
			case 0x032: BGRotB[1] = (short)val; return;
			case 0x034: BGRotC[1] = (short)val; return;
			case 0x036: BGRotD[1] = (short)val; return;
			case 0x038: BGXRef[1] = (int)((BGXRef[1] & 0xFFFF0000) | val); BGXRefReload[1] = BGXRef[1]; return;
			case 0x03A: BGXRef[1] = (BGXRef[1] & 0xFFFF) | (SignExtend12( val ) << 16); BGXRefReload[1] = BGXRef[1]; return;
			case 0x03C: BGYRef[1] = (int)((BGYRef[1] & 0xFFFF0000) | val); BGYRefReload[1] = BGYRef[1]; return;
			case 0x03E: BGYRef[1] = (BGYRef[1] & 0xFFFF) | (SignExtend12( val ) << 16); BGYRefReload[1] = BGYRef[1]; return;

			case 0x040: Win0Coords[1] = (byte)val; Win0Coords[0] = (byte)(val >> 8); return;
			case 0x042: Win1Coords[1] = (byte)val; Win1Coords[0] = (byte)(val >> 8); return;
			case 0x044: Win0Coords[3] = (byte)val; Win0Coords[2] = (byte)(val >> 8); return;
			case 0x046: Win1Coords[3] = (byte)val; Win1Coords[2] = (byte)(val >> 8); return;

			case 0x048: WinCnt[0] = (byte)val; WinCnt[1] = (byte)(val >> 8); return;
			case 0x04A: WinCnt[2] = (byte)val; WinCnt[3] = (byte)(val >> 8); return;

			case 0x04C:
				BGMosaicSize[0] = (byte)(val & 0xF);
				BGMosaicSize[1] = (byte)((val >> 4) & 0xF);
				OBJMosaicSize[0] = (byte)((val >> 8) & 0xF);
				OBJMosaicSize[1] = (byte)(val >> 12);
				return;

			case 0x050: BlendCnt = (ushort)(val & 0x3FFF); return;
			case 0x052:
				BlendAlpha = (ushort)(val & 0x1F1F);
				EVA = (byte)(val & 0x1F); if ( EVA > 16 ) EVA = 16;
				EVB = (byte)((val >> 8) & 0x1F); if ( EVB > 16 ) EVB = 16;
				return;
			case 0x054: EVY = (byte)(val & 0x1F); if ( EVY > 16 ) EVY = 16; return;
		}
	}

	public void Write32( uint addr, uint val )
	{
		if ( (addr & 0xFFF) == 0x000 )
		{
			DispCnt = val;
			if ( Num != 0 ) DispCnt &= 0xC0B1FFF7;
			return;
		}

		if ( !Enabled )
		{
			Write16( addr, (ushort)val );
			Write16( addr + 2, (ushort)(val >> 16) );
			return;
		}

		switch ( addr & 0xFFF )
		{
			case 0x028: BGXRef[0] = SignExtend28( val ); BGXRefReload[0] = BGXRef[0]; return;
			case 0x02C: BGYRef[0] = SignExtend28( val ); BGYRefReload[0] = BGYRef[0]; return;
			case 0x038: BGXRef[1] = SignExtend28( val ); BGXRefReload[1] = BGXRef[1]; return;
			case 0x03C: BGYRef[1] = SignExtend28( val ); BGYRefReload[1] = BGYRef[1]; return;
		}

		Write16( addr, (ushort)val );
		Write16( addr + 2, (ushort)(val >> 16) );
	}

	private static int SignExtend12( ushort val )
	{
		uint v = val;
		if ( (v & 0x0800) != 0 ) v |= 0xF000;
		return (int)v;
	}

	private static int SignExtend28( uint val )
	{
		if ( (val & 0x08000000) != 0 ) val |= 0xF0000000;
		return (int)val;
	}

	public void UpdateRegistersPreDraw( bool reset )
	{
		if ( !Enabled ) return;

		DispCntLatch[2] = DispCntLatch[1];
		DispCntLatch[1] = DispCntLatch[0];
		DispCntLatch[0] = DispCnt;
		LayerEnable = (byte)(((DispCntLatch[2] & DispCnt) >> 8) & 0x1F);
		OBJEnable = (byte)(((DispCntLatch[1] & DispCnt) >> 12) & 0x1);
		ForcedBlank = (byte)(((DispCntLatch[2] | DispCnt) >> 7) & 0x1);

		if ( BGMosaicLatch )
			BGMosaicLine = GPU.VCount;

		for ( int i = 0; i < 2; i++ )
		{
			if ( (BGCnt[2 + i] & (1 << 6)) == 0 || BGMosaicLatch )
			{
				BGXRefInternal[i] = BGXRef[i];
				BGYRefInternal[i] = BGYRef[i];
			}
		}

		if ( (DispCnt & (1 << 12)) != 0 )
		{
			if ( reset || OBJMosaicY == OBJMosaicSize[1] )
			{
				OBJMosaicY = 0;
				OBJMosaicLatch = true;
			}
			else
			{
				OBJMosaicY++;
				OBJMosaicY &= 0xF;
				OBJMosaicLatch = false;
			}
		}

		if ( OBJMosaicLatch )
			OBJMosaicLine = reset ? 0 : (uint)(GPU.VCount + 1);
	}

	public void UpdateRegistersPostDraw( bool reset )
	{
		if ( !Enabled ) return;

		if ( reset )
		{
			BGMosaicYMax = BGMosaicSize[1];
			BGMosaicY = 0;
			BGMosaicLatch = true;
		}
		else if ( BGMosaicY == BGMosaicYMax )
		{
			BGMosaicYMax = BGMosaicSize[1];
			BGMosaicY = 0;
			BGMosaicLatch = true;
		}
		else
		{
			BGMosaicY++;
			BGMosaicY &= 0xF;
			BGMosaicLatch = false;
		}

		for ( int i = 0; i < 2; i++ )
		{
			if ( (LayerEnable & (4 << i)) == 0 )
				continue;

			if ( reset )
			{
				BGXRef[i] = BGXRefReload[i];
				BGYRef[i] = BGYRefReload[i];
			}
			else
			{
				BGXRef[i] += BGRotB[i];
				BGYRef[i] += BGRotD[i];
			}
		}
	}

	public void UpdateWindows( uint line )
	{
		if ( !Enabled ) return;

		line &= 0xFF;
		if ( line == Win0Coords[3] ) Win0Active &= 0xFE;
		else if ( line == Win0Coords[2] ) Win0Active |= 0x1;
		if ( line == Win1Coords[3] ) Win1Active &= 0xFE;
		else if ( line == Win1Coords[2] ) Win1Active |= 0x1;
	}

	public void CalculateWindowMask( byte[] windowMask, byte[] objWindow )
	{
		for ( int i = 0; i < 256; i++ )
			windowMask[i] = WinCnt[2];

		if ( (DispCnt & (1 << 15)) != 0 )
		{
			for ( int i = 0; i < 256; i++ )
				if ( objWindow[i] != 0 )
					windowMask[i] = WinCnt[3];
		}

		if ( (DispCnt & (1 << 14)) != 0 )
		{
			byte x1 = Win1Coords[0];
			byte x2 = Win1Coords[1];
			for ( int i = 0; i < 256; i++ )
			{
				if ( i == x2 ) Win1Active &= 0xFD;
				else if ( i == x1 ) Win1Active |= 0x2;
				if ( Win1Active == 0x3 ) windowMask[i] = WinCnt[1];
			}
		}

		if ( (DispCnt & (1 << 13)) != 0 )
		{
			byte x1 = Win0Coords[0];
			byte x2 = Win0Coords[1];
			for ( int i = 0; i < 256; i++ )
			{
				if ( i == x2 ) Win0Active &= 0xFD;
				else if ( i == x1 ) Win0Active |= 0x2;
				if ( Win0Active == 0x3 ) windowMask[i] = WinCnt[0];
			}
		}
	}
}
