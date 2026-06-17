using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed class NdsScreen : IVideoOutput
{
	private readonly ComputeRenderer2D _engine;
	private readonly ComputeRenderer3D _d3;

	public NdsScreen( ComputeRenderer2D engine, ComputeRenderer3D d3 )
	{
		_engine = engine;
		_d3 = d3;
	}

	public int Width => 256;
	public int Height => 192;
	public int GpuScale => _engine.GpuScale;
	public Texture OutputTexture => _engine.OutputTexture;
	public CommandList RenderCommandList => _engine.RenderCommandList;
	public bool GpuReady => _engine.GpuReady;
	public bool RawPreview { get; set; }

	public void InitGpu( int scale )
	{
		if ( _d3 != null && !_d3.GpuReady ) _d3.InitGpu( scale );
		if ( !_engine.GpuReady ) _engine.InitGpu( scale );
	}

	public void DisposeGpu()
	{
		if ( _engine.GpuReady ) _engine.DisposeGpu();
		if ( _d3 != null && _d3.GpuReady ) _d3.DisposeGpu();
	}

	public bool UploadAndBuildCommandList() => _engine.UploadAndBuildCommandList();

	public void PresentExternalFrame( ReadOnlySpan<byte> pixels ) { }
	public byte[] CaptureScreenshot() => new byte[Width * Height * 4];
	public void CaptureScreenshotAsync( Action<byte[]> callback ) => callback?.Invoke( CaptureScreenshot() );
	public void ApplyDisplayOptions( in DisplayOptions options ) { }
}
