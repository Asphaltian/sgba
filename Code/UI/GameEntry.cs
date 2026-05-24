namespace sGBA;

public sealed class GameEntry( string path, string displayTitle, string region,
	string internalTitle, string gameCode, string publisher, string noIntroName,
	BaseFileSystem fileSystem )
{
	public string Path { get; } = path;
	public string DisplayTitle { get; } = displayTitle;
	public string Region { get; } = region;
	public string InternalTitle { get; } = internalTitle;
	public string GameCode { get; } = gameCode;
	public string Publisher { get; } = publisher;
	public string NoIntroName { get; } = noIntroName;
	public BaseFileSystem FileSystem { get; } = fileSystem;
}

