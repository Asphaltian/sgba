namespace sGBA;

public static class GamePlayHistory
{
	private const string LastPlayedCookieKey = "sgba.game_play_history";
	private const string PlayCountsCookieKey = "sgba.game_play_counts";
	private static readonly Dictionary<string, long> LastPlayedByPath = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, int> PlayCountByPath = new( StringComparer.OrdinalIgnoreCase );
	private static bool _loaded;

	public static long LastPlayedAt( string path )
	{
		EnsureLoaded();
		return !string.IsNullOrWhiteSpace( path ) && LastPlayedByPath.TryGetValue( path, out long value ) ? value : 0L;
	}

	public static bool HasPlayed( string path ) => LastPlayedAt( path ) > 0;

	public static int PlayCount( string path )
	{
		EnsureLoaded();
		return !string.IsNullOrWhiteSpace( path ) && PlayCountByPath.TryGetValue( path, out int value ) ? value : 0;
	}

	public static void MarkPlayed( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		EnsureLoaded();
		LastPlayedByPath[path] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		PlayCountByPath[path] = PlayCountByPath.GetValueOrDefault( path ) + 1;
		Save();
	}

	private static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		LoadInto( LastPlayedCookieKey, LastPlayedByPath );
		LoadInto( PlayCountsCookieKey, PlayCountByPath );
	}

	private static void LoadInto<T>( string cookieKey, Dictionary<string, T> target )
	{
		target.Clear();

		var saved = Game.Cookies?.Get<Dictionary<string, T>>( cookieKey, null );
		if ( saved is null )
			return;

		foreach ( var (path, value) in saved )
		{
			if ( !string.IsNullOrWhiteSpace( path ) )
				target[path] = value;
		}
	}

	private static void Save()
	{
		Game.Cookies?.Set( LastPlayedCookieKey, LastPlayedByPath );
		Game.Cookies?.Set( PlayCountsCookieKey, PlayCountByPath );
	}
}
