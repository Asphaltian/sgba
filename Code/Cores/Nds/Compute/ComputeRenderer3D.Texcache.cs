namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private static int TextureWidth( uint texParam ) => 8 << (int)((texParam >> 20) & 0x7);
	private static int TextureHeight( uint texParam ) => 8 << (int)((texParam >> 23) & 0x7);

	private ushort ReadTexU16( uint addr ) => (ushort)_gpu2d.ReadVRAM_Texture( addr, 2 );
	private byte ReadTexU8( uint addr ) => (byte)_gpu2d.ReadVRAM_Texture( addr, 1 );
	private uint ReadTexU32( uint addr ) => _gpu2d.ReadVRAM_Texture( addr, 4 );
	private ushort ReadPalU16( uint addr ) => (ushort)_gpu2d.ReadVRAM_TexPal( addr, 2 );

	private static uint ConvertRGB5ToRGB6( ushort val )
	{
		uint r = (uint)((val & 0x1F) << 1);
		uint g = (uint)((val & 0x3E0) >> 4);
		uint b = (uint)((val & 0x7C00) >> 9);
		if ( r != 0 ) r++;
		if ( g != 0 ) g++;
		if ( b != 0 ) b++;
		return r | (g << 8) | (b << 16);
	}

	private static ushort ColorAvg( ushort c0, ushort c1 )
	{
		uint r0 = (uint)(c0 & 0x001F), g0 = (uint)(c0 & 0x03E0), b0 = (uint)(c0 & 0x7C00);
		uint r1 = (uint)(c1 & 0x001F), g1 = (uint)(c1 & 0x03E0), b1 = (uint)(c1 & 0x7C00);
		uint r = (r0 + r1) >> 1;
		uint g = ((g0 + g1) >> 1) & 0x03E0;
		uint b = ((b0 + b1) >> 1) & 0x7C00;
		return (ushort)(r | g | b);
	}

	private static ushort Color5of3( ushort c0, ushort c1 )
	{
		uint r0 = (uint)(c0 & 0x001F), g0 = (uint)(c0 & 0x03E0), b0 = (uint)(c0 & 0x7C00);
		uint r1 = (uint)(c1 & 0x001F), g1 = (uint)(c1 & 0x03E0), b1 = (uint)(c1 & 0x7C00);
		uint r = (r0 * 5 + r1 * 3) >> 3;
		uint g = ((g0 * 5 + g1 * 3) >> 3) & 0x03E0;
		uint b = ((b0 * 5 + b1 * 3) >> 3) & 0x7C00;
		return (ushort)(r | g | b);
	}

	private static ushort Color3of5( ushort c0, ushort c1 )
	{
		uint r0 = (uint)(c0 & 0x001F), g0 = (uint)(c0 & 0x03E0), b0 = (uint)(c0 & 0x7C00);
		uint r1 = (uint)(c1 & 0x001F), g1 = (uint)(c1 & 0x03E0), b1 = (uint)(c1 & 0x7C00);
		uint r = (r0 * 3 + r1 * 5) >> 3;
		uint g = ((g0 * 3 + g1 * 5) >> 3) & 0x03E0;
		uint b = ((b0 * 3 + b1 * 5) >> 3) & 0x7C00;
		return (ushort)(r | g | b);
	}

	private ulong HashTexRange( ulong h, uint addr, uint size )
	{
		for ( uint o = 0; o < size; o += 4 )
			h = (h ^ ReadTexU32( addr + o )) * 1099511628211UL;
		return h;
	}

	private ulong HashPalRange( ulong h, uint addr, uint size )
	{
		for ( uint o = 0; o < size; o += 4 )
			h = (h ^ _gpu2d.ReadVRAM_TexPal( addr + o, 4 )) * 1099511628211UL;
		return h;
	}

	private ulong ComputeTexSourceHash( uint texParam, uint texPalette )
	{
		int width = TextureWidth( texParam );
		int height = TextureHeight( texParam );
		uint fmt = (texParam >> 26) & 0x7;
		uint addr = (texParam & 0xFFFF) * 8;
		uint palBase = texPalette & 0x1FFF;

		ulong h = 1469598103934665603UL;

		if ( fmt == 7 )
		{
			h = HashTexRange( h, addr, (uint)(width * height * 2) );
		}
		else if ( fmt == 5 )
		{
			uint slot1addr = 0x20000 + ((addr & 0x1FFFC) >> 1);
			if ( addr >= 0x40000 ) slot1addr += 0x10000;
			h = HashTexRange( h, addr, (uint)(width * height / 16 * 4) );
			h = HashTexRange( h, slot1addr, (uint)(width * height / 16 * 2) );
			h = HashPalRange( h, palBase * 16, 0x10000 );
		}
		else
		{
			uint palAddr = palBase * 16;
			uint texSize;
			uint numPalEntries;
			switch ( fmt )
			{
				case 1: texSize = (uint)(width * height); numPalEntries = 32; break;
				case 6: texSize = (uint)(width * height); numPalEntries = 8; break;
				case 2: texSize = (uint)(width * height / 4); numPalEntries = 4; palAddr >>= 1; break;
				case 3: texSize = (uint)(width * height / 2); numPalEntries = 16; break;
				default: texSize = (uint)(width * height); numPalEntries = 256; break;
			}
			palAddr &= 0x1FFFF;
			h = HashTexRange( h, addr, texSize );
			h = HashPalRange( h, palAddr, numPalEntries * 2 );
		}

		return h;
	}

	private void DecodeTexture( uint texParam, uint texPalette, uint[] output )
	{
		int width = TextureWidth( texParam );
		int height = TextureHeight( texParam );
		uint fmt = (texParam >> 26) & 0x7;
		uint addr = (texParam & 0xFFFF) * 8;
		uint palBase = texPalette & 0x1FFF;

		if ( fmt == 7 )
		{
			for ( int i = 0; i < width * height; i++ )
			{
				ushort value = ReadTexU16( addr + (uint)i * 2 );
				output[i] = ConvertRGB5ToRGB6( value ) | ((value & 0x8000) != 0 ? 0x1F000000u : 0);
			}
		}
		else if ( fmt == 5 )
		{
			uint slot1addr = 0x20000 + ((addr & 0x1FFFC) >> 1);
			if ( addr >= 0x40000 ) slot1addr += 0x10000;
			uint palAddr = palBase * 16;
			DecodeCompressed( width, height, output, addr, slot1addr, palAddr );
		}
		else if ( fmt == 1 || fmt == 6 )
		{
			uint palAddr = (palBase * 16) & 0x1FFFF;
			int xbits = fmt == 1 ? 3 : 5;
			int ybits = fmt == 1 ? 5 : 3;
			for ( int y = 0; y < height; y++ )
			{
				for ( int x = 0; x < width; x++ )
				{
					byte val = ReadTexU8( addr + (uint)(x + y * width) );
					uint idx = (uint)(val & ((1 << ybits) - 1));
					ushort color = ReadPalU16( palAddr + idx * 2 );
					uint alpha = (uint)((val >> ybits) & ((1 << xbits) - 1));
					if ( xbits != 5 ) alpha = alpha * 4 + alpha / 2;
					output[x + y * width] = ConvertRGB5ToRGB6( color ) | (alpha << 24);
				}
			}
		}
		else
		{
			uint palAddr = palBase * 16;
			int colorBits = fmt switch { 2 => 2, 3 => 4, _ => 8 };
			if ( fmt == 2 ) palAddr >>= 1;
			palAddr &= 0x1FFFF;
			bool color0Transparent = (texParam & (1 << 29)) != 0;

			int per = 16 / colorBits;
			for ( int y = 0; y < height; y++ )
			{
				for ( int x = 0; x < width / per; x++ )
				{
					ushort val = ReadTexU16( addr + (uint)(2 * (x + y * (width / per))) );
					for ( int i = 0; i < per; i++ )
					{
						uint index = (uint)(val & ((1 << colorBits) - 1));
						val >>= colorBits;
						ushort color = ReadPalU16( palAddr + index * 2 );
						bool transparent = color0Transparent && index == 0;
						output[x * per + y * width + i] = ConvertRGB5ToRGB6( color ) | (transparent ? 0 : 0x1F000000u);
					}
				}
			}
		}
	}

	private void DecodeCompressed( int width, int height, uint[] output, uint addr, uint addrAux, uint palAddr )
	{
		for ( int y = 0; y < height / 4; y++ )
		{
			for ( int x = 0; x < width / 4; x++ )
			{
				uint data = ReadTexU32( addr + (uint)((x + y * (width / 4)) * 4) );
				ushort auxData = ReadTexU16( addrAux + (uint)((x + y * (width / 4)) * 2) );

				uint paletteOffset = palAddr + (uint)(auxData & 0x3FFF) * 4;
				ushort color0 = (ushort)(ReadPalU16( paletteOffset ) | 0x8000);
				ushort color1 = (ushort)(ReadPalU16( paletteOffset + 2 ) | 0x8000);
				ushort color2 = (ushort)(ReadPalU16( paletteOffset + 4 ) | 0x8000);
				ushort color3 = (ushort)(ReadPalU16( paletteOffset + 6 ) | 0x8000);

				switch ( (auxData >> 14) & 0x3 )
				{
					case 0:
						color3 = 0;
						break;
					case 1:
						color2 = (ushort)(ColorAvg( color0, color1 ) | 0x8000);
						color3 = 0;
						break;
					case 2:
						break;
					case 3:
						color2 = (ushort)(Color5of3( color0, color1 ) | 0x8000);
						color3 = (ushort)(Color3of5( color0, color1 ) | 0x8000);
						break;
				}

				ulong packed = color0 | ((ulong)color1 << 16) | ((ulong)color2 << 32) | ((ulong)color3 << 48);

				for ( int j = 0; j < 4; j++ )
				{
					for ( int i = 0; i < 4; i++ )
					{
						int colorIdx = 16 * (int)((data >> (2 * (i + j * 4))) & 0x3);
						ushort color = (ushort)((packed >> colorIdx) & 0xFFFF);
						output[x * 4 + i + (y * 4 + j) * width] = ConvertRGB5ToRGB6( color ) | ((color & 0x8000) != 0 ? 0x1F000000u : 0);
					}
				}
			}
		}
	}
}
