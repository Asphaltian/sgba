namespace Emulotl.Nds;

public enum LogLevel
{
	Debug,
	Info,
	Warn,
	Error,
}

public static class Platform
{
	private static readonly Sandbox.Diagnostics.Logger Logger = new( "NDS" );

	public static void Log( LogLevel level, string message )
	{
		switch ( level )
		{
			case LogLevel.Error:
				Logger.Error( message );
				break;
			case LogLevel.Warn:
				Logger.Warning( message );
				break;
			default:
				Logger.Info( message );
				break;
		}
	}
}
