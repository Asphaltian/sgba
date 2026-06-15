using GbaCore = Emulotl.Gba.Gba;
using Emulotl.Gba;
namespace Emulotl;

public sealed class WirelessAdapterMode : INetMode
{
	private const int HostSlot = 0;

	private readonly NetworkManager _net;
	private GbaWirelessAdapter _wired;

	public WirelessAdapterMode( NetworkManager net )
	{
		_net = net;
	}

	private GbaWirelessAdapter Adapter => (_net.Emulator?.Core as GbaCore)?.Io?.WirelessAdapter;

	public void OnPeerJoined( Connection conn )
	{
		if ( conn is not null && conn != Connection.Local && _net.State == SessionState.Solo )
			_net.SetState( SessionState.InGame );
	}

	public void OnPeerLeft( int slot, Connection conn )
	{
		if ( slot >= 0 )
			Adapter?.EnqueueDisconnect( slot );
	}

	public void OnHostLeft()
	{
		Adapter?.EnqueueDisconnect( HostSlot );
		_net.ClearSessionAfterHostLost();
	}

	public void OnGameStarted() { }

	public void OnPacket( int senderSlot, byte[] payload )
	{
		Adapter?.EnqueueReceive( senderSlot, payload, payload.Length );
	}

	public void Tick()
	{
		var adapter = Adapter;
		if ( adapter is null || ReferenceEquals( adapter, _wired ) )
			return;

		adapter.Network = _net;
		_wired = adapter;
	}

	public void Exit() { }
}
