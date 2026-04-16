HEADER
{
	DevShader = true;
	Description = "Response-time motion blur pass";
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

	float4 OutputSize < Attribute( "OutputSize" ); >;
	float4 OriginalSize < Attribute( "OriginalSize" ); >;
	float4 SourceSize < Attribute( "SourceSize" ); >;
	float response_time < Attribute( "response_time" ); Default( 0.333 ); >;

	Texture2D<float4> Source < Attribute( "Source" ); >;
	Texture2D<float4> OriginalHistory1 < Attribute( "OriginalHistory1" ); >;
	Texture2D<float4> OriginalHistory2 < Attribute( "OriginalHistory2" ); >;
	Texture2D<float4> OriginalHistory3 < Attribute( "OriginalHistory3" ); >;
	Texture2D<float4> OriginalHistory4 < Attribute( "OriginalHistory4" ); >;
	Texture2D<float4> OriginalHistory5 < Attribute( "OriginalHistory5" ); >;
	Texture2D<float4> OriginalHistory6 < Attribute( "OriginalHistory6" ); >;
	Texture2D<float4> OriginalHistory7 < Attribute( "OriginalHistory7" ); >;
	RWTexture2D<float4> OutputTex < Attribute( "OutputTex" ); >;

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( id.x >= (uint)OutputSize.x || id.y >= (uint)OutputSize.y )
			return;

		float2 vTexCoord = PixelCoordToUv( id.xy, OutputSize.xy );
		float3 input_rgb = Source.SampleLevel( PointClamp, vTexCoord, 0 ).rgb;
		input_rgb += ( OriginalHistory1.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * response_time;
		input_rgb += ( OriginalHistory2.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 2.0 );
		input_rgb += ( OriginalHistory3.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 3.0 );
		input_rgb += ( OriginalHistory4.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 4.0 );
		input_rgb += ( OriginalHistory5.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 5.0 );
		input_rgb += ( OriginalHistory6.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 6.0 );
		input_rgb += ( OriginalHistory7.SampleLevel( PointClamp, vTexCoord, 0 ).rgb - input_rgb ) * pow( response_time, 7.0 );

		OutputTex[id.xy] = float4( input_rgb, 0.0 );
	}
}