namespace Emulotl.Nds;

public sealed partial class GPU3D
{
	public int[] ProjMatrix = new int[16];
	public int[] PosMatrix = new int[16];
	public int[] VecMatrix = new int[16];
	public int[] TexMatrix = new int[16];
	public int[] ClipMatrix = new int[16];
	public bool ClipMatrixDirty;

	public uint[] Viewport = new uint[6];

	public int[] ProjMatrixStack = new int[16];
	public int[,] PosMatrixStack = new int[32, 16];
	public int[,] VecMatrixStack = new int[32, 16];
	public int[] TexMatrixStack = new int[16];

	public byte AlphaRefVal, AlphaRef;

	public ushort[] ToonTable = new ushort[32];
	public ushort[] EdgeTable = new ushort[8];

	public uint FogColor, FogOffset;
	public byte[] FogDensityTable = new byte[32];

	public uint ClearAttr1, ClearAttr2;

	public uint RenderDispCnt;
	public byte RenderAlphaRef;
	public ushort[] RenderToonTable = new ushort[32];
	public ushort[] RenderEdgeTable = new ushort[8];
	public uint RenderFogColor, RenderFogOffset, RenderFogShift;
	public byte[] RenderFogDensityTable = new byte[34];
	public uint RenderClearAttr1, RenderClearAttr2;
	public bool RenderFrameIdentical;
	public ushort RenderXPos;
	public bool AbortFrame;

	public uint PolygonMode;
	public short[] CurVertex = new short[3];
	public byte[] VertexColor = new byte[3];
	public short[] TexCoords = new short[2];
	public short[] RawTexCoords = new short[2];
	public short[] Normal = new short[3];

	public short[,] LightDirection = new short[4, 3];
	public int[] SpecRecip = new int[4];
	public byte[,] LightColor = new byte[4, 3];
	public byte[] MatDiffuse = new byte[3];
	public byte[] MatAmbient = new byte[3];
	public byte[] MatSpecular = new byte[3];
	public byte[] MatEmission = new byte[3];

	public bool UseShininessTable;
	public byte[] ShininessTable = new byte[128];

	public uint PolygonAttr, CurPolygonAttr;
	public uint TexParam, TexPalette;

	public Vertex[] TempVertexBuffer = new Vertex[4];
	public uint VertexNum, VertexNumInPoly, NumConsecutivePolygons;
	public int LastStripPolygon = -1;
	public uint NumOpaquePolygons;

	public Vertex[] VertexRAM = new Vertex[6144 * 2];
	public Polygon[] PolygonRAM = new Polygon[2048 * 2];

	public int CurVertexBase, CurPolygonBase;
	public uint CurRAMBank;

	public Polygon[] RenderPolygonRAM = new Polygon[2048];
	public uint RenderNumPolygons;

	private readonly Vertex[] _clipTemp = new Vertex[10];
	private readonly Vertex[] _clippedVertices = new Vertex[10];

	private void NextVertexSlot()
	{
		int num = 9 - VertexSlotCounter + 1;

		for (; ; )
		{
			CycleCount += num;

			if ( VertexPipeline > 0 )
			{
				if ( VertexPipeline > num ) VertexPipeline -= num;
				else VertexPipeline = 0;
			}

			if ( PolygonPipeline > 0 )
			{
				if ( PolygonPipeline > num )
				{
					PolygonPipeline -= num;
					VertexSlotCounter = 1;
					VertexSlotsFree >>= 1;
					if ( (VertexSlotsFree & 0x1) != 0 )
					{
						VertexSlotsFree &= ~0x1u;
						break;
					}
					else
					{
						num = 9;
						continue;
					}
				}
				else
				{
					PolygonPipeline = 0;
					VertexSlotCounter = 0;
					VertexSlotsFree = 1;
					break;
				}
			}
			else
			{
				break;
			}
		}
	}

	private void StallPolygonPipeline( int delay, int nonstalldelay )
	{
		if ( PolygonPipeline > 0 )
		{
			CycleCount += PolygonPipeline + delay;

			VertexPipeline = 0;
			NormalPipeline = 0;

			PolygonPipeline = 0;
			VertexSlotCounter = 0;
			VertexSlotsFree = 1;
		}
		else
		{
			if ( VertexPipeline > nonstalldelay )
				AddCycles( VertexPipeline - nonstalldelay + 1 );
			else
				AddCycles( NormalPipeline + 1 );
		}
	}

	private void VertexPipelineSubmitCmd()
	{
		if ( (VertexSlotsFree & 0x1) == 0 ) NextVertexSlot();
		else AddCycles( 1 );
		NormalPipeline = 0;
	}

	private void VertexPipelineCmdDelayed6()
	{
		if ( VertexPipeline > 2 ) AddCycles( VertexPipeline - 2 + 1 );
		else AddCycles( NormalPipeline + 1 );
		NormalPipeline = 0;
	}

	private void VertexPipelineCmdDelayed8()
	{
		if ( VertexPipeline > 0 ) AddCycles( VertexPipeline + 1 );
		else AddCycles( NormalPipeline + 1 );
		NormalPipeline = 0;
	}

	private void VertexPipelineCmdDelayed4()
	{
		AddCycles( NormalPipeline + 1 );
		NormalPipeline = 0;
	}

	private static void ClipSegment( ref Vertex outv, in Vertex vin, in Vertex vout, int comp, int plane, bool attribs )
	{
		long factor_num = vin.Pw - (long)plane * vin.GetPos( comp );
		int factor_den = (int)(factor_num - (vout.Pw - (long)plane * vout.GetPos( comp )));

		if ( comp != 0 ) outv.Px = (int)(vin.Px + (vout.Px - (long)vin.Px) * factor_num / factor_den);
		if ( comp != 1 ) outv.Py = (int)(vin.Py + (vout.Py - (long)vin.Py) * factor_num / factor_den);
		if ( comp != 2 ) outv.Pz = (int)(vin.Pz + (vout.Pz - (long)vin.Pz) * factor_num / factor_den);
		outv.Pw = (int)(vin.Pw + (vout.Pw - (long)vin.Pw) * factor_num / factor_den);
		outv.SetPos( comp, plane * outv.Pw );

		if ( attribs )
		{
			outv.Cr = (int)(vin.Cr + (vout.Cr - (long)vin.Cr) * factor_num / factor_den);
			outv.Cg = (int)(vin.Cg + (vout.Cg - (long)vin.Cg) * factor_num / factor_den);
			outv.Cb = (int)(vin.Cb + (vout.Cb - (long)vin.Cb) * factor_num / factor_den);
			outv.Tu = (short)(vin.Tu + (vout.Tu - (long)vin.Tu) * factor_num / factor_den);
			outv.Tv = (short)(vin.Tv + (vout.Tv - (long)vin.Tv) * factor_num / factor_den);
		}

		outv.Clipped = true;
	}

	private int ClipAgainstPlane( Vertex[] vertices, int nverts, int clipstart, int comp, bool attribs )
	{
		Vertex[] temp = _clipTemp;
		int prev, next;
		int c = clipstart;

		if ( clipstart == 2 )
		{
			temp[0] = vertices[0];
			temp[1] = vertices[1];
		}

		for ( int i = clipstart; i < nverts; i++ )
		{
			prev = i - 1; if ( prev < 0 ) prev = nverts - 1;
			next = i + 1; if ( next >= nverts ) next = 0;

			Vertex vtx = vertices[i];
			if ( vtx.GetPos( comp ) > vtx.Pw )
			{
				if ( (comp == 2) && ((CurPolygonAttr & (1 << 12)) == 0) ) return 0;

				Vertex vprev = vertices[prev];
				if ( vprev.GetPos( comp ) <= vprev.Pw )
				{
					ClipSegment( ref temp[c], vtx, vprev, comp, 1, attribs );
					c++;
				}

				Vertex vnext = vertices[next];
				if ( vnext.GetPos( comp ) <= vnext.Pw )
				{
					ClipSegment( ref temp[c], vtx, vnext, comp, 1, attribs );
					c++;
				}
			}
			else
				temp[c++] = vtx;
		}

		nverts = c; c = clipstart;
		for ( int i = clipstart; i < nverts; i++ )
		{
			prev = i - 1; if ( prev < 0 ) prev = nverts - 1;
			next = i + 1; if ( next >= nverts ) next = 0;

			Vertex vtx = temp[i];
			if ( vtx.GetPos( comp ) < -vtx.Pw )
			{
				Vertex vprev = temp[prev];
				if ( vprev.GetPos( comp ) >= -vprev.Pw )
				{
					ClipSegment( ref vertices[c], vtx, vprev, comp, -1, attribs );
					c++;
				}

				Vertex vnext = temp[next];
				if ( vnext.GetPos( comp ) >= -vnext.Pw )
				{
					ClipSegment( ref vertices[c], vtx, vnext, comp, -1, attribs );
					c++;
				}
			}
			else
				vertices[c++] = vtx;
		}

		for ( int i = 0; i < c; i++ )
		{
			vertices[i].Cr = (vertices[i].Cr & ~0xFFF) + 0xFFF;
			vertices[i].Cg = (vertices[i].Cg & ~0xFFF) + 0xFFF;
			vertices[i].Cb = (vertices[i].Cb & ~0xFFF) + 0xFFF;
		}

		return c;
	}

	private int ClipPolygon( Vertex[] vertices, int nverts, int clipstart, bool attribs )
	{
		nverts = ClipAgainstPlane( vertices, nverts, clipstart, 2, attribs );
		nverts = ClipAgainstPlane( vertices, nverts, clipstart, 1, attribs );
		nverts = ClipAgainstPlane( vertices, nverts, clipstart, 0, attribs );
		return nverts;
	}

	private static bool ClipCoordsEqual( in Vertex a, in Vertex b )
	{
		return a.Px == b.Px && a.Py == b.Py && a.Pz == b.Pz && a.Pw == b.Pw;
	}

	private void SubmitPolygon()
	{
		Vertex[] clippedvertices = _clippedVertices;
		int reused0 = -1, reused1 = -1;
		int clipstart = 0;
		int lastpolyverts = 0;

		int nverts = (PolygonMode & 0x1) != 0 ? 4 : 3;

		PolygonPipeline = 8;
		VertexSlotCounter = 1;
		VertexSlotsFree = 0b11110;

		Vertex v0 = TempVertexBuffer[0];
		Vertex v1 = TempVertexBuffer[1];
		Vertex v2 = TempVertexBuffer[2];

		long normalX = ((long)(v0.Py - v1.Py) * (v2.Pw - v1.Pw)) - ((long)(v0.Pw - v1.Pw) * (v2.Py - v1.Py));
		long normalY = ((long)(v0.Pw - v1.Pw) * (v2.Px - v1.Px)) - ((long)(v0.Px - v1.Px) * (v2.Pw - v1.Pw));
		long normalZ = ((long)(v0.Px - v1.Px) * (v2.Py - v1.Py)) - ((long)(v0.Py - v1.Py) * (v2.Px - v1.Px));

		while ( (((normalX >> 31) ^ (normalX >> 63)) != 0) ||
				(((normalY >> 31) ^ (normalY >> 63)) != 0) ||
				(((normalZ >> 31) ^ (normalZ >> 63)) != 0) )
		{
			normalX >>= 4;
			normalY >>= 4;
			normalZ >>= 4;
		}

		long dot = ((long)v1.Px * normalX) + ((long)v1.Py * normalY) + ((long)v1.Pw * normalZ);

		bool facingview = dot <= 0;

		if ( dot < 0 )
		{
			if ( (CurPolygonAttr & (1 << 7)) == 0 )
			{
				LastStripPolygon = -1;
				return;
			}
		}
		else if ( dot > 0 )
		{
			if ( (CurPolygonAttr & (1 << 6)) == 0 )
			{
				LastStripPolygon = -1;
				return;
			}
		}

		if ( PolygonMode >= 2 && LastStripPolygon >= 0 )
		{
			int id0, id1;
			if ( PolygonMode == 2 )
			{
				if ( (NumConsecutivePolygons & 1) != 0 ) { id0 = 2; id1 = 1; }
				else { id0 = 0; id1 = 2; }
				lastpolyverts = 3;
			}
			else
			{
				id0 = 3; id1 = 2;
				lastpolyverts = 4;
			}

			Polygon lsp = PolygonRAM[LastStripPolygon];
			if ( lsp.NumVertices == lastpolyverts &&
				!VertexRAM[lsp.Vertices[id0]].Clipped &&
				!VertexRAM[lsp.Vertices[id1]].Clipped )
			{
				reused0 = lsp.Vertices[id0];
				reused1 = lsp.Vertices[id1];

				clippedvertices[0] = VertexRAM[reused0];
				clippedvertices[1] = VertexRAM[reused1];

				clipstart = 2;
			}
		}

		for ( int i = clipstart; i < nverts; i++ )
			clippedvertices[i] = TempVertexBuffer[i];

		int polytype = 0;
		if ( nverts == 3 )
		{
			if ( ClipCoordsEqual( clippedvertices[0], clippedvertices[1] ) ||
				ClipCoordsEqual( clippedvertices[0], clippedvertices[2] ) ||
				ClipCoordsEqual( clippedvertices[1], clippedvertices[2] ) )
			{
				polytype = 1;
			}
		}

		nverts = ClipPolygon( clippedvertices, nverts, clipstart, true );
		if ( nverts == 0 )
		{
			LastStripPolygon = -1;
			return;
		}

		if ( NumPolygons >= 2048 || NumVertices + nverts > 6144 )
		{
			LastStripPolygon = -1;
			DispCnt |= 1 << 13;
			return;
		}

		for ( int i = clipstart; i < nverts; i++ )
		{
			ref Vertex vtx = ref clippedvertices[i];

			vtx.Pw &= 0x00FFFFFF;

			uint w = (uint)vtx.Pw;
			uint posX, posY;
			if ( w == 0 )
			{
				posX = 0;
				posY = 0;
			}
			else
			{
				posX = (uint)vtx.Px + w;
				posY = (uint)-vtx.Py + w;
				uint den = w;

				if ( w > 0xFFFF )
				{
					posX >>= 1;
					posY >>= 1;
					den >>= 1;
				}

				den <<= 1;
				posX = (posX * Viewport[4] / den) + Viewport[0];
				posY = (posY * Viewport[5] / den) + Viewport[3];
			}

			vtx.Fx = (int)(posX & 0x1FF);
			vtx.Fy = (int)(posY & 0xFF);

			if ( w != 0 )
			{
				long hx = ((((long)((uint)vtx.Px + w) * Viewport[4]) << 4) / (((long)w) << 1)) + (Viewport[0] << 4);
				long hy = ((((long)((uint)-vtx.Py + w) * Viewport[5]) << 4) / (((long)w) << 1)) + (Viewport[3] << 4);

				vtx.Hx = (int)((uint)hx & 0x1FFF);
				vtx.Hy = (int)((uint)hy & 0xFFF);
			}
		}

		if ( (CurPolygonAttr & (1 << 13)) == 0 )
		{
			bool zerodot = true;
			bool allbehind = true;

			for ( int i = 0; i < nverts; i++ )
			{
				ref Vertex vtx = ref clippedvertices[i];

				if ( vtx.Fx != clippedvertices[0].Fx || vtx.Fy != clippedvertices[0].Fy )
				{
					zerodot = false;
					break;
				}

				if ( (uint)vtx.Pw <= ZeroDotWLimit )
				{
					allbehind = false;
					break;
				}
			}

			if ( zerodot && allbehind )
			{
				LastStripPolygon = -1;
				return;
			}
		}

		if ( nverts == 4 )
		{
			PolygonPipeline = 35;
			VertexSlotCounter = 1;
			if ( (PolygonMode & 0x2) != 0 ) VertexSlotsFree = 0b11100;
			else VertexSlotsFree = 0b11110;
		}
		else
		{
			PolygonPipeline = 26;
			VertexSlotCounter = 1;
			if ( (PolygonMode & 0x2) != 0 ) VertexSlotsFree = 0b1000;
			else VertexSlotsFree = 0b1110;
		}

		int polyidx = CurPolygonBase + (int)NumPolygons;
		Polygon poly = PolygonRAM[polyidx];
		NumPolygons++;
		poly.NumVertices = 0;

		poly.Attr = CurPolygonAttr;
		poly.TexParam = TexParam;
		poly.TexPalette = TexPalette;

		poly.Degenerate = false;
		poly.Type = 0;

		poly.FacingView = facingview;

		uint texfmt = (TexParam >> 26) & 0x7;
		uint polyalpha = (CurPolygonAttr >> 16) & 0x1F;
		poly.Translucent = texfmt == 1 || texfmt == 6 || (polyalpha > 0 && polyalpha < 31);

		poly.IsShadowMask = (CurPolygonAttr & 0x3F000030) == 0x00000030;
		poly.IsShadow = ((CurPolygonAttr & 0x30) == 0x30) && !poly.IsShadowMask;

		if ( !poly.Translucent ) NumOpaquePolygons++;

		poly.Type = polytype;

		if ( LastStripPolygon >= 0 && clipstart > 0 )
		{
			if ( nverts == lastpolyverts )
			{
				poly.Vertices[0] = reused0;
				poly.Vertices[1] = reused1;
			}
			else
			{
				VertexRAM[CurVertexBase + (int)NumVertices] = VertexRAM[reused0];
				poly.Vertices[0] = CurVertexBase + (int)NumVertices;
				VertexRAM[CurVertexBase + (int)NumVertices + 1] = VertexRAM[reused1];
				poly.Vertices[1] = CurVertexBase + (int)NumVertices + 1;
				NumVertices += 2;
			}

			poly.NumVertices += 2;
		}

		for ( int i = clipstart; i < nverts; i++ )
		{
			int slot = CurVertexBase + (int)NumVertices;
			VertexRAM[slot] = clippedvertices[i];
			poly.Vertices[i] = slot;

			NumVertices++;
			poly.NumVertices++;

			ref Vertex vtx = ref VertexRAM[slot];
			vtx.Fcr = vtx.Cr >> 12;
			if ( vtx.Fcr != 0 ) vtx.Fcr = (vtx.Fcr << 4) + 0xF;
			vtx.Fcg = vtx.Cg >> 12;
			if ( vtx.Fcg != 0 ) vtx.Fcg = (vtx.Fcg << 4) + 0xF;
			vtx.Fcb = vtx.Cb >> 12;
			if ( vtx.Fcb != 0 ) vtx.Fcb = (vtx.Fcb << 4) + 0xF;
		}

		uint vtop = 0, vbot = 0;
		int ytop = 192, ybot = 0;
		int xtop = 256, xbot = 0;
		int wsize = 0;

		for ( int i = 0; i < nverts; i++ )
		{
			ref Vertex vtx = ref VertexRAM[poly.Vertices[i]];

			if ( vtx.Fy < ytop )
			{
				xtop = vtx.Fx;
				ytop = vtx.Fy;
				vtop = (uint)i;
			}
			if ( vtx.Fy > ybot || (vtx.Fy == ybot && vtx.Fx > xbot) )
			{
				xbot = vtx.Fx;
				ybot = vtx.Fy;
				vbot = (uint)i;
			}

			uint w = (uint)vtx.Pw;
			if ( w == 0 ) poly.Degenerate = true;

			while ( (w >> wsize) != 0 && (wsize < 32) )
				wsize += 4;
		}

		poly.VTop = vtop; poly.VBottom = vbot;
		poly.YTop = ytop; poly.YBottom = ybot;
		poly.XTop = xtop; poly.XBottom = xbot;

		if ( ybot > 192 ) poly.Degenerate = true;

		poly.SortKey = (uint)((ybot << 8) | ytop);
		if ( poly.Translucent ) poly.SortKey |= 0x10000;

		poly.WBuffer = (FlushAttributes & 0x2) != 0;

		for ( int i = 0; i < nverts; i++ )
		{
			ref Vertex vtx = ref VertexRAM[poly.Vertices[i]];
			int w, wshifted;

			if ( wsize < 16 )
			{
				w = vtx.Pw << (16 - wsize);
				wshifted = w >> (16 - wsize);
			}
			else
			{
				w = vtx.Pw >> (wsize - 16);
				wshifted = w << (wsize - 16);
			}

			int z;
			if ( (FlushAttributes & 0x2) != 0 )
				z = wshifted;
			else if ( vtx.Pw != 0 )
				z = (int)((((long)vtx.Pz * 0x4000 / vtx.Pw) + 0x3FFF) * 0x200);
			else
				z = 0x7FFE00;

			if ( z < 0 ) z = 0;
			else if ( z > 0xFFFFFF ) z = 0xFFFFFF;

			poly.FinalZ[i] = z;
			poly.FinalW[i] = w;
		}

		if ( PolygonMode >= 2 )
			LastStripPolygon = polyidx;
		else
			LastStripPolygon = -1;
	}

	private void SubmitVertex()
	{
		long vx = CurVertex[0];
		long vy = CurVertex[1];
		long vz = CurVertex[2];
		const long vw = 0x1000;

		ref Vertex vertextrans = ref TempVertexBuffer[VertexNumInPoly];

		UpdateClipMatrix();
		vertextrans.Px = (int)((vx * ClipMatrix[0] + vy * ClipMatrix[4] + vz * ClipMatrix[8] + vw * ClipMatrix[12]) >> 12);
		vertextrans.Py = (int)((vx * ClipMatrix[1] + vy * ClipMatrix[5] + vz * ClipMatrix[9] + vw * ClipMatrix[13]) >> 12);
		vertextrans.Pz = (int)((vx * ClipMatrix[2] + vy * ClipMatrix[6] + vz * ClipMatrix[10] + vw * ClipMatrix[14]) >> 12);
		vertextrans.Pw = (int)((vx * ClipMatrix[3] + vy * ClipMatrix[7] + vz * ClipMatrix[11] + vw * ClipMatrix[15]) >> 12);

		vertextrans.Cr = (VertexColor[0] << 12) + 0xFFF;
		vertextrans.Cg = (VertexColor[1] << 12) + 0xFFF;
		vertextrans.Cb = (VertexColor[2] << 12) + 0xFFF;

		if ( (TexParam >> 30) == 3 )
		{
			vertextrans.Tu = (short)(((vx * TexMatrix[0] + vy * TexMatrix[4] + vz * TexMatrix[8]) >> 24) + RawTexCoords[0]);
			vertextrans.Tv = (short)(((vx * TexMatrix[1] + vy * TexMatrix[5] + vz * TexMatrix[9]) >> 24) + RawTexCoords[1]);
		}
		else
		{
			vertextrans.Tu = TexCoords[0];
			vertextrans.Tv = TexCoords[1];
		}

		vertextrans.Clipped = false;

		VertexNum++;
		VertexNumInPoly++;

		switch ( PolygonMode )
		{
			case 0:
				if ( VertexNumInPoly == 3 )
				{
					VertexNumInPoly = 0;
					SubmitPolygon();
					NumConsecutivePolygons++;
				}
				break;

			case 1:
				if ( VertexNumInPoly == 4 )
				{
					VertexNumInPoly = 0;
					SubmitPolygon();
					NumConsecutivePolygons++;
				}
				break;

			case 2:
				if ( (NumConsecutivePolygons & 1) != 0 )
				{
					Vertex tmp = TempVertexBuffer[1];
					TempVertexBuffer[1] = TempVertexBuffer[0];
					TempVertexBuffer[0] = tmp;

					VertexNumInPoly = 2;
					SubmitPolygon();
					NumConsecutivePolygons++;

					TempVertexBuffer[1] = TempVertexBuffer[2];
				}
				else if ( VertexNumInPoly == 3 )
				{
					VertexNumInPoly = 2;
					SubmitPolygon();
					NumConsecutivePolygons++;

					TempVertexBuffer[0] = TempVertexBuffer[1];
					TempVertexBuffer[1] = TempVertexBuffer[2];
				}
				break;

			case 3:
				if ( VertexNumInPoly == 4 )
				{
					Vertex tmp = TempVertexBuffer[3];
					TempVertexBuffer[3] = TempVertexBuffer[2];
					TempVertexBuffer[2] = tmp;

					VertexNumInPoly = 2;
					SubmitPolygon();
					NumConsecutivePolygons++;

					TempVertexBuffer[0] = TempVertexBuffer[3];
					TempVertexBuffer[1] = TempVertexBuffer[2];
				}
				break;
		}

		VertexPipeline = 7;
		AddCycles( 3 );
	}

	private void CalculateLighting()
	{
		if ( (TexParam >> 30) == 2 )
		{
			TexCoords[0] = (short)(RawTexCoords[0] + (((long)Normal[0] * TexMatrix[0] + (long)Normal[1] * TexMatrix[4] + (long)Normal[2] * TexMatrix[8]) >> 21));
			TexCoords[1] = (short)(RawTexCoords[1] + (((long)Normal[0] * TexMatrix[1] + (long)Normal[1] * TexMatrix[5] + (long)Normal[2] * TexMatrix[9]) >> 21));
		}

		int[] normaltrans =
		[
			((Normal[0] * VecMatrix[0] + Normal[1] * VecMatrix[4] + Normal[2] * VecMatrix[8]) << 9) >> 21,
			((Normal[0] * VecMatrix[1] + Normal[1] * VecMatrix[5] + Normal[2] * VecMatrix[9]) << 9) >> 21,
			((Normal[0] * VecMatrix[2] + Normal[1] * VecMatrix[6] + Normal[2] * VecMatrix[10]) << 9) >> 21,
		];

		int c = 0;
		uint[] vtxbuff =
		[
			(uint)MatEmission[0] << 14,
			(uint)MatEmission[1] << 14,
			(uint)MatEmission[2] << 14
		];
		for ( int i = 0; i < 4; i++ )
		{
			if ( (CurPolygonAttr & (1 << i)) == 0 )
				continue;

			int dot = ((LightDirection[i, 0] * normaltrans[0]) >> 9) +
					  ((LightDirection[i, 1] * normaltrans[1]) >> 9) +
					  ((LightDirection[i, 2] * normaltrans[2]) >> 9);

			int shinelevel;
			if ( dot > 0 )
			{
				int diffdot = (dot << 21) >> 21;
				vtxbuff[0] += (uint)((MatDiffuse[0] * LightColor[i, 0] * diffdot) & 0xFFFFF);
				vtxbuff[1] += (uint)((MatDiffuse[1] * LightColor[i, 1] * diffdot) & 0xFFFFF);
				vtxbuff[2] += (uint)((MatDiffuse[2] * LightColor[i, 2] * diffdot) & 0xFFFFF);

				dot += normaltrans[2];

				dot = (dot << 21) >> 21;
				dot = ((dot * dot) >> 10) & 0x3FF;

				shinelevel = ((dot * SpecRecip[i]) >> 8) - (1 << 9);

				if ( shinelevel < 0 ) shinelevel = 0;
				else
				{
					shinelevel = (shinelevel << 18) >> 18;
					if ( shinelevel < 0 ) shinelevel = 0;
					else if ( shinelevel > 0x1FF ) shinelevel = 0x1FF;
				}
			}
			else shinelevel = 0;

			if ( UseShininessTable )
			{
				shinelevel >>= 2;
				shinelevel = ShininessTable[shinelevel];
				shinelevel <<= 1;
			}

			vtxbuff[0] += (uint)(((MatSpecular[0] * shinelevel) + (MatAmbient[0] << 9)) * LightColor[i, 0]);
			vtxbuff[1] += (uint)(((MatSpecular[1] * shinelevel) + (MatAmbient[1] << 9)) * LightColor[i, 1]);
			vtxbuff[2] += (uint)(((MatSpecular[2] * shinelevel) + (MatAmbient[2] << 9)) * LightColor[i, 2]);

			c++;
		}

		VertexColor[0] = (byte)((vtxbuff[0] >> 14) > 31 ? 31 : (vtxbuff[0] >> 14));
		VertexColor[1] = (byte)((vtxbuff[1] >> 14) > 31 ? 31 : (vtxbuff[1] >> 14));
		VertexColor[2] = (byte)((vtxbuff[2] >> 14) > 31 ? 31 : (vtxbuff[2] >> 14));

		if ( c < 1 ) c = 1;
		NormalPipeline = 7;
		AddCycles( c );
	}

	private void BoxTest( int[] paramArr )
	{
		Vertex[] cube = new Vertex[8];
		Vertex[] face = new Vertex[10];

		AddCycles( 254 );
		GXStat &= ~(1u << 1);

		short x0 = (short)(paramArr[0] & 0xFFFF);
		short y0 = (short)(paramArr[0] >> 16);
		short z0 = (short)(paramArr[1] & 0xFFFF);
		short x1 = (short)(paramArr[1] >> 16);
		short y1 = (short)(paramArr[2] & 0xFFFF);
		short z1 = (short)(paramArr[2] >> 16);

		x1 += x0;
		y1 += y0;
		z1 += z0;

		cube[0].Px = x0; cube[0].Py = y0; cube[0].Pz = z0;
		cube[1].Px = x1; cube[1].Py = y0; cube[1].Pz = z0;
		cube[2].Px = x1; cube[2].Py = y1; cube[2].Pz = z0;
		cube[3].Px = x0; cube[3].Py = y1; cube[3].Pz = z0;
		cube[4].Px = x0; cube[4].Py = y1; cube[4].Pz = z1;
		cube[5].Px = x0; cube[5].Py = y0; cube[5].Pz = z1;
		cube[6].Px = x1; cube[6].Py = y0; cube[6].Pz = z1;
		cube[7].Px = x1; cube[7].Py = y1; cube[7].Pz = z1;

		UpdateClipMatrix();
		for ( int i = 0; i < 8; i++ )
		{
			long x = cube[i].Px;
			long y = cube[i].Py;
			long z = cube[i].Pz;

			cube[i].Px = (int)((x * ClipMatrix[0] + y * ClipMatrix[4] + z * ClipMatrix[8] + (long)0x1000 * ClipMatrix[12]) >> 12);
			cube[i].Py = (int)((x * ClipMatrix[1] + y * ClipMatrix[5] + z * ClipMatrix[9] + (long)0x1000 * ClipMatrix[13]) >> 12);
			cube[i].Pz = (int)((x * ClipMatrix[2] + y * ClipMatrix[6] + z * ClipMatrix[10] + (long)0x1000 * ClipMatrix[14]) >> 12);
			cube[i].Pw = (int)((x * ClipMatrix[3] + y * ClipMatrix[7] + z * ClipMatrix[11] + (long)0x1000 * ClipMatrix[15]) >> 12);
		}

		if ( BoxTestFace( cube, face, 0, 1, 2, 3 ) ) { GXStat |= 1u << 1; return; }
		if ( BoxTestFace( cube, face, 4, 5, 6, 7 ) ) { GXStat |= 1u << 1; return; }
		if ( BoxTestFace( cube, face, 0, 3, 4, 5 ) ) { GXStat |= 1u << 1; return; }
		if ( BoxTestFace( cube, face, 1, 2, 7, 6 ) ) { GXStat |= 1u << 1; return; }
		if ( BoxTestFace( cube, face, 0, 1, 6, 5 ) ) { GXStat |= 1u << 1; return; }
		if ( BoxTestFace( cube, face, 2, 3, 4, 7 ) ) { GXStat |= 1u << 1; return; }
	}

	private bool BoxTestFace( Vertex[] cube, Vertex[] face, int a, int b, int c, int d )
	{
		face[0] = cube[a]; face[1] = cube[b]; face[2] = cube[c]; face[3] = cube[d];
		return ClipPolygon( face, 4, 0, false ) > 0;
	}

	private void PosTest()
	{
		long vx = CurVertex[0];
		long vy = CurVertex[1];
		long vz = CurVertex[2];
		const long vw = 0x1000;

		UpdateClipMatrix();
		PosTestResult[0] = (int)((vx * ClipMatrix[0] + vy * ClipMatrix[4] + vz * ClipMatrix[8] + vw * ClipMatrix[12]) >> 12);
		PosTestResult[1] = (int)((vx * ClipMatrix[1] + vy * ClipMatrix[5] + vz * ClipMatrix[9] + vw * ClipMatrix[13]) >> 12);
		PosTestResult[2] = (int)((vx * ClipMatrix[2] + vy * ClipMatrix[6] + vz * ClipMatrix[10] + vw * ClipMatrix[14]) >> 12);
		PosTestResult[3] = (int)((vx * ClipMatrix[3] + vy * ClipMatrix[7] + vz * ClipMatrix[11] + vw * ClipMatrix[15]) >> 12);

		AddCycles( 5 );
	}

	private void VecTest( uint param )
	{
		short[] normal =
		[
			(short)((short)((param & 0x000003FF) << 6) >> 6),
			(short)((short)((param & 0x000FFC00) >> 4) >> 6),
			(short)((short)((param & 0x3FF00000) >> 14) >> 6),
		];

		VecTestResult[0] = (short)((normal[0] * VecMatrix[0] + normal[1] * VecMatrix[4] + normal[2] * VecMatrix[8]) >> 9);
		VecTestResult[1] = (short)((normal[0] * VecMatrix[1] + normal[1] * VecMatrix[5] + normal[2] * VecMatrix[9]) >> 9);
		VecTestResult[2] = (short)((normal[0] * VecMatrix[2] + normal[1] * VecMatrix[6] + normal[2] * VecMatrix[10]) >> 9);

		if ( (VecTestResult[0] & 0x1000) != 0 ) VecTestResult[0] |= unchecked((short)0xF000);
		if ( (VecTestResult[1] & 0x1000) != 0 ) VecTestResult[1] |= unchecked((short)0xF000);
		if ( (VecTestResult[2] & 0x1000) != 0 ) VecTestResult[2] |= unchecked((short)0xF000);

		AddCycles( 4 );
	}
}
