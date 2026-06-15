namespace Emulotl;

public sealed class SessionRoster
{
	private readonly Connection[] _slots;
	private readonly Dictionary<Guid, int> _slotByConn = [];
	private readonly List<SessionPlayer> _players;
	private readonly Dictionary<Guid, bool> _ready = [];
	private int _localSlot = -1;

	public SessionRoster( int capacity )
	{
		_slots = new Connection[capacity];
		_players = new List<SessionPlayer>( capacity );
	}

	public int SlotCount => _slots.Length;
	public int PlayerCount => _players.Count;
	public int LocalSlot => _localSlot;
	public IReadOnlyList<SessionPlayer> Players => _players;
	public SessionPlayer LocalPlayer => _localSlot >= 0 ? _players.FirstOrDefault( p => p.Slot == _localSlot ) : null;

	public Connection ConnectionAt( int slot ) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;
	public bool TryGetSlot( Guid connId, out int slot ) => _slotByConn.TryGetValue( connId, out slot );

	public void SetReady( Guid connId, bool ready ) => _ready[connId] = ready;
	public void RemoveReady( Guid connId ) => _ready.Remove( connId );

	public int AssignSlot( Connection conn, int cap )
	{
		if ( conn is null )
			return -1;

		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i].Id == conn.Id )
				return i;
		}

		if ( conn == Connection.Local && _slots[0] is null )
		{
			_slots[0] = conn;
			return 0;
		}

		int start = conn.IsHost ? 0 : 1;
		for ( int i = start; i < cap && i < _slots.Length; i++ )
		{
			if ( _slots[i] is null )
			{
				_slots[i] = conn;
				return i;
			}
		}
		return -1;
	}

	public void ReleaseSlot( Connection conn )
	{
		if ( conn is null )
			return;

		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i].Id == conn.Id )
				_slots[i] = null;
		}
	}

	public void ReleaseRemoteSlots()
	{
		var local = Connection.Local;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i] != local )
				_slots[i] = null;
		}
	}

	public void EnsureLocal( int cap )
	{
		var local = Connection.Local;
		if ( local is null )
			return;

		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i].Id == local.Id )
				return;
		}
		AssignSlot( local, cap );
	}

	public void RebuildLocalView()
	{
		_slotByConn.Clear();
		_players.Clear();
		_localSlot = -1;

		var localId = Connection.Local?.Id ?? Guid.Empty;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			var conn = _slots[i];
			if ( conn is null )
				continue;

			_slotByConn[conn.Id] = i;
			var ready = conn.IsHost || _ready.GetValueOrDefault( conn.Id );
			_players.Add( new SessionPlayer( conn, i ) { Ready = ready } );
			if ( conn.Id == localId )
				_localSlot = i;
		}
	}

	public int ActiveMask()
	{
		int mask = 0;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i].IsActive )
				mask |= 1 << i;
		}
		return mask;
	}

	public List<int> ActiveSlots()
	{
		var slots = new List<int>( _slots.Length );
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null && _slots[i].IsActive )
				slots.Add( i );
		}
		return slots;
	}

	public Connection ConnectionForSend( int slot )
	{
		if ( slot < 0 || slot >= _slots.Length )
			return null;

		var conn = _slots[slot];
		if ( conn is not null )
			return conn;

		if ( slot == 0 && !Networking.IsHost )
		{
			var all = Connection.All;
			if ( all is not null )
			{
				for ( int i = 0; i < all.Count; i++ )
				{
					if ( all[i] is not null && all[i].IsHost )
						return all[i];
				}
			}
		}
		return null;
	}

	public byte[] Serialize()
	{
		int count = 0;
		for ( int i = 0; i < _slots.Length; i++ )
		{
			if ( _slots[i] is not null )
				count++;
		}

		using var ms = new System.IO.MemoryStream();
		using var w = new System.IO.BinaryWriter( ms );
		w.Write( count );
		for ( int i = 0; i < _slots.Length; i++ )
		{
			var conn = _slots[i];
			if ( conn is null )
				continue;

			w.Write( i );
			w.Write( conn.Id.ToByteArray() );
			w.Write( conn.SteamId );
			w.Write( conn.IsHost );
			w.Write( conn.IsHost || _ready.GetValueOrDefault( conn.Id ) );
			w.Write( conn.DisplayName ?? string.Empty );
		}
		return ms.ToArray();
	}

	public void Apply( byte[] payload )
	{
		Array.Clear( _slots, 0, _slots.Length );
		_slotByConn.Clear();
		_players.Clear();
		_localSlot = -1;

		var localId = Connection.Local?.Id ?? Guid.Empty;

		try
		{
			using var ms = new System.IO.MemoryStream( payload );
			using var r = new System.IO.BinaryReader( ms );
			int count = r.ReadInt32();
			for ( int n = 0; n < count; n++ )
			{
				int slot = r.ReadInt32();
				var id = new Guid( r.ReadBytes( 16 ) );
				ulong steamId = r.ReadUInt64();
				bool isHost = r.ReadBoolean();
				bool ready = r.ReadBoolean();
				string name = r.ReadString();

				if ( slot < 0 || slot >= _slots.Length )
					continue;

				var player = new SessionPlayer( slot, id, steamId, name, isHost, ready );
				_slots[slot] = player.Connection;
				_slotByConn[id] = slot;
				_players.Add( player );
				if ( id == localId )
					_localSlot = slot;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Emulotl] roster parse failed: {e.Message}" );
		}
	}

	public void Clear()
	{
		Array.Clear( _slots, 0, _slots.Length );
		_slotByConn.Clear();
		_players.Clear();
		_ready.Clear();
		_localSlot = -1;
	}
}
