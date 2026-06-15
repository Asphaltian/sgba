namespace Emulotl;

public static class SystemRegistry
{
	private static readonly Dictionary<string, SystemProfile> _byId = new( StringComparer.OrdinalIgnoreCase );

	public static IReadOnlyCollection<SystemProfile> All => _byId.Values.ToList();

	public static void Register( SystemProfile profile )
	{
		ArgumentNullException.ThrowIfNull( profile );
		_byId[profile.Id] = profile;
	}

	public static bool IsRegistered( string id ) => id != null && _byId.ContainsKey( id );

	public static SystemProfile ById( string id )
	{
		if ( id == null )
			return null;
		_byId.TryGetValue( id, out SystemProfile profile );
		return profile;
	}

	public static SystemProfile ResolveByPath( string romPath )
	{
		if ( string.IsNullOrEmpty( romPath ) )
			return null;

		string ext = System.IO.Path.GetExtension( romPath ).TrimStart( '.' );
		if ( string.IsNullOrEmpty( ext ) )
			return null;

		foreach ( SystemProfile profile in _byId.Values )
		{
			foreach ( string candidate in profile.RomExtensions )
			{
				if ( string.Equals( candidate, ext, StringComparison.OrdinalIgnoreCase ) )
					return profile;
			}
		}

		return null;
	}
}
