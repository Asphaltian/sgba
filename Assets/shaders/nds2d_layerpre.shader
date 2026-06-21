HEADER
{
	DevShader = true;
	Description = "NDS 2D BG layer prerender";
}

MODES
{
	Default();
}

CS
{
	#include "nds2d_common.hlsl"

	StructuredBuffer<uint> VramBG < Attribute( "VramBG" ); >;
	StructuredBuffer<uint> PalBG < Attribute( "PalBG" ); >;
	StructuredBuffer<sBGConfig> BGConfig < Attribute( "BGConfig" ); >;
	RWTexture2D<float4> OutputLayer < Attribute( "OutputLayer" ); >;
	RWTexture2DArray<float4> CaptureOut128 < Attribute( "CaptureOut128" ); >;
	RWTexture2DArray<float4> CaptureOut256 < Attribute( "CaptureOut256" ); >;

	int uVRAMMask < Attribute( "VRAMMask" ); >;
	int uCurBG < Attribute( "CurBG" ); >;
	int uScale < Attribute( "uScale" ); >;

	int VRAMRead8( int addr )
	{
		int x = addr & 0x3FF;
		int y = ( addr >> 10 ) & uVRAMMask;
		return (int)LoadByte( VramBG, ( y << 10 ) + x );
	}

	int VRAMRead16( int addr )
	{
		int lo = VRAMRead8( addr );
		int hi = VRAMRead8( addr + 1 );
		return lo | ( hi << 8 );
	}

	float4 GetBGPalEntry( sBGConfig cfg, int pal, int id )
	{
		int row = cfg.PalOffset + pal;
		uint col15 = LoadU16( PalBG, ( row << 8 ) + id );
		return DecodePalEntry( col15 );
	}

	float4 GetBGLayerPixel( sBGConfig cfg, int2 coord )
	{
		float4 ret = float4( 0, 0, 0, 0 );

		if ( cfg.Type == 0 )
		{
			int mapoffset = cfg.MapOffset +
				( ( ( coord.x >> 3 ) & 0x1F ) << 1 ) +
				( ( ( coord.y >> 3 ) & 0x1F ) << 6 );

			if ( cfg.SizeY == 512 )
			{
				if ( cfg.SizeX == 512 )
					mapoffset += ( ( ( coord.x >> 8 ) & 0x1 ) << 11 ) + ( ( ( coord.y >> 8 ) & 0x1 ) << 12 );
				else
					mapoffset += ( ( ( coord.y >> 8 ) & 0x1 ) << 11 );
			}
			else if ( cfg.SizeX == 512 )
			{
				mapoffset += ( ( ( coord.x >> 8 ) & 0x1 ) << 11 );
			}

			int mapval = VRAMRead16( mapoffset );
			int tileoffset = ( cfg.TileOffset << 1 ) + ( ( mapval & 0x3FF ) << 6 );

			if ( ( mapval & ( 1 << 10 ) ) != 0 )
				tileoffset += ( 7 - ( coord.x & 0x7 ) );
			else
				tileoffset += ( coord.x & 0x7 );

			if ( ( mapval & ( 1 << 11 ) ) != 0 )
				tileoffset += ( ( 7 - ( coord.y & 0x7 ) ) << 3 );
			else
				tileoffset += ( ( coord.y & 0x7 ) << 3 );

			int col = VRAMRead8( tileoffset >> 1 );
			if ( ( tileoffset & 0x1 ) != 0 )
				col >>= 4;
			else
				col &= 0xF;
			col += ( ( mapval >> 12 ) << 4 );

			ret = GetBGPalEntry( cfg, 0, col );
			ret.a = ( ( col & 0xF ) == 0 ) ? 0.0 : 1.0;
		}
		else if ( cfg.Type == 1 )
		{
			int mapoffset = cfg.MapOffset +
				( ( ( coord.x >> 3 ) & 0x1F ) << 1 ) +
				( ( ( coord.y >> 3 ) & 0x1F ) << 6 );

			if ( cfg.SizeY == 512 )
			{
				if ( cfg.SizeX == 512 )
					mapoffset += ( ( ( coord.x >> 8 ) & 0x1 ) << 11 ) + ( ( ( coord.y >> 8 ) & 0x1 ) << 12 );
				else
					mapoffset += ( ( ( coord.y >> 8 ) & 0x1 ) << 11 );
			}
			else if ( cfg.SizeX == 512 )
			{
				mapoffset += ( ( ( coord.x >> 8 ) & 0x1 ) << 11 );
			}

			int mapval = VRAMRead16( mapoffset );
			int tileoffset = cfg.TileOffset + ( ( mapval & 0x3FF ) << 6 );

			if ( ( mapval & ( 1 << 10 ) ) != 0 )
				tileoffset += ( 7 - ( coord.x & 0x7 ) );
			else
				tileoffset += ( coord.x & 0x7 );

			if ( ( mapval & ( 1 << 11 ) ) != 0 )
				tileoffset += ( ( 7 - ( coord.y & 0x7 ) ) << 3 );
			else
				tileoffset += ( ( coord.y & 0x7 ) << 3 );

			int col = VRAMRead8( tileoffset );
			int pal = ( cfg.PalOffset != 0 ) ? ( mapval >> 12 ) : 0;

			ret = GetBGPalEntry( cfg, pal, col );
			ret.a = ( col == 0 ) ? 0.0 : 1.0;
		}
		else if ( cfg.Type == 2 )
		{
			int mapoffset = cfg.MapOffset +
				( coord.x >> 3 ) +
				( ( coord.y >> 3 ) * ( cfg.SizeX >> 3 ) );

			int mapval = VRAMRead8( mapoffset );
			int tileoffset = cfg.TileOffset + ( mapval << 6 );

			tileoffset += ( ( coord.y & 0x7 ) << 3 );
			tileoffset += ( coord.x & 0x7 );

			int col = VRAMRead8( tileoffset );

			ret = GetBGPalEntry( cfg, 0, col );
			ret.a = ( col == 0 ) ? 0.0 : 1.0;
		}
		else if ( cfg.Type == 3 )
		{
			int mapoffset = cfg.MapOffset +
				( ( ( coord.x >> 3 ) + ( ( coord.y >> 3 ) * ( cfg.SizeX >> 3 ) ) ) << 1 );

			int mapval = VRAMRead16( mapoffset );
			int tileoffset = cfg.TileOffset + ( ( mapval & 0x3FF ) << 6 );

			if ( ( mapval & ( 1 << 10 ) ) != 0 )
				tileoffset += ( 7 - ( coord.x & 0x7 ) );
			else
				tileoffset += ( coord.x & 0x7 );

			if ( ( mapval & ( 1 << 11 ) ) != 0 )
				tileoffset += ( ( 7 - ( coord.y & 0x7 ) ) << 3 );
			else
				tileoffset += ( ( coord.y & 0x7 ) << 3 );

			int col = VRAMRead8( tileoffset );
			int pal = ( cfg.PalOffset != 0 ) ? ( mapval >> 12 ) : 0;

			ret = GetBGPalEntry( cfg, pal, col );
			ret.a = ( col == 0 ) ? 0.0 : 1.0;
		}
		else if ( cfg.Type == 4 )
		{
			int mapoffset = cfg.MapOffset + coord.x + ( coord.y * cfg.SizeX );
			int col = VRAMRead8( mapoffset );

			ret = GetBGPalEntry( cfg, 0, col );
			ret.a = ( col == 0 ) ? 0.0 : 1.0;
		}
		else if ( cfg.Type == 5 )
		{
			if ( cfg.CapBlock >= 0 )
			{
				int bh = cfg.CapSize == 0 ? 128 : 256;
				int cyc = coord.y + cfg.CapYOffset;
				if ( cyc >= bh ) cyc -= bh;
				int px = coord.x * uScale;
				int py = cyc * uScale;
				if ( cfg.CapSize == 0 )
					ret = CaptureOut128[int3( px, py, cfg.CapBlock )];
				else
					ret = CaptureOut256[int3( px, py, cfg.CapBlock >> 2 )];
			}
			else
			{
				int mapoffset = cfg.MapOffset + ( ( coord.x + ( coord.y * cfg.SizeX ) ) << 1 );
				int col = VRAMRead16( mapoffset );

				ret.r = float( ( col << 1 ) & 0x3E ) / 63.0;
				ret.g = float( ( col >> 4 ) & 0x3E ) / 63.0;
				ret.b = float( ( col >> 9 ) & 0x3E ) / 63.0;
				ret.a = float( col >> 15 );
			}
		}

		return ret;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		sBGConfig cfg = BGConfig[uCurBG];
		if ( (int)id.x >= cfg.SizeX || (int)id.y >= cfg.SizeY )
			return;

		OutputLayer[id.xy] = GetBGLayerPixel( cfg, int2( id.xy ) );
	}
}
