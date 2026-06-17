namespace Emulotl.Nds;

public sealed partial class GPU
{
	private readonly uint[] _composeLine = new uint[256];

	public void FinalizeScanline( uint line )
	{
		FinalizeEngine( GPU2D_A, FramebufferA, line, MasterBrightnessA );
		FinalizeEngine( GPU2D_B, FramebufferB, line, MasterBrightnessB );
	}

	private void FinalizeEngine( GPU2D eng, uint[] fb, uint line, ushort mbright )
	{
		uint off = line * 256;
		uint[] src = eng.Output2D;
		uint modeMask = eng.Num == 0 ? 0x3u : 0x1u;
		uint mode = (eng.DispCnt >> 16) & modeMask;

		switch ( mode )
		{
			case 0:
				for ( int i = 0; i < 256; i++ )
					_composeLine[i] = 0x3F3F3F;
				break;

			case 1:
				Array.Copy( src, _composeLine, 256 );
				break;

			case 2:
				{
					int bank = (int)((eng.DispCnt >> 18) & 0x3);
					if ( (VRAMMap_LCDC & (1 << bank)) != 0 )
					{
						byte[] vram = VRAM[bank];
						uint o = line * 512;
						for ( int i = 0; i < 256; i++ )
						{
							ushort color = (ushort)(vram[o] | (vram[o + 1] << 8));
							o += 2;
							uint r = (uint)((color & 0x001F) << 1);
							uint g = (uint)((color & 0x03E0) >> 4);
							uint b = (uint)((color & 0x7C00) >> 9);
							_composeLine[i] = r | (g << 8) | (b << 16);
						}
					}
					else
					{
						for ( int i = 0; i < 256; i++ )
							_composeLine[i] = 0;
					}
					break;
				}

			default:
				for ( int i = 0; i < 256; i++ )
					_composeLine[i] = 0;
				break;
		}

		if ( mode != 0 )
			ApplyMasterBrightness( mbright, _composeLine );

		if ( ScreensEnabled )
		{
			for ( int i = 0; i < 256; i++ )
				fb[off + i] = ExpandColor( _composeLine[i] );
		}
		else
		{
			for ( int i = 0; i < 256; i++ )
				fb[off + i] = 0xFF000000;
		}
	}

	private static void ApplyMasterBrightness( ushort regval, uint[] dst )
	{
		uint modeb = (uint)(regval >> 14);
		if ( modeb == 1 )
		{
			uint factor = (uint)(regval & 0x1F);
			if ( factor > 16 ) factor = 16;
			for ( int i = 0; i < 256; i++ )
				dst[i] = ColorOp.ColorBrightnessUp( dst[i], factor, 0x0 );
		}
		else if ( modeb == 2 )
		{
			uint factor = (uint)(regval & 0x1F);
			if ( factor > 16 ) factor = 16;
			for ( int i = 0; i < 256; i++ )
				dst[i] = ColorOp.ColorBrightnessDown( dst[i], factor, 0xF );
		}
	}

	private static uint ExpandColor( uint c )
	{
		uint r = c & 0x3F;
		uint g = (c >> 8) & 0x3F;
		uint b = (c >> 16) & 0x3F;
		r = (r << 2) | (r >> 4);
		g = (g << 2) | (g >> 4);
		b = (b << 2) | (b >> 4);
		return r | (g << 8) | (b << 16) | 0xFF000000u;
	}
}
