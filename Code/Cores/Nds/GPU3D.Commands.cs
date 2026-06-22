namespace Emulotl.Nds;

public sealed partial class GPU3D
{
	private void ExecuteCommand()
	{
		CmdFIFOEntry entry = CmdFIFORead();

		uint paramsRequiredCount = CmdNumParams[entry.Command];
		if ( paramsRequiredCount <= 1 )
		{
			switch ( entry.Command )
			{
				case 0x10:
					VertexPipelineCmdDelayed4();
					MatrixMode = entry.Param & 0x3;
					break;

				case 0x11:
					VertexPipelineCmdDelayed4();
					NumPushPopCommands--;
					if ( MatrixMode == 0 )
					{
						if ( ProjMatrixStackPointer > 0 ) GXStat |= 1u << 15;
						MatrixCopy( ProjMatrixStack, ProjMatrix );
						ProjMatrixStackPointer++;
						ProjMatrixStackPointer &= 0x1;
					}
					else if ( MatrixMode == 3 )
					{
						if ( TexMatrixStackPointer > 0 ) GXStat |= 1u << 15;
						MatrixCopy( TexMatrixStack, TexMatrix );
						TexMatrixStackPointer++;
						TexMatrixStackPointer &= 0x1;
					}
					else
					{
						if ( PosMatrixStackPointer > 30 ) GXStat |= 1u << 15;
						MatrixCopyToStack( PosMatrixStack, (int)(PosMatrixStackPointer & 0x1F), PosMatrix );
						MatrixCopyToStack( VecMatrixStack, (int)(PosMatrixStackPointer & 0x1F), VecMatrix );
						PosMatrixStackPointer++;
						PosMatrixStackPointer &= 0x3F;
					}
					AddCycles( 16 );
					break;

				case 0x12:
					VertexPipelineCmdDelayed4();
					NumPushPopCommands--;
					if ( MatrixMode == 0 )
					{
						if ( ProjMatrixStackPointer == 0 ) GXStat |= 1u << 15;
						ProjMatrixStackPointer--;
						ProjMatrixStackPointer &= 0x1;
						MatrixCopy( ProjMatrix, ProjMatrixStack );
						ClipMatrixDirty = true;
						AddCycles( 35 );
					}
					else if ( MatrixMode == 3 )
					{
						if ( TexMatrixStackPointer == 0 ) GXStat |= 1u << 15;
						TexMatrixStackPointer--;
						TexMatrixStackPointer &= 0x1;
						MatrixCopy( TexMatrix, TexMatrixStack );
						AddCycles( 17 );
					}
					else
					{
						int offset = ((int)(entry.Param << 26)) >> 26;
						PosMatrixStackPointer = (uint)(((int)PosMatrixStackPointer - offset) & 0x3F);
						if ( PosMatrixStackPointer > 30 ) GXStat |= 1u << 15;
						MatrixCopyFromStack( PosMatrix, PosMatrixStack, (int)(PosMatrixStackPointer & 0x1F) );
						MatrixCopyFromStack( VecMatrix, VecMatrixStack, (int)(PosMatrixStackPointer & 0x1F) );
						ClipMatrixDirty = true;
						AddCycles( 35 );
					}
					break;

				case 0x13:
					VertexPipelineCmdDelayed4();
					if ( MatrixMode == 0 )
						MatrixCopy( ProjMatrixStack, ProjMatrix );
					else if ( MatrixMode == 3 )
						MatrixCopy( TexMatrixStack, TexMatrix );
					else
					{
						uint addr = entry.Param & 0x1F;
						if ( addr > 30 ) GXStat |= 1u << 15;
						MatrixCopyToStack( PosMatrixStack, (int)addr, PosMatrix );
						MatrixCopyToStack( VecMatrixStack, (int)addr, VecMatrix );
					}
					AddCycles( 16 );
					break;

				case 0x14:
					VertexPipelineCmdDelayed4();
					if ( MatrixMode == 0 )
					{
						MatrixCopy( ProjMatrix, ProjMatrixStack );
						ClipMatrixDirty = true;
						AddCycles( 35 );
					}
					else if ( MatrixMode == 3 )
					{
						MatrixCopy( TexMatrix, TexMatrixStack );
						AddCycles( 17 );
					}
					else
					{
						uint addr = entry.Param & 0x1F;
						if ( addr > 30 ) GXStat |= 1u << 15;
						MatrixCopyFromStack( PosMatrix, PosMatrixStack, (int)addr );
						MatrixCopyFromStack( VecMatrix, VecMatrixStack, (int)addr );
						ClipMatrixDirty = true;
						AddCycles( 35 );
					}
					break;

				case 0x15:
					VertexPipelineCmdDelayed4();
					if ( MatrixMode == 0 )
					{
						MatrixLoadIdentity( ProjMatrix );
						ClipMatrixDirty = true;
						AddCycles( 18 );
					}
					else if ( MatrixMode == 3 )
						MatrixLoadIdentity( TexMatrix );
					else
					{
						MatrixLoadIdentity( PosMatrix );
						if ( MatrixMode == 2 )
							MatrixLoadIdentity( VecMatrix );
						ClipMatrixDirty = true;
						AddCycles( 18 );
					}
					break;

				case 0x20:
					VertexPipelineCmdDelayed6();
					{
						uint cc = entry.Param;
						VertexColor[0] = (byte)(cc & 0x1F);
						VertexColor[1] = (byte)((cc >> 5) & 0x1F);
						VertexColor[2] = (byte)((cc >> 10) & 0x1F);
					}
					break;

				case 0x21:
					VertexPipelineCmdDelayed4();
					Normal[0] = (short)((short)((entry.Param & 0x000003FF) << 6) >> 6);
					Normal[1] = (short)((short)((entry.Param & 0x000FFC00) >> 4) >> 6);
					Normal[2] = (short)((short)((entry.Param & 0x3FF00000) >> 14) >> 6);
					CalculateLighting();
					break;

				case 0x22:
					VertexPipelineCmdDelayed4();
					RawTexCoords[0] = (short)(entry.Param & 0xFFFF);
					RawTexCoords[1] = (short)(entry.Param >> 16);
					if ( (TexParam >> 30) == 1 )
					{
						TexCoords[0] = (short)((RawTexCoords[0] * TexMatrix[0] + RawTexCoords[1] * TexMatrix[4] + TexMatrix[8] + TexMatrix[12]) >> 12);
						TexCoords[1] = (short)((RawTexCoords[0] * TexMatrix[1] + RawTexCoords[1] * TexMatrix[5] + TexMatrix[9] + TexMatrix[13]) >> 12);
					}
					else
					{
						TexCoords[0] = RawTexCoords[0];
						TexCoords[1] = RawTexCoords[1];
					}
					break;

				case 0x24:
					VertexPipelineSubmitCmd();
					CurVertex[0] = (short)((entry.Param & 0x000003FF) << 6);
					CurVertex[1] = (short)((entry.Param & 0x000FFC00) >> 4);
					CurVertex[2] = (short)((entry.Param & 0x3FF00000) >> 14);
					SubmitVertex();
					break;

				case 0x25:
					VertexPipelineSubmitCmd();
					CurVertex[0] = (short)(entry.Param & 0xFFFF);
					CurVertex[1] = (short)(entry.Param >> 16);
					SubmitVertex();
					break;

				case 0x26:
					VertexPipelineSubmitCmd();
					CurVertex[0] = (short)(entry.Param & 0xFFFF);
					CurVertex[2] = (short)(entry.Param >> 16);
					SubmitVertex();
					break;

				case 0x27:
					VertexPipelineSubmitCmd();
					CurVertex[1] = (short)(entry.Param & 0xFFFF);
					CurVertex[2] = (short)(entry.Param >> 16);
					SubmitVertex();
					break;

				case 0x28:
					VertexPipelineSubmitCmd();
					CurVertex[0] += (short)((short)((entry.Param & 0x000003FF) << 6) >> 6);
					CurVertex[1] += (short)((short)((entry.Param & 0x000FFC00) >> 4) >> 6);
					CurVertex[2] += (short)((short)((entry.Param & 0x3FF00000) >> 14) >> 6);
					SubmitVertex();
					break;

				case 0x29:
					VertexPipelineCmdDelayed8();
					PolygonAttr = entry.Param;
					break;

				case 0x2A:
					VertexPipelineCmdDelayed8();
					TexParam = entry.Param;
					break;

				case 0x2B:
					VertexPipelineCmdDelayed8();
					TexPalette = entry.Param & 0x1FFF;
					break;

				case 0x30:
					VertexPipelineCmdDelayed6();
					MatDiffuse[0] = (byte)(entry.Param & 0x1F);
					MatDiffuse[1] = (byte)((entry.Param >> 5) & 0x1F);
					MatDiffuse[2] = (byte)((entry.Param >> 10) & 0x1F);
					MatAmbient[0] = (byte)((entry.Param >> 16) & 0x1F);
					MatAmbient[1] = (byte)((entry.Param >> 21) & 0x1F);
					MatAmbient[2] = (byte)((entry.Param >> 26) & 0x1F);
					if ( (entry.Param & 0x8000) != 0 )
					{
						VertexColor[0] = MatDiffuse[0];
						VertexColor[1] = MatDiffuse[1];
						VertexColor[2] = MatDiffuse[2];
					}
					AddCycles( 3 );
					break;

				case 0x31:
					VertexPipelineCmdDelayed6();
					MatSpecular[0] = (byte)(entry.Param & 0x1F);
					MatSpecular[1] = (byte)((entry.Param >> 5) & 0x1F);
					MatSpecular[2] = (byte)((entry.Param >> 10) & 0x1F);
					MatEmission[0] = (byte)((entry.Param >> 16) & 0x1F);
					MatEmission[1] = (byte)((entry.Param >> 21) & 0x1F);
					MatEmission[2] = (byte)((entry.Param >> 26) & 0x1F);
					UseShininessTable = (entry.Param & 0x8000) != 0;
					AddCycles( 3 );
					break;

				case 0x32:
					StallPolygonPipeline( 8 + 1, 2 );
					{
						uint l = entry.Param >> 30;
						short d0 = (short)((short)((entry.Param & 0x000003FF) << 6) >> 6);
						short d1 = (short)((short)((entry.Param & 0x000FFC00) >> 4) >> 6);
						short d2 = (short)((short)((entry.Param & 0x3FF00000) >> 14) >> 6);
						LightDirection[l, 0] = (short)((-((d0 * VecMatrix[0] + d1 * VecMatrix[4] + d2 * VecMatrix[8]) >> 12) << 21) >> 21);
						LightDirection[l, 1] = (short)((-((d0 * VecMatrix[1] + d1 * VecMatrix[5] + d2 * VecMatrix[9]) >> 12) << 21) >> 21);
						LightDirection[l, 2] = (short)((-((d0 * VecMatrix[2] + d1 * VecMatrix[6] + d2 * VecMatrix[10]) >> 12) << 21) >> 21);
						int den = -(((d0 * VecMatrix[2] + d1 * VecMatrix[6] + d2 * VecMatrix[10]) << 9) >> 21) + (1 << 9);

						if ( den == 0 ) SpecRecip[l] = 0;
						else SpecRecip[l] = (1 << 18) / den;
					}
					AddCycles( 5 );
					break;

				case 0x33:
					VertexPipelineCmdDelayed8();
					{
						uint l = entry.Param >> 30;
						LightColor[l, 0] = (byte)(entry.Param & 0x1F);
						LightColor[l, 1] = (byte)((entry.Param >> 5) & 0x1F);
						LightColor[l, 2] = (byte)((entry.Param >> 10) & 0x1F);
					}
					AddCycles( 1 );
					break;

				case 0x40:
					StallPolygonPipeline( 1, 0 );
					PolygonMode = entry.Param & 0x3;
					VertexNum = 0;
					VertexNumInPoly = 0;
					NumConsecutivePolygons = 0;
					LastStripPolygon = -1;
					CurPolygonAttr = PolygonAttr;
					break;

				case 0x41:
					VertexPipelineCmdDelayed8();
					break;

				case 0x50:
					VertexPipelineCmdDelayed4();
					FlushRequest = 1;
					FlushAttributes = entry.Param & 0x3;
					CycleCount = 325;
					VertexPipeline = 0;
					NormalPipeline = 0;
					PolygonPipeline = 0;
					VertexSlotCounter = 0;
					VertexSlotsFree = 1;
					break;

				case 0x60:
					VertexPipelineCmdDelayed8();
					Viewport[0] = entry.Param & 0xFF;
					Viewport[1] = (191 - ((entry.Param >> 8) & 0xFF)) & 0xFF;
					Viewport[2] = (entry.Param >> 16) & 0xFF;
					Viewport[3] = (191 - (entry.Param >> 24)) & 0xFF;
					Viewport[4] = (Viewport[2] - Viewport[0] + 1) & 0x1FF;
					Viewport[5] = (Viewport[1] - Viewport[3] + 1) & 0xFF;
					break;

				case 0x72:
					VertexPipelineCmdDelayed6();
					NumTestCommands--;
					VecTest( entry.Param );
					break;

				default:
					VertexPipelineCmdDelayed4();
					break;
			}
		}
		else
		{
			ExecParams[ExecParamCount] = (int)entry.Param;
			ExecParamCount++;

			if ( ExecParamCount == 1 )
			{
				switch ( entry.Command )
				{
					case 0x23: VertexPipelineSubmitCmd(); break;
					case 0x34:
					case 0x71:
						VertexPipelineCmdDelayed8();
						break;
					case 0x70: StallPolygonPipeline( 10 + 1, 0 ); break;
					default: VertexPipelineCmdDelayed4(); break;
				}
			}
			else
			{
				AddCycles( 1 );

				if ( ExecParamCount >= paramsRequiredCount )
				{
					ExecParamCount = 0;

					switch ( entry.Command )
					{
						case 0x16:
							if ( MatrixMode == 0 )
							{
								MatrixLoad4x4( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 18 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixLoad4x4( TexMatrix, ExecParams );
								AddCycles( 10 );
							}
							else
							{
								MatrixLoad4x4( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
									MatrixLoad4x4( VecMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 18 );
							}
							break;

						case 0x17:
							if ( MatrixMode == 0 )
							{
								MatrixLoad4x3( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 18 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixLoad4x3( TexMatrix, ExecParams );
								AddCycles( 7 );
							}
							else
							{
								MatrixLoad4x3( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
									MatrixLoad4x3( VecMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 18 );
							}
							break;

						case 0x18:
							if ( MatrixMode == 0 )
							{
								MatrixMult4x4( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 16 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixMult4x4( TexMatrix, ExecParams );
								AddCycles( 33 - 16 );
							}
							else
							{
								MatrixMult4x4( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
								{
									MatrixMult4x4( VecMatrix, ExecParams );
									AddCycles( 35 + 30 - 16 );
								}
								else AddCycles( 35 - 16 );
								ClipMatrixDirty = true;
							}
							break;

						case 0x19:
							if ( MatrixMode == 0 )
							{
								MatrixMult4x3( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 12 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixMult4x3( TexMatrix, ExecParams );
								AddCycles( 33 - 12 );
							}
							else
							{
								MatrixMult4x3( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
								{
									MatrixMult4x3( VecMatrix, ExecParams );
									AddCycles( 35 + 30 - 12 );
								}
								else AddCycles( 35 - 12 );
								ClipMatrixDirty = true;
							}
							break;

						case 0x1A:
							if ( MatrixMode == 0 )
							{
								MatrixMult3x3( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 9 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixMult3x3( TexMatrix, ExecParams );
								AddCycles( 33 - 9 );
							}
							else
							{
								MatrixMult3x3( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
								{
									MatrixMult3x3( VecMatrix, ExecParams );
									AddCycles( 35 + 30 - 9 );
								}
								else AddCycles( 35 - 9 );
								ClipMatrixDirty = true;
							}
							break;

						case 0x1B:
							if ( MatrixMode == 0 )
							{
								MatrixScale( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 3 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixScale( TexMatrix, ExecParams );
								AddCycles( 33 - 3 );
							}
							else
							{
								MatrixScale( PosMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 3 );
							}
							break;

						case 0x1C:
							if ( MatrixMode == 0 )
							{
								MatrixTranslate( ProjMatrix, ExecParams );
								ClipMatrixDirty = true;
								AddCycles( 35 - 3 );
							}
							else if ( MatrixMode == 3 )
							{
								MatrixTranslate( TexMatrix, ExecParams );
								AddCycles( 33 - 3 );
							}
							else
							{
								MatrixTranslate( PosMatrix, ExecParams );
								if ( MatrixMode == 2 )
								{
									MatrixTranslate( VecMatrix, ExecParams );
									AddCycles( 35 + 30 - 3 );
								}
								else AddCycles( 35 - 3 );
								ClipMatrixDirty = true;
							}
							break;

						case 0x23:
							CurVertex[0] = (short)(ExecParams[0] & 0xFFFF);
							CurVertex[1] = (short)((uint)ExecParams[0] >> 16);
							CurVertex[2] = (short)(ExecParams[1] & 0xFFFF);
							SubmitVertex();
							break;

						case 0x34:
							for ( int i = 0; i < 128; i += 4 )
							{
								uint val = (uint)ExecParams[i >> 2];
								ShininessTable[i + 0] = (byte)(val & 0xFF);
								ShininessTable[i + 1] = (byte)((val >> 8) & 0xFF);
								ShininessTable[i + 2] = (byte)((val >> 16) & 0xFF);
								ShininessTable[i + 3] = (byte)(val >> 24);
							}
							break;

						case 0x71:
							NumTestCommands -= 2;
							CurVertex[0] = (short)(ExecParams[0] & 0xFFFF);
							CurVertex[1] = (short)((uint)ExecParams[0] >> 16);
							CurVertex[2] = (short)(ExecParams[1] & 0xFFFF);
							PosTest();
							break;

						case 0x70:
							NumTestCommands -= 3;
							BoxTest( ExecParams );
							break;
					}
				}
			}
		}
	}

	private static void MatrixCopy( int[] dst, int[] src )
	{
		for ( int i = 0; i < 16; i++ ) dst[i] = src[i];
	}

	private static void MatrixCopyToStack( int[,] stack, int idx, int[] src )
	{
		for ( int i = 0; i < 16; i++ ) stack[idx, i] = src[i];
	}

	private static void MatrixCopyFromStack( int[] dst, int[,] stack, int idx )
	{
		for ( int i = 0; i < 16; i++ ) dst[i] = stack[idx, i];
	}
}
