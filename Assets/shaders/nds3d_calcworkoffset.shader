HEADER
{
	DevShader = true;
	Description = "NDS 3D Calculate Work List Offsets";
}

MODES
{
	Default();
}

CS
{
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	RWStructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;
	RWStructuredBuffer<uint3> IndirectArgs < Attribute( "IndirectArgs" ); >;

	static const uint SortedWorkOffsetStart = 1024u;
	static const uint SortWorkCountStart = 1280u;
	static const uint SortWorkArgIndex = 256u;

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint numVariants = MetaUniform[1];
		if ( id.x >= numVariants )
			return;

		if ( id.x == 0u )
		{
			uint sortGroups = (BinResult[3] + 31u) / 32u;
			BinResult[SortWorkCountStart + 0u] = sortGroups;
			BinResult[SortWorkCountStart + 1u] = 1u;
			BinResult[SortWorkCountStart + 2u] = 1u;
			BinResult[SortWorkCountStart + 3u] = 0u;
			IndirectArgs[SortWorkArgIndex] = uint3( sortGroups, 1u, 1u );
		}

		uint count = BinResult[id.x * 4u + 2u];

		uint orig;
		InterlockedAdd( BinResult[7], count, orig );
		BinResult[SortedWorkOffsetStart + id.x] = orig;

		IndirectArgs[id.x] = uint3( 1u, 1u, count );
	}
}
