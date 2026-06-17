namespace Emulotl.Nds;

public sealed class FIFO<T>
{
	private readonly T[] Entries;
	private readonly uint NumEntries;
	private uint NumOccupied;
	private uint ReadPos, WritePos;

	public FIFO( uint numEntries )
	{
		NumEntries = numEntries;
		Entries = new T[numEntries];
	}

	public void Clear()
	{
		NumOccupied = 0;
		ReadPos = 0;
		WritePos = 0;
		Entries[ReadPos] = default;
	}

	public void Write( T val )
	{
		if ( IsFull() ) return;

		Entries[WritePos] = val;

		WritePos++;
		if ( WritePos >= NumEntries )
			WritePos = 0;

		NumOccupied++;
	}

	public T Read()
	{
		if ( IsEmpty() )
			return Entries[(ReadPos == 0 ? NumEntries : ReadPos) - 1];

		T ret = Entries[ReadPos];

		ReadPos++;
		if ( ReadPos >= NumEntries )
			ReadPos = 0;

		NumOccupied--;
		return ret;
	}

	public T Peek() => Entries[ReadPos];

	public T Peek( uint offset )
	{
		uint pos = ReadPos + offset;
		if ( pos >= NumEntries )
			pos -= NumEntries;

		return Entries[pos];
	}

	public uint Level() => NumOccupied;
	public bool IsEmpty() => NumOccupied == 0;
	public bool IsFull() => NumOccupied >= NumEntries;

	public bool CanFit( uint num ) => NumOccupied + num <= NumEntries;
}
