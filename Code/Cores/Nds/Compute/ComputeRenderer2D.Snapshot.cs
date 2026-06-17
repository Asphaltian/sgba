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
	private uint[][] _snapVramDisplay;

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
		_snapVramDisplay = new uint[FrameSlots][];

		for ( int i = 0; i < FrameSlots; i++ )
		{
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
		if ( dispMode == 2 )
		{
			int blk = (int)((_gpu2d.DispCnt >> 18) & 0x3);
			byte[] bank = blk == 0 ? _gpu.VRAM_A : blk == 1 ? _gpu.VRAM_B : blk == 2 ? _gpu.VRAM_C : _gpu.VRAM_D;
			Buffer.BlockCopy( bank, 0, _snapVramDisplay[s], 0, 0x20000 );
		}

		byte[] bgSrc = _num == 0 ? _gpu.VRAMFlat_ABG : _gpu.VRAMFlat_BBG;
		byte[] objSrc = _num == 0 ? _gpu.VRAMFlat_AOBJ : _gpu.VRAMFlat_BOBJ;
		Buffer.BlockCopy( bgSrc, 0, _snapVramBG[s], 0, _bgFlatBytes );
		Buffer.BlockCopy( objSrc, 0, _snapVramOBJ[s], 0, _objFlatBytes );

		BuildPalettes( s );

		_writeSlot = Interlocked.Exchange( ref _readySlot, _writeSlot );
		Interlocked.Exchange( ref _frameReady, 1 );
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
