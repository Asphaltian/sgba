HEADER
{
	DevShader = true;
	Description = "NDS 2D sprite atlas prerender";
}

MODES
{
	Default();
}

CS
{
	#include "nds2d_common.hlsl"

	StructuredBuffer<uint> VramOBJ < Attribute( "VramOBJ" ); >;
	StructuredBuffer<uint> PalOBJ < Attribute( "PalOBJ" ); >;
	StructuredBuffer<sOAM> OAMConfig < Attribute( "OAMConfig" ); >;
	RWTexture2D<float4> OutputAtlas < Attribute( "OutputAtlas" ); >;

	int uVRAMMask < Attribute( "VRAMMask" ); >;
	int uNumSprites < Attribute( "NumSprites" ); >;

	int VRAMRead8( int addr )
	{
		int x = addr & 0x3FF;
		int y = ( addr >> 10 ) & uVRAMMask;
		return (int)LoadByte( VramOBJ, ( y << 10 ) + x );
	}

	int VRAMRead16( int addr )
	{
		int lo = VRAMRead8( addr );
		int hi = VRAMRead8( addr + 1 );
		return lo | ( hi << 8 );
	}

	float4 GetOBJPalEntry( int pal, int id )
	{
		uint col15 = LoadU16( PalOBJ, ( pal << 8 ) + id );
		return DecodePalEntry( col15 );
	}

	float4 GetSpritePixel( sOAM spr, int2 coord )
	{
		float4 ret = float4( 0, 0, 0, 0 );

		if ( spr.Type == 0 )
		{
			int tileoffset = spr.TileOffset +
				( ( coord.x >> 3 ) * 32 ) +
				( ( coord.y >> 3 ) * spr.TileStride ) +
				( ( coord.x & 0x7 ) >> 1 ) +
				( ( coord.y & 0x7 ) << 2 );

			int col = VRAMRead8( tileoffset );
			if ( ( coord.x & 1 ) != 0 )
				col >>= 4;
			else
				col &= 0xF;
			col += spr.PalOffset;

			ret = GetOBJPalEntry( 0, col );
			ret.a = ( ( col & 0xF ) == 0 ) ? 0.0 : 1.0;
		}
		else if ( spr.Type == 1 )
		{
			int tileoffset = spr.TileOffset +
				( ( coord.x >> 3 ) * 64 ) +
				( ( coord.y >> 3 ) * spr.TileStride ) +
				( coord.x & 0x7 ) +
				( ( coord.y & 0x7 ) << 3 );

			int col = VRAMRead8( tileoffset );

			ret = GetOBJPalEntry( spr.PalOffset, col );
			ret.a = ( col == 0 ) ? 0.0 : 1.0;
		}
		else
		{
			int tileoffset = spr.TileOffset +
				( coord.x * 2 ) +
				( coord.y * spr.TileStride );

			int col = VRAMRead16( tileoffset );

			ret.r = float( ( col << 1 ) & 0x3E ) / 63.0;
			ret.g = float( ( col >> 4 ) & 0x3E ) / 63.0;
			ret.b = float( ( col >> 9 ) & 0x3E ) / 63.0;
			ret.a = float( col >> 15 );
		}

		return ret;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int slot = ( ( (int)id.y >> 6 ) * 16 ) + ( (int)id.x >> 6 );
		if ( slot >= uNumSprites )
			return;

		sOAM spr = OAMConfig[slot];
		if ( spr.Type >= 3 )
			return;

		int2 local = int2( (int)id.x & 0x3F, (int)id.y & 0x3F );
		if ( local.x >= spr.SizeX || local.y >= spr.SizeY )
			return;

		OutputAtlas[id.xy] = GetSpritePixel( spr, local );
	}
}
