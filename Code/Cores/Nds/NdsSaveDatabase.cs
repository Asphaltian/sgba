using System.Collections.Generic;

namespace Emulotl.Nds;

public static class NdsSaveDatabase
{
	public const int DefaultLength = 8192;

	private static readonly int[] SramLengths =
	{
		0,
		512,
		8192, 65536, 131072,
		262144, 524288, 1048576,
		8388608, 16777216, 67108864
	};

	private static readonly Dictionary<uint, int> SaveTypeByTitle = new()
	{
		{ Id( "C2S" ), 4 },
		{ Id( "ADA" ), 6 }, { Id( "APA" ), 6 }, { Id( "CPU" ), 6 },
		{ Id( "IPK" ), 6 }, { Id( "IPG" ), 6 },
		{ Id( "IRB" ), 6 }, { Id( "IRA" ), 6 }, { Id( "IRE" ), 6 }, { Id( "IRD" ), 6 },
		{ Id( "ASM" ), 2 },
		{ Id( "A2D" ), 2 },
		{ Id( "ADM" ), 5 },
		{ Id( "AMH" ), 5 },
		{ Id( "AY9" ), 3 },
		{ Id( "BZH" ), 1 },
		{ Id( "AKW" ), 2 },
		{ Id( "YKG" ), 3 },
		{ Id( "B3R" ), 5 },
		{ Id( "CB6" ), 2 },
		{ Id( "YFT" ), 5 },
		{ Id( "A5F" ), 2 },
		{ Id( "COL" ), 3 },
	};

	private static uint Id( string s ) => (uint)(s[0] | (s[1] << 8) | (s[2] << 16));

	public static int TypeForRom( byte[] rom )
	{
		if ( rom == null || rom.Length < 0x10 )
			return -1;

		uint title = (uint)(rom[0x0C] | (rom[0x0D] << 8) | (rom[0x0E] << 16));
		return SaveTypeByTitle.TryGetValue( title, out int type ) ? type : -1;
	}

	public static int Length( int type )
	{
		if ( type < 0 || type >= SramLengths.Length )
			return DefaultLength;
		return SramLengths[type];
	}
}
