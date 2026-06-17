using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D : IVideoOutput
{
	private CommandList _renderCmd;

	public int Width => 256;
	public int Height => 192;
	public int GpuScale => _scaleFactor;
	public Texture OutputTexture => _outputTex;
	public CommandList RenderCommandList => _renderCmd;
	public bool GpuReady => _gpuReady;
	public bool RawPreview { get; set; }

	public void InitGpu( int scale )
	{
		DisposeGpu();
		SetRenderSettings( scale );
		InitGpuResources();
		_renderCmd = new CommandList( "NDS3D" );
	}

	public void DisposeGpu()
	{
		_gpuReady = false;

		for ( int i = 0; i < BufRing; i++ )
		{
			_metaBufferR[i]?.Dispose();
			_renderPolygonBufferR[i]?.Dispose();
			_ySpanSetupBufferR[i]?.Dispose();
			_ySpanIndicesBufferR[i]?.Dispose();
			_metaBufferR[i] = null;
			_renderPolygonBufferR[i] = null;
			_ySpanSetupBufferR[i] = null;
			_ySpanIndicesBufferR[i] = null;
		}
		_xSpanSetupBuffer?.Dispose();
		_binResultBuffer?.Dispose();
		_workDescBuffer?.Dispose();
		for ( int i = 0; i < 3; i++ )
			_tileMemory[i]?.Dispose();
		_finalTileBuffer?.Dispose();
		_indirectArgs?.Dispose();
		_outputTex?.Dispose();
		for ( int i = 0; i < BufRing; i++ )
		{
			_texBufSmallR[i]?.Dispose();
			_texBufLargeR[i]?.Dispose();
			_texBufSmallR[i] = null;
			_texBufLargeR[i] = null;
		}

		_metaBuffer = null;
		_renderPolygonBuffer = null;
		_ySpanSetupBuffer = null;
		_xSpanSetupBuffer = null;
		_ySpanIndicesBuffer = null;
		_binResultBuffer = null;
		_workDescBuffer = null;
		_finalTileBuffer = null;
		_indirectArgs = null;
		_outputTex = null;
		_texBufSmall = null;
		_texBufLarge = null;
		_renderCmd = null;
	}

	public bool UploadAndBuildCommandList()
	{
		if ( !_gpuReady || _renderCmd == null )
			return false;

		return BuildCommandList( _renderCmd );
	}

	public void PresentExternalFrame( ReadOnlySpan<byte> pixels ) { }

	public byte[] CaptureScreenshot() => new byte[Width * Height * 4];

	public void CaptureScreenshotAsync( Action<byte[]> callback ) => callback?.Invoke( CaptureScreenshot() );

	public void ApplyDisplayOptions( in DisplayOptions options ) { }
}
