using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed class NdsVideo : IVideoOutput
{
	public int Width => NdsConstants.ScreenWidth;
	public int Height => NdsConstants.ScreenHeight;
	public int GpuScale { get; private set; } = 1;
	public Texture OutputTexture { get; private set; }
	public CommandList RenderCommandList => null;
	public bool GpuReady { get; private set; }
	public bool RawPreview { get; set; }

	public void InitGpu( int scale )
	{
		GpuScale = Math.Max( 1, scale );
		OutputTexture = Texture.Create( Width, Height )
			.WithFormat( ImageFormat.RGBA8888 )
			.WithDynamicUsage()
			.Finish();
		OutputTexture.Update( (ReadOnlySpan<byte>)new byte[Width * Height * 4] );
		GpuReady = true;
	}

	public void DisposeGpu()
	{
		OutputTexture?.Dispose();
		OutputTexture = null;
		GpuReady = false;
	}

	private byte[] _frameBuffer;
	private bool _frameDirty;
	private readonly object _frameLock = new();

	public void SubmitFrame( uint[] framebuffer )
	{
		lock ( _frameLock )
		{
			int need = framebuffer.Length * 4;
			if ( _frameBuffer == null || _frameBuffer.Length != need )
				_frameBuffer = new byte[need];

			for ( int i = 0; i < framebuffer.Length; i++ )
			{
				uint p = framebuffer[i];
				int o = i << 2;
				_frameBuffer[o] = (byte)p;
				_frameBuffer[o + 1] = (byte)(p >> 8);
				_frameBuffer[o + 2] = (byte)(p >> 16);
				_frameBuffer[o + 3] = (byte)(p >> 24);
			}

			_frameDirty = true;
		}
	}

	public bool UploadAndBuildCommandList()
	{
		if ( OutputTexture == null )
			return false;

		lock ( _frameLock )
		{
			if ( !_frameDirty || _frameBuffer == null )
				return false;

			OutputTexture.Update( (ReadOnlySpan<byte>)_frameBuffer );
			_frameDirty = false;
		}

		return true;
	}

	public void PresentExternalFrame( ReadOnlySpan<byte> pixels )
	{
		int expected = Width * Height * 4;
		if ( OutputTexture != null && pixels.Length >= expected )
			OutputTexture.Update( pixels.Slice( 0, expected ) );
	}

	public byte[] CaptureScreenshot() => new byte[Width * Height * 4];

	public void CaptureScreenshotAsync( Action<byte[]> callback ) => callback?.Invoke( CaptureScreenshot() );

	public void ApplyDisplayOptions( in DisplayOptions options ) { }
}
