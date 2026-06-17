HEADER
{
	DevShader = true;
	Description = "NDS 3D Clear Coarse Bin Mask";
}

MODES
{
	Default();
}

CS
{
	RWStructuredBuffer<uint> BinResult < Attribute( "BinResult" ); >;

	static const uint HeaderUints = 1284u;
	static const uint CoarseBinStride = 2u;

	[numthreads( 64, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		BinResult[HeaderUints + id.x * CoarseBinStride + 0u] = 0u;
		BinResult[HeaderUints + id.x * CoarseBinStride + 1u] = 0u;
	}
}
