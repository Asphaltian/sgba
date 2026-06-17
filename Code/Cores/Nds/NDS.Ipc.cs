namespace Emulotl.Nds;

public sealed partial class NDS
{
	public ushort IPCSync9, IPCSync7;
	public ushort IPCFIFOCnt9, IPCFIFOCnt7;
	public FIFO<uint> IPCFIFO9 = new( 16 );
	public FIFO<uint> IPCFIFO7 = new( 16 );

	private void ResetIpc()
	{
		IPCSync9 = 0;
		IPCSync7 = 0;
		IPCFIFOCnt9 = 0;
		IPCFIFOCnt7 = 0;
		IPCFIFO9.Clear();
		IPCFIFO7.Clear();
	}
}
