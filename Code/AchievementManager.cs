namespace Emulotl;

public static class AchievementManager
{
	private const string GamesLaunchedStat = "games_launched";
	private const string SameGameLaunchesStat = "same_game_launches";

	private const string JamSession = "jam_session";
	private const string Bedtime = "bedtime";
	private const string OneMoreLevel = "one_more_level";

	private const float MarathonSessionSeconds = 4 * 60 * 60;

	private static bool _marathonReported;

	public static void OnGameLaunched( string path )
	{
		Sandbox.Services.Stats.Increment( GamesLaunchedStat, 1 );
		Sandbox.Services.Stats.SetValue( SameGameLaunchesStat, GamePlayHistory.PlayCount( path ) );

		if ( AllBundledGamesPlayed() )
			Sandbox.Services.Achievements.Unlock( JamSession );

		if ( DateTime.Now.Hour is >= 2 and < 5 )
			Sandbox.Services.Achievements.Unlock( Bedtime );
	}

	public static void OnSessionStarted()
	{
		_marathonReported = false;
	}

	public static void OnSessionTime( float seconds )
	{
		if ( _marathonReported || seconds < MarathonSessionSeconds )
			return;

		_marathonReported = true;
		Sandbox.Services.Achievements.Unlock( OneMoreLevel );
	}

	private static bool AllBundledGamesPlayed()
	{
		EmulatorSystems.EnsureRegistered();

		bool any = false;
		foreach ( SystemProfile profile in SystemRegistry.All )
		{
			foreach ( string extension in profile.RomExtensions )
			{
				IEnumerable<string> bundled;
				try { bundled = FileSystem.Mounted.FindFile( "roms", $"*.{extension}" ) ?? []; }
				catch { continue; }

				foreach ( string fileName in bundled )
				{
					any = true;
					if ( !GamePlayHistory.HasPlayed( $"roms/{fileName}" ) )
						return false;
				}
			}
		}

		return any;
	}
}
