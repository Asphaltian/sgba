using System.Runtime.InteropServices;

namespace Emulotl.Nds;

[StructLayout( LayoutKind.Sequential )]
public struct sBGConfig
{
	public int SizeX;
	public int SizeY;
	public int Type;
	public int PalOffset;
	public int TileOffset;
	public int MapOffset;
	public int Clamp;
	public int Pad;
}

[StructLayout( LayoutKind.Sequential )]
public struct sOAM
{
	public int PositionX;
	public int PositionY;
	public int FlipX;
	public int FlipY;
	public int SizeX;
	public int SizeY;
	public int BoundSizeX;
	public int BoundSizeY;
	public int OBJMode;
	public int Type;
	public int PalOffset;
	public int TileOffset;
	public int TileStride;
	public int Rotscale;
	public int BGPrio;
	public int Mosaic;
}

[StructLayout( LayoutKind.Sequential )]
public struct Vec4i
{
	public int X;
	public int Y;
	public int Z;
	public int W;
}

[StructLayout( LayoutKind.Sequential )]
public struct sScanline
{
	public int BGOffsetX0;
	public int BGOffsetY0;
	public int BGOffsetX1;
	public int BGOffsetY1;
	public int BGOffsetX2;
	public int BGOffsetY2;
	public int BGOffsetX3;
	public int BGOffsetY3;

	public int BGRotscale0_0;
	public int BGRotscale0_1;
	public int BGRotscale0_2;
	public int BGRotscale0_3;
	public int BGRotscale1_0;
	public int BGRotscale1_1;
	public int BGRotscale1_2;
	public int BGRotscale1_3;

	public int BackColor;
	public uint WinRegs;
	public int WinMask;

	public int WinPos0;
	public int WinPos1;
	public int WinPos2;
	public int WinPos3;

	public int BGMosaicEnable0;
	public int BGMosaicEnable1;
	public int BGMosaicEnable2;
	public int BGMosaicEnable3;

	public int MosaicSize0;
	public int MosaicSize1;
	public int MosaicSize2;
	public int MosaicSize3;
}

public struct sCompositorConfig
{
	public int BGPrio0;
	public int BGPrio1;
	public int BGPrio2;
	public int BGPrio3;
	public int EnableOBJ;
	public int Enable3D;
	public int BlendCnt;
	public int BlendEffect;
	public int BlendCoef0;
	public int BlendCoef1;
	public int BlendCoef2;
}
