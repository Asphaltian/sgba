namespace sGBA;

public interface INetMode
{
	void OnPeerJoined( Connection conn );
	void OnPeerLeft( int slot, Connection conn );
	void OnHostLeft();
	void OnGameStarted();
	void OnPacket( int senderSlot, byte[] payload );
	void Tick();
	void Exit();
}
