namespace Emulotl.Nds;

public sealed partial class GPU
{
	public uint CaptureCnt;
	public bool CaptureEnable;

	private void DoCapture( uint line )
	{
		uint captureCnt = CaptureCnt;

		uint sz = (captureCnt >> 20) & 0x3;
		uint width, height;
		if ( sz == 0 ) { width = 128; height = 128; }
		else { width = 256; height = 64 * sz; }

		if ( line >= height )
			return;

		uint dstbank = (captureCnt >> 16) & 0x3;
		if ( (VRAMMap_LCDC & (1u << (int)dstbank)) == 0 )
			return;

		byte[] dst = VRAM[(int)dstbank];
		uint dstmask = VRAMMask[(int)dstbank];
		uint dstword = ((((captureCnt >> 18) & 0x3) << 14) + (line * width)) & 0xFFFF;

		uint[] srcA = GPU2D_A.Output2D;
		bool srcA3D = (captureCnt & (1 << 24)) != 0;

		byte[] srcBbank = null;
		uint srcBword = 0;
		uint srcBmask = 0;
		if ( (captureCnt & (1 << 25)) == 0 )
		{
			uint dispcnt = GPU2D_A.DispCnt;
			uint srcvram = (dispcnt >> 18) & 0x3;
			if ( (VRAMMap_LCDC & (1u << (int)srcvram)) != 0 )
			{
				srcBbank = VRAM[(int)srcvram];
				srcBmask = VRAMMask[(int)srcvram];
				uint offset = line * 256;
				if ( ((dispcnt >> 16) & 0x3) != 2 )
					offset += ((captureCnt >> 26) & 0x3) << 14;
				srcBword = offset & 0xFFFF;
			}
		}

		uint mode = (captureCnt >> 29) & 0x3;
		uint eva = captureCnt & 0x1F;
		uint evb = (captureCnt >> 8) & 0x1F;
		if ( eva > 16 ) eva = 16;
		if ( evb > 16 ) evb = 16;

		for ( uint i = 0; i < width; i++ )
		{
			ushort outc;

			if ( mode == 0 )
			{
				outc = CaptureSrcA( srcA, srcA3D, i );
			}
			else if ( mode == 1 )
			{
				outc = CaptureSrcB( srcBbank, srcBword, srcBmask, i );
			}
			else
			{
				uint va = srcA[i];
				uint rA = (va >> 1) & 0x1F;
				uint gA = (va >> 9) & 0x1F;
				uint bA = (va >> 17) & 0x1F;
				uint aA = (va >> 24) != 0 ? 1u : 0u;

				ushort vb = CaptureSrcB( srcBbank, srcBword, srcBmask, i );
				uint rB = (uint)(vb & 0x1F);
				uint gB = (uint)((vb >> 5) & 0x1F);
				uint bB = (uint)((vb >> 10) & 0x1F);
				uint aB = (uint)(vb >> 15);

				uint rD = ((rA * aA * eva) + (rB * aB * evb) + 8) >> 4;
				uint gD = ((gA * aA * eva) + (gB * aB * evb) + 8) >> 4;
				uint bD = ((bA * aA * eva) + (bB * aB * evb) + 8) >> 4;
				uint aD = (eva > 0 ? aA : 0) | (evb > 0 ? aB : 0);

				if ( rD > 0x1F ) rD = 0x1F;
				if ( gD > 0x1F ) gD = 0x1F;
				if ( bD > 0x1F ) bD = 0x1F;

				outc = (ushort)(rD | (gD << 5) | (bD << 10) | (aD << 15));
			}

			uint a = ((dstword + i) * 2) & dstmask;
			dst[a] = (byte)outc;
			dst[a + 1] = (byte)(outc >> 8);
		}
	}

	private static ushort CaptureSrcA( uint[] srcA, bool is3D, uint i )
	{
		uint val = srcA[i];
		uint r, g, b, alpha;
		if ( is3D )
		{
			r = (val >> 1) & 0x1F;
			g = (val >> 9) & 0x1F;
			b = (val >> 17) & 0x1F;
			alpha = (val >> 24) != 0 ? 0x8000u : 0;
		}
		else
		{
			r = (val >> 1) & 0x1F;
			g = (val >> 9) & 0x1F;
			b = (val >> 17) & 0x1F;
			alpha = 0x8000u;
		}
		return (ushort)(r | (g << 5) | (b << 10) | alpha);
	}

	private static ushort CaptureSrcB( byte[] bank, uint word, uint mask, uint i )
	{
		if ( bank == null )
			return 0;
		uint a = ((word + i) * 2) & mask;
		return (ushort)(bank[a] | (bank[a + 1] << 8));
	}
}
