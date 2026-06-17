namespace Emulotl.Nds;

public sealed partial class GPU2D
{
	private void DrawBG_Text( uint line, uint bgnum, bool mosaic )
	{
		ushort bgcnt = BGCnt[bgnum];

		uint tilesetaddr, tilemapaddr;
		uint xoff = BGXPos[bgnum];
		uint yoff = BGYPos[bgnum];

		if ( (bgcnt & (1 << 6)) != 0 )
			yoff += BGMosaicLine;
		else
			yoff += line;

		uint widexmask = (bgcnt & (1 << 14)) != 0 ? 0x100u : 0;

		bool extpal = (DispCnt & (1 << 30)) != 0;
		uint extpalslot = 0;
		if ( extpal )
			extpalslot = (bgnum < 2 && (bgcnt & 0x2000) != 0) ? (2 + bgnum) : bgnum;

		uint palByte;
		if ( Num != 0 )
		{
			tilesetaddr = (uint)((bgcnt & 0x003C) << 12);
			tilemapaddr = (uint)((bgcnt & 0x1F00) << 3);
			palByte = 0x400;
		}
		else
		{
			tilesetaddr = ((DispCnt & 0x07000000) >> 8) + (uint)((bgcnt & 0x003C) << 12);
			tilemapaddr = ((DispCnt & 0x38000000) >> 11) + (uint)((bgcnt & 0x1F00) << 3);
			palByte = 0;
		}

		if ( (bgcnt & (1 << 15)) != 0 )
		{
			tilemapaddr += (yoff & 0x1F8) << 3;
			if ( (bgcnt & (1 << 14)) != 0 )
				tilemapaddr += (yoff & 0x100) << 3;
		}
		else
			tilemapaddr += (yoff & 0xF8) << 3;

		uint curtile = 0;
		uint extPalNum = 0;
		uint curpalByte = 0;
		uint pixelsaddr = 0;
		uint lastxpos = 0;

		bool is256 = (bgcnt & (1 << 7)) != 0;

		if ( is256 )
		{
			if ( (xoff & 0x7) != 0 || mosaic )
			{
				curtile = BgRead16( tilemapaddr + ((xoff & 0xF8) >> 2) + ((xoff & widexmask) << 3) );
				extPalNum = curtile >> 12;
				pixelsaddr = tilesetaddr + ((curtile & 0x03FF) << 6)
					+ (((curtile & (1 << 11)) != 0 ? (7 - (yoff & 0x7)) : (yoff & 0x7)) << 3);
			}

			if ( mosaic ) lastxpos = xoff;

			for ( int i = 0; i < 256; i++ )
			{
				uint xpos = mosaic ? (xoff - CurBGXMosaicTable[i]) : xoff;

				if ( (!mosaic && (xpos & 0x7) == 0) || (mosaic && (xpos >> 3) != (lastxpos >> 3)) )
				{
					curtile = BgRead16( tilemapaddr + ((xpos & 0xF8) >> 2) + ((xpos & widexmask) << 3) );
					extPalNum = curtile >> 12;
					pixelsaddr = tilesetaddr + ((curtile & 0x03FF) << 6)
						+ (((curtile & (1 << 11)) != 0 ? (7 - (yoff & 0x7)) : (yoff & 0x7)) << 3);
					if ( mosaic ) lastxpos = xpos;
				}

				if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
				{
					uint tilexoff = (curtile & (1 << 10)) != 0 ? (7 - (xpos & 0x7)) : (xpos & 0x7);
					byte color = BgRead8( pixelsaddr + tilexoff );
					if ( color != 0 )
					{
						ushort c = extpal ? BgExtPalRead( extpalslot, extPalNum, color ) : PalRead( palByte + (uint)color * 2 );
						DrawPixel( i, c, 0x01000000u << (int)bgnum );
					}
				}

				xoff++;
			}
		}
		else
		{
			if ( (xoff & 0x7) != 0 || mosaic )
			{
				curtile = BgRead16( tilemapaddr + ((xoff & 0xF8) >> 2) + ((xoff & widexmask) << 3) );
				curpalByte = palByte + ((curtile & 0xF000) >> 7);
				pixelsaddr = tilesetaddr + ((curtile & 0x03FF) << 5)
					+ (((curtile & (1 << 11)) != 0 ? (7 - (yoff & 0x7)) : (yoff & 0x7)) << 2);
			}

			if ( mosaic ) lastxpos = xoff;

			for ( int i = 0; i < 256; i++ )
			{
				uint xpos = mosaic ? (xoff - CurBGXMosaicTable[i]) : xoff;

				if ( (!mosaic && (xpos & 0x7) == 0) || (mosaic && (xpos >> 3) != (lastxpos >> 3)) )
				{
					curtile = BgRead16( tilemapaddr + ((xpos & 0xF8) >> 2) + ((xpos & widexmask) << 3) );
					curpalByte = palByte + ((curtile & 0xF000) >> 7);
					pixelsaddr = tilesetaddr + ((curtile & 0x03FF) << 5)
						+ (((curtile & (1 << 11)) != 0 ? (7 - (yoff & 0x7)) : (yoff & 0x7)) << 2);
					if ( mosaic ) lastxpos = xpos;
				}

				if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
				{
					uint tilexoff = (curtile & (1 << 10)) != 0 ? (7 - (xpos & 0x7)) : (xpos & 0x7);
					byte raw = BgRead8( pixelsaddr + (tilexoff >> 1) );
					byte color = (tilexoff & 0x1) != 0 ? (byte)(raw >> 4) : (byte)(raw & 0x0F);
					if ( color != 0 )
						DrawPixel( i, PalRead( curpalByte + (uint)color * 2 ), 0x01000000u << (int)bgnum );
				}

				xoff++;
			}
		}
	}

	private void DrawBG_Affine( uint line, uint bgnum, bool mosaic )
	{
		ushort bgcnt = BGCnt[bgnum];

		uint tilesetaddr, tilemapaddr;
		uint palByte;

		uint coordmask, yshift;
		switch ( (bgcnt >> 14) & 0x3 )
		{
			case 0: coordmask = 0x07800; yshift = 7; break;
			case 1: coordmask = 0x0F800; yshift = 8; break;
			case 2: coordmask = 0x1F800; yshift = 9; break;
			default: coordmask = 0x3F800; yshift = 10; break;
		}

		uint overflowmask = (bgcnt & (1 << 13)) != 0 ? 0 : ~(coordmask | 0x7FF);

		int rotA = BGRotA[bgnum - 2];
		int rotC = BGRotC[bgnum - 2];
		int rotX = BGXRefInternal[bgnum - 2];
		int rotY = BGYRefInternal[bgnum - 2];

		if ( Num != 0 )
		{
			tilesetaddr = (uint)((bgcnt & 0x003C) << 12);
			tilemapaddr = (uint)((bgcnt & 0x1F00) << 3);
			palByte = 0x400;
		}
		else
		{
			tilesetaddr = ((DispCnt & 0x07000000) >> 8) + (uint)((bgcnt & 0x003C) << 12);
			tilemapaddr = ((DispCnt & 0x38000000) >> 11) + (uint)((bgcnt & 0x1F00) << 3);
			palByte = 0;
		}

		yshift -= 3;

		for ( int i = 0; i < 256; i++ )
		{
			if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
			{
				int finalX, finalY;
				if ( mosaic )
				{
					int im = CurBGXMosaicTable[i];
					finalX = rotX - (im * rotA);
					finalY = rotY - (im * rotC);
				}
				else
				{
					finalX = rotX;
					finalY = rotY;
				}

				if ( ((finalX | finalY) & overflowmask) == 0 )
				{
					uint curtile = BgRead8( tilemapaddr + ((((uint)finalY & coordmask) >> 11) << (int)yshift) + (((uint)finalX & coordmask) >> 11) );
					uint tilexoff = (uint)((finalX >> 8) & 0x7);
					uint tileyoff = (uint)((finalY >> 8) & 0x7);
					byte color = BgRead8( tilesetaddr + (curtile << 6) + (tileyoff << 3) + tilexoff );
					if ( color != 0 )
						DrawPixel( i, PalRead( palByte + (uint)color * 2 ), 0x01000000u << (int)bgnum );
				}
			}

			rotX += rotA;
			rotY += rotC;
		}
	}

	private void DrawBG_Extended( uint line, uint bgnum, bool mosaic )
	{
		ushort bgcnt = BGCnt[bgnum];

		uint tilesetaddr, tilemapaddr;
		uint palByte;
		bool extpal = (DispCnt & (1 << 30)) != 0;

		int rotA = BGRotA[bgnum - 2];
		int rotC = BGRotC[bgnum - 2];
		int rotX = BGXRefInternal[bgnum - 2];
		int rotY = BGYRefInternal[bgnum - 2];

		if ( (bgcnt & (1 << 7)) != 0 )
		{
			uint xmask, ymask, yshift;
			switch ( (bgcnt >> 14) & 0x3 )
			{
				case 0: xmask = 0x07FFF; ymask = 0x07FFF; yshift = 7; break;
				case 1: xmask = 0x0FFFF; ymask = 0x0FFFF; yshift = 8; break;
				case 2: xmask = 0x1FFFF; ymask = 0x0FFFF; yshift = 9; break;
				default: xmask = 0x1FFFF; ymask = 0x1FFFF; yshift = 9; break;
			}

			uint ofxmask, ofymask;
			if ( (bgcnt & (1 << 13)) != 0 ) { ofxmask = 0; ofymask = 0; }
			else { ofxmask = ~xmask; ofymask = ~ymask; }

			tilemapaddr = (uint)((bgcnt & 0x1F00) << 6);

			if ( (bgcnt & (1 << 2)) != 0 )
			{
				for ( int i = 0; i < 256; i++ )
				{
					if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
					{
						int finalX = mosaic ? rotX - (CurBGXMosaicTable[i] * rotA) : rotX;
						int finalY = mosaic ? rotY - (CurBGXMosaicTable[i] * rotC) : rotY;

						if ( ((uint)finalX & ofxmask) == 0 && ((uint)finalY & ofymask) == 0 )
						{
							ushort color = BgRead16( tilemapaddr + (((((uint)finalY & ymask) >> 8) << (int)yshift) + (((uint)finalX & xmask) >> 8)) * 2 );
							if ( (color & 0x8000) != 0 )
								DrawPixel( i, (ushort)(color & 0x7FFF), 0x01000000u << (int)bgnum );
						}
					}
					rotX += rotA;
					rotY += rotC;
				}
			}
			else
			{
				palByte = Num != 0 ? 0x400u : 0u;
				for ( int i = 0; i < 256; i++ )
				{
					if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
					{
						int finalX = mosaic ? rotX - (CurBGXMosaicTable[i] * rotA) : rotX;
						int finalY = mosaic ? rotY - (CurBGXMosaicTable[i] * rotC) : rotY;

						if ( ((uint)finalX & ofxmask) == 0 && ((uint)finalY & ofymask) == 0 )
						{
							byte color = BgRead8( tilemapaddr + ((((uint)finalY & ymask) >> 8) << (int)yshift) + (((uint)finalX & xmask) >> 8) );
							if ( color != 0 )
								DrawPixel( i, PalRead( palByte + (uint)color * 2 ), 0x01000000u << (int)bgnum );
						}
					}
					rotX += rotA;
					rotY += rotC;
				}
			}
		}
		else
		{
			uint coordmask, yshift;
			switch ( (bgcnt >> 14) & 0x3 )
			{
				case 0: coordmask = 0x07800; yshift = 7; break;
				case 1: coordmask = 0x0F800; yshift = 8; break;
				case 2: coordmask = 0x1F800; yshift = 9; break;
				default: coordmask = 0x3F800; yshift = 10; break;
			}

			uint overflowmask = (bgcnt & (1 << 13)) != 0 ? 0 : ~(coordmask | 0x7FF);

			if ( Num != 0 )
			{
				tilesetaddr = (uint)((bgcnt & 0x003C) << 12);
				tilemapaddr = (uint)((bgcnt & 0x1F00) << 3);
				palByte = 0x400;
			}
			else
			{
				tilesetaddr = ((DispCnt & 0x07000000) >> 8) + (uint)((bgcnt & 0x003C) << 12);
				tilemapaddr = ((DispCnt & 0x38000000) >> 11) + (uint)((bgcnt & 0x1F00) << 3);
				palByte = 0;
			}

			yshift -= 3;

			for ( int i = 0; i < 256; i++ )
			{
				if ( (WindowMask[i] & (1 << (int)bgnum)) != 0 )
				{
					int finalX = mosaic ? rotX - (CurBGXMosaicTable[i] * rotA) : rotX;
					int finalY = mosaic ? rotY - (CurBGXMosaicTable[i] * rotC) : rotY;

					if ( ((finalX | finalY) & overflowmask) == 0 )
					{
						uint curtile = BgRead16( tilemapaddr + ((((((uint)finalY & coordmask) >> 11) << (int)yshift) + (((uint)finalX & coordmask) >> 11)) << 1) );

						uint tilexoff = (uint)((finalX >> 8) & 0x7);
						uint tileyoff = (uint)((finalY >> 8) & 0x7);
						if ( (curtile & (1 << 10)) != 0 ) tilexoff = 7 - tilexoff;
						if ( (curtile & (1 << 11)) != 0 ) tileyoff = 7 - tileyoff;

						byte color = BgRead8( tilesetaddr + ((curtile & 0x03FF) << 6) + (tileyoff << 3) + tilexoff );
						if ( color != 0 )
						{
							ushort c = extpal ? BgExtPalRead( bgnum, curtile >> 12, color ) : PalRead( palByte + (uint)color * 2 );
							DrawPixel( i, c, 0x01000000u << (int)bgnum );
						}
					}
				}
				rotX += rotA;
				rotY += rotC;
			}
		}
	}

	private void DrawBG_Large( uint line, bool mosaic )
	{
		ushort bgcnt = BGCnt[2];
		uint palByte = Num != 0 ? 0x400u : 0u;

		uint xmask, ymask, yshift;
		switch ( (bgcnt >> 14) & 0x3 )
		{
			case 0: xmask = 0x1FFFF; ymask = 0x3FFFF; yshift = 9; break;
			case 1: xmask = 0x3FFFF; ymask = 0x1FFFF; yshift = 10; break;
			case 2: xmask = 0x1FFFF; ymask = 0x0FFFF; yshift = 9; break;
			default: xmask = 0x1FFFF; ymask = 0x1FFFF; yshift = 9; break;
		}

		uint ofxmask, ofymask;
		if ( (bgcnt & (1 << 13)) != 0 ) { ofxmask = 0; ofymask = 0; }
		else { ofxmask = ~xmask; ofymask = ~ymask; }

		int rotA = BGRotA[0];
		int rotC = BGRotC[0];
		int rotX = BGXRefInternal[0];
		int rotY = BGYRefInternal[0];

		for ( int i = 0; i < 256; i++ )
		{
			if ( (WindowMask[i] & (1 << 2)) != 0 )
			{
				int finalX = mosaic ? rotX - (CurBGXMosaicTable[i] * rotA) : rotX;
				int finalY = mosaic ? rotY - (CurBGXMosaicTable[i] * rotC) : rotY;

				if ( ((uint)finalX & ofxmask) == 0 && ((uint)finalY & ofymask) == 0 )
				{
					byte color = BgRead8( ((((uint)finalY & ymask) >> 8) << (int)yshift) + (((uint)finalX & xmask) >> 8) );
					if ( color != 0 )
						DrawPixel( i, PalRead( palByte + (uint)color * 2 ), 0x01000000u << 2 );
				}
			}
			rotX += rotA;
			rotY += rotC;
		}
	}
}
