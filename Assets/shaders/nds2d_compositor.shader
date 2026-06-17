HEADER
{
	DevShader = true;
	Description = "NDS 2D compositor";
}

MODES
{
	Default();
}

CS
{
	#include "nds2d_common.hlsl"

	Texture2D<float4> BGLayer0 < Attribute( "BGLayer0" ); >;
	Texture2D<float4> BGLayer1 < Attribute( "BGLayer1" ); >;
	Texture2D<float4> BGLayer2 < Attribute( "BGLayer2" ); >;
	Texture2D<float4> BGLayer3 < Attribute( "BGLayer3" ); >;
	Texture2D<float4> OBJColor < Attribute( "OBJColor" ); >;
	Texture2D<float4> OBJFlags < Attribute( "OBJFlags" ); >;

	StructuredBuffer<sScanline> Scanlines < Attribute( "Scanlines" ); >;
	StructuredBuffer<sBGConfig> BGConfig < Attribute( "BGConfig" ); >;
	StructuredBuffer<int> MosaicTable < Attribute( "MosaicTable" ); >;
	StructuredBuffer<uint> VramDisplay < Attribute( "VramDisplay" ); >;

	RWTexture2D<float4> OutputTex < Attribute( "OutputTex" ); >;

	int uScaleFactor < Attribute( "ScaleFactor" ); >;
	int uBGPrio0 < Attribute( "BGPrio0" ); >;
	int uBGPrio1 < Attribute( "BGPrio1" ); >;
	int uBGPrio2 < Attribute( "BGPrio2" ); >;
	int uBGPrio3 < Attribute( "BGPrio3" ); >;
	int uEnableOBJ < Attribute( "EnableOBJ" ); >;
	int uEnable3D < Attribute( "Enable3D" ); >;
	int uBlendCnt < Attribute( "BlendCnt" ); >;
	int uBlendEffect < Attribute( "BlendEffect" ); >;
	int uBlendCoef0 < Attribute( "BlendCoef0" ); >;
	int uBlendCoef1 < Attribute( "BlendCoef1" ); >;
	int uBlendCoef2 < Attribute( "BlendCoef2" ); >;
	int uOutputW < Attribute( "OutputW" ); >;
	int uOutputH < Attribute( "OutputH" ); >;
	int uBrightMode < Attribute( "BrightMode" ); >;
	int uBrightFactor < Attribute( "BrightFactor" ); >;
	int uScreenOff < Attribute( "ScreenOff" ); >;
	int uDispMode < Attribute( "DispMode" ); >;

	static int MosaicX = 0;

	int GetBGPrio( int bg )
	{
		if ( bg == 0 ) return uBGPrio0;
		if ( bg == 1 ) return uBGPrio1;
		if ( bg == 2 ) return uBGPrio2;
		return uBGPrio3;
	}

	int3 ConvertColor( int col )
	{
		int3 ret;
		ret.r = ( col & 0x1F ) << 1;
		ret.g = ( ( col & 0x3E0 ) >> 4 ) | ( col >> 15 );
		ret.b = ( col & 0x7C00 ) >> 9;
		return ret;
	}

	float4 LoadLayer( int bg, int2 ip )
	{
		if ( bg == 0 ) return BGLayer0.Load( int3( ip, 0 ) );
		if ( bg == 1 ) return BGLayer1.Load( int3( ip, 0 ) );
		if ( bg == 2 ) return BGLayer2.Load( int3( ip, 0 ) );
		return BGLayer3.Load( int3( ip, 0 ) );
	}

	float4 BGFetch( int bg, float2 bgpos )
	{
		sBGConfig cfg = BGConfig[bg];

		if ( cfg.Type == 6 )
		{
			int2 ip3 = int2( floor( bgpos * float( uScaleFactor ) ) );
			int sw = cfg.SizeX * uScaleFactor;
			int sh = cfg.SizeY * uScaleFactor;
			if ( ip3.x < 0 || ip3.y < 0 || ip3.x >= sw || ip3.y >= sh )
				return float4( 0, 0, 0, 0 );
			return LoadLayer( bg, ip3 );
		}

		int2 ip = int2( floor( bgpos ) );

		if ( cfg.Clamp != 0 )
		{
			if ( ip.x < 0 || ip.y < 0 || ip.x >= cfg.SizeX || ip.y >= cfg.SizeY )
				return float4( 0, 0, 0, 0 );
		}
		else
		{
			ip.x = ip.x & ( cfg.SizeX - 1 );
			ip.y = ip.y & ( cfg.SizeY - 1 );
		}

		return LoadLayer( bg, ip );
	}

	float4 BG01CalcAndFetch( int bg, float2 coord, int ln )
	{
		sScanline s = Scanlines[ln];
		float2 bgoffset = float2( GetBGOffsetX( s, bg ), GetBGOffsetY( s, bg ) );
		float2 bgpos = bgoffset + coord;

		if ( GetBGMosaicEnable( s, bg ) != 0 )
			bgpos = floor( bgpos ) - float2( MosaicX, 0 );

		return BGFetch( bg, bgpos );
	}

	float4 BG23CalcAndFetch( int bg, float2 coord, int ln )
	{
		sScanline s = Scanlines[ln];
		sBGConfig cfg = BGConfig[bg];
		float2 bgoffset = float2( GetBGOffsetX( s, bg ), GetBGOffsetY( s, bg ) );

		float2 bgpos;
		if ( cfg.Type >= 2 )
		{
			bgpos = bgoffset / 256.0;
			float4 rs;
			if ( bg == 2 )
				rs = float4( s.BGRotscale0_0, s.BGRotscale0_1, s.BGRotscale0_2, s.BGRotscale0_3 ) / 256.0;
			else
				rs = float4( s.BGRotscale1_0, s.BGRotscale1_1, s.BGRotscale1_2, s.BGRotscale1_3 ) / 256.0;
			float2x2 rsmatrix = float2x2( rs.x, rs.z, rs.y, rs.w );
			bgpos = bgpos + mul( coord, rsmatrix );
		}
		else
		{
			bgpos = bgoffset + coord;
		}

		if ( GetBGMosaicEnable( s, bg ) != 0 )
			bgpos = floor( bgpos ) - float2( MosaicX, 0 );

		if ( cfg.Type >= 7 )
			return float4( 0, 0, 0, 0 );

		return BGFetch( bg, bgpos );
	}

	float4 ObjColorAt( int2 coord )
	{
		return OBJColor.Load( int3( coord, 0 ) );
	}

	float4 ObjFlagsAt( int2 coord )
	{
		return OBJFlags.Load( int3( coord, 0 ) );
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( (int)id.x >= uOutputW || (int)id.y >= uOutputH )
			return;

		float2 nativef = ( float2( id.xy ) + 0.5 ) / float( uScaleFactor );
		float2 bgcoord = float2( nativef.x, frac( nativef.y ) );
		int xpos = (int)nativef.x;
		int ln = (int)nativef.y;
		int2 coord = int2( id.xy );

		sScanline s = Scanlines[ln];

		if ( s.MosaicSize0 > 0 )
			MosaicX = MosaicTable[s.MosaicSize0 * 256 + (int)bgcoord.x];

		int4 col1 = int4( ConvertColor( s.BackColor ), 0x20 );
		int mask1 = 0x20;
		int4 col2 = int4( 0, 0, 0, 0 );
		int mask2 = 0;
		bool specialcase = false;

		float4 layercol0 = BG01CalcAndFetch( 0, bgcoord, ln );
		float4 layercol1 = BG01CalcAndFetch( 1, bgcoord, ln );
		float4 layercol2 = BG23CalcAndFetch( 2, bgcoord, ln );
		float4 layercol3 = BG23CalcAndFetch( 3, bgcoord, ln );
		float4 layercol4 = ObjColorAt( coord );
		int4 objflags = int4( ObjFlagsAt( coord ) * 255.0 );

		int winmask = s.WinMask;
		bool inside_win0;
		bool inside_win1;

		if ( xpos < s.WinPos0 )
			inside_win0 = ( ( winmask & ( 1 << 0 ) ) != 0 );
		else if ( xpos < s.WinPos1 )
			inside_win0 = ( ( winmask & ( 1 << 1 ) ) != 0 );
		else
			inside_win0 = ( ( winmask & ( 1 << 2 ) ) != 0 );

		if ( xpos < s.WinPos2 )
			inside_win1 = ( ( winmask & ( 1 << 3 ) ) != 0 );
		else if ( xpos < s.WinPos3 )
			inside_win1 = ( ( winmask & ( 1 << 4 ) ) != 0 );
		else
			inside_win1 = ( ( winmask & ( 1 << 5 ) ) != 0 );

		uint winregs = s.WinRegs;
		uint winsel = winregs;
		if ( objflags.b > 0 )
			winsel = winregs >> 8;
		if ( inside_win1 )
			winsel = winregs >> 16;
		if ( inside_win0 )
			winsel = winregs >> 24;

		for ( int prio = 3; prio >= 0; prio-- )
		{
			for ( int bg = 3; bg >= 0; bg-- )
			{
				float4 lc = ( bg == 0 ) ? layercol0 : ( bg == 1 ) ? layercol1 : ( bg == 2 ) ? layercol2 : layercol3;
				if ( ( GetBGPrio( bg ) == prio ) && ( lc.a > 0.0 ) && ( ( winsel & ( 1u << (uint)bg ) ) != 0u ) )
				{
					col2 = col1;
					mask2 = mask1 << 8;
					col1 = int4( lc * 255.0 ) >> int4( 2, 2, 2, 3 );
					mask1 = (int)( 1u << (uint)bg );
					specialcase = ( bg == 0 ) && ( uEnable3D != 0 );
				}
			}

			if ( ( uEnableOBJ != 0 ) && ( objflags.a == prio ) && ( layercol4.a > 0.0 ) && ( ( winsel & ( 1u << 4 ) ) != 0u ) )
			{
				col2 = col1;
				mask2 = mask1 << 8;
				col1 = int4( layercol4 * 255.0 ) >> int4( 2, 2, 2, 3 );
				mask1 = ( 1 << 4 );
				specialcase = ( objflags.r != 0 );
			}
		}

		int effect = 0;
		int eva = 0;
		int evb = 0;
		int evy = uBlendCoef2;

		if ( specialcase && ( ( uBlendCnt & mask2 ) != 0 ) )
		{
			if ( mask1 == ( 1 << 0 ) )
			{
				effect = 4;
				eva = ( col1.a & 0x1F ) + 1;
				evb = 32 - eva;
			}
			else if ( objflags.r == 1 )
			{
				effect = 1;
				eva = uBlendCoef0;
				evb = uBlendCoef1;
			}
			else
			{
				effect = 1;
				eva = col1.a;
				evb = 16 - eva;
			}
		}
		else if ( ( ( uBlendCnt & mask1 ) != 0 ) && ( ( winsel & ( 1u << 5 ) ) != 0u ) )
		{
			effect = uBlendEffect;
			if ( effect == 1 )
			{
				if ( ( uBlendCnt & mask2 ) != 0 )
				{
					eva = uBlendCoef0;
					evb = uBlendCoef1;
				}
				else
					effect = 0;
			}
		}

		if ( effect == 1 )
		{
			col1 = ( ( col1 * eva ) + ( col2 * evb ) + 0x8 ) >> 4;
			col1 = min( col1, 0x3F );
		}
		else if ( effect == 2 )
		{
			col1 = col1 + ( ( ( ( 0x3F - col1 ) * evy ) + 0x8 ) >> 4 );
		}
		else if ( effect == 3 )
		{
			col1 = col1 - ( ( ( col1 * evy ) + 0x7 ) >> 4 );
		}
		else if ( effect == 4 )
		{
			col1 = ( ( col1 * eva ) + ( col2 * evb ) + 0x10 ) >> 5;
		}

		int3 c;
		if ( uDispMode == 2 )
		{
			int vcol = (int)LoadU16( VramDisplay, ln * 256 + xpos );
			c = int3( ( vcol & 0x1F ) << 1, ( ( vcol >> 5 ) & 0x1F ) << 1, ( ( vcol >> 10 ) & 0x1F ) << 1 );
		}
		else
		{
			c = col1.rgb;
		}

		if ( uScreenOff == 1 )
		{
			c = int3( 63, 63, 63 );
		}
		else if ( uScreenOff == 2 )
		{
			c = int3( 0, 0, 0 );
		}
		else
		{
			if ( uBrightMode == 1 )
				c += ( ( ( int3( 63, 63, 63 ) - c ) * uBrightFactor ) >> 4 );
			else if ( uBrightMode == 2 )
				c -= ( ( ( c * uBrightFactor ) + 0xF ) >> 4 );
		}

		int3 c8 = ( c << 2 ) | ( c >> 6 );
		OutputTex[id.xy] = float4( float3( c8 ) / 255.0, 1.0 );
	}
}
