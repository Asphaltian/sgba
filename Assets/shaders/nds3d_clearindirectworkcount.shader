HEADER
{
	DevShader = true;
	Description = "NDS 3D Clear Indirect Work Count";
}

MODES
{
	Default();
}

CS
{
	RWStructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		BinResult[id.x * 4u + 0u] = 1u;
		BinResult[id.x * 4u + 1u] = 1u;
		BinResult[id.x * 4u + 2u] = 0u;
		BinResult[id.x * 4u + 3u] = 0u;
	}
}
