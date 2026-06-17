HEADER
{
	DevShader = true;
	Description = "NDS 3D Interpolate X-Spans";
}

MODES
{
	Default();
}

CS
{
	DynamicCombo( D_WBUFFER, 0..1, Sys( ALL ) );

	#define YFAC 9
	#include "nds3d_common.hlsl"

	StructuredBuffer<Polygon> RenderPolygons < Attribute( "RenderPolygons" ); >;
	StructuredBuffer<YSpanSetup> YSpanSetups < Attribute( "YSpanSetups" ); >;
	StructuredBuffer<uint2> YSpanIndices < Attribute( "YSpanIndices" ); >;
	StructuredBuffer<uint> MetaUniform < Attribute( "MetaUniform" ); >;
	RWStructuredBuffer<XSpanSetup> XSpanSetups < Attribute( "XSpanSetups" ); >;

	int CalcYFactorY( YSpanSetup span, int i )
	{
		uint numLo = uint( abs( i ) ) * uint( span.W0n );
		uint numHi = 0u;
		numHi |= numLo >> (32u - YFAC);
		numLo <<= YFAC;

		uint den = uint( abs( i ) ) * uint( span.W0d ) + uint( abs( span.I1 - span.I0 - i ) ) * uint( span.W1d );

		if ( den == 0u )
			return 0;
		return int( Div64_32_32( numHi, numLo, den ) );
	}

	int CalculateDx( int y, YSpanSetup span )
	{
		return span.DxInitial + (y - span.Y0) * span.Increment;
	}

	int CalculateX( int dx, YSpanSetup span )
	{
		int x = span.X0;
		if ( span.X1 < span.X0 )
			x -= dx >> 18;
		else
			x += dx >> 18;
		return clamp( x, span.XMin, span.XMax );
	}

	void EdgeParams_XMajor( bool side, int dx, YSpanSetup span, out int edgelen, out int edgecov )
	{
		bool negative = span.X1 < span.X0;
		int len;
		if ( side != negative )
			len = (dx >> 18) - ((dx - span.Increment) >> 18);
		else
			len = ((dx + span.Increment) >> 18) - (dx >> 18);
		edgelen = len;

		int xlen = span.XMax + 1 - span.XMin;
		int startx = dx >> 18;
		if ( negative ) startx = xlen - startx;
		if ( side ) startx = startx - len + 1;

		uint r;
		int startcov = int( NdsDiv( uint( ((startx << 10) + 0x1FF) * (span.Y1 - span.Y0) ), uint( xlen ), r ) );
		edgecov = (1 << 31) | ((startcov & 0x3FF) << 12) | (span.XCovIncr & 0x3FF);
	}

	void EdgeParams_YMajor( bool side, int dx, YSpanSetup span, out int edgelen, out int edgecov )
	{
		bool negative = span.X1 < span.X0;
		edgelen = 1;

		if ( span.Increment == 0 )
		{
			edgecov = 31;
		}
		else
		{
			int cov = ((dx >> 9) + (span.Increment >> 10)) >> 4;
			if ( (cov >> 5) != (dx >> 18) ) cov = 31;
			cov &= 0x1F;
			if ( side == negative ) cov = 0x1F - cov;

			edgecov = cov;
		}
	}

	[numthreads( 32, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint2 packed = YSpanIndices[id.x];
		uint4 setup = uint4( packed.x & 0xFFFFu, packed.x >> 16, packed.y & 0xFFFFu, packed.y >> 16 );

		YSpanSetup spanL = YSpanSetups[setup.y];
		YSpanSetup spanR = YSpanSetups[setup.z];

		XSpanSetup xspan = (XSpanSetup)0;
		xspan.Flags = 0u;

		int y = int( setup.w );

		int dxl = CalculateDx( y, spanL );
		int dxr = CalculateDx( y, spanR );

		int xl = CalculateX( dxl, spanL );
		int xr = CalculateX( dxr, spanR );

		Polygon polygon = RenderPolygons[setup.x];

		int edgeLenL;
		int edgeLenR;

		if ( xl > xr )
		{
			YSpanSetup tmpSpan = spanL;
			spanL = spanR;
			spanR = tmpSpan;

			int tmp = xl;
			xl = xr;
			xr = tmp;

			EdgeParams_YMajor( false, dxr, spanL, edgeLenL, xspan.EdgeCovL );
			EdgeParams_YMajor( true, dxl, spanR, edgeLenR, xspan.EdgeCovR );
		}
		else
		{
			if ( spanL.Increment > 0x40000 )
				EdgeParams_XMajor( false, dxl, spanL, edgeLenL, xspan.EdgeCovL );
			else
				EdgeParams_YMajor( false, dxl, spanL, edgeLenL, xspan.EdgeCovL );
			if ( spanR.Increment > 0x40000 )
				EdgeParams_XMajor( true, dxr, spanR, edgeLenR, xspan.EdgeCovR );
			else
				EdgeParams_YMajor( true, dxr, spanR, edgeLenR, xspan.EdgeCovR );
		}

		xspan.CovLInitial = (xspan.EdgeCovL >> 12) & 0x3FF;
		if ( xspan.CovLInitial == 0x3FF )
			xspan.CovLInitial = 0;
		xspan.CovRInitial = (xspan.EdgeCovR >> 12) & 0x3FF;
		if ( xspan.CovRInitial == 0x3FF )
			xspan.CovRInitial = 0;

		xspan.X0 = xl;
		xspan.X1 = xr + 1;

		uint polyalpha = (polygon.Attr >> 16) & 0x1Fu;
		bool isWireframe = polyalpha == 0u;

		if ( !isWireframe || (y == polygon.YTop || y == polygon.YBot - 1) )
			xspan.Flags |= XSpanSetup_FillInside;

		xspan.InsideStart = xspan.X0 + edgeLenL;
		if ( xspan.InsideStart > xspan.X1 )
			xspan.InsideStart = xspan.X1;
		xspan.InsideEnd = xspan.X1 - edgeLenR;
		if ( xspan.InsideEnd > xspan.X1 )
			xspan.InsideEnd = xspan.X1;

		uint dispCnt = MetaUniform[3];
		bool fillAllEdges = polyalpha < 31u || (dispCnt & (3u << 4)) != 0u;

		if ( fillAllEdges || spanL.X1 < spanL.X0 || spanL.Increment <= 0x40000 )
			xspan.Flags |= XSpanSetup_FillLeft;
		if ( fillAllEdges || (spanR.X1 >= spanR.X0 && spanR.Increment > 0x40000) || spanR.Increment == 0 )
			xspan.Flags |= XSpanSetup_FillRight;

		if ( spanL.I0 == spanL.I1 )
		{
			xspan.TexcoordU0 = spanL.TexcoordU0;
			xspan.TexcoordV0 = spanL.TexcoordV0;
			xspan.ColorR0 = spanL.ColorR0;
			xspan.ColorG0 = spanL.ColorG0;
			xspan.ColorB0 = spanL.ColorB0;
			xspan.Z0 = spanL.Z0;
			xspan.W0 = spanL.W0;
		}
		else
		{
			int i = (spanL.Increment > 0x40000 ? xl : y) - spanL.I0;
			int ifactor = CalcYFactorY( spanL, i );
			int idiff = spanL.I1 - spanL.I0;

			#if D_WBUFFER
				xspan.Z0 = int( InterpolateZWBuffer( spanL.Z0, spanL.Z1, ifactor, false ) );
			#else
				xspan.Z0 = int( InterpolateZZBuffer( spanL.Z0, spanL.Z1, i, spanL.IRecip, idiff, true ) );
			#endif

			if ( spanL.Linear == 0 )
			{
				xspan.TexcoordU0 = InterpolateAttrPersp( spanL.TexcoordU0, spanL.TexcoordU1, ifactor );
				xspan.TexcoordV0 = InterpolateAttrPersp( spanL.TexcoordV0, spanL.TexcoordV1, ifactor );
				xspan.ColorR0 = InterpolateAttrPersp( spanL.ColorR0, spanL.ColorR1, ifactor );
				xspan.ColorG0 = InterpolateAttrPersp( spanL.ColorG0, spanL.ColorG1, ifactor );
				xspan.ColorB0 = InterpolateAttrPersp( spanL.ColorB0, spanL.ColorB1, ifactor );
				xspan.W0 = InterpolateAttrPersp( spanL.W0, spanL.W1, ifactor );
			}
			else
			{
				xspan.TexcoordU0 = InterpolateAttrLinear( spanL.TexcoordU0, spanL.TexcoordU1, i, spanL.IRecip, idiff, false );
				xspan.TexcoordV0 = InterpolateAttrLinear( spanL.TexcoordV0, spanL.TexcoordV1, i, spanL.IRecip, idiff, false );
				xspan.ColorR0 = InterpolateAttrLinear( spanL.ColorR0, spanL.ColorR1, i, spanL.IRecip, idiff, false );
				xspan.ColorG0 = InterpolateAttrLinear( spanL.ColorG0, spanL.ColorG1, i, spanL.IRecip, idiff, false );
				xspan.ColorB0 = InterpolateAttrLinear( spanL.ColorB0, spanL.ColorB1, i, spanL.IRecip, idiff, false );
				xspan.W0 = spanL.W0;
			}
		}

		if ( spanR.I0 == spanR.I1 )
		{
			xspan.TexcoordU1 = spanR.TexcoordU0;
			xspan.TexcoordV1 = spanR.TexcoordV0;
			xspan.ColorR1 = spanR.ColorR0;
			xspan.ColorG1 = spanR.ColorG0;
			xspan.ColorB1 = spanR.ColorB0;
			xspan.Z1 = spanR.Z0;
			xspan.W1 = spanR.W0;
		}
		else
		{
			int i = (spanR.Increment > 0x40000 ? xr : y) - spanR.I0;
			int ifactor = CalcYFactorY( spanR, i );
			int idiff = spanR.I1 - spanR.I0;

			#if D_WBUFFER
				xspan.Z1 = int( InterpolateZWBuffer( spanR.Z0, spanR.Z1, ifactor, false ) );
			#else
				xspan.Z1 = int( InterpolateZZBuffer( spanR.Z0, spanR.Z1, i, spanR.IRecip, idiff, true ) );
			#endif

			if ( spanR.Linear == 0 )
			{
				xspan.TexcoordU1 = InterpolateAttrPersp( spanR.TexcoordU0, spanR.TexcoordU1, ifactor );
				xspan.TexcoordV1 = InterpolateAttrPersp( spanR.TexcoordV0, spanR.TexcoordV1, ifactor );
				xspan.ColorR1 = InterpolateAttrPersp( spanR.ColorR0, spanR.ColorR1, ifactor );
				xspan.ColorG1 = InterpolateAttrPersp( spanR.ColorG0, spanR.ColorG1, ifactor );
				xspan.ColorB1 = InterpolateAttrPersp( spanR.ColorB0, spanR.ColorB1, ifactor );
				xspan.W1 = InterpolateAttrPersp( spanR.W0, spanR.W1, ifactor );
			}
			else
			{
				xspan.TexcoordU1 = InterpolateAttrLinear( spanR.TexcoordU0, spanR.TexcoordU1, i, spanR.IRecip, idiff, false );
				xspan.TexcoordV1 = InterpolateAttrLinear( spanR.TexcoordV0, spanR.TexcoordV1, i, spanR.IRecip, idiff, false );
				xspan.ColorR1 = InterpolateAttrLinear( spanR.ColorR0, spanR.ColorR1, i, spanR.IRecip, idiff, false );
				xspan.ColorG1 = InterpolateAttrLinear( spanR.ColorG0, spanR.ColorG1, i, spanR.IRecip, idiff, false );
				xspan.ColorB1 = InterpolateAttrLinear( spanR.ColorB0, spanR.ColorB1, i, spanR.IRecip, idiff, false );
				xspan.W1 = spanR.W0;
			}
		}

		if ( xspan.W0 == xspan.W1 && ((xspan.W0 | xspan.W1) & 0x7F) == 0 )
			xspan.Flags |= XSpanSetup_Linear;

		#if D_WBUFFER
		if ( xspan.W0 == xspan.W1 && ((xspan.W0 | xspan.W1) & 0x7F) == 0 )
		{
			uint r;
			xspan.XRecip = int( NdsDiv( 1u << 30, uint( xspan.X1 - xspan.X0 ), r ) );
		}
		#else
		{
			uint r;
			xspan.XRecip = int( NdsDiv( 1u << 30, uint( xspan.X1 - xspan.X0 ), r ) );
		}
		#endif

		XSpanSetups[id.x] = xspan;
	}
}
