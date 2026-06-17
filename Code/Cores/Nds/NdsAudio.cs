namespace Emulotl.Nds;

public sealed class NdsAudio : IAudioOutput
{
	public int SampleRate => NdsConstants.AudioSampleRate;
	public int Channels => 2;
	public int SamplesPerFrame => NdsConstants.AudioSamplesPerFrame;
	public short[] OutputBuffer { get; } = new short[NdsConstants.AudioSamplesPerFrame * 2];
	public int SamplesWritten { get; set; }

	public void BeginFrame() => SamplesWritten = 0;

	public void PushSample( short left, short right )
	{
		if ( SamplesWritten >= SamplesPerFrame )
			return;

		int i = SamplesWritten * 2;
		OutputBuffer[i] = left;
		OutputBuffer[i + 1] = right;
		SamplesWritten++;
	}

	public void EndFrame() { }
}
