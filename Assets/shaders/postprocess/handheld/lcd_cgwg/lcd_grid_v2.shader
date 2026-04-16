HEADER
{
	DevShader = true;
	Description = "LCD grid v2 pass";
}

MODES
{
	Default();
}

FEATURES
{
}

CS
{
	#include "postprocess/postprocess_common.hlsl"

	static const float coeffs_x[7] = { 1.0, -2.0 / 3.0, -1.0 / 5.0, 4.0 / 7.0, -1.0 / 9.0, -2.0 / 11.0, 1.0 / 13.0 };
	static const float coeffs_y[7] = { 1.0, 0.0, -4.0 / 5.0, 2.0 / 7.0, 4.0 / 9.0, -4.0 / 11.0, 1.0 / 13.0 };

	float4 OutputSize < Attribute( "OutputSize" ); >;
	float4 OriginalSize < Attribute( "OriginalSize" ); >;
	float4 SourceSize < Attribute( "SourceSize" ); >;
	float RSUBPIX_R < Attribute( "RSUBPIX_R" ); Default( 1.0 ); >;
	float RSUBPIX_G < Attribute( "RSUBPIX_G" ); Default( 0.0 ); >;
	float RSUBPIX_B < Attribute( "RSUBPIX_B" ); Default( 0.0 ); >;
	float GSUBPIX_R < Attribute( "GSUBPIX_R" ); Default( 0.0 ); >;
	float GSUBPIX_G < Attribute( "GSUBPIX_G" ); Default( 1.0 ); >;
	float GSUBPIX_B < Attribute( "GSUBPIX_B" ); Default( 0.0 ); >;
	float BSUBPIX_R < Attribute( "BSUBPIX_R" ); Default( 0.0 ); >;
	float BSUBPIX_G < Attribute( "BSUBPIX_G" ); Default( 0.0 ); >;
	float BSUBPIX_B < Attribute( "BSUBPIX_B" ); Default( 1.0 ); >;
	float gain < Attribute( "gain" ); Default( 1.0 ); >;
	float gamma < Attribute( "gamma" ); Default( 3.0 ); >;
	float blacklevel < Attribute( "blacklevel" ); Default( 0.05 ); >;
	float ambient < Attribute( "ambient" ); Default( 0.0 ); >;
	float BGR < Attribute( "BGR" ); Default( 0.0 ); >;

	Texture2D<float4> Source < Attribute( "Source" ); >;
	RWTexture2D<float4> OutputTex < Attribute( "OutputTex" ); >;

	float IntSmearFuncX( float z )
	{
		float z2 = z * z;
		float zn = z;
		float ret = 0.0;
		[unroll]
		for ( int i = 0; i < 7; i++ )
		{
			ret += zn * coeffs_x[i];
			zn *= z2;
		}
		return ret;
	}

	float IntSmearFuncY( float z )
	{
		float z2 = z * z;
		float zn = z;
		float ret = 0.0;
		[unroll]
		for ( int i = 0; i < 7; i++ )
		{
			ret += zn * coeffs_y[i];
			zn *= z2;
		}
		return ret;
	}

	float IntSmearX( float x, float dx, float d )
	{
		float zl = clamp( ( x - dx * 0.5 ) / d, -1.0, 1.0 );
		float zh = clamp( ( x + dx * 0.5 ) / d, -1.0, 1.0 );
		return d * ( IntSmearFuncX( zh ) - IntSmearFuncX( zl ) ) / dx;
	}

	float IntSmearY( float x, float dx, float d )
	{
		float zl = clamp( ( x - dx * 0.5 ) / d, -1.0, 1.0 );
		float zh = clamp( ( x + dx * 0.5 ) / d, -1.0, 1.0 );
		return d * ( IntSmearFuncY( zh ) - IntSmearFuncY( zl ) ) / dx;
	}

	float3 FetchOffset( int2 coord, int2 offset )
	{
		float3 sampleColor = Source.Load( int3( coord + offset, 0 ) ).rgb;
		return pow( gain * sampleColor + blacklevel.xxx, gamma.xxx ) + ambient.xxx;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( id.x >= (uint)OutputSize.x || id.y >= (uint)OutputSize.y )
			return;

		const float outgamma = 2.2;
		float2 texelSize = SourceSize.zw;
		float2 range = OutputSize.zw;
		float2 vTexCoord = PixelCoordToUv( id.xy, OutputSize.xy );
		float3 cred = pow( float3( RSUBPIX_R, RSUBPIX_G, RSUBPIX_B ), outgamma.xxx );
		float3 cgreen = pow( float3( GSUBPIX_R, GSUBPIX_G, GSUBPIX_B ), outgamma.xxx );
		float3 cblue = pow( float3( BSUBPIX_R, BSUBPIX_G, BSUBPIX_B ), outgamma.xxx );
		int2 tli = (int2)floor( vTexCoord / texelSize - 0.4999.xx );

		float subpix = ( vTexCoord.x / texelSize.x - 0.4999 - (float)tli.x ) * 3.0;
		float rsubpix = range.x / texelSize.x * 3.0;
		float3 lcol = float3(
			IntSmearX( subpix + 1.0, rsubpix, 1.5 ),
			IntSmearX( subpix, rsubpix, 1.5 ),
			IntSmearX( subpix - 1.0, rsubpix, 1.5 )
		);
		float3 rcol = float3(
			IntSmearX( subpix - 2.0, rsubpix, 1.5 ),
			IntSmearX( subpix - 3.0, rsubpix, 1.5 ),
			IntSmearX( subpix - 4.0, rsubpix, 1.5 )
		);

		if ( BGR > 0.5 )
		{
			lcol = lcol.bgr;
			rcol = rcol.bgr;
		}

		subpix = vTexCoord.y / texelSize.y - 0.4999 - (float)tli.y;
		rsubpix = range.y / texelSize.y;
		float tcol = IntSmearY( subpix, rsubpix, 0.63 );
		float bcol = IntSmearY( subpix - 1.0, rsubpix, 0.63 );

		float3 topLeftColor = FetchOffset( tli, int2( 0, 0 ) ) * lcol * tcol.xxx;
		float3 bottomRightColor = FetchOffset( tli, int2( 1, 1 ) ) * rcol * bcol.xxx;
		float3 bottomLeftColor = FetchOffset( tli, int2( 0, 1 ) ) * lcol * bcol.xxx;
		float3 topRightColor = FetchOffset( tli, int2( 1, 0 ) ) * rcol * tcol.xxx;

		float3 averageColor = topLeftColor + bottomRightColor + bottomLeftColor + topRightColor;
		averageColor = cred * averageColor.x + cgreen * averageColor.y + cblue * averageColor.z;

		OutputTex[id.xy] = float4( pow( averageColor, ( 1.0 / outgamma ).xxx ), 0.0 );
	}
}