HEADER
{
	DevShader = true;
	Description = "NDS 3D Bin Combined (two-level tile binning)";
}

MODES
{
	Default();
}

CS
{
	#define YFAC 9
	#include "nds3d_common.hlsl"

	StructuredBuffer<Polygon> RenderPolygons < Attribute( "RenderPolygons" ); >;
	StructuredBuffer<XSpanSetup> XSpanSetups < Attribute( "XSpanSetups" ); >;
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	RWStructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;
	RWStructuredBuffer<uint> WorkDesc < Attribute( "WorkDesc" ); >;

	int TileSize < Attribute( "TileSize" ); >;
	int TilesPerLine < Attribute( "TilesPerLine" ); >;
	int TileLines < Attribute( "TileLines" ); >;
	int CoarseTileW < Attribute( "CoarseTileW" ); >;
	int CoarseTileH < Attribute( "CoarseTileH" ); >;

	static const uint HeaderUints = 1284u;
	static const int BinStride = 64;
	static const int CoarseBinStride = 2;
	static const int CoarseTileCountX = 8;

	groupshared uint mergedMaskShared;

	bool BinPolygon( Polygon polygon, int2 topLeft, int2 botRight )
	{
		if ( polygon.YTop > botRight.y || polygon.YBot <= topLeft.y )
			return false;

		int polygonHeight = polygon.YBot - polygon.YTop;

		int polyInnerTopY = clamp( topLeft.y - polygon.YTop, 0, max( polygonHeight - 1, 0 ) );
		int polyInnerBotY = clamp( botRight.y - polygon.YTop, 0, max( polygonHeight - 1, 0 ) );

		XSpanSetup xspanTop = XSpanSetups[polygon.FirstXSpan + polyInnerTopY];
		XSpanSetup xspanBot = XSpanSetups[polygon.FirstXSpan + polyInnerBotY];

		int minXL;
		if ( polygon.XMinY >= topLeft.y && polygon.XMinY <= botRight.y )
			minXL = polygon.XMin;
		else
			minXL = min( xspanTop.X0, xspanBot.X0 );

		if ( minXL > botRight.x )
			return false;

		int maxXR;
		if ( polygon.XMaxY >= topLeft.y && polygon.XMaxY <= botRight.y )
			maxXR = polygon.XMax;
		else
			maxXR = max( xspanTop.X1, xspanBot.X1 ) - 1;

		if ( maxXR < topLeft.x )
			return false;

		return true;
	}

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 gid : SV_GroupID, uint localIndex : SV_GroupIndex )
	{
		int groupIdx = int( gid.x );
		int2 coarseTile = int2( gid.yz );

		int localIdx = int( localIndex );

		if ( localIdx == 0 )
			mergedMaskShared = 0u;
		GroupMemoryBarrierWithGroupSync();

		uint numPolygons = MetaUniform[0];

		int polygonIdx = groupIdx * 32 + localIdx;

		int2 coarseTopLeft = coarseTile * int2( CoarseTileW, CoarseTileH );
		int2 coarseBotRight = coarseTopLeft + int2( CoarseTileW - 1, CoarseTileH - 1 );

		bool binned = false;
		if ( polygonIdx < int( numPolygons ) )
			binned = BinPolygon( RenderPolygons[polygonIdx], coarseTopLeft, coarseBotRight );

		if ( binned )
			InterlockedOr( mergedMaskShared, 1u << localIdx );
		GroupMemoryBarrierWithGroupSync();
		uint mergedMask = mergedMaskShared;

		int2 fineTile = int2( localIdx & 0x7, localIdx >> 3 );
		int2 fineTileTopLeft = coarseTopLeft + fineTile * int2( TileSize, TileSize );
		int2 fineTileBotRight = fineTileTopLeft + int2( TileSize - 1, TileSize - 1 );

		uint binnedMask = 0u;
		while ( mergedMask != 0u )
		{
			int bit = firstbitlow( mergedMask );
			mergedMask &= ~(1u << bit);

			int polyIdx2 = groupIdx * 32 + bit;
			if ( BinPolygon( RenderPolygons[polyIdx2], fineTileTopLeft, fineTileBotRight ) )
				binnedMask |= 1u << bit;
		}

		int binMaskStart = TilesPerLine * TileLines * CoarseBinStride;
		int binWorkOffsetsStart = binMaskStart + TilesPerLine * TileLines * BinStride;

		int linearTile = fineTile.x + fineTile.y * TilesPerLine + coarseTile.x * CoarseTileCountX + coarseTile.y * TilesPerLine * (CoarseTileH / TileSize);

		BinResult[HeaderUints + uint( binMaskStart + linearTile * BinStride + groupIdx )] = binnedMask;
		int coarseMaskIdx = linearTile * CoarseBinStride + (groupIdx >> 5);
		if ( binnedMask != 0u )
			InterlockedOr( BinResult[HeaderUints + uint( coarseMaskIdx )], 1u << uint( groupIdx & 0x1F ) );

		if ( binnedMask != 0u )
		{
			uint workOffset;
			InterlockedAdd( BinResult[3], uint( countbits( binnedMask ) ), workOffset );
			BinResult[HeaderUints + uint( binWorkOffsetsStart + linearTile * BinStride + groupIdx )] = workOffset;

			uint tilePositionCombined = BitfieldInsert( uint( fineTileTopLeft.x ), uint( fineTileTopLeft.y ), 16u, 16u );

			int idx = 0;
			while ( binnedMask != 0u )
			{
				int bit = firstbitlow( binnedMask );
				binnedMask &= ~(1u << bit);

				int polyIdx3 = groupIdx * 32 + bit;
				int variantIdx = RenderPolygons[polyIdx3].Variant;

				uint inVariantOffset;
				InterlockedAdd( BinResult[uint( variantIdx * 4 + 2 )], 1u, inVariantOffset );

				uint descIdx = workOffset + uint( idx );
				WorkDesc[descIdx * 2u + 0u] = tilePositionCombined;
				WorkDesc[descIdx * 2u + 1u] = BitfieldInsert( uint( polyIdx3 ), inVariantOffset, 11u, 21u );

				idx++;
			}
		}
	}
}
