namespace Emulotl.Nds;

public sealed class NdsSaveData : ISaveData
{
	public byte[] Data { get; private set; } = [];
	public int Type { get; private set; }

	private byte[] _loaded = [];

	private const int DirtNone = 0;
	private const int DirtNew = 1;
	private const int DirtSeen = 2;
	private const int CleanupThreshold = 15;

	private int _dirty;
	private uint _dirtAge;

	public int LoadedLength => _loaded.Length;

	public void Load( byte[] data ) => _loaded = data ?? [];

	public void Setup( int length, int type )
	{
		if ( length <= 0 )
		{
			Data = [];
			Type = 0;
			return;
		}

		if ( _loaded.Length == length )
		{
			Data = _loaded;
		}
		else
		{
			Data = new byte[length];
			Array.Fill( Data, (byte)0xFF );
			if ( _loaded.Length > 0 )
				Array.Copy( _loaded, Data, Math.Min( _loaded.Length, length ) );
		}

		Type = type;
	}

	public void MarkDirty() => _dirty |= DirtNew;

	public bool ConsumeDirty()
	{
		if ( (_dirty & DirtNew) != 0 )
		{
			_dirtAge = 0;
			_dirty &= ~DirtNew;
			_dirty |= DirtSeen;
		}
		else if ( (_dirty & DirtSeen) != 0 )
		{
			_dirtAge++;
			if ( _dirtAge > CleanupThreshold )
			{
				_dirty = DirtNone;
				return true;
			}
		}

		return false;
	}
}
