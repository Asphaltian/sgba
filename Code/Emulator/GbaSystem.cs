using System.Text;
using Emulotl;

namespace sGBA;

public static class GbaSystem
{
	public static SystemProfile Profile { get; } = Build();

	private static SystemProfile Build() => new()
	{
		Id = "gba",
		DisplayName = "Game Boy Advance",
		RomExtensions = ["gba"],
		Screens = [new ScreenSpec( GbaConstants.ScreenWidth, GbaConstants.ScreenHeight )],
		AudioSampleRate = GbaAudio.SampleRate,
		AudioChannels = 2,
		AudioSamplesPerFrame = GbaAudio.SamplesPerFrame,
		NativeFps = GbaConstants.Fps,
		ClockRate = GbaConstants.Arm7TdmiFrequency,
		CyclesPerFrame = GbaConstants.VideoTotalLength,
		Buttons =
		[
			new InputButton( "A", "GBA_A" ),
			new InputButton( "B", "GBA_B" ),
			new InputButton( "Select", "GBA_Select" ),
			new InputButton( "Start", "GBA_Start" ),
			new InputButton( "Right", "GBA_Right", DpadDirection.Right ),
			new InputButton( "Left", "GBA_Left", DpadDirection.Left ),
			new InputButton( "Up", "GBA_Up", DpadDirection.Up ),
			new InputButton( "Down", "GBA_Down", DpadDirection.Down ),
			new InputButton( "R", "GBA_R" ),
			new InputButton( "L", "GBA_L" ),
		],
		StateSlotCount = GbaSerialize.SlotCount,
		LibretroPlatform = "Nintendo - Game Boy Advance",
		ReadGameId = ReadGameCode,
		CreateCore = () => new Gba(),
	};

	public static string ReadGameCode( byte[] romData )
	{
		if ( romData == null || romData.Length < 0xB0 )
			return null;
		return Encoding.ASCII.GetString( romData, 0xAC, 4 ).TrimEnd( '\0' );
	}
}
