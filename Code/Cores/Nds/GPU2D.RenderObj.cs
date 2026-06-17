namespace Emulotl.Nds;

public sealed partial class GPU2D
{
	private static readonly int[] SpriteWidth =
	[
		8, 16, 8, 8,
		16, 32, 8, 8,
		32, 32, 16, 8,
		64, 64, 32, 8
	];
	private static readonly int[] SpriteHeight =
	[
		8, 8, 16, 8,
		16, 8, 32, 8,
		32, 16, 32, 8,
		64, 32, 64, 8
	];

	private uint OamBase => Num == 0 ? 0u : 0x400u;

	private ushort OamRead16( uint u16index )
	{
		uint a = (OamBase + u16index * 2) & 0x7FF;
		return (ushort)(_oam[a] | (_oam[a + 1] << 8));
	}

	private void ApplySpriteMosaicX()
	{
		byte mosw = OBJMosaicSize[0];
		if ( mosw == 0 ) return;

		byte mosx = 0;
		uint latchcolor = 0;
		for ( int i = 0; i < 256; i++ )
		{
			uint curcolor = OBJLine[i];
			bool latch = false;

			if ( mosx == 0 ) latch = true;
			else if ( (curcolor & OBJ_Mosaic) == 0 ) latch = true;
			else if ( (latchcolor & OBJ_Mosaic) == 0 ) latch = true;
			else if ( (curcolor & OBJ_BGPrioMask) < (latchcolor & OBJ_BGPrioMask) ) latch = true;

			if ( latch ) latchcolor = curcolor;

			OBJLine[i] = latchcolor;

			if ( mosx == mosw ) mosx = 0;
			else mosx++;
		}
	}

	private void InterleaveSprites( uint prio )
	{
		uint attrmask = (prio << 16) | OBJ_IsOpaque;
		uint palByte = Num != 0 ? 0x600u : 0x200u;

		for ( int i = 0; i < 256; i++ )
		{
			if ( (OBJLine[i] & OBJ_OpaPrioMask) != attrmask )
				continue;
			if ( (WindowMask[i] & 0x10) == 0 )
				continue;

			ushort color;
			uint pixel = OBJLine[i];

			if ( (pixel & OBJ_DirectColor) != 0 )
				color = (ushort)(pixel & 0x7FFF);
			else if ( (pixel & OBJ_StandardPal) != 0 )
				color = PalRead( palByte + (pixel & 0xFF) * 2 );
			else
				color = ObjExtPalRead( pixel & 0xFFF );

			DrawPixel( i, color, pixel & 0xFF000000 );
		}
	}

	public void DrawSprites( uint line )
	{
		if ( !Enabled )
			return;

		NumSprites = 0;
		Array.Clear( OBJLine );
		Array.Clear( OBJWindow );

		if ( OBJEnable == 0 )
			return;

		for ( uint sprnum = 0; sprnum < 128; sprnum++ )
		{
			ushort attr0 = OamRead16( sprnum * 4 + 0 );
			ushort attr1 = OamRead16( sprnum * 4 + 1 );

			uint sprtype = (uint)((attr0 >> 8) & 0x3);
			if ( sprtype == 2 )
				continue;

			bool iswin = ((attr0 >> 10) & 0x3) == 2;

			uint sizeparam = (uint)((attr0 >> 14) | ((attr1 & 0xC000) >> 12));
			int width = SpriteWidth[sizeparam];
			int height = SpriteHeight[sizeparam];
			int boundwidth = width;
			int boundheight = height;

			if ( sprtype == 3 )
			{
				boundwidth <<= 1;
				boundheight <<= 1;
			}

			int ypos = attr0 & 0xFF;
			if ( (int)((line - ypos) & 0xFF) >= boundheight )
				continue;

			int xpos = (int)((uint)attr1 << 23) >> 23;
			if ( xpos <= -boundwidth )
				continue;

			if ( (attr0 & (1 << 12)) != 0 && !iswin )
			{
				ypos = (int)((OBJMosaicLine - (uint)ypos) & 0xFF);
				if ( ypos >= boundheight ) ypos = 0;
			}
			else
				ypos = (int)((line - (uint)ypos) & 0xFF);

			if ( (sprtype & 1) != 0 )
				DrawSprite_Rotscale( sprnum, (uint)boundwidth, (uint)boundheight, (uint)width, (uint)height, xpos, ypos, iswin );
			else
				DrawSprite_Normal( sprnum, (uint)width, (uint)height, xpos, ypos, iswin );

			NumSprites++;
		}
	}

	private void DrawSpritePixel( int color, uint pixelattr, int xpos, bool window )
	{
		if ( window )
		{
			if ( color != -1 )
				OBJWindow[xpos] = 1;
		}
		else
		{
			uint oldpixel = OBJLine[xpos];
			bool oldisopaque = (oldpixel & OBJ_IsOpaque) != 0;
			bool newisopaque = color != -1;
			bool priocheck = (pixelattr & OBJ_BGPrioMask) < (oldpixel & OBJ_BGPrioMask);

			if ( newisopaque && (!oldisopaque || priocheck) )
			{
				OBJLine[xpos] = (uint)color | pixelattr;
			}
			else if ( !newisopaque && !oldisopaque )
			{
				OBJLine[xpos] &= ~(OBJ_Mosaic | OBJ_BGPrioMask);
				OBJLine[xpos] |= pixelattr & (OBJ_IsSprite | OBJ_Mosaic | OBJ_BGPrioMask);
			}
		}
	}

	private void DrawSprite_Rotscale( uint num, uint boundwidth, uint boundheight, uint width, uint height, int xpos, int ypos, bool window )
	{
		ushort attr0 = OamRead16( num * 4 + 0 );
		ushort attr1 = OamRead16( num * 4 + 1 );
		ushort attr2 = OamRead16( num * 4 + 2 );
		uint rotbase = (uint)(((attr1 >> 9) & 0x1F) * 16) + 3;

		uint pixelattr = (uint)((attr2 & 0x0C00) << 6) | OBJ_IsSprite | OBJ_IsOpaque;
		uint tilenum = (uint)(attr2 & 0x03FF);
		uint spritemode = window ? 0 : (uint)((attr0 >> 10) & 0x3);

		uint ytilefactor;

		int centerX = (int)(boundwidth >> 1);
		int centerY = (int)(boundheight >> 1);

		if ( (attr0 & (1 << 12)) != 0 && !window )
			pixelattr |= OBJ_Mosaic;

		uint xoff;
		if ( xpos >= 0 )
		{
			xoff = 0;
			if ( (xpos + boundwidth) > 256 )
				boundwidth = (uint)(256 - xpos);
		}
		else
		{
			xoff = (uint)-xpos;
			xpos = 0;
		}

		short rotA = (short)OamRead16( rotbase + 0 );
		short rotB = (short)OamRead16( rotbase + 4 );
		short rotC = (short)OamRead16( rotbase + 8 );
		short rotD = (short)OamRead16( rotbase + 12 );

		int rotX = (((int)xoff - centerX) * rotA) + ((ypos - centerY) * rotB) + (int)(width << 7);
		int rotY = (((int)xoff - centerX) * rotC) + ((ypos - centerY) * rotD) + (int)(height << 7);

		width <<= 8;
		height <<= 8;

		if ( spritemode == 3 )
		{
			uint alpha = (uint)(attr2 >> 12);
			if ( alpha == 0 ) return;
			alpha++;

			pixelattr |= 0xC0000000 | (alpha << 24);

			uint pixelsaddr;
			if ( (DispCnt & 0x40) != 0 )
			{
				if ( (DispCnt & 0x20) != 0 ) return;
				pixelsaddr = tilenum << (int)(7 + ((DispCnt >> 22) & 0x1));
				ytilefactor = (width >> 8) * 2;
			}
			else
			{
				if ( (DispCnt & 0x20) != 0 )
				{
					pixelsaddr = ((tilenum & 0x01F) << 4) + ((tilenum & 0x3E0) << 7);
					ytilefactor = 256 * 2;
				}
				else
				{
					pixelsaddr = ((tilenum & 0x00F) << 4) + ((tilenum & 0x3F0) << 7);
					ytilefactor = 128 * 2;
				}
			}

			while ( xoff < boundwidth )
			{
				if ( (uint)rotX < width && (uint)rotY < height )
				{
					ushort color = ObjRead16( pixelsaddr + (uint)((rotY >> 8) * (int)ytilefactor) + (uint)((rotX >> 8) << 1) );
					DrawSpritePixel( (color & 0x8000) != 0 ? color : -1, pixelattr, xpos, window );
				}

				rotX += rotA;
				rotY += rotC;
				xoff++;
				xpos++;
			}
		}
		else
		{
			uint pixelsaddr = tilenum;
			if ( (DispCnt & (1 << 4)) != 0 )
			{
				pixelsaddr <<= (int)((DispCnt >> 20) & 0x3);
				ytilefactor = (width >> 11) << ((attr0 & 0x2000) != 0 ? 1 : 0);
			}
			else
				ytilefactor = 0x20;

			if ( spritemode == 1 ) pixelattr |= 0x80000000;
			else pixelattr |= 0x10000000;

			ytilefactor <<= 5;
			pixelsaddr <<= 5;

			if ( (attr0 & (1 << 13)) != 0 )
			{
				if ( !window )
				{
					if ( (DispCnt & (1u << 31)) == 0 )
						pixelattr |= OBJ_StandardPal;
					else
						pixelattr |= (uint)((attr2 & 0xF000) >> 4);
				}

				while ( xoff < boundwidth )
				{
					if ( (uint)rotX < width && (uint)rotY < height )
					{
						byte color = ObjRead8( pixelsaddr + (uint)((rotY >> 11) * (int)ytilefactor) + (uint)((rotY & 0x700) >> 5) + (uint)((rotX >> 11) * 64) + (uint)((rotX & 0x700) >> 8) );
						DrawSpritePixel( color != 0 ? color : -1, pixelattr, xpos, window );
					}

					rotX += rotA;
					rotY += rotC;
					xoff++;
					xpos++;
				}
			}
			else
			{
				if ( !window )
				{
					pixelattr |= OBJ_StandardPal;
					pixelattr |= (uint)((attr2 & 0xF000) >> 8);
				}

				while ( xoff < boundwidth )
				{
					if ( (uint)rotX < width && (uint)rotY < height )
					{
						byte raw = ObjRead8( pixelsaddr + (uint)((rotY >> 11) * (int)ytilefactor) + (uint)((rotY & 0x700) >> 6) + (uint)((rotX >> 11) * 32) + (uint)((rotX & 0x700) >> 9) );
						int color = (rotX & 0x100) != 0 ? (raw >> 4) : (raw & 0x0F);
						DrawSpritePixel( color != 0 ? color : -1, pixelattr, xpos, window );
					}

					rotX += rotA;
					rotY += rotC;
					xoff++;
					xpos++;
				}
			}
		}
	}

	private void DrawSprite_Normal( uint num, uint width, uint height, int xpos, int ypos, bool window )
	{
		ushort attr0 = OamRead16( num * 4 + 0 );
		ushort attr1 = OamRead16( num * 4 + 1 );
		ushort attr2 = OamRead16( num * 4 + 2 );

		uint pixelattr = (uint)((attr2 & 0x0C00) << 6) | OBJ_IsSprite | OBJ_IsOpaque;
		uint tilenum = (uint)(attr2 & 0x03FF);
		uint spritemode = window ? 0 : (uint)((attr0 >> 10) & 0x3);

		uint wmask = width - 8;

		if ( (attr0 & (1 << 12)) != 0 && !window )
			pixelattr |= OBJ_Mosaic;

		if ( (attr1 & (1 << 13)) != 0 )
			ypos = (int)height - 1 - ypos;

		uint xoff;
		uint xend = width;
		if ( xpos >= 0 )
		{
			xoff = 0;
			if ( (xpos + xend) > 256 )
				xend = (uint)(256 - xpos);
		}
		else
		{
			xoff = (uint)-xpos;
			xpos = 0;
		}

		if ( spritemode == 3 )
		{
			uint alpha = (uint)(attr2 >> 12);
			if ( alpha == 0 ) return;
			alpha++;

			pixelattr |= 0xC0000000 | (alpha << 24);

			uint pixelsaddr = tilenum;
			if ( (DispCnt & 0x40) != 0 )
			{
				if ( (DispCnt & 0x20) != 0 ) return;
				pixelsaddr <<= (int)(7 + ((DispCnt >> 22) & 0x1));
				pixelsaddr += (uint)(ypos * (int)width * 2);
			}
			else
			{
				if ( (DispCnt & 0x20) != 0 )
				{
					pixelsaddr = ((tilenum & 0x01F) << 4) + ((tilenum & 0x3E0) << 7);
					pixelsaddr += (uint)(ypos * 256 * 2);
				}
				else
				{
					pixelsaddr = ((tilenum & 0x00F) << 4) + ((tilenum & 0x3F0) << 7);
					pixelsaddr += (uint)(ypos * 128 * 2);
				}
			}

			int pixelstride;
			if ( (attr1 & (1 << 12)) != 0 )
			{
				pixelsaddr += (width - 1) << 1;
				pixelsaddr -= xoff << 1;
				pixelstride = -2;
			}
			else
			{
				pixelsaddr += xoff << 1;
				pixelstride = 2;
			}

			while ( xoff < xend )
			{
				ushort color = ObjRead16( pixelsaddr );
				pixelsaddr = (uint)(pixelsaddr + pixelstride);
				DrawSpritePixel( (color & 0x8000) != 0 ? color : -1, pixelattr, xpos, window );
				xoff++;
				xpos++;
			}
		}
		else
		{
			uint pixelsaddr = tilenum;
			if ( (DispCnt & (1 << 4)) != 0 )
			{
				pixelsaddr <<= (int)((DispCnt >> 20) & 0x3);
				pixelsaddr += (uint)(((ypos >> 3) * ((int)width >> 3)) << ((attr0 & 0x2000) != 0 ? 1 : 0));
			}
			else
				pixelsaddr += (uint)((ypos >> 3) * 0x20);

			if ( spritemode == 1 ) pixelattr |= 0x80000000;
			else pixelattr |= 0x10000000;

			if ( (attr0 & (1 << 13)) != 0 )
			{
				pixelsaddr <<= 5;
				pixelsaddr += (uint)((ypos & 0x7) << 3);
				int pixelstride;

				if ( !window )
				{
					if ( (DispCnt & (1u << 31)) == 0 )
						pixelattr |= OBJ_StandardPal;
					else
						pixelattr |= (uint)((attr2 & 0xF000) >> 4);
				}

				if ( (attr1 & (1 << 12)) != 0 )
				{
					pixelsaddr += ((width - 1) & wmask) << 3;
					pixelsaddr += (width - 1) & 0x7;
					pixelsaddr -= (xoff & wmask) << 3;
					pixelsaddr -= xoff & 0x7;
					pixelstride = -1;
				}
				else
				{
					pixelsaddr += (xoff & wmask) << 3;
					pixelsaddr += xoff & 0x7;
					pixelstride = 1;
				}

				while ( xoff < xend )
				{
					byte color = ObjRead8( pixelsaddr );
					pixelsaddr = (uint)(pixelsaddr + pixelstride);
					DrawSpritePixel( color != 0 ? color : -1, pixelattr, xpos, window );
					xoff++;
					xpos++;
					if ( (xoff & 0x7) == 0 ) pixelsaddr = (uint)(pixelsaddr + 56 * pixelstride);
				}
			}
			else
			{
				pixelsaddr <<= 5;
				pixelsaddr += (uint)((ypos & 0x7) << 2);

				if ( !window )
				{
					pixelattr |= OBJ_StandardPal;
					pixelattr |= (uint)((attr2 & 0xF000) >> 8);
				}

				if ( (attr1 & (1 << 12)) != 0 )
				{
					pixelsaddr += ((width - 1) & wmask) << 2;
					pixelsaddr += ((width - 1) & 0x7) >> 1;
					pixelsaddr -= (xoff & wmask) << 2;
					pixelsaddr -= (xoff & 0x7) >> 1;
				}
				else
				{
					pixelsaddr += (xoff & wmask) << 2;
					pixelsaddr += (xoff & 0x7) >> 1;
				}

				while ( xoff < xend )
				{
					int color;
					if ( (attr1 & (1 << 12)) != 0 )
					{
						if ( (xoff & 0x1) != 0 ) { color = ObjRead8( pixelsaddr ) & 0x0F; pixelsaddr--; }
						else color = ObjRead8( pixelsaddr ) >> 4;
					}
					else
					{
						if ( (xoff & 0x1) != 0 ) { color = ObjRead8( pixelsaddr ) >> 4; pixelsaddr++; }
						else color = ObjRead8( pixelsaddr ) & 0x0F;
					}

					DrawSpritePixel( color != 0 ? color : -1, pixelattr, xpos, window );
					xoff++;
					xpos++;
					if ( (xoff & 0x7) == 0 ) pixelsaddr = (uint)(pixelsaddr + ((attr1 & 0x1000) != 0 ? -28 : 28));
				}
			}
		}
	}
}
