namespace sGBA;

public sealed class LinkCableMode : INetMode
{
	private const int HostSlot = 0;

	private readonly NetworkManager _net;

	public LinkCableSession Session { get; private set; }
	public Action<LinkCableAvPacket> OnRemoteAv;
	public Action<LinkCableStatePacket> OnRemoteState;
	public bool ExternalAvDriver { get; set; }

	private bool _active;
	private int _linkedMask;

	public LinkCableMode( NetworkManager net )
	{
		_net = net;
	}

	public void AttachSession( LinkCableSession session ) => Session = session;
	public void DetachSession() => Session = null;

	public void OnPeerJoined( Connection conn ) => ApplyRoster();
	public void OnPeerLeft( int slot, Connection conn ) => ApplyRoster();
	public void OnGameStarted() => BeginLinkedPlay();
	public void Tick() => Pump();

	public void OnHostLeft()
	{
		var emu = _net.Emulator;
		if ( emu?.IsLinked == true )
			emu.ResumeSoloAfterHostLost();
		else
			_net.ClearSessionAfterHostLost();
	}

	public void Exit()
	{
		_active = false;
		_linkedMask = 0;
		_net.Emulator?.EndLinkedMode();
	}

	public void BeginLinkedPlay()
	{
		var emu = _net.Emulator;
		if ( emu == null )
			return;

		if ( _net.IsHost )
			emu.BeginLinkedHost( _net.Roster.ActiveSlots() );
		else if ( _net.IsClient )
			emu.BeginLinkedClient();
	}

	private void ApplyRoster()
	{
		if ( !Networking.IsHost )
			return;

		var emu = _net.Emulator;
		if ( emu == null )
			return;

		int mask = _net.Roster.ActiveMask();
		if ( mask == _linkedMask )
			return;

		int count = CountBits( mask );

		if ( !_active )
		{
			if ( count < 2 )
				return;

			_active = true;
			_linkedMask = mask;
			_net.SetState( SessionState.InGame );
			_net.BroadcastBeginGame();
			return;
		}

		if ( count < 2 )
		{
			_active = false;
			_linkedMask = 0;
			ReturnToLobby();
			emu.ContinueSoloFromLinkedHost();
			return;
		}

		int additions = mask & ~_linkedMask;
		_linkedMask = mask;
		emu.SyncLinkedHostSlots( _net.Roster.ActiveSlots() );

		for ( int slot = 0; slot < _net.Roster.SlotCount; slot++ )
		{
			if ( (additions & (1 << slot)) == 0 )
				continue;

			var conn = _net.Roster.ConnectionAt( slot );
			if ( conn is not null && conn != Connection.Local )
				_net.SendJoinLinkedGame( conn );
		}
	}

	private void ReturnToLobby()
	{
		_net.Roster.ReleaseRemoteSlots();
		_net.CommitRoster();
		_net.SetState( SessionState.Hosting );
	}

	private static int CountBits( int mask )
	{
		int n = 0;
		while ( mask != 0 )
		{
			n += mask & 1;
			mask >>= 1;
		}
		return n;
	}

	public void SendLocalInput( long frame, ushort keys )
	{
		if ( !_net.IsClient )
			return;

		var packet = new LinkCableInputPacket( frame, keys ).Serialize();
		_net.SendRaw( HostSlot, packet );
	}

	public void SendLocalSave( byte[] saveData )
	{
		if ( !_net.IsClient || saveData == null || saveData.Length == 0 )
			return;

		var packet = new LinkCableSaveUploadPacket
		{
			PlayerId = _net.LocalPlayer?.Slot ?? -1,
			SaveData = saveData,
		}.Serialize();
		_net.SendRaw( HostSlot, packet );
	}

	public void HostSendAv( int slot, LinkCableAvPacket packet )
	{
		if ( !_net.IsHost || packet is null )
			return;

		_net.SendRaw( slot, packet.Serialize() );
	}

	public void HostSendState( int slot, byte[] state )
	{
		if ( !_net.IsHost || state is null || state.Length == 0 )
			return;

		_net.SendRaw( slot, new LinkCableStatePacket { PlayerId = slot, State = state }.Serialize() );
	}

	private void Pump()
	{
		var session = Session;
		if ( session is null || !_net.IsHost || ExternalAvDriver )
			return;

		var instances = session.Host.Instances;
		for ( int i = 0; i < instances.Count; i++ )
		{
			var inst = instances[i];
			if ( inst.Slot < 0 )
				continue;

			while ( session.TryDequeueAv( inst, out var packet ) )
				_net.SendRaw( inst.Slot, packet.Serialize() );
		}
	}

	public void OnPacket( int senderSlot, byte[] payload )
	{
		if ( payload.Length < 1 )
			return;

		switch ( (LinkCablePacketType)payload[0] )
		{
			case LinkCablePacketType.Input:
				RouteInput( senderSlot, payload );
				break;
			case LinkCablePacketType.SaveUpload:
				RouteSaveUpload( senderSlot, payload );
				break;
			case LinkCablePacketType.Av:
				RouteAv( payload );
				break;
			case LinkCablePacketType.State:
				RouteState( payload );
				break;
		}
	}

	private void RouteInput( int senderSlot, byte[] payload )
	{
		if ( !_net.IsHost || Session is null )
			return;

		if ( LinkCableInputPacket.TryParse( payload, payload.Length, out var packet ) )
			Session.SetSlotInput( senderSlot, packet.Keys );
	}

	private void RouteSaveUpload( int senderSlot, byte[] payload )
	{
		if ( !_net.IsHost || Session is null )
			return;

		if ( LinkCableSaveUploadPacket.TryParse( payload, payload.Length, out var packet ) )
			Session.LoadSlotSave( senderSlot, packet.SaveData );
	}

	private void RouteAv( byte[] payload )
	{
		if ( !_net.IsClient )
			return;

		if ( LinkCableAvPacket.TryParse( payload, payload.Length, out var packet ) )
		{
			try { OnRemoteAv?.Invoke( packet ); }
			catch ( Exception e ) { Log.Warning( $"[sGBA] OnRemoteAv error: {e.Message}" ); }
		}
	}

	private void RouteState( byte[] payload )
	{
		if ( !_net.IsClient )
			return;

		if ( LinkCableStatePacket.TryParse( payload, payload.Length, out var packet ) )
		{
			try { OnRemoteState?.Invoke( packet ); }
			catch ( Exception e ) { Log.Warning( $"[sGBA] OnRemoteState error: {e.Message}" ); }
		}
	}
}
