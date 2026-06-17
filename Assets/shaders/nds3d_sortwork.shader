HEADER
{
	DevShader = true;
	Description = "NDS 3D Sort Work Descriptors by Variant";
}

MODES
{
	Default();
}

CS
{
	#include "nds3d_common.hlsl"

	StructuredBuffer<Polygon> RenderPolygons < Attribute( "RenderPolygons" ); >;
	RWStructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;
	RWStructuredBuffer<uint> WorkDesc < Attribute( "WorkDesc" ); >;

	int MaxWorkTiles < Attribute( "MaxWorkTiles" ); >;

	static const uint SortedWorkOffsetStart = 1024u;

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( id.x >= BinResult[3] )
			return;

		uint descX = WorkDesc[id.x * 2u + 0u];
		uint descY = WorkDesc[id.x * 2u + 1u];

		int inVariantOffset = int( (descY >> 11) & 0x1FFFFFu );
		int polygonIdx = int( descY & 0x7FFu );
		int variantIdx = RenderPolygons[polygonIdx].Variant;

		int sortedIndex = int( BinResult[SortedWorkOffsetStart + uint( variantIdx )] ) + inVariantOffset;
		uint sortedDescIdx = uint( MaxWorkTiles ) + uint( sortedIndex );

		WorkDesc[sortedDescIdx * 2u + 0u] = descX;
		WorkDesc[sortedDescIdx * 2u + 1u] = BitfieldInsert( descY, id.x, 11u, 21u );
	}
}
