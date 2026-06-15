using Emulotl;

namespace sGBA;

public sealed class GameEntry( string path, string displayTitle, string region,
	string gameCode, string noIntroName, BaseFileSystem fileSystem, string systemId )
{
	public string Path { get; } = path;
	public string DisplayTitle { get; } = displayTitle;
	public string Region { get; } = region;
	public string GameCode { get; } = gameCode;
	public string NoIntroName { get; } = noIntroName;
	public BaseFileSystem FileSystem { get; } = fileSystem;
	public string SystemId { get; } = systemId;

	private const string RomFolder = "roms";

	public static List<GameEntry> Discover()
	{
		EmulatorSystems.EnsureRegistered();
		List<GameEntry> entries = [];
		CollectFrom( Sandbox.FileSystem.Mounted, entries );
		CollectFrom( Sandbox.FileSystem.Data, entries );
		entries.Sort( ( a, b ) => string.Compare( a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase ) );
		return entries;
	}

	public static HashSet<string> GetInstalledPaths()
	{
		EmulatorSystems.EnsureRegistered();
		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		CollectPathsFrom( Sandbox.FileSystem.Mounted, paths );
		CollectPathsFrom( Sandbox.FileSystem.Data, paths );
		return paths;
	}

	private static void CollectFrom( BaseFileSystem fileSystem, List<GameEntry> entries )
	{
		foreach ( SystemProfile profile in SystemRegistry.All )
		{
			foreach ( string extension in profile.RomExtensions )
			{
				IEnumerable<string> found;
				try { found = fileSystem.FindFile( RomFolder, $"*.{extension}" ) ?? []; }
				catch { continue; }

				foreach ( string fileName in found )
				{
					GameEntry entry = BuildEntry( fileSystem, $"{RomFolder}/{fileName}", fileName, profile );
					if ( entry is not null )
						entries.Add( entry );
				}
			}
		}
	}

	private static void CollectPathsFrom( BaseFileSystem fileSystem, HashSet<string> paths )
	{
		foreach ( SystemProfile profile in SystemRegistry.All )
		{
			foreach ( string extension in profile.RomExtensions )
			{
				try
				{
					foreach ( var fileName in fileSystem.FindFile( RomFolder, $"*.{extension}" ) ?? [] )
						paths.Add( $"{RomFolder}/{fileName}" );
				}
				catch
				{
				}
			}
		}
	}

	private static GameEntry BuildEntry( BaseFileSystem fileSystem, string fullPath, string fileName, SystemProfile profile )
	{
		string baseName = System.IO.Path.GetFileNameWithoutExtension( fileName );
		(string displayTitle, string region) = ParseNoIntroName( baseName );
		string gameCode = ReadGameCode( fileSystem, fullPath, profile );

		return new GameEntry( fullPath, displayTitle, region, gameCode, baseName, fileSystem, profile.Id );
	}

	private static (string DisplayTitle, string Region) ParseNoIntroName( string baseName )
	{
		int parenOpen = baseName.IndexOf( '(' );
		if ( parenOpen <= 0 )
			return (baseName, string.Empty);

		string displayTitle = baseName[..parenOpen].TrimEnd();
		int parenClose = baseName.IndexOf( ')', parenOpen );
		string region = parenClose > parenOpen ? baseName[(parenOpen + 1)..parenClose] : string.Empty;
		return (displayTitle, region);
	}

	private static string ReadGameCode( BaseFileSystem fileSystem, string path, SystemProfile profile )
	{
		try
		{
			using System.IO.Stream stream = fileSystem.OpenRead( path );
			int headerLength = (int)Math.Min( stream.Length, 512 );
			if ( headerLength <= 0 )
				return string.Empty;

			byte[] header = new byte[headerLength];
			stream.ReadExactly( header, 0, headerLength );

			return profile.ReadGameId( header ) ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}
}
