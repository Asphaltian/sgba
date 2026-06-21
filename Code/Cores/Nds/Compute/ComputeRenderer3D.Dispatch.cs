using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private const uint SortWorkArgIndex = MaxVariants;

	private readonly object _snapshotLock = new();
	private bool _snapshotDirty;
	private int _snapNumYSpans;
	private int _snapNumSetupIndices;
	private int _snapNumPolygons;
	private int _snapNumVariants;
	private bool _snapWBuffer;
	private uint _snapDispCnt;

	private readonly int[] _snapVarBlend = new int[MaxVariants];
	private readonly int[] _snapVarBucket = new int[MaxVariants];
	private readonly int[] _snapVarW = new int[MaxVariants];
	private readonly int[] _snapVarH = new int[MaxVariants];
	private readonly bool[] _snapVarTexture = new bool[MaxVariants];
	private readonly bool[] _snapVarShadow = new bool[MaxVariants];
	private readonly int[] _snapVarCapture = new int[MaxVariants];
	private readonly int[] _snapVarCapYOffset = new int[MaxVariants];

	public void Snapshot()
	{
		if ( !_gpuReady )
			return;

		lock ( _snapshotLock )
		{
			BuildSpanSetups();
			FillMeta();

			_snapNumYSpans = _numYSpans;
			_snapNumSetupIndices = _numSetupIndices;
			_snapNumPolygons = (int)_gpu.RenderNumPolygons;
			_snapNumVariants = _numVariants;
			_snapWBuffer = _gpu.RenderNumPolygons > 0 && _gpu.RenderPolygonRAM[0].WBuffer;
			_snapDispCnt = _gpu.RenderDispCnt;
			_snapshotDirty = true;
		}
	}

	public bool BuildCommandList( CommandList cmd )
	{
		if ( !_gpuReady )
			return false;

		int numYSpans, numSetupIndices, numPolygons, numVariants;
		bool wbuffer;
		uint dispCnt;

		lock ( _snapshotLock )
		{
			if ( !_snapshotDirty )
				return false;

			_bufIdx = (_bufIdx + 1) % BufRing;
			_metaBuffer = _metaBufferR[_bufIdx];
			_renderPolygonBuffer = _renderPolygonBufferR[_bufIdx];
			_ySpanSetupBuffer = _ySpanSetupBufferR[_bufIdx];
			_ySpanIndicesBuffer = _ySpanIndicesBufferR[_bufIdx];

			_metaBuffer.SetData<uint>( _metaData );

			numPolygons = _snapNumPolygons;
			if ( numPolygons > 0 )
				_renderPolygonBuffer.SetData<RenderPolygonGpu>( _renderPolygons.AsSpan( 0, numPolygons ) );

			numYSpans = _snapNumYSpans;
			numSetupIndices = _snapNumSetupIndices;
			if ( numYSpans > 0 )
			{
				_ySpanSetupBuffer.SetData<SpanSetupY>( _ySpanSetups.AsSpan( 0, numYSpans ) );
				_ySpanIndicesBuffer.SetData( _ySpanIndices );
			}

			numVariants = _snapNumVariants;
			wbuffer = _snapWBuffer;
			dispCnt = _snapDispCnt;

			if ( numVariants > 0 )
			{
				Array.Copy( _varBlend, _snapVarBlend, numVariants );
				Array.Copy( _varBucket, _snapVarBucket, numVariants );
				Array.Copy( _varTexture, _snapVarTexture, numVariants );
				Array.Copy( _varShadow, _snapVarShadow, numVariants );
				Array.Copy( _varW, _snapVarW, numVariants );
				Array.Copy( _varH, _snapVarH, numVariants );
				Array.Copy( _varCapture, _snapVarCapture, numVariants );
				Array.Copy( _varCapYOffset, _snapVarCapYOffset, numVariants );
			}
			if ( _texturesChanged )
			{
				_texBufIdx = (_texBufIdx + 1) % BufRing;
				_texBufSmall = _texBufSmallR[_texBufIdx];
				_texBufLarge = _texBufLargeR[_texBufIdx];

				if ( _numLayersSmall > 0 )
					_texBufSmall.SetData<uint>( _texDataSmall.AsSpan( 0, _numLayersSmall * SmallArea ) );
				if ( _numLayersLarge > 0 )
					_texBufLarge.SetData<uint>( _texDataLarge.AsSpan( 0, _numLayersLarge * LargeArea ) );

				_texturesChanged = false;
			}

			_snapshotDirty = false;
		}

		int wb = wbuffer ? 1 : 0;

		cmd.Reset();
		BindCommon( cmd );

		cmd.DispatchCompute( _csClearCoarseBinMask, _tilesPerLine * _tileLines, 1, 1 );
		cmd.UavBarrier( _binResultBuffer );

		if ( numYSpans > 0 )
		{
			cmd.DispatchCompute( _csClearIndirectWorkCount, numVariants, 1, 1 );
			cmd.UavBarrier( _binResultBuffer );

			cmd.Attributes.SetCombo( "D_WBUFFER", wb );
			cmd.Attributes.Set( "YSpanSetups", _ySpanSetupBuffer );
			cmd.Attributes.Set( "YSpanIndices", _ySpanIndicesBuffer );
			cmd.DispatchCompute( _csInterpSpans, numSetupIndices, 1, 1 );
			cmd.UavBarrier( _xSpanSetupBuffer );

			cmd.DispatchCompute( _csBinCombined, numPolygons, _screenWidth / _coarseTileW, _screenHeight / _coarseTileH );
			cmd.UavBarrier( _binResultBuffer );
			cmd.UavBarrier( _workDescBuffer );

			cmd.ResourceBarrierTransition( _indirectArgs, ResourceState.UnorderedAccess );
			cmd.DispatchCompute( _csCalcWorkOffset, numVariants, 1, 1 );
			cmd.UavBarrier( _binResultBuffer );
			cmd.ResourceBarrierTransition( _indirectArgs, ResourceState.IndirectArgument );

			cmd.DispatchComputeIndirect( _csSortWork, _indirectArgs, SortWorkArgIndex );
			cmd.UavBarrier( _workDescBuffer );

			cmd.Attributes.SetCombo( "D_WBUFFER", wb );
			cmd.Attributes.Set( "TileColor", _tileMemory[0] );
			cmd.Attributes.Set( "TileDepth", _tileMemory[1] );
			cmd.Attributes.Set( "TileAttr", _tileMemory[2] );
			cmd.Attributes.Set( "uScale", _scaleFactor );
			if ( _cap128 != null )
			{
				cmd.Attributes.Set( "CaptureOut128", _cap128 );
				cmd.Attributes.Set( "CaptureOut256", _cap256 );
			}
			for ( int i = 0; i < numVariants; i++ )
			{
				cmd.Attributes.Set( "CurrentTexture", _snapVarBucket[i] == 0 ? _texBufSmall : _texBufLarge );
				cmd.Attributes.Set( "TexStride", _snapVarBucket[i] == 0 ? SmallSize : LargeSize );
				cmd.Attributes.SetCombo( "D_TEXTURE", _snapVarTexture[i] ? 1 : 0 );
				cmd.Attributes.SetCombo( "D_BLEND", _snapVarBlend[i] );
				cmd.Attributes.SetCombo( "D_SHADOWMASK", _snapVarShadow[i] ? 1 : 0 );
				cmd.Attributes.Set( "CurVariant", i );
				cmd.Attributes.Set( "TexWidth", _snapVarW[i] );
				cmd.Attributes.Set( "TexHeight", _snapVarH[i] );
				cmd.Attributes.Set( "uIsCapture", _snapVarCapture[i] );
				cmd.Attributes.Set( "uCapYOffset", _snapVarCapYOffset[i] );
				cmd.DispatchComputeIndirect( _csRaster, _indirectArgs, (uint)i );
			}
			cmd.UavBarrier( _tileMemory[0] );
			cmd.UavBarrier( _tileMemory[1] );
			cmd.UavBarrier( _tileMemory[2] );
		}

		cmd.Attributes.SetCombo( "D_WBUFFER", wb );
		cmd.Attributes.Set( "TileColor", _tileMemory[0] );
		cmd.Attributes.Set( "TileDepth", _tileMemory[1] );
		cmd.Attributes.Set( "TileAttr", _tileMemory[2] );
		cmd.Attributes.Set( "FinalTile", _finalTileBuffer );
		cmd.DispatchCompute( _csDepthBlend, _screenWidth, _screenHeight, 1 );
		cmd.UavBarrier( _finalTileBuffer );

		cmd.Attributes.SetCombo( "D_EDGEMARKING", (int)((dispCnt >> 5) & 1) );
		cmd.Attributes.SetCombo( "D_FOG", (int)((dispCnt >> 7) & 1) );
		cmd.Attributes.SetCombo( "D_AA", (int)((dispCnt >> 4) & 1) );
		cmd.Attributes.Set( "FinalTile", _finalTileBuffer );
		cmd.Attributes.Set( "OutputTex", _outputTex );
		cmd.DispatchCompute( _csFinalPass, _screenWidth, _screenHeight, 1 );
		cmd.UavBarrier( _outputTex );

		return true;
	}

	private void BindCommon( CommandList cmd )
	{
		cmd.Attributes.Set( "MetaUniform", _metaBuffer );
		cmd.Attributes.Set( "RenderPolygons", _renderPolygonBuffer );
		cmd.Attributes.Set( "XSpanSetups", _xSpanSetupBuffer );
		cmd.Attributes.Set( "BinResult", _binResultBuffer );
		cmd.Attributes.Set( "WorkDesc", _workDescBuffer );
		cmd.Attributes.Set( "IndirectArgs", _indirectArgs );

		cmd.Attributes.Set( "TileSize", _tileSize );
		cmd.Attributes.Set( "TilesPerLine", _tilesPerLine );
		cmd.Attributes.Set( "TileLines", _tileLines );
		cmd.Attributes.Set( "CoarseTileW", _coarseTileW );
		cmd.Attributes.Set( "CoarseTileH", _coarseTileH );
		cmd.Attributes.Set( "ScreenWidth", _screenWidth );
		cmd.Attributes.Set( "ScreenHeight", _screenHeight );
		cmd.Attributes.Set( "MaxWorkTiles", _maxWorkTiles );
	}
}
