namespace Emulotl;

[Flags]
public enum LogLevel
{
	Fatal = 0x01,
	Error = 0x02,
	Warn = 0x04,
	Info = 0x08,
	Debug = 0x10,
	Stub = 0x20,
	GameError = 0x40,

	All = 0x7F
}

public enum LogCategory
{
	Core,
	Debug,
	Dma,
	Memory,
	Io,
	Video,
	Audio,
	Bios,
	Save,
	Sio,
	State,
	Hardware,

	Status,
	Max
}

public static class EmuLog
{
	private static readonly string[] CategoryNames =
	[
		"Core",
		"Debug",
		"DMA",
		"Memory",
		"I/O",
		"Video",
		"Audio",
		"BIOS",
		"Save",
		"SIO",
		"State",
		"Hardware",
		"Status"
	];

	private static readonly string[] CategoryIds =
	[
		"core",
		"debug",
		"dma",
		"memory",
		"io",
		"video",
		"audio",
		"bios",
		"save",
		"sio",
		"state",
		"hardware",
		"status"
	];

	private static LogLevel _defaultLevels = LogLevel.Fatal | LogLevel.Error | LogLevel.Warn | LogLevel.Debug;
	private static readonly LogLevel[] _levels = new LogLevel[(int)LogCategory.Max];
	private static Action<LogCategory, LogLevel, string> _backend;

	static EmuLog()
	{
		for ( int i = 0; i < _levels.Length; i++ )
			_levels[i] = _defaultLevels;
	}

	public static LogLevel DefaultLevels
	{
		get => _defaultLevels;
		set
		{
			_defaultLevels = value;
			for ( int i = 0; i < _levels.Length; i++ )
				_levels[i] = value;
		}
	}

	public static void SetBackend( Action<LogCategory, LogLevel, string> backend )
	{
		_backend = backend;
	}

	public static void SetCategoryLevels( LogCategory category, LogLevel levels )
	{
		_levels[(int)category] = levels;
	}

	public static void ResetCategoryLevels( LogCategory category )
	{
		_levels[(int)category] = _defaultLevels;
	}

	public static LogLevel GetCategoryLevels( LogCategory category )
	{
		return _levels[(int)category];
	}

	public static bool FilterTest( LogCategory category, LogLevel level )
	{
		return (_levels[(int)category] & level) != 0;
	}

	public static string GetCategoryName( LogCategory category )
	{
		int idx = (int)category;
		if ( idx >= 0 && idx < CategoryNames.Length )
			return CategoryNames[idx];
		return "Unknown";
	}

	public static string GetCategoryId( LogCategory category )
	{
		int idx = (int)category;
		if ( idx >= 0 && idx < CategoryIds.Length )
			return CategoryIds[idx];
		return "unknown";
	}

	public static LogCategory? CategoryById( string id )
	{
		for ( int i = 0; i < CategoryIds.Length; i++ )
		{
			if ( string.Equals( CategoryIds[i], id, StringComparison.Ordinal ) )
				return (LogCategory)i;
		}
		return null;
	}

	public static void Write( LogCategory category, LogLevel level, string message )
	{
		if ( !FilterTest( category, level ) )
			return;

		if ( _backend != null )
		{
			_backend( category, level, message );
		}
		else
		{
			DefaultLog( category, level, message );
		}
	}

	public static void Write( LogCategory category, LogLevel level, string format, object arg0 )
	{
		if ( !FilterTest( category, level ) )
			return;

		Write( category, level, string.Format( format, arg0 ) );
	}

	public static void Write( LogCategory category, LogLevel level, string format, object arg0, object arg1 )
	{
		if ( !FilterTest( category, level ) )
			return;

		Write( category, level, string.Format( format, arg0, arg1 ) );
	}

	public static void Write( LogCategory category, LogLevel level, string format, object arg0, object arg1, object arg2 )
	{
		if ( !FilterTest( category, level ) )
			return;

		Write( category, level, string.Format( format, arg0, arg1, arg2 ) );
	}

	public static void Write( LogCategory category, LogLevel level, string format, params object[] args )
	{
		if ( !FilterTest( category, level ) )
			return;

		Write( category, level, string.Format( format, args ) );
	}

	private static void DefaultLog( LogCategory category, LogLevel level, string message )
	{
		string prefix = GetCategoryName( category );
		string levelTag = level switch
		{
			LogLevel.Fatal => "FATAL",
			LogLevel.Error => "ERROR",
			LogLevel.Warn => "WARN",
			LogLevel.Info => "INFO",
			LogLevel.Debug => "DEBUG",
			LogLevel.Stub => "STUB",
			LogLevel.GameError => "GAME ERROR",
			_ => level.ToString()
		};

		Log.Info( $"[{levelTag}] {prefix}: {message}" );
	}
}
