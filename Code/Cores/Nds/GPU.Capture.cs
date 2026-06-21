using System.Numerics;

namespace Emulotl.Nds;

public sealed partial class GPU
{
	private const ushort CBFlag_IsCapture = 1 << 15;
	private const ushort CBFlag_Complete = 1 << 14;
	private const ushort CBFlag_Synced = 1 << 13;

	public ushort[] VRAMCaptureBlockFlags = new ushort[16];

	public int[] VRAMCBF_ABG = new int[0x20];
	public int[] VRAMCBF_AOBJ = new int[0x10];
	public int[] VRAMCBF_BBG = new int[0x8];
	public int[] VRAMCBF_BOBJ = new int[0x8];

	private void ResetCapture()
	{
		Array.Clear( VRAMCaptureBlockFlags );
		Array.Fill( VRAMCBF_ABG, -1 );
		Array.Fill( VRAMCBF_AOBJ, -1 );
		Array.Fill( VRAMCBF_BBG, -1 );
		Array.Fill( VRAMCBF_BOBJ, -1 );
	}

	private int GetUniqueBankCBF( uint mask, uint offset )
	{
		if ( mask == 0 || (mask & (mask - 1)) != 0 ) return -1;
		if ( (mask & 0x1F0) != 0 ) return -1;
		int num = BitOperations.TrailingZeroCount( mask );
		offset = (offset >> 1) & 0x3;
		return (num << 2) | (int)offset;
	}

	private void SetCBF( int[] cbf, uint[] map, int i )
	{
		cbf[i] = GetUniqueBankCBF( map[i], (uint)i );
	}

	private void SetRangeCBF( int[] cbf, uint[] map, int based, int n )
	{
		for ( int i = 0; i < n; i++ )
			cbf[based + i] = GetUniqueBankCBF( map[based + i], (uint)(based + i) );
	}

	private void VRAMCBFlagsSet( uint bank, uint block, ushort val )
	{
		int cb = (int)(bank << 2);
		uint len = (uint)((val >> 4) & 0x3);
		uint b = block;
		for ( uint i = 0; i < len; i++ )
		{
			VRAMCaptureBlockFlags[cb + (int)b] = val;
			b = (b + 1) & 0x3;
		}
	}

	private void VRAMCBFlagsClear( uint bank, uint block )
	{
		int cb = (int)(bank << 2);
		ushort flags = VRAMCaptureBlockFlags[cb + (int)block];
		uint start = (uint)(flags & 0x3);
		uint len = (uint)((flags >> 4) & 0x3);
		uint b = start;
		for ( uint i = 0; i < len; i++ )
		{
			VRAMCaptureBlockFlags[cb + (int)b] = 0;
			b = (b + 1) & 0x3;
		}
	}

	private void VRAMCBFlagsOr( uint bank, uint block, ushort val )
	{
		int cb = (int)(bank << 2);
		ushort flags = VRAMCaptureBlockFlags[cb + (int)block];
		uint start = (uint)(flags & 0x3);
		uint len = (uint)((flags >> 4) & 0x3);
		uint b = start;
		for ( uint i = 0; i < len; i++ )
		{
			VRAMCaptureBlockFlags[cb + (int)b] |= val;
			b = (b + 1) & 0x3;
		}
	}

	public void CheckCaptureStart()
	{
		uint dstbank = (CaptureCnt >> 16) & 0x3;
		if ( (VRAMMap_LCDC & (1u << (int)dstbank)) == 0 )
			return;

		uint dstoff = (CaptureCnt >> 18) & 0x3;
		uint size = (CaptureCnt >> 20) & 0x3;
		uint len = (size == 0) ? 1 : size;

		int cb = (int)(dstbank << 2);
		uint b = dstoff;
		for ( uint i = 0; i < len; i++ )
		{
			ushort oldflags = VRAMCaptureBlockFlags[cb + (int)b];
			b = (b + 1) & 0x3;

			if ( (oldflags & CBFlag_IsCapture) == 0 )
				continue;

			uint oldstart = (uint)(oldflags & 0x3);
			uint oldsize = (uint)((oldflags >> 6) & 0x3);
			if ( oldstart == dstoff && oldsize == size )
				continue;

			NDS.Render2DA.SyncVRAMCapture( dstbank, oldstart, oldsize, (oldflags & CBFlag_Complete) != 0 );
			VRAMCBFlagsClear( dstbank, oldstart );
		}

		ushort newval = (ushort)(CBFlag_IsCapture | dstoff | (dstbank << 2) | (len << 4) | (size << 6));
		VRAMCBFlagsSet( dstbank, dstoff, newval );
		NDS.Render2DA.AllocCapture( dstbank, dstoff, size );
	}

	public void CheckCaptureEnd()
	{
		uint dstbank = (CaptureCnt >> 16) & 0x3;
		uint dstoff = (CaptureCnt >> 18) & 0x3;
		uint size = (CaptureCnt >> 20) & 0x3;

		ushort flags = VRAMCaptureBlockFlags[(int)((dstbank << 2) | dstoff)];
		if ( (flags & CBFlag_IsCapture) == 0 )
			return;

		uint oldstart = (uint)(flags & 0x3);
		uint oldsize = (uint)((flags >> 6) & 0x3);
		if ( dstoff != oldstart || size != oldsize )
			return;

		VRAMCBFlagsOr( dstbank, dstoff, CBFlag_Complete );
	}

	public void SyncVRAMCaptureBlock( uint block, bool write )
	{
		ushort flags = VRAMCaptureBlockFlags[(int)block];
		if ( (flags & CBFlag_IsCapture) == 0 ) return;

		uint bank = block >> 2;
		uint start = (uint)(flags & 0x3);
		uint len = (uint)((flags >> 6) & 0x3);

		if ( (flags & CBFlag_Synced) != 0 )
		{
			if ( write )
				VRAMCBFlagsClear( bank, start );
			return;
		}

		NDS.Render2DA.SyncVRAMCapture( bank, start, len, (flags & CBFlag_Complete) != 0 );

		if ( write )
			VRAMCBFlagsClear( bank, start );
		else
			VRAMCBFlagsOr( bank, start, CBFlag_Synced );
	}

	public void SyncAllVRAMCaptures()
	{
		for ( uint b = 0; b < 16; b++ )
		{
			ushort flags = VRAMCaptureBlockFlags[(int)b];
			if ( (flags & CBFlag_IsCapture) == 0 )
				continue;
			if ( (flags & CBFlag_Synced) != 0 )
				continue;

			uint bank = b >> 2;
			uint start = (uint)(flags & 0x3);
			uint len = (uint)((flags >> 6) & 0x3);

			NDS.Render2DA.SyncVRAMCapture( bank, start, len, (flags & CBFlag_Complete) != 0 );
			VRAMCBFlagsClear( bank, start );
		}
	}

	public int GetCaptureBlock_LCDC( uint offset )
	{
		ushort flags = VRAMCaptureBlockFlags[(int)(offset >> 15)];
		if ( (flags & CBFlag_IsCapture) != 0 )
			return (int)(((offset >> 15) & 0xC) | (uint)(flags & 0x3));
		return -1;
	}

	private void GetCaptureInfo( int[] info, int[] cbf, int len )
	{
		for ( int b = 0; b < len; b++ )
		{
			int idx = cbf[b];
			if ( idx < 0 )
			{
				info[b] = -1;
				continue;
			}

			ushort flags = VRAMCaptureBlockFlags[idx];
			if ( (flags & CBFlag_IsCapture) != 0 )
				info[b] = flags & 0xF;
			else
				info[b] = -1;
		}
	}

	public void GetCaptureInfo_ABG( int[] info ) => GetCaptureInfo( info, VRAMCBF_ABG, 32 );
	public void GetCaptureInfo_AOBJ( int[] info ) => GetCaptureInfo( info, VRAMCBF_AOBJ, 16 );
	public void GetCaptureInfo_BBG( int[] info ) => GetCaptureInfo( info, VRAMCBF_BBG, 8 );
	public void GetCaptureInfo_BOBJ( int[] info ) => GetCaptureInfo( info, VRAMCBF_BOBJ, 8 );

	public void GetCaptureInfo_Texture( int[] info )
	{
		for ( int b = 0; b < 16; b++ )
		{
			int bank = b >> 2;
			int subblock = b & 0x3;
			uint mask = VRAMMap_Texture[bank];
			ushort cbf = 0;

			if ( mask == (1 << 0) )
				cbf = VRAMCaptureBlockFlags[(0 << 2) | subblock];
			else if ( mask == (1 << 1) )
				cbf = VRAMCaptureBlockFlags[(1 << 2) | subblock];
			else if ( mask == (1 << 2) )
				cbf = VRAMCaptureBlockFlags[(2 << 2) | subblock];
			else if ( mask == (1 << 3) )
				cbf = VRAMCaptureBlockFlags[(3 << 2) | subblock];

			if ( (cbf & CBFlag_IsCapture) != 0 )
				info[b] = cbf & 0xF;
			else
				info[b] = -1;
		}
	}
}
