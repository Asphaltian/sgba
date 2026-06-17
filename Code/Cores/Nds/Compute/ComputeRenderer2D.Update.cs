namespace Emulotl.Nds;

public sealed partial class ComputeRenderer2D
{
	private static readonly byte[] SpriteWidthTab =
	[
		8, 16, 8, 8,
		16, 32, 8, 8,
		32, 32, 16, 8,
		64, 64, 32, 8
	];
	private static readonly byte[] SpriteHeightTab =
	[
		8, 8, 16, 8,
		16, 8, 32, 8,
		32, 16, 32, 8,
		64, 32, 64, 8
	];

	private readonly sBGConfig[] _layerConfig = new sBGConfig[4];
	private readonly sOAM[] _spriteOAM = new sOAM[128];
	private readonly Vec4i[] _rotscale = new Vec4i[32];
	private int _numSprites;
	private readonly sScanline[] _scanlineConfig = new sScanline[ScreenH];
	private sCompositorConfig _compConfig;
	private int _windowEnable;
	private int _masterBright;
	private int _unitEnabled;
	private int _forcedBlank;
	private bool _win0Wrap;
	private bool _win1Wrap;

	private int OamU16( int u16index )
	{
		int baseByte = (_num * 0x400) + (u16index * 2);
		return _gpu.OAM[baseByte] | (_gpu.OAM[baseByte + 1] << 8);
	}

	private int OamS16( int u16index )
	{
		int v = OamU16( u16index );
		return (v << 16) >> 16;
	}

	public void UpdateScanlineConfig( uint line )
	{
		ref sScanline cfg = ref _scanlineConfig[(int)line];

		uint dispcnt = _gpu2d.DispCnt;
		uint bgmode = dispcnt & 0x7;
		bool xmosaic = _gpu2d.BGMosaicSize[0] > 0;

		if ( (dispcnt & (1 << 3)) != 0 )
		{
			int xpos = _gpu3d.RenderXPos & 0x1FF;
			cfg.BGOffsetX0 = xpos - ((xpos & 0x100) << 1);
			cfg.BGOffsetY0 = (int)line;
			cfg.BGMosaicEnable0 = 0;
		}
		else
		{
			cfg.BGOffsetX0 = _gpu2d.BGXPos[0];
			if ( (_gpu2d.BGCnt[0] & (1 << 6)) != 0 )
			{
				cfg.BGOffsetY0 = (int)(_gpu2d.BGYPos[0] + _gpu2d.BGMosaicLine);
				cfg.BGMosaicEnable0 = xmosaic ? 1 : 0;
			}
			else
			{
				cfg.BGOffsetY0 = (int)(_gpu2d.BGYPos[0] + line);
				cfg.BGMosaicEnable0 = 0;
			}
		}

		cfg.BGOffsetX1 = _gpu2d.BGXPos[1];
		if ( (_gpu2d.BGCnt[1] & (1 << 6)) != 0 )
		{
			cfg.BGOffsetY1 = (int)(_gpu2d.BGYPos[1] + _gpu2d.BGMosaicLine);
			cfg.BGMosaicEnable1 = xmosaic ? 1 : 0;
		}
		else
		{
			cfg.BGOffsetY1 = (int)(_gpu2d.BGYPos[1] + line);
			cfg.BGMosaicEnable1 = 0;
		}

		if ( (bgmode == 2) || (bgmode >= 4 && bgmode <= 6) )
		{
			cfg.BGOffsetX2 = _gpu2d.BGXRefInternal[0];
			cfg.BGOffsetY2 = _gpu2d.BGYRefInternal[0];
			cfg.BGRotscale0_0 = _gpu2d.BGRotA[0];
			cfg.BGRotscale0_1 = _gpu2d.BGRotB[0];
			cfg.BGRotscale0_2 = _gpu2d.BGRotC[0];
			cfg.BGRotscale0_3 = _gpu2d.BGRotD[0];
		}
		else
		{
			cfg.BGOffsetX2 = _gpu2d.BGXPos[2];
			if ( (_gpu2d.BGCnt[2] & (1 << 6)) != 0 )
				cfg.BGOffsetY2 = (int)(_gpu2d.BGYPos[2] + _gpu2d.BGMosaicLine);
			else
				cfg.BGOffsetY2 = (int)(_gpu2d.BGYPos[2] + line);
		}
		cfg.BGMosaicEnable2 = ((_gpu2d.BGCnt[2] & (1 << 6)) != 0) ? (xmosaic ? 1 : 0) : 0;

		if ( bgmode >= 1 && bgmode <= 5 )
		{
			cfg.BGOffsetX3 = _gpu2d.BGXRefInternal[1];
			cfg.BGOffsetY3 = _gpu2d.BGYRefInternal[1];
			cfg.BGRotscale1_0 = _gpu2d.BGRotA[1];
			cfg.BGRotscale1_1 = _gpu2d.BGRotB[1];
			cfg.BGRotscale1_2 = _gpu2d.BGRotC[1];
			cfg.BGRotscale1_3 = _gpu2d.BGRotD[1];
		}
		else
		{
			cfg.BGOffsetX3 = _gpu2d.BGXPos[3];
			if ( (_gpu2d.BGCnt[3] & (1 << 6)) != 0 )
				cfg.BGOffsetY3 = (int)(_gpu2d.BGYPos[3] + _gpu2d.BGMosaicLine);
			else
				cfg.BGOffsetY3 = (int)(_gpu2d.BGYPos[3] + line);
		}
		cfg.BGMosaicEnable3 = ((_gpu2d.BGCnt[3] & (1 << 6)) != 0) ? (xmosaic ? 1 : 0) : 0;

		int palBase = _num * 0x400;
		cfg.BackColor = _gpu.Palette[palBase] | (_gpu.Palette[palBase + 1] << 8);

		cfg.MosaicSize0 = _gpu2d.BGMosaicSize[0];
		cfg.MosaicSize1 = _gpu2d.BGMosaicSize[1];
		cfg.MosaicSize2 = _gpu2d.OBJMosaicSize[0];
		cfg.MosaicSize3 = _gpu2d.OBJMosaicSize[1];

		uint winregs;
		if ( (dispcnt & 0xE000) != 0 )
			winregs = _gpu2d.WinCnt[2];
		else
			winregs = 0xFF;

		if ( (dispcnt & (1 << 15)) != 0 )
			winregs |= (uint)_gpu2d.WinCnt[3] << 8;
		else
			winregs |= 0xFF00;

		if ( (dispcnt & (1 << 14)) != 0 )
			winregs |= (uint)_gpu2d.WinCnt[1] << 16;
		else
			winregs |= 0xFF0000;

		if ( (dispcnt & (1 << 13)) != 0 )
			winregs |= (uint)_gpu2d.WinCnt[0] << 24;
		else
			winregs |= 0xFF000000;

		cfg.WinRegs = winregs;
		cfg.WinMask = 0;

		if ( ((dispcnt & (1 << 13)) != 0) && ((_gpu2d.Win0Active & 0x1) != 0) )
		{
			int x0 = _gpu2d.Win0Coords[0];
			int x1 = _gpu2d.Win0Coords[1];
			if ( x0 <= x1 )
			{
				cfg.WinPos0 = x0;
				cfg.WinPos1 = x1;
				if ( _win0Wrap ) cfg.WinMask |= (1 << 0);
				cfg.WinMask |= (1 << 1);
				_win0Wrap = false;
			}
			else
			{
				cfg.WinPos0 = x1;
				cfg.WinPos1 = x0;
				if ( _win0Wrap ) cfg.WinMask |= (1 << 0);
				cfg.WinMask |= (1 << 2);
				_win0Wrap = true;
			}
		}
		else
		{
			cfg.WinPos0 = 256;
			cfg.WinPos1 = 256;
		}

		if ( ((dispcnt & (1 << 14)) != 0) && ((_gpu2d.Win1Active & 0x1) != 0) )
		{
			int x0 = _gpu2d.Win1Coords[0];
			int x1 = _gpu2d.Win1Coords[1];
			if ( x0 <= x1 )
			{
				cfg.WinPos2 = x0;
				cfg.WinPos3 = x1;
				if ( _win1Wrap ) cfg.WinMask |= (1 << 3);
				cfg.WinMask |= (1 << 4);
				_win1Wrap = false;
			}
			else
			{
				cfg.WinPos2 = x1;
				cfg.WinPos3 = x0;
				if ( _win1Wrap ) cfg.WinMask |= (1 << 3);
				cfg.WinMask |= (1 << 5);
				_win1Wrap = true;
			}
		}
		else
		{
			cfg.WinPos2 = 256;
			cfg.WinPos3 = 256;
		}
	}

	public void UpdateLayerConfig()
	{
		uint dispcnt = _gpu2d.DispCnt;

		uint tilebase, mapbase;
		if ( _num == 0 )
		{
			tilebase = ((dispcnt >> 24) & 0x7) << 16;
			mapbase = ((dispcnt >> 27) & 0x7) << 16;
		}
		else
		{
			tilebase = 0;
			mapbase = 0;
		}

		int[] layertype = [1, 1, 0, 0];
		switch ( dispcnt & 0x7 )
		{
			case 0: layertype[2] = 1; layertype[3] = 1; break;
			case 1: layertype[2] = 1; layertype[3] = 2; break;
			case 2: layertype[2] = 2; layertype[3] = 2; break;
			case 3: layertype[2] = 1; layertype[3] = 3; break;
			case 4: layertype[2] = 2; layertype[3] = 3; break;
			case 5: layertype[2] = 3; layertype[3] = 3; break;
			case 6: layertype[0] = 0; layertype[1] = 0; layertype[2] = 4; layertype[3] = 0; break;
			case 7: layertype[2] = 0; layertype[3] = 0; break;
		}

		for ( int layer = 0; layer < 4; layer++ )
		{
			_layerConfig[layer] = default;
			int type = layertype[layer];
			if ( type == 0 )
				continue;

			ushort bgcnt = _gpu2d.BGCnt[layer];
			sBGConfig cfg = default;

			cfg.TileOffset = (int)tilebase + (((bgcnt >> 2) & 0xF) << 14);
			cfg.MapOffset = (int)mapbase + (((bgcnt >> 8) & 0x1F) << 11);
			cfg.PalOffset = 0;

			if ( (layer == 0) && ((dispcnt & (1 << 3)) != 0) )
			{
				cfg.SizeX = 256; cfg.SizeY = 192;
				cfg.Type = 6;
				cfg.Clamp = 1;
			}
			else if ( type == 1 )
			{
				switch ( bgcnt >> 14 )
				{
					case 0: cfg.SizeX = 256; cfg.SizeY = 256; break;
					case 1: cfg.SizeX = 512; cfg.SizeY = 256; break;
					case 2: cfg.SizeX = 256; cfg.SizeY = 512; break;
					default: cfg.SizeX = 512; cfg.SizeY = 512; break;
				}

				if ( (bgcnt & (1 << 7)) != 0 )
				{
					cfg.Type = 1;
					if ( (dispcnt & (1 << 30)) != 0 )
					{
						int paloff = layer;
						if ( (layer < 2) && ((bgcnt & (1 << 13)) != 0) )
							paloff += 2;
						cfg.PalOffset = 1 + (16 * paloff);
					}
				}
				else
				{
					cfg.Type = 0;
				}
				cfg.Clamp = 0;
			}
			else if ( type == 2 )
			{
				switch ( bgcnt >> 14 )
				{
					case 0: cfg.SizeX = 128; cfg.SizeY = 128; break;
					case 1: cfg.SizeX = 256; cfg.SizeY = 256; break;
					case 2: cfg.SizeX = 512; cfg.SizeY = 512; break;
					default: cfg.SizeX = 1024; cfg.SizeY = 1024; break;
				}
				cfg.Type = 2;
				cfg.Clamp = ((bgcnt & (1 << 13)) != 0) ? 0 : 1;
			}
			else if ( type == 3 )
			{
				if ( (bgcnt & (1 << 7)) != 0 )
				{
					switch ( bgcnt >> 14 )
					{
						case 0: cfg.SizeX = 128; cfg.SizeY = 128; break;
						case 1: cfg.SizeX = 256; cfg.SizeY = 256; break;
						case 2: cfg.SizeX = 512; cfg.SizeY = 256; break;
						default: cfg.SizeX = 512; cfg.SizeY = 512; break;
					}

					cfg.TileOffset = 0;
					cfg.MapOffset = ((bgcnt >> 8) & 0x1F) << 14;

					if ( (bgcnt & (1 << 2)) != 0 )
						cfg.Type = 5;
					else
						cfg.Type = 4;
				}
				else
				{
					switch ( bgcnt >> 14 )
					{
						case 0: cfg.SizeX = 128; cfg.SizeY = 128; break;
						case 1: cfg.SizeX = 256; cfg.SizeY = 256; break;
						case 2: cfg.SizeX = 512; cfg.SizeY = 512; break;
						default: cfg.SizeX = 1024; cfg.SizeY = 1024; break;
					}

					cfg.Type = 3;
					if ( (dispcnt & (1 << 30)) != 0 )
					{
						int paloff = layer;
						if ( (layer < 2) && ((bgcnt & (1 << 13)) != 0) )
							paloff += 2;
						cfg.PalOffset = 1 + (16 * paloff);
					}
				}
				cfg.Clamp = ((bgcnt & (1 << 13)) != 0) ? 0 : 1;
			}
			else
			{
				switch ( bgcnt >> 14 )
				{
					case 0: cfg.SizeX = 512; cfg.SizeY = 1024; break;
					case 1: cfg.SizeX = 1024; cfg.SizeY = 512; break;
					case 2: cfg.SizeX = 512; cfg.SizeY = 256; break;
					default: cfg.SizeX = 512; cfg.SizeY = 512; break;
				}
				cfg.Type = 4;
				cfg.TileOffset = 0;
				cfg.MapOffset = 0;
				cfg.Clamp = ((bgcnt & (1 << 13)) != 0) ? 0 : 1;
			}

			_layerConfig[layer] = cfg;
		}
	}

	public void UpdateOAM()
	{
		for ( int i = 0; i < 32; i++ )
		{
			_rotscale[i].X = OamS16( (i * 16) + 3 );
			_rotscale[i].Y = OamS16( (i * 16) + 7 );
			_rotscale[i].Z = OamS16( (i * 16) + 11 );
			_rotscale[i].W = OamS16( (i * 16) + 15 );
		}

		uint dispcnt = _gpu2d.DispCnt;
		int numsprites = 0;

		for ( int sprnum = 0; sprnum < 128; sprnum++ )
		{
			int attr0 = OamU16( sprnum * 4 + 0 );
			int attr1 = OamU16( sprnum * 4 + 1 );

			int sprtype = (attr0 >> 8) & 0x3;
			if ( sprtype == 2 )
				continue;

			int xpos = (attr1 << 23) >> 23;
			int ypos = (attr0 << 24) >> 24;

			int sizeparam = (attr0 >> 14) | ((attr1 & 0xC000) >> 12);
			int width = SpriteWidthTab[sizeparam];
			int height = SpriteHeightTab[sizeparam];
			int boundwidth = width;
			int boundheight = height;
			if ( sprtype == 3 )
			{
				boundwidth <<= 1;
				boundheight <<= 1;
			}

			if ( xpos <= -boundwidth )
				continue;

			bool yc0 = ((ypos + boundheight) > 0) && (ypos < 192);
			bool yc1 = (((ypos & 0xFF) + boundheight) > 0) && ((ypos & 0xFF) < 192);
			if ( !(yc0 || yc1) )
				continue;

			int attr2 = OamU16( sprnum * 4 + 2 );
			int sprmode = (attr0 >> 10) & 0x3;
			if ( sprmode == 3 )
			{
				if ( (dispcnt & 0x60) == 0x60 )
					continue;
				if ( (attr2 >> 12) == 0 )
					continue;
			}

			if ( numsprites >= 128 )
				break;

			sOAM spr = default;
			spr.PositionX = xpos;
			spr.PositionY = ypos;
			spr.SizeX = width;
			spr.SizeY = height;
			spr.BoundSizeX = boundwidth;
			spr.BoundSizeY = boundheight;

			if ( (sprtype & 1) != 0 )
			{
				spr.FlipX = 0;
				spr.FlipY = 0;
				spr.Rotscale = (attr1 >> 9) & 0x1F;
			}
			else
			{
				spr.FlipX = ((attr1 & (1 << 12)) != 0) ? 1 : 0;
				spr.FlipY = ((attr1 & (1 << 13)) != 0) ? 1 : 0;
				spr.Rotscale = -1;
			}

			spr.OBJMode = sprmode;
			spr.Mosaic = (((attr0 & (1 << 12)) != 0) && (sprmode != 2)) ? 1 : 0;
			spr.BGPrio = (attr2 >> 10) & 0x3;

			int tilenum = attr2 & 0x3FF;

			if ( sprmode == 3 )
			{
				spr.Type = 2;
				if ( (dispcnt & (1 << 6)) != 0 )
				{
					spr.TileOffset = tilenum << (7 + (int)((dispcnt >> 22) & 0x1));
					spr.TileStride = width * 2;
				}
				else
				{
					bool is256 = (dispcnt & (1 << 5)) != 0;
					if ( is256 )
					{
						spr.TileOffset = ((tilenum & 0x01F) << 4) + ((tilenum & 0x3E0) << 7);
						spr.TileStride = 256 * 2;
					}
					else
					{
						spr.TileOffset = ((tilenum & 0x00F) << 4) + ((tilenum & 0x3F0) << 7);
						spr.TileStride = 128 * 2;
					}
				}
				spr.PalOffset = 1 + (attr2 >> 12);
			}
			else
			{
				if ( (dispcnt & (1 << 4)) != 0 )
				{
					spr.TileOffset = tilenum << (5 + (int)((dispcnt >> 20) & 0x3));
					spr.TileStride = (width >> 3) * 32;
					if ( (attr0 & (1 << 13)) != 0 )
						spr.TileStride <<= 1;
				}
				else
				{
					spr.TileOffset = tilenum << 5;
					spr.TileStride = 32 * 32;
				}

				if ( (attr0 & (1 << 13)) != 0 )
				{
					spr.Type = 1;
					if ( (dispcnt & (1 << 31)) != 0 )
						spr.PalOffset = 1 + (attr2 >> 12);
					else
						spr.PalOffset = 0;
				}
				else
				{
					spr.Type = 0;
					spr.PalOffset = (attr2 >> 12) << 4;
				}
			}

			_spriteOAM[numsprites] = spr;
			numsprites++;
		}

		_numSprites = numsprites;
	}

	public void UpdateCompositorConfig()
	{
		_compConfig.BGPrio0 = -1;
		_compConfig.BGPrio1 = -1;
		_compConfig.BGPrio2 = -1;
		_compConfig.BGPrio3 = -1;

		byte le = _gpu2d.LayerEnable;
		if ( (le & (1 << 0)) != 0 ) _compConfig.BGPrio0 = _gpu2d.BGCnt[0] & 0x3;
		if ( (le & (1 << 1)) != 0 ) _compConfig.BGPrio1 = _gpu2d.BGCnt[1] & 0x3;
		if ( (le & (1 << 2)) != 0 ) _compConfig.BGPrio2 = _gpu2d.BGCnt[2] & 0x3;
		if ( (le & (1 << 3)) != 0 ) _compConfig.BGPrio3 = _gpu2d.BGCnt[3] & 0x3;

		_compConfig.EnableOBJ = ((le & (1 << 4)) != 0) ? 1 : 0;
		_compConfig.Enable3D = ((_gpu2d.DispCnt & (1 << 3)) != 0) ? 1 : 0;
		_compConfig.BlendCnt = _gpu2d.BlendCnt;
		_compConfig.BlendEffect = (_gpu2d.BlendCnt >> 6) & 0x3;
		_compConfig.BlendCoef0 = _gpu2d.EVA;
		_compConfig.BlendCoef1 = _gpu2d.EVB;
		_compConfig.BlendCoef2 = _gpu2d.EVY;
	}
}
