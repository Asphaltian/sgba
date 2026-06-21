using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed partial class ComputeRenderer2D
{
	private const int ScreenW = 256;
	private const int ScreenH = 192;
	private const int FrameSlots = 3;

	private const int LayerTexSize = 1024;
	private const int AtlasW = 1024;
	private const int AtlasH = 512;

	private const int PalBGBytes = 65 * 256 * 2;
	private const int PalOBJBytes = 17 * 256 * 2;

	private readonly int _num;
	private readonly GPU2D _gpu2d;
	private readonly GPU _gpu;
	private readonly GPU3D _gpu3d;

	private readonly int _bgFlatBytes;
	private readonly int _objFlatBytes;
	private readonly int _bgVramMask;
	private readonly int _objVramMask;

	private int _scale = 1;

	private GpuBuffer<uint> _vramBG;
	private GpuBuffer<uint> _vramOBJ;
	private GpuBuffer<uint> _palBG;
	private GpuBuffer<uint> _palOBJ;
	private GpuBuffer<sBGConfig> _bgConfigBuf;
	private GpuBuffer<sOAM> _oamConfigBuf;
	private GpuBuffer<Vec4i> _rotscaleBuf;
	private GpuBuffer<sScanline> _scanlineBuf;
	private GpuBuffer<int> _mosaicBuf;
	private GpuBuffer<uint> _vramDisplay;

	private readonly Texture[] _bgLayer = new Texture[4];
	private Texture _spriteAtlas;
	private Texture _objColor;
	private Texture _objFlags;
	private Texture _output;

	private Texture _captureSrcA;
	public Texture CaptureOutput128Tex;
	public Texture CaptureOutput256Tex;
	private GpuBuffer<uint> _captureSrcB;

	private ComputeRenderer2D _captureOwner;
	public void SetCaptureOwner( ComputeRenderer2D o ) => _captureOwner = o;
	private Texture Cap128 => CaptureOutput128Tex ?? _captureOwner?.CaptureOutput128Tex;
	private Texture Cap256 => CaptureOutput256Tex ?? _captureOwner?.CaptureOutput256Tex;

	public void AllocCapture( uint bank, uint start, uint len ) { }

	private byte[] _syncBuf;

	public void SyncVRAMCapture( uint bank, uint start, uint len, bool complete )
	{
		if ( CaptureOutput128Tex == null )
			return;

		byte[] vram = _gpu.VRAM[(int)bank];

		if ( len == 0 )
		{
			int layer = (int)((bank << 2) | start);
			ReadCaptureToVram( CaptureOutput128Tex, layer, 0, 128, 128, vram, (int)(start * 0x8000) );
		}
		else
		{
			int layer = (int)bank;
			uint pos = start;
			for ( uint i = 0; i < len; )
			{
				uint end = pos + len;
				if ( end > 4 ) end = 4;
				int rows = (int)((end - pos) * 64);
				ReadCaptureToVram( CaptureOutput256Tex, layer, (int)(pos * 64), 256, rows, vram, (int)(pos * 0x8000) );
				i += end - pos;
				pos = (pos + (end - pos)) & 3;
			}
		}
	}

	private void ReadCaptureToVram( Texture tex, int layer, int nativeY, int nativeW, int nativeH, byte[] vram, int vramOffset )
	{
		int hw = nativeW * _scale;
		int hh = nativeH * _scale;
		int needed = hw * hh * 4;
		if ( _syncBuf == null || _syncBuf.Length < needed )
			_syncBuf = new byte[needed];

		tex.GetPixels( (0, nativeY * _scale, hw, hh), layer, 0, _syncBuf.AsSpan(), ImageFormat.RGBA8888 );

		int stride = nativeW * 2;
		for ( int y = 0; y < nativeH; y++ )
		{
			float fy = (y + 0.5f) * _scale - 0.5f;
			int y0 = (int)fy;
			float wy = fy - y0;
			int y1 = y0 + 1; if ( y1 >= hh ) y1 = hh - 1;

			for ( int x = 0; x < nativeW; x++ )
			{
				float fx = (x + 0.5f) * _scale - 0.5f;
				int x0 = (int)fx;
				float wx = fx - x0;
				int x1 = x0 + 1; if ( x1 >= hw ) x1 = hw - 1;

				int i00 = (y0 * hw + x0) * 4, i10 = (y0 * hw + x1) * 4, i01 = (y1 * hw + x0) * 4, i11 = (y1 * hw + x1) * 4;
				int r = BilinearByte( i00, i10, i01, i11, 0, wx, wy ) >> 3;
				int g = BilinearByte( i00, i10, i01, i11, 1, wx, wy ) >> 3;
				int b = BilinearByte( i00, i10, i01, i11, 2, wx, wy ) >> 3;
				int a = BilinearByte( i00, i10, i01, i11, 3, wx, wy ) > 0 ? 1 : 0;

				int col = r | (g << 5) | (b << 10) | (a << 15);
				int dst = (vramOffset + y * stride + x * 2) & 0x1FFFF;
				vram[dst] = (byte)col;
				vram[dst + 1] = (byte)(col >> 8);
			}
		}
	}

	private int BilinearByte( int i00, int i10, int i01, int i11, int c, float wx, float wy )
	{
		float top = _syncBuf[i00 + c] * (1.0f - wx) + _syncBuf[i10 + c] * wx;
		float bot = _syncBuf[i01 + c] * (1.0f - wx) + _syncBuf[i11 + c] * wx;
		return (int)(top * (1.0f - wy) + bot * wy);
	}

	private Texture _output3D;
	public void SetOutput3D( Texture tex ) => _output3D = tex;

	private Texture _renderTarget;
	public void SetOutputTarget( Texture tex ) => _renderTarget = tex;

	private ComputeRenderer3D _render3D;
	public void SetRenderer3D( ComputeRenderer3D r ) => _render3D = r;

	public bool GpuReady { get; private set; }
	public CommandList RenderCommandList { get; private set; }
	public Texture OutputTexture => _output;

	public ComputeRenderer2D( int num, GPU2D gpu2d, GPU gpu, GPU3D gpu3d )
	{
		_num = num;
		_gpu2d = gpu2d;
		_gpu = gpu;
		_gpu3d = gpu3d;

		_bgFlatBytes = num == 0 ? 0x80000 : 0x20000;
		_objFlatBytes = num == 0 ? 0x40000 : 0x20000;
		_bgVramMask = (num == 0 ? 512 : 128) - 1;
		_objVramMask = (num == 0 ? 256 : 128) - 1;
	}

	private static int U( int bytes ) => bytes / 4;

	public void InitGpu( int scale )
	{
		DisposeGpu();
		_scale = Math.Max( 1, scale );

		CreateSnapshots();

		int ow = ScreenW * _scale;
		int oh = ScreenH * _scale;

		_vramBG = new GpuBuffer<uint>( U( _bgFlatBytes ) );
		_vramOBJ = new GpuBuffer<uint>( U( _objFlatBytes ) );
		_palBG = new GpuBuffer<uint>( U( PalBGBytes ) );
		_palOBJ = new GpuBuffer<uint>( U( PalOBJBytes ) );
		_bgConfigBuf = new GpuBuffer<sBGConfig>( 4 );
		_oamConfigBuf = new GpuBuffer<sOAM>( 128 );
		_rotscaleBuf = new GpuBuffer<Vec4i>( 32 );
		_scanlineBuf = new GpuBuffer<sScanline>( ScreenH );

		_mosaicBuf = new GpuBuffer<int>( 256 * 16 );
		_mosaicBuf.SetData( BuildMosaicTable() );

		_vramDisplay = new GpuBuffer<uint>( 0x20000 / 4 );

		for ( int l = 0; l < 4; l++ )
			_bgLayer[l] = MakeUav( LayerTexSize, LayerTexSize );

		_spriteAtlas = MakeUav( AtlasW, AtlasH );
		_objColor = MakeUav( ow, oh );
		_objFlags = MakeUav( ow, oh );
		_output = MakeUav( ow, oh );

		_captureSrcA = MakeUav( ow, oh );
		if ( _num == 0 )
		{
			CaptureOutput128Tex = MakeUavArray( 128 * _scale, 128 * _scale, 16 );
			CaptureOutput256Tex = MakeUavArray( 256 * _scale, 256 * _scale, 4 );
			_captureSrcB = new GpuBuffer<uint>( ScreenW * ScreenH );
		}

		InitShaders();
		RenderCommandList = new CommandList( $"NDS2D_{_num}" );
		GpuReady = true;
	}

	private static Texture MakeUav( int w, int h )
	{
		return Texture.CreateRenderTarget()
			.WithSize( w, h )
			.WithFormat( ImageFormat.RGBA8888 )
			.WithUAVBinding()
			.Create();
	}

	private static Texture MakeUavArray( int w, int h, int count )
	{
		return Texture.CreateArray( w, h, count, ImageFormat.RGBA8888 )
			.WithUAVBinding()
			.Finish();
	}

	private static int[] BuildMosaicTable()
	{
		int[] tab = new int[256 * 16];
		for ( int m = 0; m < 16; m++ )
		{
			int mosx = 0;
			for ( int x = 0; x < 256; x++ )
			{
				tab[(m * 256) + x] = mosx;
				if ( mosx == m )
					mosx = 0;
				else
					mosx++;
			}
		}
		return tab;
	}

	public void DisposeGpu()
	{
		GpuReady = false;

		_vramBG?.Dispose();
		_vramOBJ?.Dispose();
		_palBG?.Dispose();
		_palOBJ?.Dispose();
		_bgConfigBuf?.Dispose();
		_oamConfigBuf?.Dispose();
		_rotscaleBuf?.Dispose();
		_scanlineBuf?.Dispose();
		_mosaicBuf?.Dispose();
		_vramDisplay?.Dispose();
		_vramBG = null;
		_vramOBJ = null;
		_palBG = null;
		_palOBJ = null;
		_bgConfigBuf = null;
		_oamConfigBuf = null;
		_rotscaleBuf = null;
		_scanlineBuf = null;
		_mosaicBuf = null;
		_vramDisplay = null;

		for ( int l = 0; l < 4; l++ )
		{
			_bgLayer[l]?.Dispose();
			_bgLayer[l] = null;
		}
		_spriteAtlas?.Dispose();
		_objColor?.Dispose();
		_objFlags?.Dispose();
		_output?.Dispose();
		_spriteAtlas = null;
		_objColor = null;
		_objFlags = null;
		_output = null;

		_captureSrcA?.Dispose();
		CaptureOutput128Tex?.Dispose();
		CaptureOutput256Tex?.Dispose();
		_captureSrcB?.Dispose();
		_captureSrcA = null;
		CaptureOutput128Tex = null;
		CaptureOutput256Tex = null;
		_captureSrcB = null;

		DisposeShaders();
		RenderCommandList = null;
	}
}
