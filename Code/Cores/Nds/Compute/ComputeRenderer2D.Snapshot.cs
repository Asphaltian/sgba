using System.Threading;

namespace Emulotl.Nds;

public sealed partial class ComputeRenderer2D
{
	private sScanline[][] _snapScan;
	private uint[][] _snapVramBG;
	private uint[][] _snapVramOBJ;
	private uint[][] _snapPalBG;
	private uint[][] _snapPalOBJ;
	private sBGConfig[][] _snapLayer;
	private sOAM[][] _snapOAM;
	private Vec4i[][] _snapRot;
	private int[] _snapNumSprites;
	private sCompositorConfig[] _snapComp;
	private int[] _snapWindowEnable;
	private int[] _snapMasterBright;
	private int[] _snapUnitEnabled;
	private int[] _snapForcedBlank;
	private int[] _snapBrightMode;
	private int[] _snapBrightFactor;
	private int[] _snapScreenOff;
	private int[] _snapDispMode;
	private int[] _snapVramCap;
	private uint[][] _snapVramDisplay;

	private bool[] _snapCapOn;
	private int[] _snapCapBank, _snapCapOffset, _snapCapWidth, _snapCapHeight;
	private int[] _snapCapMode, _snapCapEVA, _snapCapEVB, _snapCapSrcA3D, _snapCapSrcBEn, _snapCapSize;
	private uint[][] _snapCapSrcB;

	private int _writeSlot, _readySlot = -1, _readSlot = -1;
	private int _frameReady;
	private bool _hasFrame;

	private void CreateSnapshots()
	{
		_snapScan = new sScanline[FrameSlots][];
		_snapVramBG = new uint[FrameSlots][];
		_snapVramOBJ = new uint[FrameSlots][];
		_snapPalBG = new uint[FrameSlots][];
		_snapPalOBJ = new uint[FrameSlots][];
		_snapLayer = new sBGConfig[FrameSlots][];
		_snapOAM = new sOAM[FrameSlots][];
		_snapRot = new Vec4i[FrameSlots][];
		_snapNumSprites = new int[FrameSlots];
		_snapComp = new sCompositorConfig[FrameSlots];
		_snapWindowEnable = new int[FrameSlots];
		_snapMasterBright = new int[FrameSlots];
		_snapUnitEnabled = new int[FrameSlots];
		_snapForcedBlank = new int[FrameSlots];
		_snapBrightMode = new int[FrameSlots];
		_snapBrightFactor = new int[FrameSlots];
		_snapScreenOff = new int[FrameSlots];
		_snapDispMode = new int[FrameSlots];
		_snapVramCap = new int[FrameSlots];
		_snapVramDisplay = new uint[FrameSlots][];

		_snapCapOn = new bool[FrameSlots];
		_snapCapBank = new int[FrameSlots];
		_snapCapOffset = new int[FrameSlots];
		_snapCapWidth = new int[FrameSlots];
		_snapCapHeight = new int[FrameSlots];
		_snapCapMode = new int[FrameSlots];
		_snapCapEVA = new int[FrameSlots];
		_snapCapEVB = new int[FrameSlots];
		_snapCapSrcA3D = new int[FrameSlots];
		_snapCapSrcBEn = new int[FrameSlots];
		_snapCapSize = new int[FrameSlots];
		_snapCapSrcB = new uint[FrameSlots][];

		for ( int i = 0; i < FrameSlots; i++ )
		{
			_snapCapSrcB[i] = new uint[ScreenW * ScreenH];
			_snapScan[i] = new sScanline[ScreenH];
			_snapVramBG[i] = new uint[U( _bgFlatBytes )];
			_snapVramOBJ[i] = new uint[U( _objFlatBytes )];
			_snapPalBG[i] = new uint[U( PalBGBytes )];
			_snapPalOBJ[i] = new uint[U( PalOBJBytes )];
			_snapLayer[i] = new sBGConfig[4];
			_snapOAM[i] = new sOAM[128];
			_snapRot[i] = new Vec4i[32];
			_snapVramDisplay[i] = new uint[0x20000 / 4];
		}

		_writeSlot = 0;
		_readySlot = 1;
		_readSlot = 2;
		_frameReady = 0;
		_hasFrame = false;
	}

	public void DrawScanline( uint line )
	{
		if ( !GpuReady || line >= ScreenH )
			return;

		UpdateScanlineConfig( line );
	}

	public void VBlank()
	{
		if ( !GpuReady )
			return;

		int s = _writeSlot;

		UpdateLayerConfig();
		UpdateOAM();
		UpdateCompositorConfig();

		_windowEnable = ((_gpu2d.DispCnt & (1 << 15)) != 0) ? 1 : 0;
		_masterBright = _num == 0 ? _gpu.MasterBrightnessA : _gpu.MasterBrightnessB;
		_unitEnabled = _gpu2d.Enabled ? 1 : 0;
		_forcedBlank = _gpu2d.ForcedBlank;

		Array.Copy( _scanlineConfig, _snapScan[s], ScreenH );
		Array.Copy( _layerConfig, _snapLayer[s], 4 );
		Array.Copy( _spriteOAM, _snapOAM[s], 128 );
		Array.Copy( _rotscale, _snapRot[s], 32 );
		_snapNumSprites[s] = _numSprites;
		_snapComp[s] = _compConfig;
		_snapWindowEnable[s] = _windowEnable;
		_snapMasterBright[s] = _masterBright;
		_snapUnitEnabled[s] = _unitEnabled;
		_snapForcedBlank[s] = _forcedBlank;

		int dispMode = (int)((_gpu2d.DispCnt >> 16) & 0x3);
		int screenOff;
		if ( _unitEnabled == 0 )
			screenOff = (_num == 0) ? 2 : 1;
		else if ( _forcedBlank != 0 )
			screenOff = 1;
		else if ( dispMode == 0 )
			screenOff = 1;
		else
			screenOff = 0;

		_snapBrightMode[s] = (_masterBright >> 14) & 0x3;
		_snapBrightFactor[s] = Math.Min( _masterBright & 0x1F, 16 );
		_snapScreenOff[s] = screenOff;

		_snapDispMode[s] = dispMode;
		_snapVramCap[s] = -1;
		if ( dispMode == 2 )
		{
			int blk = (int)((_gpu2d.DispCnt >> 18) & 0x3);
			if ( _num == 0 && (_gpu.VRAMMap_LCDC & (1u << blk)) != 0 )
				_snapVramCap[s] = _gpu.GetCaptureBlock_LCDC( (uint)blk << 17 );
			byte[] bank = blk == 0 ? _gpu.VRAM_A : blk == 1 ? _gpu.VRAM_B : blk == 2 ? _gpu.VRAM_C : _gpu.VRAM_D;
			Buffer.BlockCopy( bank, 0, _snapVramDisplay[s], 0, 0x20000 );
		}

		byte[] bgSrc = _num == 0 ? _gpu.VRAMFlat_ABG : _gpu.VRAMFlat_BBG;
		byte[] objSrc = _num == 0 ? _gpu.VRAMFlat_AOBJ : _gpu.VRAMFlat_BOBJ;
		Buffer.BlockCopy( bgSrc, 0, _snapVramBG[s], 0, _bgFlatBytes );
		Buffer.BlockCopy( objSrc, 0, _snapVramOBJ[s], 0, _objFlatBytes );

		BuildPalettes( s );

		if ( _num == 0 )
			DecodeCapture( s );

		_writeSlot = Interlocked.Exchange( ref _readySlot, _writeSlot );
		Interlocked.Exchange( ref _frameReady, 1 );
	}

	private void DecodeCapture( int s )
	{
		if ( !_gpu.CaptureEnable )
		{
			_snapCapOn[s] = false;
			return;
		}

		uint cap = _gpu.CaptureCnt;
		uint sz = (cap >> 20) & 0x3;
		int width, height;
		if ( sz == 0 ) { width = 128; height = 128; }
		else { width = 256; height = 64 * (int)sz; }

		_snapCapOn[s] = true;
		_snapCapWidth[s] = width;
		_snapCapHeight[s] = height;
		_snapCapSize[s] = (int)sz;
		_snapCapBank[s] = (int)((cap >> 16) & 0x3);
		_snapCapOffset[s] = (int)((cap >> 18) & 0x3);
		_snapCapMode[s] = (int)((cap >> 29) & 0x3);

		int eva = (int)(cap & 0x1F);
		int evb = (int)((cap >> 8) & 0x1F);
		if ( eva > 16 ) eva = 16;
		if ( evb > 16 ) evb = 16;
		_snapCapEVA[s] = eva;
		_snapCapEVB[s] = evb;
		_snapCapSrcA3D[s] = (int)((cap >> 24) & 0x1);

		bool srcBen = ((cap >> 25) & 0x1) == 0;
		_snapCapSrcBEn[s] = srcBen ? 1 : 0;
		if ( srcBen )
			BuildCaptureSrcB( s, cap, width, height );
	}

	private void BuildCaptureSrcB( int s, uint cap, int width, int height )
	{
		uint dispcnt = _gpu2d.DispCnt;
		uint srcvram = (dispcnt >> 18) & 0x3;
		byte[] bank = srcvram == 0 ? _gpu.VRAM_A : srcvram == 1 ? _gpu.VRAM_B : srcvram == 2 ? _gpu.VRAM_C : _gpu.VRAM_D;
		if ( bank == null )
		{
			_snapCapSrcBEn[s] = 0;
			return;
		}

		uint mask = 0x1FFFF;
		uint[] dst = _snapCapSrcB[s];
		for ( int y = 0; y < height; y++ )
		{
			uint offset = (uint)(y * 256);
			if ( ((dispcnt >> 16) & 0x3) != 2 )
				offset += ((cap >> 26) & 0x3) << 14;
			uint word = offset & 0xFFFF;
			int row = y * width;
			for ( int x = 0; x < width; x++ )
			{
				uint a = ((word + (uint)x) * 2) & mask;
				dst[row + x] = (uint)(bank[a] | (bank[a + 1] << 8));
			}
		}
	}

	private void BuildPalettes( int s )
	{
		Buffer.BlockCopy( _gpu.Palette, _num * 0x400, _snapPalBG[s], 0, 0x200 );

		byte[] bgExt = _num == 0 ? _gpu.VRAMFlat_ABGExtPal : _gpu.VRAMFlat_BBGExtPal;
		for ( int slot = 0; slot < 4; slot++ )
		{
			for ( int p = 0; p < 16; p++ )
			{
				int row = 1 + (slot * 16) + p;
				int srcOff = (slot * 0x2000) + (p * 0x200);
				Buffer.BlockCopy( bgExt, srcOff, _snapPalBG[s], row * 0x200, 0x200 );
			}
		}

		Buffer.BlockCopy( _gpu.Palette, (_num * 0x400) + 0x200, _snapPalOBJ[s], 0, 0x200 );

		byte[] objExt = _num == 0 ? _gpu.VRAMFlat_AOBJExtPal : _gpu.VRAMFlat_BOBJExtPal;
		for ( int p = 0; p < 16; p++ )
		{
			int row = 1 + p;
			int srcOff = p * 0x200;
			Buffer.BlockCopy( objExt, srcOff, _snapPalOBJ[s], row * 0x200, 0x200 );
		}
	}

	private bool AcquireReadSlot( out int slot )
	{
		if ( Interlocked.Exchange( ref _frameReady, 0 ) == 1 )
		{
			_readSlot = Interlocked.Exchange( ref _readySlot, _readSlot );
			_hasFrame = true;
		}

		slot = _readSlot;
		return _hasFrame && slot >= 0;
	}
}
