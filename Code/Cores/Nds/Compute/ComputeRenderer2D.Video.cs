namespace Emulotl.Nds;

public sealed partial class ComputeRenderer2D : IVideoOutput
{
	public int Width => ScreenW;
	public int Height => ScreenH;
	public int GpuScale => _scale;
	public bool RawPreview { get; set; }

	public void PresentExternalFrame( ReadOnlySpan<byte> pixels ) { }

	public byte[] CaptureScreenshot() => new byte[ScreenW * ScreenH * 4];

	public void CaptureScreenshotAsync( Action<byte[]> callback ) => callback?.Invoke( CaptureScreenshot() );

	public void ApplyDisplayOptions( in DisplayOptions options ) { }
}
