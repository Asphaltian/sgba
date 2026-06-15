namespace Emulotl;

public interface IEmulatorCore
{
	SystemProfile Profile { get; }

	void LoadRom( byte[] romData );

	void Reset();

	void RunFrame();

	bool StepFrame();

	void SetButtons( int player, ulong pressedMask );

	IReadOnlyList<IVideoOutput> Screens { get; }

	IAudioOutput Audio { get; }

	ISaveData SaveData { get; }

	byte[] SaveState( byte[] screenshot );

	void LoadState( byte[] data );
}
