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

		DisposeShaders();
		RenderCommandList = null;
	}
}
