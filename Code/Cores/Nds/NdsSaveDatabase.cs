namespace Emulotl.Nds;

public static class NdsSaveDatabase
{
	private static readonly int[] SramLengths =
	[
		0,
		512,
		8192, 65536, 128 * 1024,
		256 * 1024, 512 * 1024, 1024 * 1024,
		8192 * 1024, 16384 * 1024, 65536 * 1024
	];

	public static int SaveMemType( byte[] rom )
	{
		if ( rom == null || rom.Length < 0x24 )
			return 0;

		if ( !NdsRomList.ReadROMParams( GameCodeAsU32( rom ), out uint raw ) )
			raw = IsHomebrew( rom ) ? 0u : 2u;

		return raw <= 10 ? (int)raw : 0;
	}

	public static int Length( int saveMemType )
	{
		uint t = (uint)saveMemType <= 10 ? (uint)saveMemType : 0;
		return SramLengths[t];
	}

	public static int SramType( int saveMemType ) => saveMemType switch
	{
		1 => 1,
		2 or 3 or 4 => 2,
		5 or 6 or 7 => 3,
		8 or 9 or 10 => 4,
		_ => 0,
	};

	private static uint GameCodeAsU32( byte[] rom ) =>
		(uint)(rom[0x0C] | (rom[0x0D] << 8) | (rom[0x0E] << 16) | (rom[0x0F] << 24));

	private static bool IsHomebrew( byte[] rom )
	{
		uint arm9off = (uint)(rom[0x20] | (rom[0x21] << 8) | (rom[0x22] << 16) | (rom[0x23] << 24));
		bool hashes = rom[0x0C] == (byte)'#' && rom[0x0D] == (byte)'#' && rom[0x0E] == (byte)'#' && rom[0x0F] == (byte)'#';
		return arm9off < 0x4000 || hashes;
	}
}
