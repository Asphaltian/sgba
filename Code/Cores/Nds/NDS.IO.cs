namespace Emulotl.Nds;

public sealed partial class NDS
{
	private static bool IsGpu2DA( uint addr ) => addr >= 0x04000000 && addr < 0x04000060;
	private static bool IsGpu2DB( uint addr ) => addr >= 0x04001000 && addr < 0x04001060;
	private static bool IsGpu3D( uint addr ) => (addr >= 0x04000060 && addr < 0x04000064) || (addr >= 0x04000320 && addr < 0x040006A4);

	public byte ARM9IORead8( uint addr )
	{
		switch ( addr )
		{
			case 0x04000130: return (byte)KeyInput;
			case 0x04000131: return (byte)(KeyInput >> 8);
			case 0x04000240: return GPU.VRAMCNT[0];
			case 0x04000241: return GPU.VRAMCNT[1];
			case 0x04000242: return GPU.VRAMCNT[2];
			case 0x04000243: return GPU.VRAMCNT[3];
			case 0x04000244: return GPU.VRAMCNT[4];
			case 0x04000245: return GPU.VRAMCNT[5];
			case 0x04000246: return GPU.VRAMCNT[6];
			case 0x04000247: return WRAMCnt;
			case 0x04000248: return GPU.VRAMCNT[7];
			case 0x04000249: return GPU.VRAMCNT[8];
			case 0x04000300: return PostFlag9;
		}
		if ( IsGpu2DA( addr ) ) return GPU.GPU2D_A.Read8( addr );
		if ( IsGpu2DB( addr ) ) return GPU.GPU2D_B.Read8( addr );
		if ( IsGpu3D( addr ) ) return GPU3D.Read8( addr );
		return (byte)(ARM9IORead32( addr & ~3u ) >> (int)((addr & 3) * 8));
	}

	public ushort ARM9IORead16( uint addr )
	{
		switch ( addr )
		{
			case 0x04000004: return GPU.DispStat[0];
			case 0x04000006: return GPU.VCount;
			case 0x040000B8: return (ushort)(DMAs[0].Cnt & 0xFFFF);
			case 0x040000BA: return (ushort)(DMAs[0].Cnt >> 16);
			case 0x040000C4: return (ushort)(DMAs[1].Cnt & 0xFFFF);
			case 0x040000C6: return (ushort)(DMAs[1].Cnt >> 16);
			case 0x040000D0: return (ushort)(DMAs[2].Cnt & 0xFFFF);
			case 0x040000D2: return (ushort)(DMAs[2].Cnt >> 16);
			case 0x040000DC: return (ushort)(DMAs[3].Cnt & 0xFFFF);
			case 0x040000DE: return (ushort)(DMAs[3].Cnt >> 16);
			case 0x04000100: return TimerGetCounter( 0 );
			case 0x04000102: return Timers[0].Cnt;
			case 0x0400006C: return GPU.MasterBrightnessA;
			case 0x0400106C: return GPU.MasterBrightnessB;
			case 0x04000064: return (ushort)GPU.CaptureCnt;
			case 0x04000066: return (ushort)(GPU.CaptureCnt >> 16);
			case 0x040001A0: return AuxSpiCnt[0];
			case 0x040001A2: return ReadAuxSpiData( 0 );
			case 0x04000280: return DivCnt;
			case 0x040002B0: return SqrtCnt;
			case 0x04000180: return IPCSync9;
			case 0x04000184:
				{
					ushort val = IPCFIFOCnt9;
					if ( IPCFIFO9.IsEmpty() ) val |= 0x0001;
					else if ( IPCFIFO9.IsFull() ) val |= 0x0002;
					if ( IPCFIFO7.IsEmpty() ) val |= 0x0100;
					else if ( IPCFIFO7.IsFull() ) val |= 0x0200;
					return val;
				}
			case 0x04000104: return TimerGetCounter( 1 );
			case 0x04000106: return Timers[1].Cnt;
			case 0x04000108: return TimerGetCounter( 2 );
			case 0x0400010A: return Timers[2].Cnt;
			case 0x0400010C: return TimerGetCounter( 3 );
			case 0x0400010E: return Timers[3].Cnt;
			case 0x04000130: return KeyInput;
			case 0x04000208: return (ushort)IME[0];
			case 0x04000304: return (ushort)PowerControl9;
		}
		if ( IsGpu2DA( addr ) ) return GPU.GPU2D_A.Read16( addr );
		if ( IsGpu2DB( addr ) ) return GPU.GPU2D_B.Read16( addr );
		if ( IsGpu3D( addr ) ) return GPU3D.Read16( addr );
		return (ushort)(ARM9IORead32( addr & ~3u ) >> (int)((addr & 2) * 8));
	}

	public uint ARM9IORead32( uint addr )
	{
		switch ( addr )
		{
			case 0x04000004: return (uint)(GPU.DispStat[0] | (GPU.VCount << 16));
			case 0x04000064: return GPU.CaptureCnt;
			case 0x040000B0: return DMAs[0].SrcAddr;
			case 0x040000B4: return DMAs[0].DstAddr;
			case 0x040000B8: return DMAs[0].Cnt;
			case 0x040000BC: return DMAs[1].SrcAddr;
			case 0x040000C0: return DMAs[1].DstAddr;
			case 0x040000C4: return DMAs[1].Cnt;
			case 0x040000C8: return DMAs[2].SrcAddr;
			case 0x040000CC: return DMAs[2].DstAddr;
			case 0x040000D0: return DMAs[2].Cnt;
			case 0x040000D4: return DMAs[3].SrcAddr;
			case 0x040000D8: return DMAs[3].DstAddr;
			case 0x040000DC: return DMAs[3].Cnt;
			case 0x040000E0: return DMA9Fill[0];
			case 0x040000E4: return DMA9Fill[1];
			case 0x040000E8: return DMA9Fill[2];
			case 0x040000EC: return DMA9Fill[3];
			case 0x04000100: return (uint)(TimerGetCounter( 0 ) | (Timers[0].Cnt << 16));
			case 0x04000104: return (uint)(TimerGetCounter( 1 ) | (Timers[1].Cnt << 16));
			case 0x04000108: return (uint)(TimerGetCounter( 2 ) | (Timers[2].Cnt << 16));
			case 0x0400010C: return (uint)(TimerGetCounter( 3 ) | (Timers[3].Cnt << 16));
			case 0x04000180: return IPCSync9;
			case 0x04000184: return ARM9IORead16( addr );
			case 0x04000240: return (uint)(GPU.VRAMCNT[0] | (GPU.VRAMCNT[1] << 8) | (GPU.VRAMCNT[2] << 16) | (GPU.VRAMCNT[3] << 24));
			case 0x04000244: return (uint)(GPU.VRAMCNT[4] | (GPU.VRAMCNT[5] << 8) | (GPU.VRAMCNT[6] << 16) | (WRAMCnt << 24));
			case 0x04000248: return (uint)(GPU.VRAMCNT[7] | (GPU.VRAMCNT[8] << 8));
			case 0x04000208: return IME[0];
			case 0x04000210: return IE[0];
			case 0x04000214: return IF[0];
			case 0x040001A4: return ROMCnt;
			case 0x04100010: return ReadROMData();
			case 0x04000280: return DivCnt;
			case 0x04000290: return DivNumerator[0];
			case 0x04000294: return DivNumerator[1];
			case 0x04000298: return DivDenominator[0];
			case 0x0400029C: return DivDenominator[1];
			case 0x040002A0: return DivQuotient[0];
			case 0x040002A4: return DivQuotient[1];
			case 0x040002A8: return DivRemainder[0];
			case 0x040002AC: return DivRemainder[1];
			case 0x040002B0: return SqrtCnt;
			case 0x040002B4: return SqrtRes;
			case 0x040002B8: return SqrtVal[0];
			case 0x040002BC: return SqrtVal[1];
			case 0x04000304: return PowerControl9;
			case 0x04100000:
				if ( (IPCFIFOCnt9 & 0x8000) != 0 )
				{
					uint ret;
					if ( IPCFIFO7.IsEmpty() )
					{
						IPCFIFOCnt9 |= 0x4000;
						ret = IPCFIFO7.Peek();
					}
					else
					{
						ret = IPCFIFO7.Read();

						if ( IPCFIFO7.IsEmpty() && (IPCFIFOCnt7 & 0x0004) != 0 )
							SetIRQ( 1, IRQ.IPCSendDone );
					}
					return ret;
				}
				return IPCFIFO7.Peek();
		}
		if ( IsGpu2DA( addr ) ) return GPU.GPU2D_A.Read32( addr );
		if ( IsGpu2DB( addr ) ) return GPU.GPU2D_B.Read32( addr );
		if ( IsGpu3D( addr ) ) return GPU3D.Read32( addr );
		return 0;
	}

	public void ARM9IOWrite8( uint addr, byte val )
	{
		switch ( addr )
		{
			case 0x04000240: GPU.MapVRAM_AB( 0, val ); return;
			case 0x04000241: GPU.MapVRAM_AB( 1, val ); return;
			case 0x04000242: GPU.MapVRAM_CD( 2, val ); return;
			case 0x04000243: GPU.MapVRAM_CD( 3, val ); return;
			case 0x04000244: GPU.MapVRAM_E( 4, val ); return;
			case 0x04000245: GPU.MapVRAM_FG( 5, val ); return;
			case 0x04000246: GPU.MapVRAM_FG( 6, val ); return;
			case 0x04000247: MapSharedWRAM( val ); return;
			case 0x04000248: GPU.MapVRAM_H( 7, val ); return;
			case 0x04000249: GPU.MapVRAM_I( 8, val ); return;
			case 0x04000181:
				IPCSync7 &= 0xFFF0;
				IPCSync7 |= (ushort)(val & 0x0F);
				IPCSync9 &= 0xB0FF;
				IPCSync9 |= (ushort)((val & 0x4F) << 8);
				if ( (val & 0x20) != 0 && (IPCSync7 & 0x4000) != 0 )
					SetIRQ( 1, IRQ.IPCSync );
				return;
			case 0x040001A0: WriteAuxSpiCnt( 0, val, 0x00FF ); return;
			case 0x040001A1: WriteAuxSpiCnt( 0, (ushort)(val << 8), 0xFF00 ); return;
			case 0x040001A2: WriteAuxSpiData( 0, val ); return;
			case 0x04000300: PostFlag9 = (byte)((PostFlag9 & 0x1) | (val & 0x3)); return;
		}
		if ( addr >= 0x040001A8 && addr <= 0x040001AF ) { WriteROMCommand( (int)(addr - 0x040001A8), val ); return; }
		if ( IsGpu2DA( addr ) ) GPU.GPU2D_A.Write8( addr, val );
		else if ( IsGpu2DB( addr ) ) GPU.GPU2D_B.Write8( addr, val );
		else if ( IsGpu3D( addr ) ) GPU3D.Write8( addr, val );
	}

	public void ARM9IOWrite16( uint addr, ushort val )
	{
		switch ( addr )
		{
			case 0x04000004: GPU.SetDispStat( 0, val, 0xFFFF ); return;
			case 0x04000006: GPU.SetVCount( val, 0xFFFF ); return;
			case 0x0400006C: GPU.MasterBrightnessA = (ushort)(val & 0xC01F); return;
			case 0x0400106C: GPU.MasterBrightnessB = (ushort)(val & 0xC01F); return;
			case 0x04000064: GPU.CaptureCnt = (GPU.CaptureCnt & 0xFFFF0000) | (uint)(val & 0x1F1F); return;
			case 0x04000066: GPU.CaptureCnt = (GPU.CaptureCnt & 0xFFFF) | ((uint)(val & 0xEF3F) << 16); return;
			case 0x040001A0: WriteAuxSpiCnt( 0, val, 0xFFFF ); return;
			case 0x040001A2: WriteAuxSpiData( 0, (byte)val ); return;
			case 0x040001A4: WriteROMCnt( 0, val, 0x0000FFFF ); return;
			case 0x040001A6: WriteROMCnt( 0, (uint)val << 16, 0xFFFF0000 ); return;
			case 0x040001A8: WriteROMCommand( 0, (byte)val ); WriteROMCommand( 1, (byte)(val >> 8) ); return;
			case 0x040001AA: WriteROMCommand( 2, (byte)val ); WriteROMCommand( 3, (byte)(val >> 8) ); return;
			case 0x040001AC: WriteROMCommand( 4, (byte)val ); WriteROMCommand( 5, (byte)(val >> 8) ); return;
			case 0x040001AE: WriteROMCommand( 6, (byte)val ); WriteROMCommand( 7, (byte)(val >> 8) ); return;
			case 0x040000B8: DMAs[0].WriteCnt( (DMAs[0].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000BA: DMAs[0].WriteCnt( (DMAs[0].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000C4: DMAs[1].WriteCnt( (DMAs[1].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000C6: DMAs[1].WriteCnt( (DMAs[1].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000D0: DMAs[2].WriteCnt( (DMAs[2].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000D2: DMAs[2].WriteCnt( (DMAs[2].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000DC: DMAs[3].WriteCnt( (DMAs[3].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000DE: DMAs[3].WriteCnt( (DMAs[3].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x04000180:
				IPCSync7 &= 0xFFF0;
				IPCSync7 |= (ushort)((val & 0x0F00) >> 8);
				IPCSync9 &= 0xB0FF;
				IPCSync9 |= (ushort)(val & 0x4F00);
				if ( (val & 0x2000) != 0 && (IPCSync7 & 0x4000) != 0 )
					SetIRQ( 1, IRQ.IPCSync );
				return;
			case 0x04000184:
				if ( (val & 0x0008) != 0 )
					IPCFIFO9.Clear();
				if ( (val & 0x0004) != 0 && (IPCFIFOCnt9 & 0x0004) == 0 && IPCFIFO9.IsEmpty() )
					SetIRQ( 0, IRQ.IPCSendDone );
				if ( (val & 0x0400) != 0 && (IPCFIFOCnt9 & 0x0400) == 0 && !IPCFIFO7.IsEmpty() )
					SetIRQ( 0, IRQ.IPCRecv );
				if ( (val & 0x4000) != 0 )
					IPCFIFOCnt9 &= 0xBFFF;
				IPCFIFOCnt9 = (ushort)((val & 0x8404) | (IPCFIFOCnt9 & 0x4000));
				return;
			case 0x04000188:
				ARM9IOWrite32( addr, (uint)(val | (val << 16)) );
				return;
			case 0x04000240:
				GPU.MapVRAM_AB( 0, (byte)val );
				GPU.MapVRAM_AB( 1, (byte)(val >> 8) );
				return;
			case 0x04000242:
				GPU.MapVRAM_CD( 2, (byte)val );
				GPU.MapVRAM_CD( 3, (byte)(val >> 8) );
				return;
			case 0x04000244:
				GPU.MapVRAM_E( 4, (byte)val );
				GPU.MapVRAM_FG( 5, (byte)(val >> 8) );
				return;
			case 0x04000246:
				GPU.MapVRAM_FG( 6, (byte)val );
				MapSharedWRAM( (byte)(val >> 8) );
				return;
			case 0x04000248:
				GPU.MapVRAM_H( 7, (byte)val );
				GPU.MapVRAM_I( 8, (byte)(val >> 8) );
				return;
			case 0x04000100: Timers[0].Reload = val; return;
			case 0x04000102: TimerStart( 0, val ); return;
			case 0x04000104: Timers[1].Reload = val; return;
			case 0x04000106: TimerStart( 1, val ); return;
			case 0x04000108: Timers[2].Reload = val; return;
			case 0x0400010A: TimerStart( 2, val ); return;
			case 0x0400010C: Timers[3].Reload = val; return;
			case 0x0400010E: TimerStart( 3, val ); return;
			case 0x04000208: IME[0] = (uint)(val & 0x1); UpdateIRQ( 0 ); return;
			case 0x04000280: DivCnt = val; StartDiv(); return;
			case 0x040002B0: SqrtCnt = val; StartSqrt(); return;
			case 0x04000304: PowerControl9 = (uint)(val & 0x820F); GPU.SetPowerCnt( PowerControl9 ); return;
		}
		if ( IsGpu2DA( addr ) ) GPU.GPU2D_A.Write16( addr, val );
		else if ( IsGpu2DB( addr ) ) GPU.GPU2D_B.Write16( addr, val );
		else if ( IsGpu3D( addr ) ) GPU3D.Write16( addr, val );
	}

	public void ARM9IOWrite32( uint addr, uint val )
	{
		switch ( addr )
		{
			case 0x04000004:
				GPU.SetDispStat( 0, (ushort)(val & 0xFFFF), 0xFFFF );
				GPU.SetVCount( (ushort)(val >> 16), 0xFFFF );
				return;
			case 0x040000B0: DMAs[0].SrcAddr = val; return;
			case 0x040000B4: DMAs[0].DstAddr = val; return;
			case 0x040000B8: DMAs[0].WriteCnt( val ); return;
			case 0x040000BC: DMAs[1].SrcAddr = val; return;
			case 0x040000C0: DMAs[1].DstAddr = val; return;
			case 0x040000C4: DMAs[1].WriteCnt( val ); return;
			case 0x040000C8: DMAs[2].SrcAddr = val; return;
			case 0x040000CC: DMAs[2].DstAddr = val; return;
			case 0x040000D0: DMAs[2].WriteCnt( val ); return;
			case 0x040000D4: DMAs[3].SrcAddr = val; return;
			case 0x040000D8: DMAs[3].DstAddr = val; return;
			case 0x040000DC: DMAs[3].WriteCnt( val ); return;
			case 0x040000E0: DMA9Fill[0] = val; return;
			case 0x040000E4: DMA9Fill[1] = val; return;
			case 0x040000E8: DMA9Fill[2] = val; return;
			case 0x040000EC: DMA9Fill[3] = val; return;
			case 0x0400006C: GPU.MasterBrightnessA = (ushort)(val & 0xC01F); return;
			case 0x0400106C: GPU.MasterBrightnessB = (ushort)(val & 0xC01F); return;
			case 0x04000240:
				GPU.MapVRAM_AB( 0, (byte)val );
				GPU.MapVRAM_AB( 1, (byte)(val >> 8) );
				GPU.MapVRAM_CD( 2, (byte)(val >> 16) );
				GPU.MapVRAM_CD( 3, (byte)(val >> 24) );
				return;
			case 0x04000244:
				GPU.MapVRAM_E( 4, (byte)val );
				GPU.MapVRAM_FG( 5, (byte)(val >> 8) );
				GPU.MapVRAM_FG( 6, (byte)(val >> 16) );
				MapSharedWRAM( (byte)(val >> 24) );
				return;
			case 0x04000248:
				GPU.MapVRAM_H( 7, (byte)val );
				GPU.MapVRAM_I( 8, (byte)(val >> 8) );
				return;
			case 0x04000180:
			case 0x04000184:
				ARM9IOWrite16( addr, (ushort)val );
				return;
			case 0x04000188:
				if ( (IPCFIFOCnt9 & 0x8000) != 0 )
				{
					if ( IPCFIFO9.IsFull() )
						IPCFIFOCnt9 |= 0x4000;
					else
					{
						bool wasempty = IPCFIFO9.IsEmpty();
						IPCFIFO9.Write( val );
						if ( (IPCFIFOCnt7 & 0x0400) != 0 && wasempty )
							SetIRQ( 1, IRQ.IPCRecv );
					}
				}
				return;
			case 0x04000100:
				Timers[0].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 0, (ushort)(val >> 16) );
				return;
			case 0x04000104:
				Timers[1].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 1, (ushort)(val >> 16) );
				return;
			case 0x04000108:
				Timers[2].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 2, (ushort)(val >> 16) );
				return;
			case 0x0400010C:
				Timers[3].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 3, (ushort)(val >> 16) );
				return;
			case 0x04000064: GPU.CaptureCnt = val & 0xEF3F1F1F; return;
			case 0x04000208: IME[0] = val & 0x1; UpdateIRQ( 0 ); return;
			case 0x04000210: IE[0] = val; UpdateIRQ( 0 ); return;
			case 0x04000214: IF[0] &= ~val; UpdateIRQ( 0 ); return;
			case 0x040001A0:
				WriteAuxSpiCnt( 0, (ushort)val, 0xFFFF );
				return;
			case 0x040001A4: WriteROMCnt( 0, val, 0xFFFFFFFF ); return;
			case 0x040001A8:
				WriteROMCommand( 0, (byte)val );
				WriteROMCommand( 1, (byte)(val >> 8) );
				WriteROMCommand( 2, (byte)(val >> 16) );
				WriteROMCommand( 3, (byte)(val >> 24) );
				return;
			case 0x040001AC:
				WriteROMCommand( 4, (byte)val );
				WriteROMCommand( 5, (byte)(val >> 8) );
				WriteROMCommand( 6, (byte)(val >> 16) );
				WriteROMCommand( 7, (byte)(val >> 24) );
				return;
			case 0x04100010: WriteROMData( val, 0xFFFFFFFF ); return;
			case 0x04000280: DivCnt = (ushort)val; StartDiv(); return;
			case 0x04000290: DivNumerator[0] = val; StartDiv(); return;
			case 0x04000294: DivNumerator[1] = val; StartDiv(); return;
			case 0x04000298: DivDenominator[0] = val; StartDiv(); return;
			case 0x0400029C: DivDenominator[1] = val; StartDiv(); return;
			case 0x040002B0: SqrtCnt = (ushort)val; StartSqrt(); return;
			case 0x040002B8: SqrtVal[0] = val; StartSqrt(); return;
			case 0x040002BC: SqrtVal[1] = val; StartSqrt(); return;
			case 0x04000304: PowerControl9 = val & 0x820F; GPU.SetPowerCnt( PowerControl9 ); return;
		}
		if ( IsGpu2DA( addr ) ) GPU.GPU2D_A.Write32( addr, val );
		else if ( IsGpu2DB( addr ) ) GPU.GPU2D_B.Write32( addr, val );
		else if ( IsGpu3D( addr ) ) GPU3D.Write32( addr, val );
	}

	public byte ARM7IORead8( uint addr )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
			return SPU.Read8( addr );
		switch ( addr )
		{
			case 0x04000130: return (byte)KeyInput;
			case 0x04000131: return (byte)(KeyInput >> 8);
			case 0x04000138: return (byte)RtcRead();
			case 0x040001C2: return ReadSpiData();
			case 0x04000240: return GPU.VRAMSTAT;
			case 0x04000241: return WRAMCnt;
			case 0x04000300: return PostFlag7;
		}
		return (byte)(ARM7IORead32( addr & ~3u ) >> (int)((addr & 3) * 8));
	}

	public ushort ARM7IORead16( uint addr )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
			return SPU.Read16( addr );
		switch ( addr )
		{
			case 0x04000004: return GPU.DispStat[1];
			case 0x04000006: return GPU.VCount;
			case 0x040000B8: return (ushort)(DMAs[4].Cnt & 0xFFFF);
			case 0x040000BA: return (ushort)(DMAs[4].Cnt >> 16);
			case 0x040000C4: return (ushort)(DMAs[5].Cnt & 0xFFFF);
			case 0x040000C6: return (ushort)(DMAs[5].Cnt >> 16);
			case 0x040000D0: return (ushort)(DMAs[6].Cnt & 0xFFFF);
			case 0x040000D2: return (ushort)(DMAs[6].Cnt >> 16);
			case 0x040000DC: return (ushort)(DMAs[7].Cnt & 0xFFFF);
			case 0x040000DE: return (ushort)(DMAs[7].Cnt >> 16);
			case 0x04000180: return IPCSync7;
			case 0x04000184:
				{
					ushort val = IPCFIFOCnt7;
					if ( IPCFIFO7.IsEmpty() ) val |= 0x0001;
					else if ( IPCFIFO7.IsFull() ) val |= 0x0002;
					if ( IPCFIFO9.IsEmpty() ) val |= 0x0100;
					else if ( IPCFIFO9.IsFull() ) val |= 0x0200;
					return val;
				}
			case 0x04000100: return TimerGetCounter( 4 );
			case 0x04000102: return Timers[4].Cnt;
			case 0x04000104: return TimerGetCounter( 5 );
			case 0x04000106: return Timers[5].Cnt;
			case 0x04000108: return TimerGetCounter( 6 );
			case 0x0400010A: return Timers[6].Cnt;
			case 0x0400010C: return TimerGetCounter( 7 );
			case 0x0400010E: return Timers[7].Cnt;
			case 0x04000130: return KeyInput;
			case 0x04000136: return ExtKeyIn;
			case 0x04000138: return RtcRead();
			case 0x040001A0: return AuxSpiCnt[1];
			case 0x040001A2: return ReadAuxSpiData( 1 );
			case 0x040001C0: return SpiCnt;
			case 0x040001C2: return ReadSpiData();
			case 0x04000208: return (ushort)IME[1];
			case 0x04000304: return (ushort)PowerControl7;
		}
		return (ushort)(ARM7IORead32( addr & ~3u ) >> (int)((addr & 2) * 8));
	}

	public uint ARM7IORead32( uint addr )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
			return SPU.Read32( addr );
		switch ( addr )
		{
			case 0x04000004: return (uint)(GPU.DispStat[1] | (GPU.VCount << 16));
			case 0x040000B0: return DMAs[4].SrcAddr;
			case 0x040000B4: return DMAs[4].DstAddr;
			case 0x040000B8: return DMAs[4].Cnt;
			case 0x040000BC: return DMAs[5].SrcAddr;
			case 0x040000C0: return DMAs[5].DstAddr;
			case 0x040000C4: return DMAs[5].Cnt;
			case 0x040000C8: return DMAs[6].SrcAddr;
			case 0x040000CC: return DMAs[6].DstAddr;
			case 0x040000D0: return DMAs[6].Cnt;
			case 0x040000D4: return DMAs[7].SrcAddr;
			case 0x040000D8: return DMAs[7].DstAddr;
			case 0x040000DC: return DMAs[7].Cnt;
			case 0x04000100: return (uint)(TimerGetCounter( 4 ) | (Timers[4].Cnt << 16));
			case 0x04000104: return (uint)(TimerGetCounter( 5 ) | (Timers[5].Cnt << 16));
			case 0x04000108: return (uint)(TimerGetCounter( 6 ) | (Timers[6].Cnt << 16));
			case 0x0400010C: return (uint)(TimerGetCounter( 7 ) | (Timers[7].Cnt << 16));
			case 0x040001A4: return ROMCnt;
			case 0x04100010: return ReadROMData();
			case 0x04000180: return IPCSync7;
			case 0x04000184: return ARM7IORead16( addr );
			case 0x04000208: return IME[1];
			case 0x04000210: return IE[1];
			case 0x04000214: return IF[1];
			case 0x04100000:
				if ( (IPCFIFOCnt7 & 0x8000) != 0 )
				{
					uint ret;
					if ( IPCFIFO9.IsEmpty() )
					{
						IPCFIFOCnt7 |= 0x4000;
						ret = IPCFIFO9.Peek();
					}
					else
					{
						ret = IPCFIFO9.Read();

						if ( IPCFIFO9.IsEmpty() && (IPCFIFOCnt9 & 0x0004) != 0 )
							SetIRQ( 0, IRQ.IPCSendDone );
					}
					return ret;
				}
				return IPCFIFO9.Peek();
		}
		return 0;
	}

	public void ARM7IOWrite8( uint addr, byte val )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
		{
			SPU.Write8( addr, val );
			return;
		}
		switch ( addr )
		{
			case 0x04000181:
				IPCSync9 &= 0xFFF0;
				IPCSync9 |= (ushort)(val & 0x0F);
				IPCSync7 &= 0xB0FF;
				IPCSync7 |= (ushort)((val & 0x4F) << 8);
				if ( (val & 0x20) != 0 && (IPCSync9 & 0x4000) != 0 )
					SetIRQ( 0, IRQ.IPCSync );
				return;
			case 0x040001C2: WriteSpiData( val ); return;
			case 0x040001A0: WriteAuxSpiCnt( 1, val, 0x00FF ); return;
			case 0x040001A1: WriteAuxSpiCnt( 1, (ushort)(val << 8), 0xFF00 ); return;
			case 0x040001A2: WriteAuxSpiData( 1, val ); return;
			case 0x04000138: RtcWrite( val, true ); return;
			default:
				if ( addr >= 0x040001A8 && addr <= 0x040001AF ) WriteROMCommand( (int)(addr - 0x040001A8), val );
				return;
			case 0x04000301:
				if ( (val & 0xC0) == 0x80 ) ARM7.Halt( 1 );
				return;
			case 0x04000300:
				if ( ARM7.R[15] >= 0x4000 ) return;
				if ( (PostFlag7 & 0x01) == 0 ) PostFlag7 = (byte)(val & 0x01);
				return;
		}
	}

	public void ARM7IOWrite16( uint addr, ushort val )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
		{
			SPU.Write16( addr, val );
			return;
		}
		switch ( addr )
		{
			case 0x04000004: GPU.SetDispStat( 1, val, 0xFFFF ); return;
			case 0x04000006: GPU.SetVCount( val, 0xFFFF ); return;
			case 0x04000138: RtcWrite( val, false ); return;
			case 0x040001A0: WriteAuxSpiCnt( 1, val, 0xFFFF ); return;
			case 0x040001A2: WriteAuxSpiData( 1, (byte)val ); return;
			case 0x040001A4: WriteROMCnt( 1, val, 0x0000FFFF ); return;
			case 0x040001A6: WriteROMCnt( 1, (uint)val << 16, 0xFFFF0000 ); return;
			case 0x040001A8: WriteROMCommand( 0, (byte)val ); WriteROMCommand( 1, (byte)(val >> 8) ); return;
			case 0x040001AA: WriteROMCommand( 2, (byte)val ); WriteROMCommand( 3, (byte)(val >> 8) ); return;
			case 0x040001AC: WriteROMCommand( 4, (byte)val ); WriteROMCommand( 5, (byte)(val >> 8) ); return;
			case 0x040001AE: WriteROMCommand( 6, (byte)val ); WriteROMCommand( 7, (byte)(val >> 8) ); return;
			case 0x040001C0: WriteSpiCnt( val ); return;
			case 0x040001C2: WriteSpiData( (byte)val ); return;
			case 0x040000B8: DMAs[4].WriteCnt( (DMAs[4].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000BA: DMAs[4].WriteCnt( (DMAs[4].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000C4: DMAs[5].WriteCnt( (DMAs[5].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000C6: DMAs[5].WriteCnt( (DMAs[5].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000D0: DMAs[6].WriteCnt( (DMAs[6].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000D2: DMAs[6].WriteCnt( (DMAs[6].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x040000DC: DMAs[7].WriteCnt( (DMAs[7].Cnt & 0xFFFF0000) | val ); return;
			case 0x040000DE: DMAs[7].WriteCnt( (DMAs[7].Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
			case 0x04000180:
				IPCSync9 &= 0xFFF0;
				IPCSync9 |= (ushort)((val & 0x0F00) >> 8);
				IPCSync7 &= 0xB0FF;
				IPCSync7 |= (ushort)(val & 0x4F00);
				if ( (val & 0x2000) != 0 && (IPCSync9 & 0x4000) != 0 )
					SetIRQ( 0, IRQ.IPCSync );
				return;
			case 0x04000184:
				if ( (val & 0x0008) != 0 )
					IPCFIFO7.Clear();
				if ( (val & 0x0004) != 0 && (IPCFIFOCnt7 & 0x0004) == 0 && IPCFIFO7.IsEmpty() )
					SetIRQ( 1, IRQ.IPCSendDone );
				if ( (val & 0x0400) != 0 && (IPCFIFOCnt7 & 0x0400) == 0 && !IPCFIFO9.IsEmpty() )
					SetIRQ( 1, IRQ.IPCRecv );
				if ( (val & 0x4000) != 0 )
					IPCFIFOCnt7 &= 0xBFFF;
				IPCFIFOCnt7 = (ushort)((val & 0x8404) | (IPCFIFOCnt7 & 0x4000));
				return;
			case 0x04000188:
				ARM7IOWrite32( addr, (uint)(val | (val << 16)) );
				return;
			case 0x04000100: Timers[4].Reload = val; return;
			case 0x04000102: TimerStart( 4, val ); return;
			case 0x04000104: Timers[5].Reload = val; return;
			case 0x04000106: TimerStart( 5, val ); return;
			case 0x04000108: Timers[6].Reload = val; return;
			case 0x0400010A: TimerStart( 6, val ); return;
			case 0x0400010C: Timers[7].Reload = val; return;
			case 0x0400010E: TimerStart( 7, val ); return;
			case 0x04000208: IME[1] = (uint)(val & 0x1); UpdateIRQ( 1 ); return;
			case 0x04000304: PowerControl7 = (uint)(val & 0x0003); SPU.SetPowerCnt( PowerControl7 & 0x0001 ); Wifi.SetPowerCnt( PowerControl7 & 0x0002 ); return;
			case 0x04000308: if ( ARM7BIOSProt == 0 ) ARM7BIOSProt = (uint)(val & 0xFFFE); return;
		}
	}

	public void ARM7IOWrite32( uint addr, uint val )
	{
		if ( addr >= 0x04000400 && addr < 0x04000520 )
		{
			SPU.Write32( addr, val );
			return;
		}
		switch ( addr )
		{
			case 0x040001A0: WriteAuxSpiCnt( 1, (ushort)val, 0xFFFF ); return;
			case 0x040001A4: WriteROMCnt( 1, val, 0xFFFFFFFF ); return;
			case 0x040001A8:
				WriteROMCommand( 0, (byte)val );
				WriteROMCommand( 1, (byte)(val >> 8) );
				WriteROMCommand( 2, (byte)(val >> 16) );
				WriteROMCommand( 3, (byte)(val >> 24) );
				return;
			case 0x040001AC:
				WriteROMCommand( 4, (byte)val );
				WriteROMCommand( 5, (byte)(val >> 8) );
				WriteROMCommand( 6, (byte)(val >> 16) );
				WriteROMCommand( 7, (byte)(val >> 24) );
				return;
			case 0x04100010: WriteROMData( val, 0xFFFFFFFF ); return;
			case 0x04000004:
				GPU.SetDispStat( 1, (ushort)(val & 0xFFFF), 0xFFFF );
				GPU.SetVCount( (ushort)(val >> 16), 0xFFFF );
				return;
			case 0x04000100:
				Timers[4].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 4, (ushort)(val >> 16) );
				return;
			case 0x04000104:
				Timers[5].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 5, (ushort)(val >> 16) );
				return;
			case 0x04000108:
				Timers[6].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 6, (ushort)(val >> 16) );
				return;
			case 0x0400010C:
				Timers[7].Reload = (ushort)(val & 0xFFFF);
				TimerStart( 7, (ushort)(val >> 16) );
				return;
			case 0x04000180:
			case 0x04000184:
				ARM7IOWrite16( addr, (ushort)val );
				return;
			case 0x04000188:
				if ( (IPCFIFOCnt7 & 0x8000) != 0 )
				{
					if ( IPCFIFO7.IsFull() )
						IPCFIFOCnt7 |= 0x4000;
					else
					{
						bool wasempty = IPCFIFO7.IsEmpty();
						IPCFIFO7.Write( val );
						if ( (IPCFIFOCnt9 & 0x0400) != 0 && wasempty )
							SetIRQ( 0, IRQ.IPCRecv );
					}
				}
				return;
			case 0x04000208: IME[1] = val & 0x1; UpdateIRQ( 1 ); return;
			case 0x04000210: IE[1] = val; UpdateIRQ( 1 ); return;
			case 0x04000214: IF[1] &= ~val; UpdateIRQ( 1 ); return;
			case 0x04000304: PowerControl7 = val & 0x0003; SPU.SetPowerCnt( PowerControl7 & 0x0001 ); Wifi.SetPowerCnt( PowerControl7 & 0x0002 ); return;
		}
	}
}
