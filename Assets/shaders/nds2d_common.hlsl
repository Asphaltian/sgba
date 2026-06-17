#ifndef NDS2D_COMMON_HLSL
#define NDS2D_COMMON_HLSL

struct sBGConfig
{
	int SizeX;
	int SizeY;
	int Type;
	int PalOffset;
	int TileOffset;
	int MapOffset;
	int Clamp;
	int Pad;
};

struct sOAM
{
	int PositionX;
	int PositionY;
	int FlipX;
	int FlipY;
	int SizeX;
	int SizeY;
	int BoundSizeX;
	int BoundSizeY;
	int OBJMode;
	int Type;
	int PalOffset;
	int TileOffset;
	int TileStride;
	int Rotscale;
	int BGPrio;
	int Mosaic;
};

struct sScanline
{
	int BGOffsetX0;
	int BGOffsetY0;
	int BGOffsetX1;
	int BGOffsetY1;
	int BGOffsetX2;
	int BGOffsetY2;
	int BGOffsetX3;
	int BGOffsetY3;

	int BGRotscale0_0;
	int BGRotscale0_1;
	int BGRotscale0_2;
	int BGRotscale0_3;
	int BGRotscale1_0;
	int BGRotscale1_1;
	int BGRotscale1_2;
	int BGRotscale1_3;

	int BackColor;
	uint WinRegs;
	int WinMask;

	int WinPos0;
	int WinPos1;
	int WinPos2;
	int WinPos3;

	int BGMosaicEnable0;
	int BGMosaicEnable1;
	int BGMosaicEnable2;
	int BGMosaicEnable3;

	int MosaicSize0;
	int MosaicSize1;
	int MosaicSize2;
	int MosaicSize3;
};

int GetBGOffsetX( sScanline s, int bg )
{
	if ( bg == 0 ) return s.BGOffsetX0;
	if ( bg == 1 ) return s.BGOffsetX1;
	if ( bg == 2 ) return s.BGOffsetX2;
	return s.BGOffsetX3;
}

int GetBGOffsetY( sScanline s, int bg )
{
	if ( bg == 0 ) return s.BGOffsetY0;
	if ( bg == 1 ) return s.BGOffsetY1;
	if ( bg == 2 ) return s.BGOffsetY2;
	return s.BGOffsetY3;
}

int GetBGMosaicEnable( sScanline s, int bg )
{
	if ( bg == 0 ) return s.BGMosaicEnable0;
	if ( bg == 1 ) return s.BGMosaicEnable1;
	if ( bg == 2 ) return s.BGMosaicEnable2;
	return s.BGMosaicEnable3;
}

uint LoadByte( StructuredBuffer<uint> buf, int byteAddr )
{
	uint a = (uint)byteAddr;
	return ( buf[a >> 2u] >> ( ( a & 3u ) * 8u ) ) & 0xFFu;
}

uint LoadU16( StructuredBuffer<uint> buf, int u16Index )
{
	uint a = (uint)u16Index * 2u;
	return ( buf[a >> 2u] >> ( ( a & 2u ) * 8u ) ) & 0xFFFFu;
}

float4 DecodePalEntry( uint col15 )
{
	uint r = col15 & 0x1Fu;
	uint g = ( col15 >> 5 ) & 0x1Fu;
	uint b = ( col15 >> 10 ) & 0x1Fu;
	uint a = ( col15 >> 15 ) & 0x1u;

	float4 ret;
	ret.r = ( float( r ) / 31.0 ) * ( 62.0 / 63.0 );
	ret.g = ( float( g ) / 31.0 ) * ( 62.0 / 63.0 ) + ( float( a ) * ( 1.0 / 63.0 ) );
	ret.b = ( float( b ) / 31.0 ) * ( 62.0 / 63.0 );
	ret.a = 1.0;
	return ret;
}

#endif
