namespace sGBA;

public static class GameLibrary
{
	private static readonly HashSet<string> WirelessAdapterGameCodes =
	[
		"BPR", // Pokémon FireRed
		"BPG", // Pokémon LeafGreen
		"BPE", // Pokémon Emerald
		// You can add more as needed through a PR, I'm too lazy to research any further
	];

	public static bool UsesWirelessAdapter( string gameCode )
	{
		if ( string.IsNullOrEmpty( gameCode ) || gameCode.Length < 3 )
			return false;

		return WirelessAdapterGameCodes.Contains( gameCode[..3] );
	}

	public static SessionMode DetectMode( string gameCode ) =>
		UsesWirelessAdapter( gameCode ) ? SessionMode.WirelessAdapter : SessionMode.LinkCable;

	public static GameEntry FindMatch( string sha1, string gameCode )
	{
		List<GameEntry> games;
		try { games = GameEntry.Discover(); }
		catch ( Exception e ) { Log.Warning( $"[sGBA] GameEntry.Discover failed: {e.Message}" ); return null; }

		if ( !string.IsNullOrEmpty( sha1 ) )
		{
			foreach ( var game in games )
			{
				if ( string.Equals( ComputeSha1( game ), sha1, StringComparison.OrdinalIgnoreCase ) )
					return game;
			}
		}

		if ( !string.IsNullOrEmpty( gameCode ) )
			return games.FirstOrDefault( game => string.Equals( game.GameCode, gameCode, StringComparison.OrdinalIgnoreCase ) );

		return null;
	}

	public static string ComputeSha1( GameEntry game )
	{
		try
		{
			var bytes = game.FileSystem.ReadAllBytes( game.Path ).ToArray();
			return Convert.ToHexString( System.Security.Cryptography.SHA1.HashData( bytes ) );
		}
		catch
		{
			return string.Empty;
		}
	}
}
