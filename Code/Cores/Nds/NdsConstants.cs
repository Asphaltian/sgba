namespace Emulotl.Nds;

public static class NdsConstants
{
	public const int ScreenWidth = 256;
	public const int ScreenHeight = 192;
	public const int ScreenCount = 2;

	public const int Arm7ClockHz = 33513982;
	public const int Arm9ClockHz = Arm7ClockHz * 2;

	public const int DotsPerLine = 355;
	public const int CyclesPerDot = 6;
	public const int CyclesPerLine = DotsPerLine * CyclesPerDot;
	public const int LinesPerFrame = 263;
	public const int VisibleLines = 192;
	public const int CyclesPerFrame = CyclesPerLine * LinesPerFrame;
	public const double Fps = (double)Arm7ClockHz / CyclesPerFrame;

	public const int AudioCyclesPerSample = 1024;
	public const int AudioSampleRate = Arm7ClockHz / AudioCyclesPerSample;
	public const int AudioSamplesPerFrame = (CyclesPerFrame + AudioCyclesPerSample - 1) / AudioCyclesPerSample;

	public const int MainRamSize = 4 * 1024 * 1024;
	public const int SharedWramSize = 32 * 1024;
	public const int Arm7WramSize = 64 * 1024;
	public const int Arm9ItcmSize = 32 * 1024;
	public const int Arm9DtcmSize = 16 * 1024;
	public const int Arm7BiosSize = 16 * 1024;
	public const int Arm9BiosSize = 32 * 1024;

	public const int VramSize = 656 * 1024;
	public const int PaletteSize = 2 * 1024;
	public const int OamSize = 2 * 1024;

	public const int HeaderGameCodeOffset = 0x0C;
	public const int HeaderSize = 0x200;
}
