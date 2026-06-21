namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private const int SmallSize = 256;
	private const int SmallLayers = 96;
	private const int LargeSize = 1024;
	private const int LargeLayers = 8;

	private const int SmallArea = SmallSize * SmallSize;
	private const int LargeArea = LargeSize * LargeSize;

	private readonly uint[] _texDataSmall = new uint[SmallArea * SmallLayers];
	private readonly uint[] _texDataLarge = new uint[LargeArea * LargeLayers];
	private readonly uint[] _decodeTmp = new uint[1024 * 1024];

	private struct TexCacheEntry
	{
		public int Layer;
		public int Bucket;
		public ulong Hash;
		public uint TexParam;
		public uint TexPalette;
	}

	private readonly Dictionary<ulong, TexCacheEntry> _texCache = [];
	private readonly Stack<int> _freeSmall = new();
	private readonly Stack<int> _freeLarge = new();
	private readonly List<ulong> _evictList = [];
	private int _numLayersSmall;
	private int _numLayersLarge;

	private ulong _curTexVersion;
	private ulong _cacheBuiltVersion = ulong.MaxValue;
	private bool _texturesChanged = true;

	private readonly int[] _varBlend = new int[MaxVariants];
	private readonly int[] _varLayer = new int[MaxVariants];
	private readonly int[] _varBucket = new int[MaxVariants];
	private readonly int[] _varW = new int[MaxVariants];
	private readonly int[] _varH = new int[MaxVariants];
	private readonly bool[] _varTexture = new bool[MaxVariants];
	private readonly bool[] _varShadow = new bool[MaxVariants];
	private readonly int[] _varCapture = new int[MaxVariants];
	private readonly int[] _varCapYOffset = new int[MaxVariants];
	private readonly int[] _captureInfoTex = new int[16];

	private ComputeRenderer2D _captureSrc;
	public void SetCaptureSource( ComputeRenderer2D s ) => _captureSrc = s;
	private Texture _cap128 => _captureSrc?.CaptureOutput128Tex;
	private Texture _cap256 => _captureSrc?.CaptureOutput256Tex;

	private void ResetVariants()
	{
		_numVariants = 0;
	}

	private void BeginTextureFrame()
	{
		_curTexVersion = _gpu2d.TexVRAMVersion;

		bool smallSaturated = _numLayersSmall >= SmallLayers && _freeSmall.Count == 0;
		bool largeSaturated = _numLayersLarge >= LargeLayers && _freeLarge.Count == 0;
		if ( smallSaturated || largeSaturated )
		{
			_texCache.Clear();
			_freeSmall.Clear();
			_freeLarge.Clear();
			_numLayersSmall = 0;
			_numLayersLarge = 0;
			_texturesChanged = true;
			_cacheBuiltVersion = _curTexVersion;
			return;
		}

		if ( _curTexVersion == _cacheBuiltVersion )
			return;

		_evictList.Clear();
		foreach ( KeyValuePair<ulong, TexCacheEntry> kv in _texCache )
		{
			ulong nh = ComputeTexSourceHash( kv.Value.TexParam, kv.Value.TexPalette );
			if ( nh != kv.Value.Hash )
				_evictList.Add( kv.Key );
		}

		for ( int i = 0; i < _evictList.Count; i++ )
		{
			TexCacheEntry e = _texCache[_evictList[i]];
			if ( e.Bucket == 0 ) _freeSmall.Push( e.Layer );
			else _freeLarge.Push( e.Layer );
			_texCache.Remove( _evictList[i] );
		}

		_cacheBuiltVersion = _curTexVersion;
	}

	private int GetOrDecodeLayer( ulong texKey, uint texParam, uint texPalette, out int bucket )
	{
		int w = TextureWidth( texParam );
		int h = TextureHeight( texParam );

		if ( _texCache.TryGetValue( texKey, out TexCacheEntry existing ) )
		{
			bucket = existing.Bucket;
			return existing.Layer;
		}

		bucket = (w <= SmallSize && h <= SmallSize) ? 0 : 1;

		int layer;
		if ( bucket == 0 )
		{
			if ( _freeSmall.Count > 0 ) layer = _freeSmall.Pop();
			else if ( _numLayersSmall < SmallLayers ) layer = _numLayersSmall++;
			else return 0;
		}
		else
		{
			if ( _freeLarge.Count > 0 ) layer = _freeLarge.Pop();
			else if ( _numLayersLarge < LargeLayers ) layer = _numLayersLarge++;
			else return 0;
		}

		DecodeIntoLayer( texParam, texPalette, bucket, layer, w, h );
		_texturesChanged = true;

		_texCache[texKey] = new TexCacheEntry
		{
			Layer = layer,
			Bucket = bucket,
			Hash = ComputeTexSourceHash( texParam, texPalette ),
			TexParam = texParam,
			TexPalette = texPalette
		};
		return layer;
	}

	private void DecodeIntoLayer( uint texParam, uint texPalette, int bucket, int layer, int w, int h )
	{
		int size = bucket == 0 ? SmallSize : LargeSize;
		uint[] data = bucket == 0 ? _texDataSmall : _texDataLarge;
		int area = size * size;

		DecodeTexture( texParam, texPalette, _decodeTmp );

		int cw = Math.Min( w, size );
		int ch = Math.Min( h, size );
		int basei = layer * area;
		for ( int y = 0; y < ch; y++ )
			Array.Copy( _decodeTmp, y * w, data, basei + y * size, cw );
	}

	private int GetOrAddVariant( int bucket, int layer, int blend, bool texture, bool shadow, int w, int h, int capture, int capYOffset )
	{
		for ( int j = 0; j < _numVariants; j++ )
		{
			if ( _varBucket[j] == bucket && _varLayer[j] == layer && _varBlend[j] == blend && _varTexture[j] == texture && _varShadow[j] == shadow && _varCapture[j] == capture && _varCapYOffset[j] == capYOffset )
				return j;
		}

		if ( _numVariants >= MaxVariants )
			return 0;

		int v = _numVariants++;
		_varBucket[v] = bucket;
		_varLayer[v] = layer;
		_varBlend[v] = blend;
		_varTexture[v] = texture;
		_varShadow[v] = shadow;
		_varW[v] = w;
		_varH[v] = h;
		_varCapture[v] = capture;
		_varCapYOffset[v] = capYOffset;
		return v;
	}

	private void AssignVariant( Polygon polygon, int i, bool enableTex )
	{
		uint attr = polygon.Attr;
		uint texParam = polygon.TexParam;
		uint fmt = (texParam >> 26) & 0x7;

		bool shadowMask = (attr & 0x3F000030u) == 0x00000030u;
		int bm = (int)((attr >> 4) & 0x3);
		bool hasTex = enableTex && fmt != 0 && !shadowMask;

		int blend;
		if ( shadowMask )
			blend = 0;
		else if ( bm == 2 )
			blend = ((_gpu.RenderDispCnt >> 1) & 1) != 0 ? 3 : 2;
		else if ( (bm == 1 || bm == 3) && hasTex )
			blend = 1;
		else
			blend = 0;

		int layer = 0, w = 0, h = 0, bucket = 0, capture = 0, capYOffset = 0;
		if ( hasTex )
		{
			w = TextureWidth( texParam );
			h = TextureHeight( texParam );

			if ( fmt == 7 && (w == 128 || w == 256) && _cap128 != null )
			{
				uint texaddr = texParam & 0xFFFF;
				uint startaddr = (texaddr << 3) >> 15;
				uint endaddr = ((texaddr << 3) + (uint)(h * w * 2) + 0x7FFF) >> 15;
				int capblock = -1;
				for ( uint b = startaddr; b < endaddr && b < 16; b++ )
					if ( _captureInfoTex[b] != -1 ) capblock = _captureInfoTex[b];

				if ( capblock != -1 )
				{
					if ( w == 128 ) { capture = 1; layer = capblock; capYOffset = (int)((texaddr >> 5) & 0x7F); }
					else { capture = 2; layer = capblock >> 2; capYOffset = (int)((texaddr >> 6) & 0xFF); }
				}
			}

			if ( capture == 0 )
			{
				uint tpKey = texParam & ~0xC00F0000u;
				if ( fmt == 5 ) tpKey &= ~(1u << 29);
				ulong texKey = fmt == 7 ? tpKey : (tpKey | ((ulong)(polygon.TexPalette & 0x1FFF) << 32));
				layer = GetOrDecodeLayer( texKey, texParam, polygon.TexPalette, out bucket );
			}
		}

		_renderPolygons[i].Variant = (uint)GetOrAddVariant( bucket, layer, blend, hasTex, shadowMask, w, h, capture, capYOffset );
		_renderPolygons[i].TextureLayer = layer;
	}
}
