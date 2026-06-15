using System;
using Sandbox;
using Sandbox.Rendering;

namespace Emulotl;

public readonly struct DisplayOptions
{
	public readonly bool ReproduceClassicFeel;
	public readonly bool SmallScreen;

	public DisplayOptions( bool reproduceClassicFeel, bool smallScreen )
	{
		ReproduceClassicFeel = reproduceClassicFeel;
		SmallScreen = smallScreen;
	}
}

public interface IVideoOutput
{
	int Width { get; }
	int Height { get; }
	int GpuScale { get; }
	Texture OutputTexture { get; }
	CommandList RenderCommandList { get; }
	bool GpuReady { get; }
	bool RawPreview { get; set; }

	void InitGpu( int scale );
	void DisposeGpu();
	bool UploadAndBuildCommandList();
	void PresentExternalFrame( ReadOnlySpan<byte> pixels );
	byte[] CaptureScreenshot();
	void CaptureScreenshotAsync( Action<byte[]> callback );
	void ApplyDisplayOptions( in DisplayOptions options );
}
