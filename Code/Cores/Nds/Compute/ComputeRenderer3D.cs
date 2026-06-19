namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private const int MaxYSpanSetups = 6144 * 2;

	private readonly GPU3D _gpu;
	private readonly GPU _gpu2d;

	private readonly List<SetupIndices> _ySpanIndices = [];
	private readonly SpanSetupY[] _ySpanSetups = new SpanSetupY[MaxYSpanSetups];
	private readonly RenderPolygonGpu[] _renderPolygons = new RenderPolygonGpu[2048];

	private int _scaleFactor = 1;
	private bool _hiresCoordinates = true;

	public ComputeRenderer3D( GPU3D gpu, GPU gpu2d )
	{
		_gpu = gpu;
		_gpu2d = gpu2d;
	}

	public void SetRenderSettings( int scale, bool hiresCoordinates = true )
	{
		_scaleFactor = Math.Max( 1, scale );
		_hiresCoordinates = hiresCoordinates;
	}
}
