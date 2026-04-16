HEADER
{
	DevShader = true;
	Description = "NSO GBA color pass";
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
	float mode < Attribute( "mode" ); Default( 1.0 ); >;
	float darken_screen < Attribute( "darken_screen" ); Default( 0.8 ); >;

	Texture2D<float4> Source < Attribute( "Source" ); >;
	RWTexture2D<float4> OutputTex < Attribute( "OutputTex" ); >;

	float4 ApplyColorProfile( float4 screen, int colorMode )
	{
		if ( colorMode == 2 )
		{
			return float4(
				0.72 * screen.x + 0.2675 * screen.y + 0.0125 * screen.z,
				0.0875 * screen.x + 0.9 * screen.y + 0.0125 * screen.z,
				0.0725 * screen.x + 0.185 * screen.y + 0.7425 * screen.z,
				screen.w
			);
		}

		if ( colorMode == 3 )
		{
			return float4(
				0.57 * screen.x + 0.3825 * screen.y + 0.0475 * screen.z,
				0.115 * screen.x + 0.8625 * screen.y + 0.0225 * screen.z,
				0.0725 * screen.x + 0.195 * screen.y + 0.7325 * screen.z,
				screen.w
			);
		}

		return float4(
			0.865 * screen.x + 0.1225 * screen.y + 0.0125 * screen.z,
			0.0575 * screen.x + 0.925 * screen.y + 0.0125 * screen.z,
			0.0575 * screen.x + 0.1225 * screen.y + 0.82 * screen.z,
			screen.w
		);
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( id.x >= (uint)OutputSize.x || id.y >= (uint)OutputSize.y )
			return;

		const float target_gamma = 2.2;
		const float display_gamma = 2.2;
		int colorMode = (int)mode;
		float exponent = target_gamma + darken_screen;
		float2 vTexCoord = PixelCoordToUv( id.xy, OutputSize.xy );
		float4 sourceColor = Source.SampleLevel( PointClamp, vTexCoord, 0 );
		sourceColor.a = 1.0;
		float4 screen = pow( sourceColor, float4( exponent, exponent, exponent, exponent ) );
		screen = clamp( screen, 0.0, 1.0 );
		float4 output = pow( ApplyColorProfile( screen, colorMode ), ( 1.0 / display_gamma ).xxxx );
		OutputTex[id.xy] = output;
	}
}