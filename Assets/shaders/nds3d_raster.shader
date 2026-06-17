HEADER
{
	DevShader = true;
	Description = "NDS 3D Rasterise";
}

MODES
{
	Default();
}

CS
{
	DynamicCombo( D_WBUFFER, 0..1, Sys( ALL ) );
	DynamicCombo( D_TEXTURE, 0..1, Sys( ALL ) );
	DynamicCombo( D_BLEND, 0..3, Sys( ALL ) );
	DynamicCombo( D_SHADOWMASK, 0..1, Sys( ALL ) );

	#define YFAC 8
	#include "nds3d_common.hlsl"

	StructuredBuffer<Polygon> RenderPolygons < Attribute( "RenderPolygons" ); >;
	StructuredBuffer<XSpanSetup> XSpanSetups < Attribute( "XSpanSetups" ); >;
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	StructuredBuffer<uint> WorkDesc < Attribute( "WorkDesc" ); >;
	StructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;

	RWStructuredBuffer<uint> TileColor < Attribute( "TileColor" ); >;
	RWStructuredBuffer<uint> TileDepth < Attribute( "TileDepth" ); >;
	RWStructuredBuffer<uint> TileAttr < Attribute( "TileAttr" ); >;

	int TileSize < Attribute( "TileSize" ); >;
	int MaxWorkTiles < Attribute( "MaxWorkTiles" ); >;
	int CurVariant < Attribute( "CurVariant" ); >;

	#if D_TEXTURE
	StructuredBuffer<uint> CurrentTexture < Attribute( "CurrentTexture" ); >;
	int TexWidth < Attribute( "TexWidth" ); >;
	int TexHeight < Attribute( "TexHeight" ); >;
	int TexStride < Attribute( "TexStride" ); >;
	#endif

	static const uint SortedWorkOffsetStart = 1024u;

	int WrapCoord( int c, int size, bool wrap, bool mirror )
	{
		if ( !wrap )
			return clamp( c, 0, size - 1 );
		if ( !mirror )
			return c & ( size - 1 );
		int t = c & ( ( size << 1 ) - 1 );
		return ( t >= size ) ? ( ( ( size << 1 ) - 1 ) - t ) : t;
	}

	int CalcYFactorX( XSpanSetup span, int x )
	{
		x -= span.X0;
		if ( span.X0 != span.X1 )
		{
			uint numLo = uint( x ) * uint( span.W0 );
			uint numHi = 0u;
			numHi |= numLo >> (32u - YFAC);
			numLo <<= YFAC;

			uint den = uint( x ) * uint( span.W0 ) + uint( span.X1 - span.X0 - x ) * uint( span.W1 );

			if ( den == 0u )
				return 0;
			return int( Div64_32_32( numHi, numLo, den ) );
		}
		return 0;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 groupId : SV_GroupID, uint3 localId : SV_GroupThreadID )
	{
		uint workIdx = groupId.z;
		uint sortedBase = uint( MaxWorkTiles ) + BinResult[SortedWorkOffsetStart + uint( CurVariant )] + workIdx;
		uint descX = WorkDesc[sortedBase * 2u + 0u];
		uint descY = WorkDesc[sortedBase * 2u + 1u];

		Polygon polygon = RenderPolygons[descY & 0x7FFu];
		int2 position = int2( int( descX & 0xFFFFu ), int( descX >> 16 ) ) + int2( localId.xy );
		int tileOffset = int( (descY >> 11) & 0x1FFFFFu ) * TileSize * TileSize + TileSize * int( localId.y ) + int( localId.x );

		uint color = 0u;
		if ( position.y >= polygon.YTop && position.y < polygon.YBot )
		{
			XSpanSetup xspan = XSpanSetups[polygon.FirstXSpan + (position.y - polygon.YTop)];

			bool insideLeftEdge = position.x < xspan.InsideStart;
			bool insideRightEdge = position.x >= xspan.InsideEnd;
			bool insidePolygonInside = !insideLeftEdge && !insideRightEdge;

			if ( position.x >= xspan.X0 && position.x < xspan.X1
				&& ((insideLeftEdge && (xspan.Flags & XSpanSetup_FillLeft) != 0u)
					|| (insideRightEdge && (xspan.Flags & XSpanSetup_FillRight) != 0u)
					|| (insidePolygonInside && (xspan.Flags & XSpanSetup_FillInside) != 0u)) )
			{
				uint attr = 0u;
				if ( position.y == polygon.YTop )
					attr |= 0x4u;
				else if ( position.y == polygon.YBot - 1 )
					attr |= 0x8u;

				if ( insideLeftEdge )
				{
					attr |= 0x1u;
					int cov = xspan.EdgeCovL;
					if ( cov < 0 )
					{
						int xcov = xspan.CovLInitial + (xspan.EdgeCovL & 0x3FF) * (position.x - xspan.X0);
						cov = min( xcov >> 5, 31 );
					}
					attr |= uint( cov ) << 8;
				}
				else if ( insideRightEdge )
				{
					attr |= 0x2u;
					int cov = xspan.EdgeCovR;
					if ( cov < 0 )
					{
						int xcov = xspan.CovRInitial + (xspan.EdgeCovR & 0x3FF) * (position.x - xspan.InsideEnd);
						cov = max( 0x1F - (xcov >> 5), 0 );
					}
					attr |= uint( cov ) << 8;
				}

				uint z;
				int u;
				int v;
				int vr;
				int vg;
				int vb;

				if ( xspan.X0 == xspan.X1 )
				{
					z = uint( xspan.Z0 );
					u = xspan.TexcoordU0;
					v = xspan.TexcoordV0;
					vr = xspan.ColorR0;
					vg = xspan.ColorG0;
					vb = xspan.ColorB0;
				}
				else
				{
					int ifactor = CalcYFactorX( xspan, position.x );
					int idiff = xspan.X1 - xspan.X0;
					int i = position.x - xspan.X0;

					#if D_WBUFFER
						z = InterpolateZWBuffer( xspan.Z0, xspan.Z1, ifactor, true );
					#else
						z = InterpolateZZBuffer( xspan.Z0, xspan.Z1, i, xspan.XRecip, idiff, false );
					#endif

					if ( (xspan.Flags & XSpanSetup_Linear) == 0u )
					{
						u = InterpolateAttrPersp( xspan.TexcoordU0, xspan.TexcoordU1, ifactor );
						v = InterpolateAttrPersp( xspan.TexcoordV0, xspan.TexcoordV1, ifactor );
						vr = InterpolateAttrPersp( xspan.ColorR0, xspan.ColorR1, ifactor );
						vg = InterpolateAttrPersp( xspan.ColorG0, xspan.ColorG1, ifactor );
						vb = InterpolateAttrPersp( xspan.ColorB0, xspan.ColorB1, ifactor );
					}
					else
					{
						u = InterpolateAttrLinear( xspan.TexcoordU0, xspan.TexcoordU1, i, xspan.XRecip, idiff, true );
						v = InterpolateAttrLinear( xspan.TexcoordV0, xspan.TexcoordV1, i, xspan.XRecip, idiff, true );
						vr = InterpolateAttrLinear( xspan.ColorR0, xspan.ColorR1, i, xspan.XRecip, idiff, true );
						vg = InterpolateAttrLinear( xspan.ColorG0, xspan.ColorG1, i, xspan.XRecip, idiff, true );
						vb = InterpolateAttrLinear( xspan.ColorB0, xspan.ColorB1, i, xspan.XRecip, idiff, true );
					}
				}

				#if D_SHADOWMASK
					color = 0xFFFFFFFFu;
					TileDepth[tileOffset] = z;
				#else
					vr >>= 3;
					vg >>= 3;
					vb >>= 3;

					uint r;
					uint g;
					uint b;
					uint a = 0u;
					uint polyalpha = (polygon.Attr >> 16) & 0x1Fu;

					#if D_BLEND == 2
						uint tooncolorM = MetaUniform[4u + uint( vr >> 1 ) * 4u];
						vr = int( tooncolorM & 0xFFu );
						vg = int( (tooncolorM >> 8) & 0xFFu );
						vb = int( (tooncolorM >> 16) & 0xFFu );
					#endif
					#if D_BLEND == 3
						vg = vr;
						vb = vr;
					#endif

					#if !D_TEXTURE
						a = polyalpha;
					#endif
					r = uint( vr );
					g = uint( vg );
					b = uint( vb );

					#if D_TEXTURE
						int2 texel = int2( u, v ) >> 4;
						texel.x = WrapCoord( texel.x, TexWidth, ((polygon.TexParam >> 16) & 1u) != 0u, ((polygon.TexParam >> 18) & 1u) != 0u );
						texel.y = WrapCoord( texel.y, TexHeight, ((polygon.TexParam >> 17) & 1u) != 0u, ((polygon.TexParam >> 19) & 1u) != 0u );
						uint packed = CurrentTexture[int( polygon.TextureLayer ) * TexStride * TexStride + texel.y * TexStride + texel.x];
						uint4 texcolor = uint4( packed & 0xFFu, (packed >> 8) & 0xFFu, (packed >> 16) & 0xFFu, (packed >> 24) & 0xFFu );

						#if D_BLEND == 1
							if ( texcolor.a == 31u )
							{
								r = texcolor.r;
								g = texcolor.g;
								b = texcolor.b;
							}
							else if ( texcolor.a > 0u )
							{
								r = ((texcolor.r * texcolor.a) + (uint( vr ) * (31u - texcolor.a))) >> 5;
								g = ((texcolor.g * texcolor.a) + (uint( vg ) * (31u - texcolor.a))) >> 5;
								b = ((texcolor.b * texcolor.a) + (uint( vb ) * (31u - texcolor.a))) >> 5;
							}
							a = polyalpha;
						#else
							r = ((texcolor.r + 1u) * (uint( vr ) + 1u) - 1u) >> 6;
							g = ((texcolor.g + 1u) * (uint( vg ) + 1u) - 1u) >> 6;
							b = ((texcolor.b + 1u) * (uint( vb ) + 1u) - 1u) >> 6;
							a = ((texcolor.a + 1u) * (polyalpha + 1u) - 1u) >> 5;
						#endif
					#endif

					#if D_BLEND == 3
						uint tooncolorH = MetaUniform[4u + uint( vr >> 1 ) * 4u];
						r = min( r + (tooncolorH & 0xFFu), 63u );
						g = min( g + ((tooncolorH >> 8) & 0xFFu), 63u );
						b = min( b + ((tooncolorH >> 16) & 0xFFu), 63u );
					#endif

					if ( polyalpha == 0u )
						a = 31u;

					uint alphaRef = MetaUniform[2];
					if ( a > alphaRef )
					{
						color = r | (g << 8) | (b << 16) | (a << 24);
						TileDepth[tileOffset] = z;
						TileAttr[tileOffset] = attr;
					}
				#endif
			}
		}

		TileColor[tileOffset] = color;
	}
}
