namespace Emulotl;

public interface IAudioOutput
{
	int SampleRate { get; }
	int Channels { get; }
	int SamplesPerFrame { get; }
	short[] OutputBuffer { get; }
	int SamplesWritten { get; set; }
}
