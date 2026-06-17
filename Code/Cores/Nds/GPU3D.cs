namespace Emulotl.Nds;

public sealed partial class GPU3D
{
	private readonly NDS NDS;

	private struct CmdFIFOEntry
	{
		public byte Command;
		public uint Param;
	}

	private static readonly byte[] CmdNumParams =
	[
		0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 0, 1, 1, 1, 0, 16, 12, 16, 12, 9, 3, 3,
		0, 0, 0,
		1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1,
		0, 0, 0, 0,
		1, 1, 1, 1, 32,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		1,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		3, 2, 1,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	];

	private readonly FIFO<CmdFIFOEntry> CmdFIFO = new( 256 );
	private readonly FIFO<CmdFIFOEntry> CmdPIPE = new( 4 );
	private readonly FIFO<CmdFIFOEntry> CmdStallQueue = new( 64 );

	public uint GXStat;
	public uint DispCnt;
	public uint ZeroDotWLimit;

	private readonly int[] ExecParams = new int[32];
	private uint ExecParamCount;

	private long CycleCount;
	public long Timestamp;

	private int VertexPipeline, NormalPipeline, PolygonPipeline;
	private int VertexSlotCounter;
	private uint VertexSlotsFree;

	private uint NumPushPopCommands;
	private uint NumTestCommands;

	private uint NumCommands, CurCommand, ParamCount, TotalParams;

	private uint MatrixMode;
	private uint ProjMatrixStackPointer;
	private uint PosMatrixStackPointer;
	private uint TexMatrixStackPointer;

	public uint FlushRequest;
	private uint FlushAttributes;

	public uint NumVertices, NumPolygons;

	private bool GeometryEnabled;
	private bool RenderingEnabled;

	private readonly int[] PosTestResult = new int[4];
	private readonly short[] VecTestResult = new short[3];

	public GPU3D( NDS nds )
	{
		NDS = nds;

		for ( int i = 0; i < PolygonRAM.Length; i++ )
			PolygonRAM[i] = new Polygon();
	}

	private void ResetRenderingState()
	{
		RenderNumPolygons = 0;

		RenderDispCnt = 0;
		RenderAlphaRef = 0;

		Array.Clear( RenderEdgeTable );
		Array.Clear( RenderToonTable );

		RenderFogColor = 0;
		RenderFogOffset = 0;
		RenderFogShift = 0;
		Array.Clear( RenderFogDensityTable );

		RenderClearAttr1 = 0x3F000000;
		RenderClearAttr2 = 0x00007FFF;
	}

	public void Reset()
	{
		CmdFIFO.Clear();
		CmdPIPE.Clear();
		CmdStallQueue.Clear();

		GXStat = 0;
		DispCnt = 0;
		ZeroDotWLimit = 0xFFFFFF;

		Array.Clear( ExecParams );
		ExecParamCount = 0;

		CycleCount = 0;
		Timestamp = 0;

		VertexPipeline = NormalPipeline = PolygonPipeline = 0;
		VertexSlotCounter = 0;
		VertexSlotsFree = 1;

		NumPushPopCommands = 0;
		NumTestCommands = 0;

		MatrixMode = 0;

		MatrixLoadIdentity( ProjMatrix );
		MatrixLoadIdentity( PosMatrix );
		MatrixLoadIdentity( VecMatrix );
		MatrixLoadIdentity( TexMatrix );

		ClipMatrixDirty = true;
		UpdateClipMatrix();

		Array.Clear( Viewport );

		Array.Clear( ProjMatrixStack );
		Array.Clear( PosMatrixStack );
		Array.Clear( VecMatrixStack );
		Array.Clear( TexMatrixStack );

		ProjMatrixStackPointer = 0;
		PosMatrixStackPointer = 0;
		TexMatrixStackPointer = 0;

		NumCommands = CurCommand = ParamCount = TotalParams = 0;

		GeometryEnabled = false;
		RenderingEnabled = false;

		DispCnt = 0;
		AlphaRefVal = 0;
		AlphaRef = 0;

		Array.Clear( ToonTable );
		Array.Clear( EdgeTable );

		FogOffset = 0;
		FogColor = 0;
		Array.Clear( FogDensityTable );

		ClearAttr1 = 0x3F000000;
		ClearAttr2 = 0x00007FFF;

		ResetRenderingState();

		AbortFrame = false;

		PolygonMode = 0;
		Array.Clear( CurVertex );
		Array.Clear( VertexColor );
		Array.Clear( TexCoords );
		Array.Clear( RawTexCoords );
		Array.Clear( Normal );

		Array.Clear( LightDirection );
		Array.Clear( LightColor );
		Array.Clear( MatDiffuse );
		Array.Clear( MatAmbient );
		Array.Clear( MatSpecular );
		Array.Clear( MatEmission );

		UseShininessTable = false;
		Array.Clear( ShininessTable );

		PolygonAttr = 0;
		CurPolygonAttr = 0;

		TexParam = 0;
		TexPalette = 0;

		Array.Clear( PosTestResult );
		Array.Clear( VecTestResult );

		Array.Clear( TempVertexBuffer );
		VertexNum = 0;
		VertexNumInPoly = 0;
		NumConsecutivePolygons = 0;
		LastStripPolygon = -1;
		NumOpaquePolygons = 0;

		CurVertexBase = 0;
		CurPolygonBase = 0;
		NumVertices = 0;
		NumPolygons = 0;
		CurRAMBank = 0;

		FlushRequest = 0;
		FlushAttributes = 0;

		RenderXPos = 0;
	}

	public void SetEnabled( bool geometry, bool rendering )
	{
		GeometryEnabled = geometry;
		RenderingEnabled = rendering;

		if ( !rendering ) ResetRenderingState();
	}

	public void SetRenderXPos( ushort xpos, ushort mask )
	{
		if ( !RenderingEnabled ) return;
		RenderXPos = (ushort)((RenderXPos & ~mask) | (xpos & mask & 0x01FF));
	}

	private void AddCycles( int num )
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
				VertexSlotCounter += num;
				while ( VertexSlotCounter > 9 )
				{
					VertexSlotCounter -= 9;
					VertexSlotsFree >>= 1;
				}
			}
			else
			{
				PolygonPipeline = 0;
				VertexSlotCounter = 0;
				VertexSlotsFree = 0x1;
			}
		}
	}

	private void CmdFIFOWrite( CmdFIFOEntry entry )
	{
		if ( CmdFIFO.IsEmpty() && !CmdPIPE.IsFull() )
		{
			CmdPIPE.Write( entry );
		}
		else
		{
			if ( CmdFIFO.IsFull() )
			{
				CmdStallQueue.Write( entry );
				NDS.GXFIFOStall();
				return;
			}

			CmdFIFO.Write( entry );
		}

		GXStat |= 1u << 27;

		if ( entry.Command == 0x11 || entry.Command == 0x12 )
		{
			GXStat |= 1u << 14;
			NumPushPopCommands++;
		}
		else if ( entry.Command == 0x70 || entry.Command == 0x71 || entry.Command == 0x72 )
		{
			GXStat |= 1u << 0;
			NumTestCommands++;
		}
	}

	private CmdFIFOEntry CmdFIFORead()
	{
		CmdFIFOEntry ret = CmdPIPE.Read();

		if ( CmdPIPE.Level() <= 2 )
		{
			if ( !CmdFIFO.IsEmpty() )
				CmdPIPE.Write( CmdFIFO.Read() );
			if ( !CmdFIFO.IsEmpty() )
				CmdPIPE.Write( CmdFIFO.Read() );

			if ( !CmdStallQueue.IsEmpty() )
			{
				while ( !CmdStallQueue.IsEmpty() )
				{
					if ( CmdFIFO.IsFull() ) break;
					CmdFIFOEntry entry = CmdStallQueue.Read();
					CmdFIFOWrite( entry );
				}

				if ( CmdStallQueue.IsEmpty() )
					NDS.GXFIFOUnstall();
			}

			CheckFIFODMA();
			CheckFIFOIRQ();
		}

		return ret;
	}

	public long CyclesToRunFor()
	{
		if ( CycleCount < 0 ) return 0;
		return CycleCount;
	}

	private void FinishWork( long cycles )
	{
		AddCycles( (int)cycles );

		CycleCount = 0;

		if ( VertexPipeline != 0 || NormalPipeline != 0 || PolygonPipeline != 0 )
			return;

		GXStat &= ~(1u << 27);
	}

	public void Run()
	{
		if ( !GeometryEnabled || FlushRequest != 0 ||
			(CmdPIPE.IsEmpty() && (GXStat & (1 << 27)) == 0) )
		{
			Timestamp = NDS.ARM9Timestamp >> 1;
			return;
		}

		long cycles = (NDS.ARM9Timestamp >> 1) - Timestamp;
		CycleCount -= cycles;
		Timestamp = NDS.ARM9Timestamp >> 1;

		if ( CycleCount <= 0 )
		{
			while ( CycleCount <= 0 && !CmdPIPE.IsEmpty() )
			{
				if ( NumPushPopCommands == 0 ) GXStat &= ~(1u << 14);
				if ( NumTestCommands == 0 ) GXStat &= ~(1u << 0);

				ExecuteCommand();
			}
		}

		if ( CycleCount <= 0 && CmdPIPE.IsEmpty() )
		{
			if ( (GXStat & (1 << 27)) != 0 ) FinishWork( -CycleCount );
			else CycleCount = 0;

			if ( NumPushPopCommands == 0 ) GXStat &= ~(1u << 14);
			if ( NumTestCommands == 0 ) GXStat &= ~(1u << 0);
		}
	}

	private void CheckFIFOIRQ()
	{
		bool irq = false;
		switch ( GXStat >> 30 )
		{
			case 1: irq = CmdFIFO.Level() < 128; break;
			case 2: irq = CmdFIFO.IsEmpty(); break;
		}

		if ( irq ) NDS.SetIRQ( 0, IRQ.GXFIFO );
		else NDS.ClearIRQ( 0, IRQ.GXFIFO );
	}

	private void CheckFIFODMA()
	{
		if ( CmdFIFO.Level() < 128 )
			NDS.CheckDMAs( 0, 0x07 );
	}

	public void VBlank()
	{
		if ( GeometryEnabled )
		{
			if ( RenderingEnabled )
			{
				if ( FlushRequest != 0 )
				{
					if ( NumPolygons != 0 )
					{
						uint io = 0, it = NumOpaquePolygons;
						for ( uint i = 0; i < NumPolygons; i++ )
						{
							Polygon poly = PolygonRAM[CurPolygonBase + (int)i];
							if ( poly.Translucent )
								RenderPolygonRAM[it++] = poly;
							else
								RenderPolygonRAM[io++] = poly;
						}

						uint sortcount = (FlushAttributes & 0x1) != 0 ? NumOpaquePolygons : NumPolygons;
						StableSortPolygons( (int)sortcount );
					}

					RenderNumPolygons = NumPolygons;
					RenderFrameIdentical = false;
				}
				else
				{
					RenderFrameIdentical = RenderDispCnt == DispCnt
						&& RenderAlphaRef == AlphaRef
						&& RenderClearAttr1 == ClearAttr1
						&& RenderClearAttr2 == ClearAttr2
						&& RenderFogColor == FogColor
						&& RenderFogOffset == FogOffset * 0x200
						&& TableEqual( RenderEdgeTable, EdgeTable, 8 )
						&& FogTableEqual()
						&& TableEqual( RenderToonTable, ToonTable, 32 );
				}

				RenderDispCnt = DispCnt;
				RenderAlphaRef = AlphaRef;

				Array.Copy( EdgeTable, RenderEdgeTable, 8 );
				Array.Copy( ToonTable, RenderToonTable, 32 );

				RenderFogColor = FogColor;
				RenderFogOffset = FogOffset * 0x200;
				RenderFogShift = (RenderDispCnt >> 8) & 0xF;
				RenderFogDensityTable[0] = FogDensityTable[0];
				Array.Copy( FogDensityTable, 0, RenderFogDensityTable, 1, 32 );
				RenderFogDensityTable[33] = FogDensityTable[31];

				RenderClearAttr1 = ClearAttr1;
				RenderClearAttr2 = ClearAttr2;
			}

			if ( FlushRequest != 0 )
			{
				CurRAMBank = CurRAMBank != 0 ? 0u : 1u;
				CurVertexBase = CurRAMBank != 0 ? 6144 : 0;
				CurPolygonBase = CurRAMBank != 0 ? 2048 : 0;

				NumVertices = 0;
				NumPolygons = 0;
				NumOpaquePolygons = 0;

				FlushRequest = 0;
			}
		}
	}

	private static bool TableEqual( ushort[] a, ushort[] b, int count )
	{
		for ( int i = 0; i < count; i++ )
			if ( a[i] != b[i] ) return false;
		return true;
	}

	private bool FogTableEqual()
	{
		for ( int i = 0; i < 32; i++ )
			if ( RenderFogDensityTable[i + 1] != FogDensityTable[i] ) return false;
		return true;
	}

	private void StableSortPolygons( int count )
	{
		for ( int i = 1; i < count; i++ )
		{
			Polygon key = RenderPolygonRAM[i];
			int j = i - 1;
			while ( j >= 0 && RenderPolygonRAM[j].SortKey > key.SortKey )
			{
				RenderPolygonRAM[j + 1] = RenderPolygonRAM[j];
				j--;
			}
			RenderPolygonRAM[j + 1] = key;
		}
	}

	private void WriteToGXFIFO( uint val )
	{
		if ( NumCommands == 0 )
		{
			NumCommands = 4;
			CurCommand = val;
			ParamCount = 0;
			TotalParams = CmdNumParams[CurCommand & 0xFF];

			if ( TotalParams > 0 ) return;
		}
		else
		{
			ParamCount++;
		}

		for (; ; )
		{
			if ( (CurCommand & 0xFF) != 0 || (NumCommands == 4 && CurCommand == 0) )
			{
				CmdFIFOEntry entry;
				entry.Command = (byte)(CurCommand & 0xFF);
				entry.Param = val;
				CmdFIFOWrite( entry );
			}

			if ( ParamCount >= TotalParams )
			{
				CurCommand >>= 8;
				NumCommands--;
				if ( NumCommands == 0 ) break;

				ParamCount = 0;
				TotalParams = CmdNumParams[CurCommand & 0xFF];
			}
			if ( ParamCount < TotalParams )
				break;
		}
	}

	public byte Read8( uint addr )
	{
		switch ( addr )
		{
			case 0x04000600:
				Run();
				return (byte)(GXStat & 0xFF);
			case 0x04000601:
				Run();
				return (byte)(((GXStat >> 8) & 0xFF) |
					(PosMatrixStackPointer & 0x1F) |
					((ProjMatrixStackPointer & 0x1) << 5));
			case 0x04000602:
				Run();
				return (byte)(CmdFIFO.Level() & 0xFF);
			case 0x04000603:
				{
					Run();
					uint fifolevel = CmdFIFO.Level();
					return (byte)(((GXStat >> 24) & 0xFF) |
						(fifolevel >> 8) |
						(fifolevel < 128 ? (1u << 1) : 0) |
						(fifolevel == 0 ? (1u << 2) : 0));
				}
		}

		return 0;
	}

	public ushort Read16( uint addr )
	{
		switch ( addr )
		{
			case 0x04000060:
				return (ushort)DispCnt;

			case 0x04000320:
				return 46;

			case 0x04000600:
				Run();
				return (ushort)((GXStat & 0xFFFF) |
					((PosMatrixStackPointer & 0x1F) << 8) |
					((ProjMatrixStackPointer & 0x1) << 13));
			case 0x04000602:
				{
					Run();
					uint fifolevel = CmdFIFO.Level();
					return (ushort)((GXStat >> 16) |
						fifolevel |
						(fifolevel < 128 ? (1u << 9) : 0) |
						(fifolevel == 0 ? (1u << 10) : 0));
				}

			case 0x04000604: return (ushort)NumPolygons;
			case 0x04000606: return (ushort)NumVertices;

			case 0x04000630: return (ushort)VecTestResult[0];
			case 0x04000632: return (ushort)VecTestResult[1];
			case 0x04000634: return (ushort)VecTestResult[2];
		}

		return 0;
	}

	public uint Read32( uint addr )
	{
		switch ( addr )
		{
			case 0x04000060:
				return DispCnt;

			case 0x04000320:
				return 46;

			case 0x04000600:
				{
					Run();
					uint fifolevel = CmdFIFO.Level();
					return GXStat |
						((PosMatrixStackPointer & 0x1F) << 8) |
						((ProjMatrixStackPointer & 0x1) << 13) |
						(fifolevel << 16) |
						(fifolevel < 128 ? (1u << 25) : 0) |
						(fifolevel == 0 ? (1u << 26) : 0);
				}

			case 0x04000604:
				return NumPolygons | (NumVertices << 16);

			case 0x04000620: return (uint)PosTestResult[0];
			case 0x04000624: return (uint)PosTestResult[1];
			case 0x04000628: return (uint)PosTestResult[2];
			case 0x0400062C: return (uint)PosTestResult[3];
		}

		return 0;
	}

	public void Write8( uint addr, byte val )
	{
		if ( !RenderingEnabled && addr >= 0x04000320 && addr < 0x04000400 ) return;
		if ( !GeometryEnabled && addr >= 0x04000400 && addr < 0x04000700 ) return;

		switch ( addr )
		{
			case 0x04000340:
				AlphaRefVal = (byte)(val & 0x1F);
				AlphaRef = (DispCnt & (1 << 2)) != 0 ? AlphaRefVal : (byte)0;
				return;

			case 0x04000601:
				if ( (val & 0x80) != 0 )
				{
					GXStat &= ~0x8000u;
					ProjMatrixStackPointer = 0;
					TexMatrixStackPointer = 0;
				}
				return;
			case 0x04000603:
				GXStat &= 0x3FFFFFFF;
				GXStat |= (uint)(val & 0xC0) << 24;
				CheckFIFOIRQ();
				return;
		}

		if ( addr >= 0x04000330 && addr < 0x04000340 )
		{
			int idx = (int)(addr - 0x04000330);
			if ( (idx & 1) == 0 ) EdgeTable[idx >> 1] = (ushort)((EdgeTable[idx >> 1] & 0xFF00) | val);
			else EdgeTable[idx >> 1] = (ushort)((EdgeTable[idx >> 1] & 0x00FF) | (val << 8));
			return;
		}

		if ( addr >= 0x04000360 && addr < 0x04000380 )
		{
			FogDensityTable[addr - 0x04000360] = (byte)(val & 0x7F);
			return;
		}

		if ( addr >= 0x04000380 && addr < 0x040003C0 )
		{
			int idx = (int)(addr - 0x04000380);
			if ( (idx & 1) == 0 ) ToonTable[idx >> 1] = (ushort)((ToonTable[idx >> 1] & 0xFF00) | val);
			else ToonTable[idx >> 1] = (ushort)((ToonTable[idx >> 1] & 0x00FF) | (val << 8));
			return;
		}
	}

	public void Write16( uint addr, ushort val )
	{
		if ( !RenderingEnabled && addr >= 0x04000320 && addr < 0x04000400 ) return;
		if ( !GeometryEnabled && addr >= 0x04000400 && addr < 0x04000700 ) return;

		switch ( addr )
		{
			case 0x04000060:
				DispCnt = ((uint)val & 0x4FFF) | (DispCnt & 0x3000);
				if ( (val & (1 << 12)) != 0 ) DispCnt &= ~(1u << 12);
				if ( (val & (1 << 13)) != 0 ) DispCnt &= ~(1u << 13);
				AlphaRef = (DispCnt & (1 << 2)) != 0 ? AlphaRefVal : (byte)0;
				return;

			case 0x04000340:
				AlphaRefVal = (byte)(val & 0x1F);
				AlphaRef = (DispCnt & (1 << 2)) != 0 ? AlphaRefVal : (byte)0;
				return;

			case 0x04000350: ClearAttr1 = (ClearAttr1 & 0xFFFF0000) | val; return;
			case 0x04000352: ClearAttr1 = (ClearAttr1 & 0xFFFF) | ((uint)val << 16); return;
			case 0x04000354: ClearAttr2 = (ClearAttr2 & 0xFFFF0000) | val; return;
			case 0x04000356: ClearAttr2 = (ClearAttr2 & 0xFFFF) | ((uint)val << 16); return;

			case 0x04000358: FogColor = (FogColor & 0xFFFF0000) | val; return;
			case 0x0400035A: FogColor = (FogColor & 0xFFFF) | ((uint)val << 16); return;
			case 0x0400035C: FogOffset = (uint)(val & 0x7FFF); return;

			case 0x04000600:
				if ( (val & 0x8000) != 0 )
				{
					GXStat &= ~0x8000u;
					ProjMatrixStackPointer = 0;
					TexMatrixStackPointer = 0;
				}
				return;
			case 0x04000602:
				GXStat &= 0x3FFFFFFF;
				GXStat |= (uint)(val & 0xC000) << 16;
				CheckFIFOIRQ();
				return;

			case 0x04000610:
				ZeroDotWLimit = (uint)(((val & 0x7FFF) * 0x200) + 0x1FF);
				return;
		}

		if ( addr >= 0x04000330 && addr < 0x04000340 )
		{
			EdgeTable[(addr - 0x04000330) >> 1] = val;
			return;
		}

		if ( addr >= 0x04000360 && addr < 0x04000380 )
		{
			uint a = addr - 0x04000360;
			FogDensityTable[a] = (byte)(val & 0x7F);
			FogDensityTable[a + 1] = (byte)((val >> 8) & 0x7F);
			return;
		}

		if ( addr >= 0x04000380 && addr < 0x040003C0 )
		{
			ToonTable[(addr - 0x04000380) >> 1] = val;
			return;
		}
	}

	public void Write32( uint addr, uint val )
	{
		if ( !RenderingEnabled && addr >= 0x04000320 && addr < 0x04000400 ) return;
		if ( !GeometryEnabled && addr >= 0x04000400 && addr < 0x04000700 ) return;

		switch ( addr )
		{
			case 0x04000060:
				DispCnt = (val & 0x4FFF) | (DispCnt & 0x3000);
				if ( (val & (1 << 12)) != 0 ) DispCnt &= ~(1u << 12);
				if ( (val & (1 << 13)) != 0 ) DispCnt &= ~(1u << 13);
				AlphaRef = (DispCnt & (1 << 2)) != 0 ? AlphaRefVal : (byte)0;
				return;

			case 0x04000340:
				AlphaRefVal = (byte)(val & 0x1F);
				AlphaRef = (DispCnt & (1 << 2)) != 0 ? AlphaRefVal : (byte)0;
				return;

			case 0x04000350: ClearAttr1 = val; return;
			case 0x04000354: ClearAttr2 = val; return;

			case 0x04000358: FogColor = val; return;
			case 0x0400035C: FogOffset = val & 0x7FFF; return;

			case 0x04000600:
				if ( (val & 0x8000) != 0 )
				{
					GXStat &= ~0x8000u;
					ProjMatrixStackPointer = 0;
					TexMatrixStackPointer = 0;
				}
				GXStat &= 0x3FFFFFFF;
				GXStat |= val & 0xC0000000;
				CheckFIFOIRQ();
				return;

			case 0x04000610:
				ZeroDotWLimit = ((val & 0x7FFF) * 0x200) + 0x1FF;
				return;
		}

		if ( addr >= 0x04000330 && addr < 0x04000340 )
		{
			uint a = (addr - 0x04000330) >> 1;
			EdgeTable[a] = (ushort)(val & 0xFFFF);
			EdgeTable[a + 1] = (ushort)(val >> 16);
			return;
		}

		if ( addr >= 0x04000360 && addr < 0x04000380 )
		{
			uint a = addr - 0x04000360;
			FogDensityTable[a] = (byte)(val & 0x7F);
			FogDensityTable[a + 1] = (byte)((val >> 8) & 0x7F);
			FogDensityTable[a + 2] = (byte)((val >> 16) & 0x7F);
			FogDensityTable[a + 3] = (byte)((val >> 24) & 0x7F);
			return;
		}

		if ( addr >= 0x04000380 && addr < 0x040003C0 )
		{
			uint a = (addr - 0x04000380) >> 1;
			ToonTable[a] = (ushort)(val & 0xFFFF);
			ToonTable[a + 1] = (ushort)(val >> 16);
			return;
		}

		if ( addr >= 0x04000400 && addr < 0x04000440 )
		{
			WriteToGXFIFO( val );
			return;
		}

		if ( addr >= 0x04000440 && addr < 0x040005CC )
		{
			CmdFIFOEntry entry;
			entry.Command = (byte)((addr & 0x1FC) >> 2);
			entry.Param = val;
			CmdFIFOWrite( entry );
			return;
		}
	}
}
