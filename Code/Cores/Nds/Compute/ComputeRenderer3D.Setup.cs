using System.Runtime.InteropServices;

namespace Emulotl.Nds;

[StructLayout( LayoutKind.Sequential )]
public struct RenderPolygonGpu
{
	public uint FirstXSpan;
	public int YTop, YBot;
	public int XMin, XMax;
	public int XMinY, XMaxY;
	public uint Variant;
	public uint Attr;
	public float TextureLayer;
	public uint TexParam;
}

[StructLayout( LayoutKind.Sequential )]
public struct SpanSetupY
{
	public int Z0, Z1, W0, W1;
	public int ColorR0, ColorG0, ColorB0;
	public int ColorR1, ColorG1, ColorB1;
	public int TexcoordU0, TexcoordV0;
	public int TexcoordU1, TexcoordV1;

	public int I0, I1;
	public int Linear;
	public int IRecip;
	public int W0n, W0d, W1d;

	public int Increment;

	public int X0, X1, Y0, Y1;
	public int XMin, XMax;
	public int DxInitial;

	public int XCovIncr;
	public uint IsDummy;
}

[StructLayout( LayoutKind.Sequential )]
public struct SpanSetupX
{
	public int X0, X1;

	public int EdgeLenL, EdgeLenR, EdgeCovL, EdgeCovR;

	public int XRecip;

	public uint Flags;

	public int Z0, Z1, W0, W1;
	public int ColorR0, ColorG0, ColorB0;
	public int ColorR1, ColorG1, ColorB1;
	public int TexcoordU0, TexcoordV0;
	public int TexcoordU1, TexcoordV1;

	public int CovLInitial, CovRInitial;
}

[StructLayout( LayoutKind.Sequential )]
public struct SetupIndices
{
	public ushort PolyIdx, SpanIdxL, SpanIdxR, Y;
}

public sealed partial class ComputeRenderer3D
{
	private void SetupAttrs( ref SpanSetupY span, Polygon poly, int from, int to )
	{
		span.Z0 = poly.FinalZ[from];
		span.W0 = poly.FinalW[from];
		span.Z1 = poly.FinalZ[to];
		span.W1 = poly.FinalW[to];

		ref Vertex vf = ref _gpu.VertexRAM[poly.Vertices[from]];
		ref Vertex vt = ref _gpu.VertexRAM[poly.Vertices[to]];

		span.ColorR0 = vf.Fcr;
		span.ColorG0 = vf.Fcg;
		span.ColorB0 = vf.Fcb;
		span.ColorR1 = vt.Fcr;
		span.ColorG1 = vt.Fcg;
		span.ColorB1 = vt.Fcb;
		span.TexcoordU0 = vf.Tu;
		span.TexcoordV0 = vf.Tv;
		span.TexcoordU1 = vt.Tu;
		span.TexcoordV1 = vt.Tv;
	}

	private void SetupYSpanDummy( ref RenderPolygonGpu rp, ref SpanSetupY span, Polygon poly, int vertex, int side, int[,] positions )
	{
		int x0 = positions[vertex, 0];
		if ( side != 0 )
		{
			span.DxInitial = -0x40000;
			x0--;
		}
		else
		{
			span.DxInitial = 0;
		}

		span.X0 = span.X1 = x0;
		span.XMin = x0;
		span.XMax = x0;
		span.Y0 = span.Y1 = positions[vertex, 1];

		if ( span.XMin < rp.XMin )
		{
			rp.XMin = span.XMin;
			rp.XMinY = span.Y0;
		}
		if ( span.XMax > rp.XMax )
		{
			rp.XMax = span.XMax;
			rp.XMaxY = span.Y0;
		}

		span.Increment = 0;

		span.I0 = span.I1 = span.IRecip = 0;
		span.Linear = 1;

		span.XCovIncr = 0;

		span.IsDummy = 1;

		SetupAttrs( ref span, poly, vertex, vertex );
	}

	private void SetupYSpan( ref RenderPolygonGpu rp, ref SpanSetupY span, Polygon poly, int from, int to, int side, int[,] positions )
	{
		span.X0 = positions[from, 0];
		span.X1 = positions[to, 0];
		span.Y0 = positions[from, 1];
		span.Y1 = positions[to, 1];

		SetupAttrs( ref span, poly, from, to );

		int minXY, maxXY;
		bool negative = false;
		if ( span.X1 > span.X0 )
		{
			span.XMin = span.X0;
			span.XMax = span.X1 - 1;

			minXY = span.Y0;
			maxXY = span.Y1;
		}
		else if ( span.X1 < span.X0 )
		{
			span.XMin = span.X1;
			span.XMax = span.X0 - 1;
			negative = true;

			minXY = span.Y1;
			maxXY = span.Y0;
		}
		else
		{
			span.XMin = span.X0;
			if ( side != 0 ) span.XMin--;
			span.XMax = span.XMin;

			minXY = span.Y0;
			maxXY = span.Y0;
		}

		if ( span.XMin < rp.XMin )
		{
			rp.XMin = span.XMin;
			rp.XMinY = minXY;
		}
		if ( span.XMax > rp.XMax )
		{
			rp.XMax = span.XMax;
			rp.XMaxY = maxXY;
		}

		span.IsDummy = 0;

		int xlen = span.XMax + 1 - span.XMin;
		int ylen = span.Y1 - span.Y0;

		if ( ylen == 0 )
		{
			span.Increment = 0;
		}
		else if ( ylen == xlen )
		{
			span.Increment = 0x40000;
		}
		else
		{
			int yrecip = (1 << 18) / ylen;
			span.Increment = (span.X1 - span.X0) * yrecip;
			if ( span.Increment < 0 ) span.Increment = -span.Increment;
		}

		bool xMajor = span.Increment > 0x40000;

		if ( side != 0 )
		{
			if ( xMajor )
				span.DxInitial = negative ? (0x20000 + 0x40000) : (span.Increment - 0x20000);
			else if ( span.Increment != 0 )
				span.DxInitial = negative ? 0x40000 : 0;
			else
				span.DxInitial = -0x40000;
		}
		else
		{
			if ( xMajor )
				span.DxInitial = negative ? (span.Increment - 0x20000 + 0x40000) : 0x20000;
			else if ( span.Increment != 0 )
				span.DxInitial = negative ? 0x40000 : 0;
			else
				span.DxInitial = 0;
		}

		if ( xMajor )
		{
			if ( side != 0 )
			{
				span.I0 = span.X0 - 1;
				span.I1 = span.X1 - 1;
			}
			else
			{
				span.I0 = span.X0;
				span.I1 = span.X1;
			}

			span.XCovIncr = (ylen << 10) / xlen;
		}
		else
		{
			span.I0 = span.Y0;
			span.I1 = span.Y1;
		}

		if ( span.I0 != span.I1 )
			span.IRecip = (1 << 30) / (span.I1 - span.I0);
		else
			span.IRecip = 0;

		span.Linear = (span.W0 == span.W1) && ((span.W0 & 0x7E) == 0) && ((span.W1 & 0x7E) == 0) ? 1 : 0;

		if ( (span.W0 & 0x1) != 0 && (span.W1 & 0x1) == 0 )
		{
			span.W0n = (span.W0 - 1) >> 1;
			span.W0d = (span.W0 + 1) >> 1;
			span.W1d = span.W1 >> 1;
		}
		else
		{
			span.W0n = span.W0 >> 1;
			span.W0d = span.W0 >> 1;
			span.W1d = span.W1 >> 1;
		}
	}
}
