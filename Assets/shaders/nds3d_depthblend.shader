HEADER
{
	DevShader = true;
	Description = "NDS 3D Depth Test + Blend (sort-middle merge)";
}

MODES
{
	Default();
}

CS
{
	DynamicCombo( D_WBUFFER, 0..1, Sys( ALL ) );

	#include "nds3d_common.hlsl"

	StructuredBuffer<Polygon> RenderPolygons < Attribute( "RenderPolygons" ); >;
	StructuredBuffer<uint> TileColor < Attribute( "TileColor" ); >;
	StructuredBuffer<uint> TileDepth < Attribute( "TileDepth" ); >;
	StructuredBuffer<uint> TileAttr < Attribute( "TileAttr" ); >;
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	StructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;
	RWStructuredBuffer<uint> FinalTile < Attribute( "FinalTile" ); >;

	int TileSize < Attribute( "TileSize" ); >;
	int TilesPerLine < Attribute( "TilesPerLine" ); >;
	int TileLines < Attribute( "TileLines" ); >;
	int ScreenWidth < Attribute( "ScreenWidth" ); >;
	int ScreenHeight < Attribute( "ScreenHeight" ); >;

	static const uint HeaderUints = 1284u;
	static const int BinStride = 64;
	static const int CoarseBinStride = 2;

	bool DepthInRange( bool equalDepthTest, uint dstDepth, uint tileDepth )
	{
		#if D_WBUFFER
			return equalDepthTest ? (dstDepth - tileDepth + 0xFFu <= 0x1FEu) : (tileDepth < dstDepth);
		#else
			return equalDepthTest ? (dstDepth - tileDepth + 0x200u <= 0x400u) : (tileDepth < dstDepth);
		#endif
	}

	void PlotTranslucent( inout uint color, inout uint depth, inout uint attr, bool isShadow, uint tileColor, uint srcA, uint tileDepth, uint srcAttr, bool writeDepth )
	{
		uint blendAttr = (srcAttr & 0xE0F0u) | ((srcAttr >> 8) & 0xFF0000u) | (1u << 22) | (attr & 0xFF001F0Fu);

		bool doBlend = ((!isShadow || (attr & (1u << 22)) != 0u))
			? (attr & 0x007F0000u) != (blendAttr & 0x007F0000u)
			: (attr & 0x3F000000u) != (srcAttr & 0x3F000000u);

		if ( doBlend )
		{
			if ( writeDepth )
				depth = tileDepth;

			if ( (attr & (1u << 15)) == 0u )
				blendAttr &= ~(1u << 15);
			attr = blendAttr;

			uint srcRB = tileColor & 0x3F003Fu;
			uint srcG = tileColor & 0x003F00u;
			uint dstRB = color & 0x3F003Fu;
			uint dstG = color & 0x003F00u;
			uint dstA = color & 0x1F000000u;

			uint alpha = (srcA >> 24) + 1u;
			if ( dstA != 0u )
			{
				srcRB = ((srcRB * alpha) + (dstRB * (32u - alpha))) >> 5;
				srcG = ((srcG * alpha) + (dstG * (32u - alpha))) >> 5;
			}

			color = (srcRB & 0x3F003Fu) | (srcG & 0x003F00u) | max( dstA, srcA );
		}
	}

	void ProcessCoarseMask( int linearTile, uint coarseMask, uint coarseOffset, int tileInnerOffset,
		inout uint2 color, inout uint2 depth, inout uint2 attr, inout uint stencil, inout bool prevIsShadowMask )
	{
		int binMaskStart = TilesPerLine * TileLines * CoarseBinStride;
		int binWorkOffsetsStart = binMaskStart + TilesPerLine * TileLines * BinStride;

		while ( coarseMask != 0u )
		{
			uint coarseBit = firstbitlow( coarseMask );
			coarseMask &= ~(1u << coarseBit);

			uint tileOffset = uint( linearTile * BinStride ) + coarseBit + coarseOffset;

			uint fineMask = BinResult[HeaderUints + uint( binMaskStart ) + tileOffset];
			uint workIdx = BinResult[HeaderUints + uint( binWorkOffsetsStart ) + tileOffset];

			while ( fineMask != 0u )
			{
				uint fineIdx = firstbitlow( fineMask );
				fineMask &= ~(1u << fineIdx);

				uint pixelindex = uint( tileInnerOffset ) + workIdx * uint( TileSize * TileSize );
				uint tileColor = TileColor[pixelindex];
				workIdx++;

				uint polygonIdx = fineIdx + (coarseBit + coarseOffset) * 32u;

				if ( tileColor != 0u )
				{
					uint polygonAttr = RenderPolygons[polygonIdx].Attr;

					bool isShadowMask = (polygonAttr & 0x3F000030u) == 0x00000030u;
					bool prevIsShadowMaskOld = prevIsShadowMask;
					prevIsShadowMask = isShadowMask;

					bool equalDepthTest = (polygonAttr & (1u << 14)) != 0u;

					uint tileDepth = TileDepth[pixelindex];
					uint tileAttr = TileAttr[pixelindex];

					uint dstattr = attr.x;

					if ( !isShadowMask )
					{
						bool isShadow = (polygonAttr & 0x30u) == 0x30u;
						bool writeSecondLayer = false;

						if ( isShadow )
						{
							if ( stencil == 0u )
								continue;
							if ( (stencil & 1u) == 0u )
								writeSecondLayer = true;
							if ( (stencil & 2u) == 0u )
								dstattr &= ~0x3u;
						}

						uint dstDepth = writeSecondLayer ? depth.y : depth.x;
						if ( !DepthInRange( equalDepthTest, dstDepth, tileDepth ) )
						{
							if ( (dstattr & 0x3u) == 0u || writeSecondLayer )
								continue;

							writeSecondLayer = true;
							dstattr = attr.y;
							if ( !DepthInRange( equalDepthTest, depth.y, tileDepth ) )
								continue;
						}

						uint srcAttr = (polygonAttr & 0x3F008000u);

						uint srcA = tileColor & 0x1F000000u;
						if ( srcA == 0x1F000000u )
						{
							srcAttr |= tileAttr;

							if ( !writeSecondLayer )
							{
								if ( (srcAttr & 0x3u) != 0u )
								{
									color.y = color.x;
									depth.y = depth.x;
									attr.y = attr.x;
								}

								color.x = tileColor;
								depth.x = tileDepth;
								attr.x = srcAttr;
							}
							else
							{
								color.y = tileColor;
								depth.y = tileDepth;
								attr.y = srcAttr;
							}
						}
						else
						{
							bool writeDepth = (polygonAttr & (1u << 11)) != 0u;

							if ( !writeSecondLayer )
								PlotTranslucent( color.x, depth.x, attr.x, isShadow, tileColor, srcA, tileDepth, srcAttr, writeDepth );
							if ( writeSecondLayer || (dstattr & 0x3u) != 0u )
								PlotTranslucent( color.y, depth.y, attr.y, isShadow, tileColor, srcA, tileDepth, srcAttr, writeDepth );
						}
					}
					else
					{
						if ( !prevIsShadowMaskOld )
							stencil = 0u;

						if ( !DepthInRange( equalDepthTest, depth.x, tileDepth ) )
							stencil = 0x1u;

						if ( (dstattr & 0x3u) != 0u )
						{
							if ( !DepthInRange( equalDepthTest, depth.y, tileDepth ) )
								stencil |= 0x2u;
						}
					}
				}
			}
		}
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 groupId : SV_GroupID, uint3 globalId : SV_DispatchThreadID, uint3 localId : SV_GroupThreadID )
	{
		int linearTile = int( groupId.x + groupId.y * uint( TilesPerLine ) );

		uint coarseMaskLo = BinResult[HeaderUints + uint( linearTile * CoarseBinStride + 0 )];
		uint coarseMaskHi = BinResult[HeaderUints + uint( linearTile * CoarseBinStride + 1 )];

		uint clearAttr = MetaUniform[142];
		uint clearColor = MetaUniform[140];
		uint clearDepth = MetaUniform[141];

		uint2 color = uint2( clearColor, 0u );
		uint2 depth = uint2( clearDepth, 0u );
		uint2 attr = uint2( clearAttr, 0u );

		uint stencil = 0u;
		bool prevIsShadowMask = false;

		int tileInnerOffset = int( localId.x ) + int( localId.y ) * TileSize;

		ProcessCoarseMask( linearTile, coarseMaskLo, 0u, tileInnerOffset, color, depth, attr, stencil, prevIsShadowMask );
		ProcessCoarseMask( linearTile, coarseMaskHi, uint( BinStride / 2 ), tileInnerOffset, color, depth, attr, stencil, prevIsShadowMask );

		int fbStride = ScreenWidth * ScreenHeight;
		int resultOffset = int( globalId.x ) + int( globalId.y ) * ScreenWidth;

		FinalTile[resultOffset] = color.x;
		FinalTile[resultOffset + fbStride] = color.y;
		FinalTile[2 * fbStride + resultOffset] = depth.x;
		FinalTile[2 * fbStride + resultOffset + fbStride] = depth.y;
		FinalTile[4 * fbStride + resultOffset] = attr.x;
		FinalTile[4 * fbStride + resultOffset + fbStride] = attr.y;
	}
}
