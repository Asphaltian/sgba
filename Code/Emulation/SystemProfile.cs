namespace Emulotl;

public readonly struct ScreenSpec
{
	public readonly int Width;
	public readonly int Height;

	public ScreenSpec( int width, int height )
	{
		Width = width;
		Height = height;
	}
}

public enum DpadDirection
{
	None,
	Up,
	Down,
	Left,
	Right,
}

public readonly struct InputButton
{
	public readonly string Name;
	public readonly string ActionName;
	public readonly DpadDirection Dpad;

	public InputButton( string name, string actionName, DpadDirection dpad = DpadDirection.None )
	{
		Name = name;
		ActionName = actionName;
		Dpad = dpad;
	}
}

public sealed class SystemProfile
{
	public required string Id { get; init; }
	public required string DisplayName { get; init; }
	public required string[] RomExtensions { get; init; }
	public required ScreenSpec[] Screens { get; init; }
	public required int AudioSampleRate { get; init; }
	public int AudioChannels { get; init; } = 2;
	public required int AudioSamplesPerFrame { get; init; }
	public required double NativeFps { get; init; }
	public required int ClockRate { get; init; }
	public required int CyclesPerFrame { get; init; }
	public required InputButton[] Buttons { get; init; }
	public bool HasTouchscreen { get; init; }
	public int StateSlotCount { get; init; } = 4;
	public required string LibretroPlatform { get; init; }
	public required Func<byte[], string> ReadGameCode { get; init; }
	public required Func<IEmulatorCore> CreateCore { get; init; }
}
