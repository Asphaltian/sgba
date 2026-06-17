namespace Emulotl.Nds;

public struct Vertex
{
	public int Px, Py, Pz, Pw;
	public int Cr, Cg, Cb;
	public short Tu, Tv;

	public bool Clipped;

	public int Fx, Fy;
	public int Fcr, Fcg, Fcb;
	public int Hx, Hy;

	public readonly int GetPos( int i ) => i switch { 0 => Px, 1 => Py, 2 => Pz, _ => Pw };
	public void SetPos( int i, int v ) { switch ( i ) { case 0: Px = v; break; case 1: Py = v; break; case 2: Pz = v; break; default: Pw = v; break; } }
	public readonly int GetColor( int i ) => i switch { 0 => Cr, 1 => Cg, _ => Cb };
	public readonly int GetTex( int i ) => i == 0 ? Tu : Tv;
}

public sealed class Polygon
{
	public int[] Vertices = new int[10];
	public uint NumVertices;

	public int[] FinalZ = new int[10];
	public int[] FinalW = new int[10];
	public bool WBuffer;

	public uint Attr;
	public uint TexParam;
	public uint TexPalette;

	public bool Degenerate;

	public bool FacingView;
	public bool Translucent;

	public bool IsShadowMask;
	public bool IsShadow;

	public int Type;

	public uint VTop, VBottom;
	public int YTop, YBottom;
	public int XTop, XBottom;

	public uint SortKey;
}
