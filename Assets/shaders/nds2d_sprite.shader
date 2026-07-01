HEADER
{
	DevShader = true;
	Description = "NDS 2D sprite layer";
}

MODES
{
	Default();
}

CS
{
	#include "nds2d_common.hlsl"

	Texture2D<float4> SpriteAtlas < Attribute( "SpriteAtlas" ); >;
	StructuredBuffer<sOAM> OAMConfig < Attribute( "OAMConfig" ); >;
	StructuredBuffer<int4> Rotscale < Attribute( "Rotscale" ); >;

	RWTexture2D<float4> OBJColor < Attribute( "OBJColor" ); >;
	RWTexture2D<float4> OBJFlags < Attribute( "OBJFlags" ); >;

	int uNumSprites < Attribute( "NumSprites" ); >;
	int uScaleFactor < Attribute( "ScaleFactor" ); >;
	int uWindowEnable < Attribute( "WindowEnable" ); >;
	int uOutputW < Attribute( "OutputW" ); >;
	int uOutputH < Attribute( "OutputH" ); >;

	float4 SampleAtlas( int sprite, int2 coord )
	{
		int2 basecoord = int2( ( sprite & 0xF ) * 64, ( sprite >> 4 ) * 64 );
		return SpriteAtlas.Load( int3( basecoord + coord, 0 ) );
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( (int)id.x >= uOutputW || (int)id.y >= uOutputH )
			return;

		OBJColor[id.xy] = float4( 0, 0, 0, 0 );
		OBJFlags[id.xy] = float4( 0, 0, 0, 0 );

		float2 nativef = ( float2( id.xy ) + 0.5 ) / float( uScaleFactor );
		int sx = (int)nativef.x;
		int sy = (int)nativef.y;

		int bestZ = 0x7FFFFFFF;
		float4 bestColor = float4( 0, 0, 0, 0 );
		int bestBlend = 0;
		int bestMosaic = 0;
		int bestPrio = 0;
		bool found = false;
		bool objwin = false;

		for ( int i = 0; i < uNumSprites; i++ )
		{
			sOAM spr = OAMConfig[i];

			bool iswin = ( spr.OBJMode == 2 );
			if ( iswin && ( uWindowEnable == 0 ) )
				continue;

			if ( spr.Type >= 5 )
				continue;

			int z = ( spr.BGPrio * 128 ) + i;
			if ( ( !iswin ) && found && ( z >= bestZ ) )
				continue;

			int boundwidth = spr.BoundSizeX;
			int boundheight = spr.BoundSizeY;

			int relX = sx - spr.PositionX;
			if ( relX < 0 || relX >= boundwidth )
				continue;

			int relY = -1;
			int relY0 = sy - spr.PositionY;
			int relY1 = sy - ( spr.PositionY & 0xFF );
			if ( relY0 >= 0 && relY0 < boundheight )
				relY = relY0;
			else if ( relY1 >= 0 && relY1 < boundheight )
				relY = relY1;
			else
				continue;

			int coordx;
			int coordy;

			if ( spr.Rotscale != -1 )
			{
				float fcx = ( float( relX ) + 0.5 ) - ( float( boundwidth ) * 0.5 );
				float fcy = ( float( relY ) + 0.5 ) - ( float( boundheight ) * 0.5 );

				int4 rs = Rotscale[spr.Rotscale];
				float a = float( rs.x ) / 256.0;
				float b = float( rs.y ) / 256.0;
				float c = float( rs.z ) / 256.0;
				float d = float( rs.w ) / 256.0;

				float cx = ( fcx * a ) + ( fcy * b ) + ( float( spr.SizeX ) * 0.5 );
				float cy = ( fcx * c ) + ( fcy * d ) + ( float( spr.SizeY ) * 0.5 );

				if ( cx < 0.0 || cy < 0.0 || cx >= float( spr.SizeX ) || cy >= float( spr.SizeY ) )
					continue;

				coordx = (int)cx;
				coordy = (int)cy;
			}
			else
			{
				coordx = ( spr.FlipX != 0 ) ? ( spr.SizeX - 1 - relX ) : relX;
				coordy = ( spr.FlipY != 0 ) ? ( spr.SizeY - 1 - relY ) : relY;
			}

			float4 col = SampleAtlas( i, int2( coordx, coordy ) );
			if ( col.a == 0.0 )
				continue;

			if ( iswin )
			{
				objwin = true;
				continue;
			}

			if ( spr.OBJMode == 3 )
				col.a = float( spr.PalOffset ) / 31.0;
			else
				col.a = 1.0;

			int blend = 0;
			if ( spr.OBJMode == 1 )
				blend = 1;
			else if ( spr.OBJMode == 3 )
				blend = 2;

			bestZ = z;
			bestColor = col;
			bestBlend = blend;
			bestMosaic = ( spr.Mosaic != 0 ) ? 1 : 0;
			bestPrio = spr.BGPrio;
			found = true;
		}

		if ( found )
			OBJColor[id.xy] = bestColor;

		OBJFlags[id.xy] = float4(
			float( bestBlend ) / 255.0,
			float( bestMosaic ),
			objwin ? 1.0 : 0.0,
			float( bestPrio ) / 255.0 );
	}
}
