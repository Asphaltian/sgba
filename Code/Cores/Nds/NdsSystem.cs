using System.Text;

namespace Emulotl.Nds;

public static class NdsSystem
{
	public static SystemProfile Profile { get; } = Build();

	private static SystemProfile Build() => new()
	{
		Id = "nds",
		DisplayName = "Nintendo DS",
		RomExtensions = ["nds"],
		Screens =
		[
			new ScreenSpec( NdsConstants.ScreenWidth, NdsConstants.ScreenHeight ),
			new ScreenSpec( NdsConstants.ScreenWidth, NdsConstants.ScreenHeight ),
		],
		AudioSampleRate = NdsConstants.AudioSampleRate,
		AudioChannels = 2,
		AudioSamplesPerFrame = NdsConstants.AudioSamplesPerFrame,
		NativeFps = NdsConstants.Fps,
		ClockRate = NdsConstants.Arm7ClockHz,
		CyclesPerFrame = NdsConstants.CyclesPerFrame,
		Buttons =
		[
			new InputButton( "A", "Nds_A" ),
			new InputButton( "B", "Nds_B" ),
			new InputButton( "Select", "Nds_Select" ),
			new InputButton( "Start", "Nds_Start" ),
			new InputButton( "Right", "Nds_Right", DpadDirection.Right ),
			new InputButton( "Left", "Nds_Left", DpadDirection.Left ),
			new InputButton( "Up", "Nds_Up", DpadDirection.Up ),
			new InputButton( "Down", "Nds_Down", DpadDirection.Down ),
			new InputButton( "R", "Nds_R" ),
			new InputButton( "L", "Nds_L" ),
			new InputButton( "X", "Nds_X" ),
			new InputButton( "Y", "Nds_Y" ),
		],
		HasTouchscreen = true,
		StateSlotCount = 4,
		LibretroPlatform = "Nintendo - Nintendo DS",
		ReadGameCode = ReadGameCode,
		CreateCore = () => new NDS(),
	};

	public static string ReadGameCode( byte[] romData )
	{
		if ( romData == null || romData.Length < NdsConstants.HeaderGameCodeOffset + 4 )
			return null;
		return Encoding.ASCII.GetString( romData, NdsConstants.HeaderGameCodeOffset, 4 ).TrimEnd( '\0' );
	}
}
