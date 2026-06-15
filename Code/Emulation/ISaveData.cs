namespace Emulotl;

public interface ISaveData
{
	byte[] Data { get; }
	void Load( byte[] data );
	bool ConsumeDirty();
}
