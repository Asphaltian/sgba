#ifndef NDS3D_COMMON_H
#define NDS3D_COMMON_H

#ifndef YFAC
#define YFAC 9
#endif

struct Polygon
{
	int FirstXSpan;
	int YTop;
	int YBot;
	int XMin;
	int XMax;
	int XMinY;
	int XMaxY;
	int Variant;
	uint Attr;
	float TextureLayer;
	uint TexParam;
};

struct YSpanSetup
{
	int Z0;
	int Z1;
	int W0;
	int W1;
	int ColorR0;
	int ColorG0;
	int ColorB0;
	int ColorR1;
	int ColorG1;
	int ColorB1;
	int TexcoordU0;
	int TexcoordV0;
	int TexcoordU1;
	int TexcoordV1;

	int I0;
	int I1;
	int Linear;
	int IRecip;
	int W0n;
	int W0d;
	int W1d;

	int Increment;

	int X0;
	int X1;
	int Y0;
	int Y1;
	int XMin;
	int XMax;
	int DxInitial;

	int XCovIncr;
	uint IsDummy;
};

struct XSpanSetup
{
	int X0;
	int X1;

	int InsideStart;
	int InsideEnd;
	int EdgeCovL;
	int EdgeCovR;

	int XRecip;

	uint Flags;

	int Z0;
	int Z1;
	int W0;
	int W1;
	int ColorR0;
	int ColorG0;
	int ColorB0;
	int ColorR1;
	int ColorG1;
	int ColorB1;
	int TexcoordU0;
	int TexcoordV0;
	int TexcoordU1;
	int TexcoordV1;

	int CovLInitial;
	int CovRInitial;
};

static const uint XSpanSetup_Linear = 1u;
static const uint XSpanSetup_FillInside = 2u;
static const uint XSpanSetup_FillLeft = 4u;
static const uint XSpanSetup_FillRight = 8u;

void UmulExtended( uint a, uint b, out uint hi, out uint lo )
{
	uint al = a & 0xFFFFu;
	uint ah = a >> 16;
	uint bl = b & 0xFFFFu;
	uint bh = b >> 16;
	uint ll = al * bl;
	uint lh = al * bh;
	uint hl = ah * bl;
	uint hh = ah * bh;
	uint cross = (ll >> 16) + (lh & 0xFFFFu) + (hl & 0xFFFFu);
	lo = (ll & 0xFFFFu) | (cross << 16);
	hi = hh + (lh >> 16) + (hl >> 16) + (cross >> 16);
}

uint Umulh( uint a, uint b )
{
	uint hi;
	uint lo;
	UmulExtended( a, b, hi, lo );
	return hi;
}

uint UaddCarry( uint a, uint b, out uint carry )
{
	uint s = a + b;
	carry = (s < a) ? 1u : 0u;
	return s;
}

uint BitfieldInsert( uint base, uint insert, uint offset, uint bits )
{
	uint mask = ((1u << bits) - 1u) << offset;
	return (base & ~mask) | ((insert << offset) & mask);
}

static const uint NdsStartTable[256] =
{
	254, 252, 250, 248, 246, 244, 242, 240, 238, 236, 234, 233, 231, 229, 227, 225, 224, 222, 220, 218, 217, 215, 213, 212, 210, 208, 207, 205, 203, 202, 200, 199, 197, 195, 194, 192, 191, 189, 188, 186, 185, 183, 182, 180, 179, 178, 176, 175, 173, 172, 170, 169, 168, 166, 165, 164, 162, 161, 160, 158,
	157, 156, 154, 153, 152, 151, 149, 148, 147, 146, 144, 143, 142, 141, 139, 138, 137, 136, 135, 134, 132, 131, 130, 129, 128, 127, 126, 125, 123, 122, 121, 120, 119, 118, 117, 116, 115, 114, 113, 112, 111, 110, 109, 108, 107, 106, 105, 104, 103, 102, 101, 100, 99, 98, 97, 96, 95, 94, 93, 92, 91, 90, 89, 88, 88, 87, 86, 85, 84, 83, 82, 81, 80, 80, 79, 78, 77, 76, 75, 74, 74, 73, 72, 71, 70, 70, 69, 68, 67, 66, 66, 65, 64, 63, 62, 62, 61, 60, 59, 59, 58, 57, 56, 56, 55, 54, 53, 53, 52, 51, 50, 50, 49, 48, 48, 47, 46, 46, 45, 44, 43, 43, 42, 41, 41, 40, 39, 39, 38, 37, 37, 36, 35, 35, 34, 33, 33, 32, 32, 31, 30, 30, 29, 28, 28, 27, 27, 26, 25, 25, 24, 24, 23, 22, 22, 21, 21, 20, 19, 19, 18, 18, 17, 17, 16, 15, 15, 14, 14, 13, 13, 12, 12, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 0, 0
};

uint NdsDiv( uint x, uint y, out uint r )
{
	uint k = 31u - firstbithigh( y );
	uint ty = (y << k) >> (32u - 9u);
	uint t = NdsStartTable[ty - 256u] + 256u;
	uint z = (t << (32u - 9u)) >> (32u - k - 1u);
	uint my = 0u - y;

	z += Umulh( z, my * z );
	z += Umulh( z, my * z );

	uint q = Umulh( x, z );
	r = x - y * q;
	if ( r >= y )
	{
		r = r - y;
		q = q + 1u;
		if ( r >= y )
		{
			r = r - y;
			q = q + 1u;
		}
	}

	return q;
}

uint Div64_32_32( uint numHi, uint numLo, uint den )
{
	const uint b = (1u << 16);

	uint shift = 31u - firstbithigh( den );
	den <<= shift;
	numHi <<= shift;
	numHi |= (numLo >> ((0u - shift) & 31u)) & uint( ( -(int)shift ) >> 31 );
	numLo <<= shift;

	uint num1 = (numLo >> 16);
	uint num0 = (numLo & 0xFFFFu);
	uint den1 = (den >> 16);
	uint den0 = (den & 0xFFFFu);

	uint rhat;
	uint qhat = NdsDiv( numHi, den1, rhat );
	uint c1 = qhat * den0;
	uint c2 = rhat * b + num1;
	if ( c1 > c2 ) qhat -= (c1 - c2 > den) ? 2u : 1u;
	uint q1 = qhat & 0xFFFFu;

	uint rem = numHi * b + num1 - q1 * den;

	qhat = NdsDiv( rem, den1, rhat );
	c1 = qhat * den0;
	c2 = rhat * b + num0;
	if ( c1 > c2 ) qhat -= (c1 - c2 > den) ? 2u : 1u;

	return BitfieldInsert( qhat, q1, 16u, 16u );
}

int InterpolateAttrPersp( int y0, int y1, int ifactor )
{
	if ( y0 == y1 )
		return y0;

	if ( y0 < y1 )
		return y0 + (((y1 - y0) * ifactor) >> YFAC);
	else
		return y1 + (((y0 - y1) * ((1 << YFAC) - ifactor)) >> YFAC);
}

int InterpolateAttrLinear( int y0, int y1, int i, int irecip, int idiff, bool rasterise )
{
	if ( y0 == y1 )
		return y0;

	if ( !rasterise )
		irecip = abs( irecip );

	uint mulLo;
	uint mulHi;
	uint carry;
	if ( y0 < y1 )
	{
		uint offset = rasterise ? uint( i ) : uint( abs( i ) );
		UmulExtended( uint( y1 - y0 ) * offset, uint( irecip ), mulHi, mulLo );
		mulLo = UaddCarry( mulLo, 3u << 24, carry );
		mulHi += carry;
		return y0 + int( (mulLo >> 30) | (mulHi << (32 - 30)) );
	}
	else
	{
		uint offset = rasterise ? uint( idiff - i ) : uint( abs( idiff - i ) );
		UmulExtended( uint( y0 - y1 ) * offset, uint( irecip ), mulHi, mulLo );
		mulLo = UaddCarry( mulLo, 3u << 24, carry );
		mulHi += carry;
		return y1 + int( (mulLo >> 30) | (mulHi << (32 - 30)) );
	}
}

uint InterpolateZZBuffer( int z0, int z1, int i, int irecip, int idiff, bool interpSpans )
{
	if ( z0 == z1 )
		return uint( z0 );

	uint base_;
	uint disp;
	uint factor;
	if ( z0 < z1 )
	{
		base_ = uint( z0 );
		disp = uint( z1 - z0 );
		factor = uint( abs( i ) );
	}
	else
	{
		base_ = uint( z1 );
		disp = uint( z0 - z1 );
		factor = uint( abs( idiff - i ) );
	}

	int shiftl;
	int shiftr;
	if ( interpSpans )
	{
		shiftl = 0;
		shiftr = 22;
		if ( disp > 0x3FFu )
		{
			shiftl = int( firstbithigh( disp ) ) - 9;
			disp >>= shiftl;
		}
	}
	else
	{
		disp >>= 9;
		shiftl = 0;
		shiftr = 13;
	}

	uint mulLo;
	uint mulHi;
	UmulExtended( disp * factor, uint( abs( irecip ) ) >> 8, mulHi, mulLo );

	return base_ + (((mulLo >> shiftr) | (mulHi << (32 - shiftr))) << shiftl);
}

uint InterpolateZWBuffer( int z0, int z1, int ifactor, bool rasterise )
{
	if ( z0 == z1 )
		return uint( z0 );

	uint mulLo;
	uint mulHi;
	if ( z0 < z1 )
	{
		UmulExtended( uint( z1 - z0 ), uint( ifactor ), mulHi, mulLo );
		return uint( z0 ) + ((mulLo >> YFAC) | (mulHi << (32 - YFAC)));
	}
	else
	{
		UmulExtended( uint( z0 - z1 ), uint( (1 << YFAC) - ifactor ), mulHi, mulLo );
		return uint( z1 ) + ((mulLo >> YFAC) | (mulHi << (32 - YFAC)));
	}
}

#endif
