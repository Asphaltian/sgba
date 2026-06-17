namespace Emulotl.Nds;

public static class ColorOp
{
	public static uint ColorBlend4( uint val1, uint val2, uint eva, uint evb )
	{
		uint r = (((val1 & 0x00003F) * eva) + ((val2 & 0x00003F) * evb) + 0x000008) >> 4;
		uint g = ((((val1 & 0x003F00) * eva) + ((val2 & 0x003F00) * evb) + 0x000800) >> 4) & 0x007F00;
		uint b = ((((val1 & 0x3F0000) * eva) + ((val2 & 0x3F0000) * evb) + 0x080000) >> 4) & 0x7F0000;

		if ( r > 0x00003F ) r = 0x00003F;
		if ( g > 0x003F00 ) g = 0x003F00;
		if ( b > 0x3F0000 ) b = 0x3F0000;

		return r | g | b | 0xFF000000;
	}

	public static uint ColorBlend5( uint val1, uint val2 )
	{
		uint eva = ((val1 >> 24) & 0x1F) + 1;
		uint evb = 32 - eva;

		if ( eva == 32 ) return val1;

		uint r = (((val1 & 0x00003F) * eva) + ((val2 & 0x00003F) * evb) + 0x000010) >> 5;
		uint g = ((((val1 & 0x003F00) * eva) + ((val2 & 0x003F00) * evb) + 0x001000) >> 5) & 0x007F00;
		uint b = ((((val1 & 0x3F0000) * eva) + ((val2 & 0x3F0000) * evb) + 0x100000) >> 5) & 0x7F0000;

		if ( r > 0x00003F ) r = 0x00003F;
		if ( g > 0x003F00 ) g = 0x003F00;
		if ( b > 0x3F0000 ) b = 0x3F0000;

		return r | g | b | 0xFF000000;
	}

	public static uint ColorBrightnessUp( uint val, uint factor, uint bias )
	{
		uint rb = val & 0x3F003F;
		uint g = val & 0x003F00;

		rb += ((((0x3F003F - rb) * factor) + (bias * 0x010001)) >> 4) & 0x3F003F;
		g += ((((0x003F00 - g) * factor) + (bias * 0x000100)) >> 4) & 0x003F00;

		return rb | g | 0xFF000000;
	}

	public static uint ColorBrightnessDown( uint val, uint factor, uint bias )
	{
		uint rb = val & 0x3F003F;
		uint g = val & 0x003F00;

		rb -= (((rb * factor) + (bias * 0x010001)) >> 4) & 0x3F003F;
		g -= (((g * factor) + (bias * 0x000100)) >> 4) & 0x003F00;

		return rb | g | 0xFF000000;
	}
}
