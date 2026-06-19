namespace Emulotl.Nds;

public sealed partial class ComputeRenderer3D
{
	private int _numYSpans;
	private int _numSetupIndices;
	private readonly int[,] _scaledPositions = new int[10, 2];

	public int NumYSpans => _numYSpans;
	public int NumSetupIndices => _numSetupIndices;

	private void BuildSpanSetups()
	{
		_numYSpans = 0;
		_numSetupIndices = 0;
		_ySpanIndices.Clear();
		ResetVariants();
		BeginTextureFrame();

		bool enableTex = (_gpu.RenderDispCnt & 1) != 0;

		int screenWidth = 256 * _scaleFactor;
		int screenHeight = 192 * _scaleFactor;

		for ( int i = 0; i < _gpu.RenderNumPolygons; i++ )
		{
			Polygon polygon = _gpu.RenderPolygonRAM[i];

			int nverts = (int)polygon.NumVertices;
			uint vtop = polygon.VTop, vbot = polygon.VBottom;

			uint curVL = vtop, curVR = vtop;
			uint nextVL, nextVR;

			_renderPolygons[i].FirstXSpan = (uint)_numSetupIndices;
			_renderPolygons[i].Attr = polygon.Attr;
			_renderPolygons[i].TexParam = polygon.TexParam;
			AssignVariant( polygon, i, enableTex );

			if ( polygon.FacingView )
			{
				nextVL = curVL + 1;
				if ( nextVL >= nverts ) nextVL = 0;
				nextVR = curVR - 1;
				if ( (int)nextVR < 0 ) nextVR = (uint)(nverts - 1);
			}
			else
			{
				nextVL = curVL - 1;
				if ( (int)nextVL < 0 ) nextVL = (uint)(nverts - 1);
				nextVR = curVR + 1;
				if ( nextVR >= nverts ) nextVR = 0;
			}

			int ytop = screenHeight, ybot = 0;
			for ( int v = 0; v < nverts; v++ )
			{
				ref Vertex vtx = ref _gpu.VertexRAM[polygon.Vertices[v]];
				if ( _hiresCoordinates )
				{
					_scaledPositions[v, 0] = (vtx.Hx * _scaleFactor) >> 4;
					_scaledPositions[v, 1] = (vtx.Hy * _scaleFactor) >> 4;
				}
				else
				{
					_scaledPositions[v, 0] = vtx.Fx * _scaleFactor;
					_scaledPositions[v, 1] = vtx.Fy * _scaleFactor;
				}
				ytop = Math.Min( _scaledPositions[v, 1], ytop );
				ybot = Math.Max( _scaledPositions[v, 1], ybot );
			}
			_renderPolygons[i].YTop = ytop;
			_renderPolygons[i].YBot = ybot;
			_renderPolygons[i].XMin = screenWidth;
			_renderPolygons[i].XMax = 0;

			if ( ybot == ytop )
			{
				int dtop = 0, dbot = 0;

				_renderPolygons[i].YBot++;

				int j = 1;
				if ( _scaledPositions[j, 0] < _scaledPositions[dtop, 0] ) dtop = j;
				if ( _scaledPositions[j, 0] > _scaledPositions[dbot, 0] ) dbot = j;

				j = nverts - 1;
				if ( _scaledPositions[j, 0] < _scaledPositions[dtop, 0] ) dtop = j;
				if ( _scaledPositions[j, 0] > _scaledPositions[dbot, 0] ) dbot = j;

				int curSpanL = _numYSpans;
				SetupYSpanDummy( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, dtop, 0, _scaledPositions );
				int curSpanR = _numYSpans;
				SetupYSpanDummy( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, dbot, 1, _scaledPositions );

				_ySpanIndices.Add( new SetupIndices
				{
					PolyIdx = (ushort)i,
					SpanIdxL = (ushort)curSpanL,
					SpanIdxR = (ushort)curSpanR,
					Y = (ushort)ytop
				} );
				_numSetupIndices++;
			}
			else
			{
				int curSpanL = _numYSpans;
				SetupYSpan( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, (int)curVL, (int)nextVL, 0, _scaledPositions );
				int curSpanR = _numYSpans;
				SetupYSpan( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, (int)curVR, (int)nextVR, 1, _scaledPositions );

				for ( int y = ytop; y < ybot; y++ )
				{
					if ( y >= _scaledPositions[nextVL, 1] && curVL != polygon.VBottom )
					{
						while ( y >= _scaledPositions[nextVL, 1] && curVL != polygon.VBottom )
						{
							curVL = nextVL;
							if ( polygon.FacingView )
							{
								nextVL = curVL + 1;
								if ( nextVL >= nverts ) nextVL = 0;
							}
							else
							{
								nextVL = curVL - 1;
								if ( (int)nextVL < 0 ) nextVL = (uint)(nverts - 1);
							}
						}

						curSpanL = _numYSpans;
						SetupYSpan( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, (int)curVL, (int)nextVL, 0, _scaledPositions );
					}
					if ( y >= _scaledPositions[nextVR, 1] && curVR != polygon.VBottom )
					{
						while ( y >= _scaledPositions[nextVR, 1] && curVR != polygon.VBottom )
						{
							curVR = nextVR;
							if ( polygon.FacingView )
							{
								nextVR = curVR - 1;
								if ( (int)nextVR < 0 ) nextVR = (uint)(nverts - 1);
							}
							else
							{
								nextVR = curVR + 1;
								if ( nextVR >= nverts ) nextVR = 0;
							}
						}

						curSpanR = _numYSpans;
						SetupYSpan( ref _renderPolygons[i], ref _ySpanSetups[_numYSpans++], polygon, (int)curVR, (int)nextVR, 1, _scaledPositions );
					}

					_ySpanIndices.Add( new SetupIndices
					{
						PolyIdx = (ushort)i,
						SpanIdxL = (ushort)curSpanL,
						SpanIdxR = (ushort)curSpanR,
						Y = (ushort)y
					} );
					_numSetupIndices++;
				}
			}
		}
	}
}
