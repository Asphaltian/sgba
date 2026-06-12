using System.Collections.Concurrent;
using Sandbox.Network;

namespace sGBA;

public sealed class NetworkManager : Component, IWirelessNetwork, Component.INetworkListener
{
	public const int MaxWirelessPlayers = 5;
	public const int MaxLinkPlayers = 4;

	private const int BroadcastClientId = 0xFFFF;

	public int PlayerCap => Mode == SessionMode.LinkCable ? MaxLinkPlayers : MaxWirelessPlayers;

	public static NetworkManager Current { get; private set; }

	public event Action JoinRejected;

	public SessionState State { get; private set; } = SessionState.Solo;
	public SessionMode Mode { get; private set; } = SessionMode.LinkCable;
	public SessionVisibility Visibility { get; private set; } = SessionVisibility.InviteOnly;
	public string GameTitle { get; private set; }
	public string GameSha1 { get; private set; }
	public string GameCode { get; private set; }

	public IReadOnlyList<SessionPlayer> Players => _roster.Players;
	public SessionPlayer LocalPlayer => _roster.LocalPlayer;

	public bool IsActive => State != SessionState.Solo;
	public bool IsHost => Networking.IsHost && IsActive;
	public bool IsClient => IsActive && !Networking.IsHost;

	public bool InMultiplayerSession => _roster.PlayerCount > 1;
	public bool AllReady
	{
		get
		{
			var ps = _roster.Players;
			return ps.Count > 1 && ps.All( p => p.Ready || p.IsHost );
		}
	}
	public bool CanStart => IsHost && AllReady && State == SessionState.Hosting;

	private readonly SessionRoster _roster = new( MaxWirelessPlayers );

	private EmulatorComponent _emulator;
	private readonly ConcurrentQueue<(int Slot, byte[] Payload)> _outbox = new();

	public LinkCableMode Link { get; private set; }
	private WirelessAdapterMode _wireless;
	private INetMode ActiveMode => Mode == SessionMode.LinkCable ? Link : _wireless;

	internal SessionRoster Roster => _roster;
	internal EmulatorComponent Emulator => _emulator;
	internal void SetState( SessionState state ) => State = state;
	internal void CommitRoster() => HostCommitRoster();
	internal void BroadcastBeginGame() => RpcBeginGame();

	internal void SendJoinLinkedGame( Connection conn )
	{
		using ( Rpc.FilterInclude( conn ) )
			RpcJoinLinkedGame();
	}

	internal void SendRaw( int slot, byte[] data ) => ((IWirelessNetwork)this).Send( slot, data, data.Length );

	protected override void OnAwake()
	{
		Current = this;
		Link = new LinkCableMode( this );
		_wireless = new WirelessAdapterMode( this );
	}

	protected override void OnStart()
	{
		_emulator = Scene.GetAllComponents<EmulatorComponent>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsActive && Networking.IsHost && State == SessionState.Solo )
			State = SessionState.Hosting;

		while ( _outbox.TryDequeue( out var pkt ) )
			DispatchSend( pkt.Slot, pkt.Payload );

		DetectHostLeft();
		HostUpdateMode();

		ActiveMode?.Tick();
	}

	protected override void OnDestroy()
	{
		if ( Current == this )
			Current = null;
	}

	private string _modeGameCode;

	private void HostUpdateMode()
	{
		if ( !Networking.IsHost )
			return;

		var code = EmulatorComponent.Current?.GameCode;
		if ( string.IsNullOrEmpty( code ) || code == _modeGameCode )
			return;

		_modeGameCode = code;
		GameCode = code;

		var mode = GameLibrary.DetectMode( code );
		if ( mode == Mode )
			return;

		Mode = mode;
		try { Networking.SetData( LobbyDataKeys.Mode, ((int)mode).ToString() ); }
		catch { }
		RpcSyncMode( (int)mode );
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcSyncMode( int mode )
	{
		if ( Networking.IsHost )
			return;
		Mode = (SessionMode)mode;
	}

	public bool Host( GameEntry rom, SessionVisibility visibility )
	{
		if ( IsActive )
			return false;
		if ( rom is null )
			return false;

		Visibility = visibility;
		Mode = GameLibrary.DetectMode( rom.GameCode );
		GameTitle = rom.DisplayTitle;
		GameCode = rom.GameCode;
		GameSha1 = GameLibrary.ComputeSha1( rom );

		var config = new LobbyConfig
		{
			MaxPlayers = PlayerCap,
			Name = $"{rom.DisplayTitle}",
			Privacy = ToLobbyPrivacy( visibility ),
			Hidden = visibility == SessionVisibility.InviteOnly,
			DestroyWhenHostLeaves = true,
			AutoSwitchToBestHost = false
		};

		Networking.CreateLobby( config );
		State = SessionState.Hosting;

		Networking.SetData( LobbyDataKeys.GameTitle, GameTitle ?? string.Empty );
		Networking.SetData( LobbyDataKeys.GameCode, GameCode ?? string.Empty );
		Networking.SetData( LobbyDataKeys.GameSha1, GameSha1 ?? string.Empty );
		Networking.SetData( LobbyDataKeys.Visibility, ((int)Visibility).ToString() );
		Networking.SetData( LobbyDataKeys.Mode, ((int)Mode).ToString() );
		Networking.SetData( LobbyDataKeys.HostName, Connection.Local?.DisplayName ?? string.Empty );

		HostCommitRoster();

		return true;
	}

	public bool Join( ulong lobbyId )
	{
		if ( IsActive )
			return false;

		Networking.Connect( lobbyId );
		State = SessionState.Joined;
		return true;
	}

	public void Leave()
	{
		if ( !IsActive )
			return;

		if ( IsHost )
			RpcHostLeaving();

		Link.Exit();
		Networking.Disconnect();
		ClearSessionState();
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcHostLeaving()
	{
		if ( Networking.IsHost )
			return;

		ActiveMode?.OnHostLeft();
	}

	private void ClearSessionState()
	{
		_roster.Clear();
		State = SessionState.Solo;
		Mode = SessionMode.WirelessAdapter;
		GameTitle = null;
		GameSha1 = null;
		GameCode = null;
		_modeGameCode = null;
		_joinedGameLoaded = false;
		_hadRemoteHost = false;
	}

	private bool _hadRemoteHost;

	private void DetectHostLeft()
	{
		var host = Connection.Host;
		bool hasRemoteHost = Networking.IsActive && host is not null && host != Connection.Local && host.IsActive;

		if ( !_hadRemoteHost )
		{
			_hadRemoteHost = hasRemoteHost;
			return;
		}

		if ( hasRemoteHost )
			return;

		_hadRemoteHost = false;
		ActiveMode?.OnHostLeft();
	}

	public string ResolveSessionRomPath() => GameLibrary.FindMatch( GameSha1, GameCode )?.Path;

	public void ClearSessionAfterHostLost()
	{
		Link.Exit();
		Networking.Disconnect();
		ClearSessionState();
	}

	public void SetReady( bool ready )
	{
		if ( !IsActive )
			return;
		if ( IsHost )
			return;

		RpcSetReady( ready );
	}

	public void StartGame()
	{
		if ( !CanStart )
			return;
		RpcBeginGame();
	}

	public void OnActive( Connection conn )
	{
		if ( !Networking.IsHost && State == SessionState.Solo )
		{
			State = SessionState.Joined;
			LoadSessionFromLobbyData();
		}

		if ( !Networking.IsHost )
			return;

		_roster.AssignSlot( conn, PlayerCap );

		if ( conn is not null && conn != Connection.Local )
		{
			using ( Rpc.FilterInclude( conn ) )
				RpcSyncSession( GameTitle ?? string.Empty, GameSha1 ?? string.Empty, GameCode ?? string.Empty, (int)Visibility, (int)Mode );
		}

		HostCommitRoster();
		ActiveMode?.OnPeerJoined( conn );
	}

	private void LoadSessionFromLobbyData()
	{
		var title = Networking.GetData( LobbyDataKeys.GameTitle );
		var sha1 = Networking.GetData( LobbyDataKeys.GameSha1 );
		var code = Networking.GetData( LobbyDataKeys.GameCode );

		if ( !string.IsNullOrEmpty( title ) )
			GameTitle = title;
		if ( !string.IsNullOrEmpty( sha1 ) )
			GameSha1 = sha1;
		if ( !string.IsNullOrEmpty( code ) )
			GameCode = code;

		var vis = Networking.GetData( LobbyDataKeys.Visibility );
		if ( int.TryParse( vis, out var v ) )
			Visibility = (SessionVisibility)v;

		var modeRaw = Networking.GetData( LobbyDataKeys.Mode );
		if ( int.TryParse( modeRaw, out var m ) )
			Mode = (SessionMode)m;

		TryLoadJoinedGame();
	}

	public void OnDisconnected( Connection conn )
	{
		if ( conn is not null )
			_roster.RemoveReady( conn.Id );

		if ( !Networking.IsHost )
			return;

		int slot = conn is not null && _roster.TryGetSlot( conn.Id, out var s ) ? s : -1;

		_roster.ReleaseSlot( conn );
		HostCommitRoster();
		ActiveMode?.OnPeerLeft( slot, conn );
	}

	private void HostCommitRoster()
	{
		if ( !Networking.IsHost )
			return;

		_roster.EnsureLocal( PlayerCap );
		_roster.RebuildLocalView();
		RpcSyncRoster( _roster.Serialize() );
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcSyncRoster( byte[] payload )
	{
		if ( Networking.IsHost || payload is null || payload.Length < 4 )
			return;

		_roster.Apply( payload );
	}

	[Rpc.Host( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcSetReady( bool ready )
	{
		var caller = Rpc.Caller;
		if ( caller is null )
			return;

		_roster.SetReady( caller.Id, ready );
		HostCommitRoster();
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcSyncSession( string gameTitle, string gameSha1, string gameCode, int visibility, int mode )
	{
		if ( Networking.IsHost )
			return;

		if ( !string.IsNullOrEmpty( gameTitle ) )
			GameTitle = gameTitle;
		if ( !string.IsNullOrEmpty( gameSha1 ) )
			GameSha1 = gameSha1;
		if ( !string.IsNullOrEmpty( gameCode ) )
			GameCode = gameCode;
		Visibility = (SessionVisibility)visibility;
		Mode = (SessionMode)mode;
		State = SessionState.Joined;

		TryLoadJoinedGame();
	}

	private bool _joinedGameLoaded;

	private void TryLoadJoinedGame()
	{
		if ( _joinedGameLoaded )
			return;

		if ( string.IsNullOrEmpty( GameSha1 ) && string.IsNullOrEmpty( GameCode ) )
			return;

		var match = GameLibrary.FindMatch( GameSha1, GameCode );
		if ( match is null )
		{
			Log.Warning( $"[sGBA] Join: no matching game for '{GameTitle}' (code {GameCode}); returning to home." );
			Leave();
			JoinRejected?.Invoke();
			return;
		}

		_joinedGameLoaded = true;

		if ( Mode == SessionMode.LinkCable )
			return;

		EmulatorComponent.Current?.Restart( match.Path );
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcBeginGame()
	{
		State = SessionState.InGame;
		ActiveMode?.OnGameStarted();
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcJoinLinkedGame()
	{
		if ( Networking.IsHost )
			return;

		State = SessionState.InGame;
		Link.OnGameStarted();
	}

	private static LobbyPrivacy ToLobbyPrivacy( SessionVisibility v ) => v switch
	{
		SessionVisibility.Public => LobbyPrivacy.Public,
		SessionVisibility.FriendsOnly => LobbyPrivacy.FriendsOnly,
		SessionVisibility.InviteOnly => LobbyPrivacy.Private,
		_ => LobbyPrivacy.Private
	};

	void IWirelessNetwork.Send( int clientId, byte[] data, int length )
	{
		if ( data is null || length <= 0 )
			return;

		var payload = new byte[length];
		Buffer.BlockCopy( data, 0, payload, 0, length );
		_outbox.Enqueue( (clientId, payload) );
	}

	private void DispatchSend( int slot, byte[] payload )
	{
		bool isAv = payload is not null && payload.Length > 0
			&& payload[0] == (byte)LinkCablePacketType.Av;

		if ( slot == BroadcastClientId )
		{
			if ( isAv )
				RpcReceiveAv( payload );
			else
				RpcReceive( payload );
			return;
		}

		var target = _roster.ConnectionForSend( slot );
		if ( target is null || target == Connection.Local )
			return;

		using ( Rpc.FilterInclude( target ) )
		{
			if ( isAv )
				RpcReceiveAv( payload );
			else
				RpcReceive( payload );
		}
	}

	[Rpc.Broadcast( NetFlags.Reliable | NetFlags.SendImmediate )]
	private void RpcReceive( byte[] payload )
	{
		HandleReceive( payload );
	}

	[Rpc.Broadcast( NetFlags.UnreliableNoDelay )]
	private void RpcReceiveAv( byte[] payload )
	{
		HandleReceive( payload );
	}

	private void HandleReceive( byte[] payload )
	{
		if ( Rpc.Caller == Connection.Local )
			return;

		if ( payload is null || payload.Length < 12 )
			return;

		if ( !_roster.TryGetSlot( Rpc.Caller.Id, out var senderIndex ) )
			return;

		ActiveMode?.OnPacket( senderIndex, payload );
	}
}
