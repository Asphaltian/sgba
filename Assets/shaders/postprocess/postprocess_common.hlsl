#ifndef POSTPROCESS_COMMON_HLSL
#define POSTPROCESS_COMMON_HLSL

SamplerState PointClamp < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); >;
SamplerState BilinearClamp < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); >;

float2 PixelCoordToUv( uint2 pixelCoord, float2 outputSize )
{
	return ( float2( pixelCoord ) + 0.5 ) / outputSize;
}

#endif