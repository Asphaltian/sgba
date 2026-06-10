namespace sGBA;

public sealed partial class NetworkManager
{
	private GbaWirelessAdapter Adapter => _emulator?.Core?.Io?.WirelessAdapter;
	private GbaWirelessAdapter _wiredAdapter;

	private void TryWireAdapter()
	{
		var adapter = Adapter;
		if ( adapter is null )
			return;
		adapter.Network = this;
		_wiredAdapter = adapter;
	}

	private void WireAdapterIfNeeded()
	{
		var adapter = Adapter;
		if ( adapter is null || ReferenceEquals( adapter, _wiredAdapter ) )
			return;

		adapter.Network = this;
		_wiredAdapter = adapter;
	}

	private void RouteWirelessPacket( int senderIndex, byte[] payload )
	{
		Adapter?.EnqueueReceive( senderIndex, payload, payload.Length );
	}
}
