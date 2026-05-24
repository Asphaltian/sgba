namespace sGBA;

public static class GamePlayHistory
{
	private const string HistoryPath = "game-play-history.txt";
	private static readonly Dictionary<string, long> LastPlayedByPath = new( StringComparer.OrdinalIgnoreCase );
	private static bool _loaded;

	public static long LastPlayedAt( string path )
	{
		EnsureLoaded();
		return !string.IsNullOrWhiteSpace( path ) && LastPlayedByPath.TryGetValue( path, out long value ) ? value : 0L;
	}

	public static void MarkPlayed( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		EnsureLoaded();
		LastPlayedByPath[path] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		Save();
	}

	private static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		LastPlayedByPath.Clear();

		try
		{
			if ( !FileSystem.Data.FileExists( HistoryPath ) )
				return;

			string text = System.Text.Encoding.UTF8.GetString( FileSystem.Data.ReadAllBytes( HistoryPath ).ToArray() );
			foreach ( string line in text.Split( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries ) )
			{
				int separator = line.IndexOf( '\t' );
				if ( separator <= 0 )
					continue;

				string escapedPath = line[..separator];
				string valueText = line[(separator + 1)..];
				if ( !long.TryParse( valueText, out long value ) )
					continue;

				LastPlayedByPath[Uri.UnescapeDataString( escapedPath )] = value;
			}
		}
		catch
		{
			LastPlayedByPath.Clear();
		}
	}

	private static void Save()
	{
		try
		{
			string text = string.Join( "\n", LastPlayedByPath
				.OrderByDescending( pair => pair.Value )
				.Select( pair => $"{Uri.EscapeDataString( pair.Key )}\t{pair.Value}" ) );
			FileSystem.Data.WriteAllBytes( HistoryPath, System.Text.Encoding.UTF8.GetBytes( text ) );
		}
		catch
		{
		}
	}
}
