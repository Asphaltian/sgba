HEADER
{
	DevShader = true;
	Description = "NDS 3D Final Pass (edge mark, fog, AA)";
}

MODES
{
	Default();
}

CS
{
	DynamicCombo( D_EDGEMARKING, 0..1, Sys( ALL ) );
	DynamicCombo( D_FOG, 0..1, Sys( ALL ) );
	DynamicCombo( D_AA, 0..1, Sys( ALL ) );

	#include "nds3d_common.hlsl"

	StructuredBuffer<uint> FinalTile < Attribute( "FinalTile" ); >;
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	RWTexture2D<float4> OutputTex < Attribute( "OutputTex" ); >;

	int ScreenWidth < Attribute( "ScreenWidth" ); >;
	int ScreenHeight < Attribute( "ScreenHeight" ); >;

	uint ToonG( uint idx ) { return MetaUniform[4u + idx * 4u + 1u]; }
	uint ToonB( uint idx ) { return MetaUniform[4u + idx * 4u + 2u]; }

	#if D_FOG
	uint BlendFog( uint color, uint depth )
	{
		uint fogOffset = MetaUniform[143];
		uint fogShift = MetaUniform[144];
		uint fogColor = MetaUniform[145];
		uint dispCnt = MetaUniform[3];

		uint densityid = 0u;
		uint densityfrac = 0u;

		if ( depth >= fogOffset )
		{
			depth -= fogOffset;
			depth = (depth >> 2) << fogShift;

			densityid = depth >> 17;
			if ( densityid >= 32u )
			{
				densityid = 32u;
				densityfrac = 0u;
			}
			else
			{
				densityfrac = depth & 0x1FFFFu;
			}
		}

		uint density = ((ToonG( densityid ) * (0x20000u - densityfrac)) + (ToonG( densityid + 1u ) * densityfrac)) >> 17;
		density = min( density, 128u );

		uint colorRB = color & 0x3F003Fu;
		uint colorGA = (color >> 8) & 0x3F003Fu;

		uint fogRB = fogColor & 0x3F003Fu;
		uint fogGA = (fogColor >> 8) & 0x1F003Fu;

		uint finalColorRB = ((fogRB * density) + (colorRB * (128u - density))) >> 7;
		uint finalColorGA = ((fogGA * density) + (colorGA * (128u - density))) >> 7;

		finalColorRB &= 0x3F003Fu;
		finalColorGA &= 0x1F003Fu;

		return (dispCnt & (1u << 6)) != 0u
			? BitfieldInsert( color, finalColorGA >> 16, 24u, 8u )
			: (finalColorRB | (finalColorGA << 8));
	}
	#endif

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 globalId : SV_DispatchThreadID )
	{
		int srcX = int( globalId.x );
		int fbStride = ScreenWidth * ScreenHeight;
		int resultOffset = srcX + int( globalId.y ) * ScreenWidth;

		int colorStart = 0;
		int depthStart = 2 * fbStride;
		int attrStart = 4 * fbStride;

		uint2 color = uint2( FinalTile[resultOffset + colorStart], FinalTile[resultOffset + fbStride + colorStart] );
		uint2 depth = uint2( FinalTile[resultOffset + depthStart], FinalTile[resultOffset + fbStride + depthStart] );
		uint2 attr = uint2( FinalTile[resultOffset + attrStart], FinalTile[resultOffset + fbStride + attrStart] );

		#if D_EDGEMARKING
		if ( (attr.x & 0xFu) != 0u )
		{
			uint clearAttr = MetaUniform[142];
			uint clearDepth = MetaUniform[141];

			uint4 otherAttr = uint4( clearAttr, clearAttr, clearAttr, clearAttr );
			uint4 otherDepth = uint4( clearDepth, clearDepth, clearDepth, clearDepth );

			if ( srcX > 0 )
			{
				otherAttr.x = FinalTile[resultOffset - 1 + attrStart];
				otherDepth.x = FinalTile[resultOffset - 1 + depthStart];
			}
			if ( srcX < ScreenWidth - 1 )
			{
				otherAttr.y = FinalTile[resultOffset + 1 + attrStart];
				otherDepth.y = FinalTile[resultOffset + 1 + depthStart];
			}
			if ( globalId.y > 0u )
			{
				otherAttr.z = FinalTile[resultOffset - ScreenWidth + attrStart];
				otherDepth.z = FinalTile[resultOffset - ScreenWidth + depthStart];
			}
			if ( int( globalId.y ) < ScreenHeight - 1 )
			{
				otherAttr.w = FinalTile[resultOffset + ScreenWidth + attrStart];
				otherDepth.w = FinalTile[resultOffset + ScreenWidth + depthStart];
			}

			uint polyId = (attr.x >> 24) & 0x3Fu;
			uint4 otherPolyId = (otherAttr >> 24) & 0x3Fu;

			bool4 polyIdMismatch = polyId != otherPolyId;
			bool4 nearer = depth.x < otherDepth;

			if ( (polyIdMismatch.x && nearer.x)
				|| (polyIdMismatch.y && nearer.y)
				|| (polyIdMismatch.z && nearer.z)
				|| (polyIdMismatch.w && nearer.w) )
			{
				color.x = ToonB( polyId >> 3 ) | (color.x & 0xFF000000u);
				attr.x = (attr.x & 0xFFFFE0FFu) | 0x00001000u;
			}
		}
		#endif

		#if D_FOG
		if ( (attr.x & (1u << 15)) != 0u )
			color.x = BlendFog( color.x, depth.x );

		if ( (attr.x & 0xFu) != 0u && (attr.y & (1u << 15)) != 0u )
			color.y = BlendFog( color.y, depth.y );
		#endif

		#if D_AA
		if ( (attr.x & 0x3u) != 0u )
		{
			uint coverage = (attr.x >> 8) & 0x1Fu;

			if ( coverage != 0u )
			{
				uint topRB = color.x & 0x3F003Fu;
				uint topG = color.x & 0x003F00u;
				uint topA = (color.x >> 24) & 0x1Fu;

				uint botRB = color.y & 0x3F003Fu;
				uint botG = color.y & 0x003F00u;
				uint botA = (color.y >> 24) & 0x1Fu;

				coverage++;

				if ( botA > 0u )
				{
					topRB = ((topRB * coverage) + (botRB * (32u - coverage))) >> 5;
					topG = ((topG * coverage) + (botG * (32u - coverage))) >> 5;

					topRB &= 0x3F003Fu;
					topG &= 0x003F00u;
				}

				topA = ((topA * coverage) + (botA * (32u - coverage))) >> 5;

				color.x = topRB | topG | (topA << 24);
			}
			else
			{
				color.x = color.y;
			}
		}
		#endif

		float4 result = float4(
			float( color.x & 0x3Fu ),
			float( (color.x >> 8) & 0xFFu ),
			float( (color.x >> 16) & 0xFFu ),
			float( (color.x >> 24) & 0xFFu ) );
		result /= float4( 63.0, 63.0, 63.0, 31.0 );

		OutputTex[globalId.xy] = result;
	}
}
