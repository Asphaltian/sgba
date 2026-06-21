HEADER
{
	DevShader = true;
	Description = "NDS Display Capture";
}

MODES
{
	Default();
}

CS
{
	Texture2D<float4> SrcA2D < Attribute( "SrcA2D" ); >;
	Texture2D<float4> SrcA3D < Attribute( "SrcA3D" ); >;
	StructuredBuffer<uint> SrcB < Attribute( "SrcB" ); >;
	RWTexture2DArray<float4> CaptureOut128 < Attribute( "CaptureOut128" ); >;
	RWTexture2DArray<float4> CaptureOut256 < Attribute( "CaptureOut256" ); >;

	int uSrcA3D < Attribute( "uSrcA3D" ); >;
	int uSrcBEnable < Attribute( "uSrcBEnable" ); >;
	int uMode < Attribute( "uMode" ); >;
	int uEVA < Attribute( "uEVA" ); >;
	int uEVB < Attribute( "uEVB" ); >;
	int uDstWidth < Attribute( "uDstWidth" ); >;
	int uDstHeight < Attribute( "uDstHeight" ); >;
	int uCapSize < Attribute( "uCapSize" ); >;
	int uDstLayer < Attribute( "uDstLayer" ); >;
	int uDstOffset < Attribute( "uDstOffset" ); >;
	int uBufferHeight < Attribute( "uBufferHeight" ); >;
	int uScale < Attribute( "uScale" ); >;

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int hw = uDstWidth * uScale;
		int hh = uDstHeight * uScale;
		if ( (int)id.x >= hw || (int)id.y >= hh )
			return;

		int sx = (int)id.x;
		int sy = (int)id.y;

		uint ar, ag, ab, aa;
		if ( uSrcA3D != 0 )
		{
			float4 c = SrcA3D.Load( int3( sx, sy, 0 ) );
			ar = (uint)( c.r * 255.0 + 0.5 );
			ag = (uint)( c.g * 255.0 + 0.5 );
			ab = (uint)( c.b * 255.0 + 0.5 );
			aa = c.a > 0.0 ? 1u : 0u;
		}
		else
		{
			float4 c = SrcA2D.Load( int3( sx, sy, 0 ) );
			ar = (uint)( c.r * 255.0 + 0.5 );
			ag = (uint)( c.g * 255.0 + 0.5 );
			ab = (uint)( c.b * 255.0 + 0.5 );
			aa = 1u;
		}

		int nx = sx / uScale;
		int ny = sy / uScale;
		uint vb = uSrcBEnable != 0 ? SrcB[ny * uDstWidth + nx] : 0u;
		uint br = ( vb & 0x1Fu ) << 3;
		uint bg = ( ( vb >> 5 ) & 0x1Fu ) << 3;
		uint bb = ( ( vb >> 10 ) & 0x1Fu ) << 3;
		uint ba = vb >> 15;

		uint rr, rg, rb, ra;
		if ( uMode == 0 )
		{
			rr = ar; rg = ag; rb = ab; ra = aa;
		}
		else if ( uMode == 1 )
		{
			rr = br; rg = bg; rb = bb; ra = ba;
		}
		else
		{
			uint eva = (uint)uEVA;
			uint evb = (uint)uEVB;
			uint sar = ar >> 3, sag = ag >> 3, sab = ab >> 3;
			uint sbr = br >> 3, sbg = bg >> 3, sbb = bb >> 3;
			uint dr = min( ( ( sar * aa * eva ) + ( sbr * ba * evb ) + 8 ) >> 4, 0x1Fu ) << 3;
			uint dg = min( ( ( sag * aa * eva ) + ( sbg * ba * evb ) + 8 ) >> 4, 0x1Fu ) << 3;
			uint db = min( ( ( sab * aa * eva ) + ( sbb * ba * evb ) + 8 ) >> 4, 0x1Fu ) << 3;
			rr = dr; rg = dg; rb = db;
			ra = ( ( uEVA > 0 && aa != 0u ) ? 1u : 0u ) | ( ( uEVB > 0 && ba != 0u ) ? 1u : 0u );
		}

		float4 outc = float4( (float)rr / 255.0, (float)rg / 255.0, (float)rb / 255.0, ra != 0u ? 1.0 : 0.0 );

		int ly = ( uDstOffset * 64 * uScale ) + (int)id.y;
		int bh = uBufferHeight * uScale;
		if ( ly >= bh ) ly -= bh;

		if ( uCapSize == 0 )
			CaptureOut128[int3( (int)id.x, ly, uDstLayer )] = outc;
		else
			CaptureOut256[int3( (int)id.x, ly, uDstLayer )] = outc;
	}
}
