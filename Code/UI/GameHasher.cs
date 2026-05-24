using System.Security.Cryptography;

namespace sGBA;

public sealed record GameHashes( string Md5, string Sha1, long Size, string Crc = "" );

internal static class GameHasher
{
	private static readonly Dictionary<string, GameHashes> Hashes = new( StringComparer.OrdinalIgnoreCase );
	private static readonly uint[] Crc32Table = BuildCrc32Table();

	public static GameHashes Compute( GameEntry game )
	{
		if ( game is null )
			return new GameHashes( string.Empty, string.Empty, 0 );

		if ( Hashes.TryGetValue( game.Path, out var cached ) )
			return cached;

		try
		{
			var bytes = game.FileSystem.ReadAllBytes( game.Path ).ToArray();
			var hashes = new GameHashes(
				Convert.ToHexString( MD5.HashData( bytes ) ).ToLowerInvariant(),
				Convert.ToHexString( SHA1.HashData( bytes ) ).ToLowerInvariant(),
				bytes.LongLength,
				ComputeCrc32( bytes )
			);

			Hashes[game.Path] = hashes;
			return hashes;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[sGBA] Failed to hash {game.DisplayTitle}: {ex.Message}" );
			return new GameHashes( string.Empty, string.Empty, 0 );
		}
	}

	private static string ComputeCrc32( byte[] bytes )
	{
		var checksum = 0xFFFFFFFFu;
		foreach ( var value in bytes )
		{
			var index = (checksum ^ value) & 0xFF;
			checksum = (checksum >> 8) ^ Crc32Table[index];
		}

		return Convert.ToHexString( BitConverter.GetBytes( ~checksum ).Reverse().ToArray() ).ToUpperInvariant();
	}

	private static uint[] BuildCrc32Table()
	{
		var table = new uint[256];
		for ( var index = 0; index < table.Length; index++ )
		{
			var entry = (uint)index;
			for ( var bit = 0; bit < 8; bit++ )
			{
				entry = (entry & 1) != 0 ? (entry >> 1) ^ 0xEDB88320u : entry >> 1;
			}

			table[index] = entry;
		}

		return table;
	}
}