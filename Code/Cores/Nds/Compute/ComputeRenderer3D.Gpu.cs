namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private const int MaxVariants = 256;
	private const int BinStride = 2048 / 32;
	private const int CoarseBinStride = BinStride / 32;
	private const int CoarseTileCountX = 8;

	private int _screenWidth, _screenHeight;
	private int _tileSize;
	private int _coarseTileCountY, _coarseTileArea, _coarseTileW, _coarseTileH;
	private int _clearCoarseBinMaskLocalSize;
	private int _tilesPerLine, _tileLines;
	private int _maxWorkTiles, _maxYSpanIndices;

	private int _numVariants = 1;

	private readonly uint[] _metaData = new uint[148];

	private const int BufRing = 3;
	private int _bufIdx;
	private int _texBufIdx;

	private readonly GpuBuffer<uint>[] _metaBufferR = new GpuBuffer<uint>[BufRing];
	private readonly GpuBuffer<RenderPolygonGpu>[] _renderPolygonBufferR = new GpuBuffer<RenderPolygonGpu>[BufRing];
	private readonly GpuBuffer<SpanSetupY>[] _ySpanSetupBufferR = new GpuBuffer<SpanSetupY>[BufRing];
	private readonly GpuBuffer<SetupIndices>[] _ySpanIndicesBufferR = new GpuBuffer<SetupIndices>[BufRing];

	private GpuBuffer<uint> _metaBuffer;
	private GpuBuffer<RenderPolygonGpu> _renderPolygonBuffer;
	private GpuBuffer<SpanSetupY> _ySpanSetupBuffer;
	private GpuBuffer<SpanSetupX> _xSpanSetupBuffer;
	private GpuBuffer<SetupIndices> _ySpanIndicesBuffer;
	private GpuBuffer<uint> _binResultBuffer;
	private GpuBuffer<uint> _workDescBuffer;
	private readonly GpuBuffer<uint>[] _tileMemory = new GpuBuffer<uint>[3];
	private GpuBuffer<uint> _finalTileBuffer;
	private GpuBuffer<GpuBuffer.IndirectDispatchArguments> _indirectArgs;

	private Texture _outputTex;
	private readonly GpuBuffer<uint>[] _texBufSmallR = new GpuBuffer<uint>[BufRing];
	private readonly GpuBuffer<uint>[] _texBufLargeR = new GpuBuffer<uint>[BufRing];
	private GpuBuffer<uint> _texBufSmall;
	private GpuBuffer<uint> _texBufLarge;

	private ComputeShader _csClearCoarseBinMask;
	private ComputeShader _csClearIndirectWorkCount;
	private ComputeShader _csInterpSpans;
	private ComputeShader _csBinCombined;
	private ComputeShader _csCalcWorkOffset;
	private ComputeShader _csSortWork;
	private ComputeShader _csRaster;
	private ComputeShader _csDepthBlend;
	private ComputeShader _csFinalPass;

	private bool _gpuReady;

	private void InitGpuResources()
	{
		ComputeDimensions();

		for ( int i = 0; i < BufRing; i++ )
		{
			_metaBufferR[i] = new GpuBuffer<uint>( _metaData.Length, GpuBuffer.UsageFlags.Structured );
			_renderPolygonBufferR[i] = new GpuBuffer<RenderPolygonGpu>( 2048, GpuBuffer.UsageFlags.Structured );
			_ySpanSetupBufferR[i] = new GpuBuffer<SpanSetupY>( MaxYSpanSetups, GpuBuffer.UsageFlags.Structured );
			_ySpanIndicesBufferR[i] = new GpuBuffer<SetupIndices>( _maxYSpanIndices, GpuBuffer.UsageFlags.Structured );
		}
		_metaBuffer = _metaBufferR[0];
		_renderPolygonBuffer = _renderPolygonBufferR[0];
		_ySpanSetupBuffer = _ySpanSetupBufferR[0];
		_ySpanIndicesBuffer = _ySpanIndicesBufferR[0];
		_xSpanSetupBuffer = new GpuBuffer<SpanSetupX>( _maxYSpanIndices, GpuBuffer.UsageFlags.Structured );

		int binResultUints = BinResultHeaderUints
			+ _tilesPerLine * _tileLines * CoarseBinStride
			+ _tilesPerLine * _tileLines * BinStride
			+ _tilesPerLine * _tileLines * BinStride;
		_binResultBuffer = new GpuBuffer<uint>( binResultUints, GpuBuffer.UsageFlags.Structured );

		_workDescBuffer = new GpuBuffer<uint>( _maxWorkTiles * 2 * 2, GpuBuffer.UsageFlags.Structured );

		int tileUints = _tileSize * _tileSize * _maxWorkTiles;
		for ( int i = 0; i < 3; i++ )
			_tileMemory[i] = new GpuBuffer<uint>( tileUints, GpuBuffer.UsageFlags.Structured );

		_finalTileBuffer = new GpuBuffer<uint>( 3 * 2 * _screenWidth * _screenHeight, GpuBuffer.UsageFlags.Structured );

		_indirectArgs = new GpuBuffer<GpuBuffer.IndirectDispatchArguments>(
			MaxVariants + 1, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.IndirectDrawArguments );

		_outputTex = Texture.Create( _screenWidth, _screenHeight )
			.WithUAVBinding()
			.WithFormat( ImageFormat.RGBA8888 )
			.WithDynamicUsage()
			.Finish();

		for ( int i = 0; i < BufRing; i++ )
		{
			_texBufSmallR[i] = new GpuBuffer<uint>( SmallArea * SmallLayers, GpuBuffer.UsageFlags.Structured );
			_texBufLargeR[i] = new GpuBuffer<uint>( LargeArea * LargeLayers, GpuBuffer.UsageFlags.Structured );
		}
		_texBufSmall = _texBufSmallR[0];
		_texBufLarge = _texBufLargeR[0];

		_texCache.Clear();
		_freeSmall.Clear();
		_freeLarge.Clear();
		_numLayersSmall = 0;
		_numLayersLarge = 0;
		_texBufIdx = 0;
		_cacheBuiltVersion = ulong.MaxValue;
		_texturesChanged = true;

		_csClearCoarseBinMask = new ComputeShader( "shaders/nds3d_clearcoarsebinmask.shader" );
		_csClearIndirectWorkCount = new ComputeShader( "shaders/nds3d_clearindirectworkcount.shader" );
		_csInterpSpans = new ComputeShader( "shaders/nds3d_interpspans.shader" );
		_csBinCombined = new ComputeShader( "shaders/nds3d_bincombined.shader" );
		_csCalcWorkOffset = new ComputeShader( "shaders/nds3d_calcworkoffset.shader" );
		_csSortWork = new ComputeShader( "shaders/nds3d_sortwork.shader" );
		_csRaster = new ComputeShader( "shaders/nds3d_raster.shader" );
		_csDepthBlend = new ComputeShader( "shaders/nds3d_depthblend.shader" );
		_csFinalPass = new ComputeShader( "shaders/nds3d_finalpass.shader" );

		_gpuReady = true;
	}

	private static int BinResultHeaderUints => MaxVariants * 4 + MaxVariants + 4;

	private void ComputeDimensions()
	{
		int scale = _scaleFactor;
		_screenWidth = 256 * scale;
		_screenHeight = 192 * scale;

		_tileSize = 8;
		_coarseTileCountY = _tileSize < 32 ? 4 : 6;
		_clearCoarseBinMaskLocalSize = _tileSize < 32 ? 64 : 48;
		_coarseTileArea = CoarseTileCountX * _coarseTileCountY;
		_coarseTileW = CoarseTileCountX * _tileSize;
		_coarseTileH = _coarseTileCountY * _tileSize;

		_tilesPerLine = _screenWidth / _tileSize;
		_tileLines = _screenHeight / _tileSize;

		_maxWorkTiles = _tilesPerLine * _tileLines * 16;
		_maxYSpanIndices = 64 * 2048 * scale;
	}

	private void FillMeta()
	{
		_metaData[0] = _gpu.RenderNumPolygons;
		_metaData[1] = (uint)_numVariants;
		_metaData[2] = _gpu.RenderAlphaRef;
		_metaData[3] = _gpu.RenderDispCnt;

		uint cr = (_gpu.RenderClearAttr1 << 1) & 0x3E; if ( cr != 0 ) cr++;
		uint cg = (_gpu.RenderClearAttr1 >> 4) & 0x3E; if ( cg != 0 ) cg++;
		uint cb = (_gpu.RenderClearAttr1 >> 9) & 0x3E; if ( cb != 0 ) cb++;
		uint ca = (_gpu.RenderClearAttr1 >> 16) & 0x1F;
		_metaData[140] = cr | (cg << 8) | (cb << 16) | (ca << 24);
		_metaData[141] = ((_gpu.RenderClearAttr2 & 0x7FFF) * 0x200) + 0x1FF;
		_metaData[142] = _gpu.RenderClearAttr1 & 0x3F008000;

		uint xoff = (_gpu.RenderClearAttr2 >> 16) & 0xFF;
		uint yoff = (_gpu.RenderClearAttr2 >> 24) & 0xFF;
		_metaData[146] = BitConverter.SingleToUInt32Bits( xoff / 256.0f );
		_metaData[147] = BitConverter.SingleToUInt32Bits( yoff / 256.0f );

		for ( int i = 0; i < 32; i++ )
		{
			uint color = _gpu.RenderToonTable[i];
			uint r = (color << 1) & 0x3E;
			uint g = (color >> 4) & 0x3E;
			uint b = (color >> 9) & 0x3E;
			if ( r != 0 ) r++;
			if ( g != 0 ) g++;
			if ( b != 0 ) b++;
			_metaData[4 + i * 4 + 0] = r | (g << 8) | (b << 16);
		}
		for ( int i = 0; i < 34; i++ )
			_metaData[4 + i * 4 + 1] = _gpu.RenderFogDensityTable[i];
		for ( int i = 0; i < 8; i++ )
		{
			uint color = _gpu.RenderEdgeTable[i];
			uint r = (color << 1) & 0x3E;
			uint g = (color >> 4) & 0x3E;
			uint b = (color >> 9) & 0x3E;
			if ( r != 0 ) r++;
			if ( g != 0 ) g++;
			if ( b != 0 ) b++;
			_metaData[4 + i * 4 + 2] = r | (g << 8) | (b << 16);
		}

		_metaData[143] = _gpu.RenderFogOffset;
		_metaData[144] = _gpu.RenderFogShift;
		uint fr = (_gpu.RenderFogColor << 1) & 0x3E; if ( fr != 0 ) fr++;
		uint fg = (_gpu.RenderFogColor >> 4) & 0x3E; if ( fg != 0 ) fg++;
		uint fb = (_gpu.RenderFogColor >> 9) & 0x3E; if ( fb != 0 ) fb++;
		uint fa = (_gpu.RenderFogColor >> 16) & 0x1F;
		_metaData[145] = fr | (fg << 8) | (fb << 16) | (fa << 24);
	}
}
