namespace Emulotl.Nds;

public sealed partial class GPU3D
{
	private static void MatrixLoadIdentity( int[] m )
	{
		m[0] = 0x1000; m[1] = 0; m[2] = 0; m[3] = 0;
		m[4] = 0; m[5] = 0x1000; m[6] = 0; m[7] = 0;
		m[8] = 0; m[9] = 0; m[10] = 0x1000; m[11] = 0;
		m[12] = 0; m[13] = 0; m[14] = 0; m[15] = 0x1000;
	}

	private static void MatrixLoad4x4( int[] m, int[] s )
	{
		for ( int i = 0; i < 16; i++ ) m[i] = s[i];
	}

	private static void MatrixLoad4x3( int[] m, int[] s )
	{
		m[0] = s[0]; m[1] = s[1]; m[2] = s[2]; m[3] = 0;
		m[4] = s[3]; m[5] = s[4]; m[6] = s[5]; m[7] = 0;
		m[8] = s[6]; m[9] = s[7]; m[10] = s[8]; m[11] = 0;
		m[12] = s[9]; m[13] = s[10]; m[14] = s[11]; m[15] = 0x1000;
	}

	private static void MatrixMult4x4( int[] m, int[] s )
	{
		int[] tmp = new int[16];
		for ( int i = 0; i < 16; i++ ) tmp[i] = m[i];

		m[0] = (int)(((long)s[0] * tmp[0] + (long)s[1] * tmp[4] + (long)s[2] * tmp[8] + (long)s[3] * tmp[12]) >> 12);
		m[1] = (int)(((long)s[0] * tmp[1] + (long)s[1] * tmp[5] + (long)s[2] * tmp[9] + (long)s[3] * tmp[13]) >> 12);
		m[2] = (int)(((long)s[0] * tmp[2] + (long)s[1] * tmp[6] + (long)s[2] * tmp[10] + (long)s[3] * tmp[14]) >> 12);
		m[3] = (int)(((long)s[0] * tmp[3] + (long)s[1] * tmp[7] + (long)s[2] * tmp[11] + (long)s[3] * tmp[15]) >> 12);

		m[4] = (int)(((long)s[4] * tmp[0] + (long)s[5] * tmp[4] + (long)s[6] * tmp[8] + (long)s[7] * tmp[12]) >> 12);
		m[5] = (int)(((long)s[4] * tmp[1] + (long)s[5] * tmp[5] + (long)s[6] * tmp[9] + (long)s[7] * tmp[13]) >> 12);
		m[6] = (int)(((long)s[4] * tmp[2] + (long)s[5] * tmp[6] + (long)s[6] * tmp[10] + (long)s[7] * tmp[14]) >> 12);
		m[7] = (int)(((long)s[4] * tmp[3] + (long)s[5] * tmp[7] + (long)s[6] * tmp[11] + (long)s[7] * tmp[15]) >> 12);

		m[8] = (int)(((long)s[8] * tmp[0] + (long)s[9] * tmp[4] + (long)s[10] * tmp[8] + (long)s[11] * tmp[12]) >> 12);
		m[9] = (int)(((long)s[8] * tmp[1] + (long)s[9] * tmp[5] + (long)s[10] * tmp[9] + (long)s[11] * tmp[13]) >> 12);
		m[10] = (int)(((long)s[8] * tmp[2] + (long)s[9] * tmp[6] + (long)s[10] * tmp[10] + (long)s[11] * tmp[14]) >> 12);
		m[11] = (int)(((long)s[8] * tmp[3] + (long)s[9] * tmp[7] + (long)s[10] * tmp[11] + (long)s[11] * tmp[15]) >> 12);

		m[12] = (int)(((long)s[12] * tmp[0] + (long)s[13] * tmp[4] + (long)s[14] * tmp[8] + (long)s[15] * tmp[12]) >> 12);
		m[13] = (int)(((long)s[12] * tmp[1] + (long)s[13] * tmp[5] + (long)s[14] * tmp[9] + (long)s[15] * tmp[13]) >> 12);
		m[14] = (int)(((long)s[12] * tmp[2] + (long)s[13] * tmp[6] + (long)s[14] * tmp[10] + (long)s[15] * tmp[14]) >> 12);
		m[15] = (int)(((long)s[12] * tmp[3] + (long)s[13] * tmp[7] + (long)s[14] * tmp[11] + (long)s[15] * tmp[15]) >> 12);
	}

	private static void MatrixMult4x3( int[] m, int[] s )
	{
		int[] tmp = new int[16];
		for ( int i = 0; i < 16; i++ ) tmp[i] = m[i];

		m[0] = (int)(((long)s[0] * tmp[0] + (long)s[1] * tmp[4] + (long)s[2] * tmp[8]) >> 12);
		m[1] = (int)(((long)s[0] * tmp[1] + (long)s[1] * tmp[5] + (long)s[2] * tmp[9]) >> 12);
		m[2] = (int)(((long)s[0] * tmp[2] + (long)s[1] * tmp[6] + (long)s[2] * tmp[10]) >> 12);
		m[3] = (int)(((long)s[0] * tmp[3] + (long)s[1] * tmp[7] + (long)s[2] * tmp[11]) >> 12);

		m[4] = (int)(((long)s[3] * tmp[0] + (long)s[4] * tmp[4] + (long)s[5] * tmp[8]) >> 12);
		m[5] = (int)(((long)s[3] * tmp[1] + (long)s[4] * tmp[5] + (long)s[5] * tmp[9]) >> 12);
		m[6] = (int)(((long)s[3] * tmp[2] + (long)s[4] * tmp[6] + (long)s[5] * tmp[10]) >> 12);
		m[7] = (int)(((long)s[3] * tmp[3] + (long)s[4] * tmp[7] + (long)s[5] * tmp[11]) >> 12);

		m[8] = (int)(((long)s[6] * tmp[0] + (long)s[7] * tmp[4] + (long)s[8] * tmp[8]) >> 12);
		m[9] = (int)(((long)s[6] * tmp[1] + (long)s[7] * tmp[5] + (long)s[8] * tmp[9]) >> 12);
		m[10] = (int)(((long)s[6] * tmp[2] + (long)s[7] * tmp[6] + (long)s[8] * tmp[10]) >> 12);
		m[11] = (int)(((long)s[6] * tmp[3] + (long)s[7] * tmp[7] + (long)s[8] * tmp[11]) >> 12);

		m[12] = (int)(((long)s[9] * tmp[0] + (long)s[10] * tmp[4] + (long)s[11] * tmp[8] + (long)0x1000 * tmp[12]) >> 12);
		m[13] = (int)(((long)s[9] * tmp[1] + (long)s[10] * tmp[5] + (long)s[11] * tmp[9] + (long)0x1000 * tmp[13]) >> 12);
		m[14] = (int)(((long)s[9] * tmp[2] + (long)s[10] * tmp[6] + (long)s[11] * tmp[10] + (long)0x1000 * tmp[14]) >> 12);
		m[15] = (int)(((long)s[9] * tmp[3] + (long)s[10] * tmp[7] + (long)s[11] * tmp[11] + (long)0x1000 * tmp[15]) >> 12);
	}

	private static void MatrixMult3x3( int[] m, int[] s )
	{
		int[] tmp = new int[12];
		for ( int i = 0; i < 12; i++ ) tmp[i] = m[i];

		m[0] = (int)(((long)s[0] * tmp[0] + (long)s[1] * tmp[4] + (long)s[2] * tmp[8]) >> 12);
		m[1] = (int)(((long)s[0] * tmp[1] + (long)s[1] * tmp[5] + (long)s[2] * tmp[9]) >> 12);
		m[2] = (int)(((long)s[0] * tmp[2] + (long)s[1] * tmp[6] + (long)s[2] * tmp[10]) >> 12);
		m[3] = (int)(((long)s[0] * tmp[3] + (long)s[1] * tmp[7] + (long)s[2] * tmp[11]) >> 12);

		m[4] = (int)(((long)s[3] * tmp[0] + (long)s[4] * tmp[4] + (long)s[5] * tmp[8]) >> 12);
		m[5] = (int)(((long)s[3] * tmp[1] + (long)s[4] * tmp[5] + (long)s[5] * tmp[9]) >> 12);
		m[6] = (int)(((long)s[3] * tmp[2] + (long)s[4] * tmp[6] + (long)s[5] * tmp[10]) >> 12);
		m[7] = (int)(((long)s[3] * tmp[3] + (long)s[4] * tmp[7] + (long)s[5] * tmp[11]) >> 12);

		m[8] = (int)(((long)s[6] * tmp[0] + (long)s[7] * tmp[4] + (long)s[8] * tmp[8]) >> 12);
		m[9] = (int)(((long)s[6] * tmp[1] + (long)s[7] * tmp[5] + (long)s[8] * tmp[9]) >> 12);
		m[10] = (int)(((long)s[6] * tmp[2] + (long)s[7] * tmp[6] + (long)s[8] * tmp[10]) >> 12);
		m[11] = (int)(((long)s[6] * tmp[3] + (long)s[7] * tmp[7] + (long)s[8] * tmp[11]) >> 12);
	}

	private static void MatrixScale( int[] m, int[] s )
	{
		m[0] = (int)(((long)s[0] * m[0]) >> 12);
		m[1] = (int)(((long)s[0] * m[1]) >> 12);
		m[2] = (int)(((long)s[0] * m[2]) >> 12);
		m[3] = (int)(((long)s[0] * m[3]) >> 12);

		m[4] = (int)(((long)s[1] * m[4]) >> 12);
		m[5] = (int)(((long)s[1] * m[5]) >> 12);
		m[6] = (int)(((long)s[1] * m[6]) >> 12);
		m[7] = (int)(((long)s[1] * m[7]) >> 12);

		m[8] = (int)(((long)s[2] * m[8]) >> 12);
		m[9] = (int)(((long)s[2] * m[9]) >> 12);
		m[10] = (int)(((long)s[2] * m[10]) >> 12);
		m[11] = (int)(((long)s[2] * m[11]) >> 12);
	}

	private static void MatrixTranslate( int[] m, int[] s )
	{
		m[12] += (int)(((long)s[0] * m[0] + (long)s[1] * m[4] + (long)s[2] * m[8]) >> 12);
		m[13] += (int)(((long)s[0] * m[1] + (long)s[1] * m[5] + (long)s[2] * m[9]) >> 12);
		m[14] += (int)(((long)s[0] * m[2] + (long)s[1] * m[6] + (long)s[2] * m[10]) >> 12);
		m[15] += (int)(((long)s[0] * m[3] + (long)s[1] * m[7] + (long)s[2] * m[11]) >> 12);
	}

	private void UpdateClipMatrix()
	{
		if ( !ClipMatrixDirty ) return;
		ClipMatrixDirty = false;

		for ( int i = 0; i < 16; i++ ) ClipMatrix[i] = ProjMatrix[i];
		MatrixMult4x4( ClipMatrix, PosMatrix );
	}
}
