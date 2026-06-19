namespace Emulotl.Nds;

public sealed class Wifi
{
	private const int W_ID = 0x000;
	private const int W_ModeReset = 0x004;
	private const int W_ModeWEP = 0x006;
	private const int W_TXStatCnt = 0x008;
	private const int W_IF = 0x010;
	private const int W_IE = 0x012;
	private const int W_MACAddr0 = 0x018;
	private const int W_MACAddr1 = 0x01A;
	private const int W_MACAddr2 = 0x01C;
	private const int W_BSSID0 = 0x020;
	private const int W_BSSID1 = 0x022;
	private const int W_BSSID2 = 0x024;
	private const int W_AIDLow = 0x028;
	private const int W_AIDFull = 0x02A;
	private const int W_TXRetryLimit = 0x02C;
	private const int W_RXCnt = 0x030;
	private const int W_WEPCnt = 0x032;
	private const int W_TRXPower = 0x034;
	private const int W_PowerUS = 0x036;
	private const int W_PowerTX = 0x038;
	private const int W_PowerState = 0x03C;
	private const int W_PowerForce = 0x040;
	private const int W_PowerDownCtrl = 0x048;
	private const int W_Random = 0x044;
	private const int W_RXBufBegin = 0x050;
	private const int W_RXBufEnd = 0x052;
	private const int W_RXBufWriteCursor = 0x054;
	private const int W_RXBufWriteAddr = 0x056;
	private const int W_RXBufReadAddr = 0x058;
	private const int W_RXBufReadCursor = 0x05A;
	private const int W_RXBufCount = 0x05C;
	private const int W_RXBufDataRead = 0x060;
	private const int W_RXBufGapAddr = 0x062;
	private const int W_RXBufGapSize = 0x064;
	private const int W_TXBufWriteAddr = 0x068;
	private const int W_TXBufCount = 0x06C;
	private const int W_TXBufDataWrite = 0x070;
	private const int W_TXBufGapAddr = 0x074;
	private const int W_TXBufGapSize = 0x076;
	private const int W_TXSlotBeacon = 0x080;
	private const int W_TXBeaconTIM = 0x084;
	private const int W_ListenCount = 0x088;
	private const int W_BeaconInterval = 0x08C;
	private const int W_ListenInterval = 0x08E;
	private const int W_TXSlotCmd = 0x090;
	private const int W_TXSlotReply1 = 0x094;
	private const int W_TXSlotReply2 = 0x098;
	private const int W_TXSlotLoc1 = 0x0A0;
	private const int W_TXSlotLoc2 = 0x0A4;
	private const int W_TXSlotLoc3 = 0x0A8;
	private const int W_TXReqReset = 0x0AC;
	private const int W_TXReqSet = 0x0AE;
	private const int W_TXReqRead = 0x0B0;
	private const int W_TXSlotReset = 0x0B4;
	private const int W_TXBusy = 0x0B6;
	private const int W_TXStat = 0x0B8;
	private const int W_Preamble = 0x0BC;
	private const int W_CmdTotalTime = 0x0C0;
	private const int W_CmdReplyTime = 0x0C4;
	private const int W_RXFilter = 0x0D0;
	private const int W_RXLenCrop = 0x0DA;
	private const int W_RXFilter2 = 0x0E0;
	private const int W_USCountCnt = 0x0E8;
	private const int W_USCompareCnt = 0x0EA;
	private const int W_CmdCountCnt = 0x0EE;
	private const int W_USCount0 = 0x0F8;
	private const int W_USCount1 = 0x0FA;
	private const int W_USCount2 = 0x0FC;
	private const int W_USCount3 = 0x0FE;
	private const int W_USCompare0 = 0x0F0;
	private const int W_USCompare1 = 0x0F2;
	private const int W_USCompare2 = 0x0F4;
	private const int W_USCompare3 = 0x0F6;
	private const int W_ContentFree = 0x10C;
	private const int W_PreBeacon = 0x110;
	private const int W_CmdCount = 0x118;
	private const int W_BeaconCount1 = 0x11C;
	private const int W_BeaconCount2 = 0x134;
	private const int W_BBCnt = 0x158;
	private const int W_BBWrite = 0x15A;
	private const int W_BBRead = 0x15C;
	private const int W_BBBusy = 0x15E;
	private const int W_BBMode = 0x160;
	private const int W_BBPower = 0x168;
	private const int W_RFData2 = 0x17C;
	private const int W_RFData1 = 0x17E;
	private const int W_RFBusy = 0x180;
	private const int W_RFCnt = 0x184;
	private const int W_TXHeaderCnt = 0x194;
	private const int W_RFPins = 0x19C;
	private const int W_TXErrorCount = 0x1C0;
	private const int W_RXCount = 0x1C4;
	private const int W_CMDStat0 = 0x1D0;
	private const int W_TXSeqNo = 0x210;
	private const int W_RFStatus = 0x214;
	private const int W_IFSet = 0x21C;
	private const int W_RXTXAddr = 0x268;

	private readonly NDS _nds;

	private readonly byte[] RAM = new byte[0x2000];
	private readonly ushort[] IO = new ushort[0x1000 >> 1];

	private static readonly byte[] MPCmdMAC = { 0x03, 0x09, 0xBF, 0x00, 0x00, 0x00 };
	private static readonly byte[] MPReplyMAC = { 0x03, 0x09, 0xBF, 0x00, 0x00, 0x10 };
	private static readonly byte[] MPAckMAC = { 0x03, 0x09, 0xBF, 0x00, 0x00, 0x03 };

	private const int kTimerInterval = 8;
	private const uint kTimeCheckMask = 0xFFFFFFF8;

	private bool Enabled;
	private bool PowerOn;

	private int TimerError;

	private ushort Random;

	private ulong USTimestamp;

	private ulong USCounter;
	private ulong USCompare;
	private bool BlockBeaconIRQ14;

	private uint CmdCounter;

	private readonly byte[] BBRegs = new byte[0x100];
	private readonly byte[] BBRegsRO = new byte[0x100];

	private byte RFVersion;
	private readonly uint[] RFRegs = new uint[0x40];

	private readonly uint[] RFChannelIndex = new uint[2];
	private readonly uint[,] RFChannelData = new uint[14, 2];
	private int CurChannel;

	private struct TXSlot
	{
		public bool Valid;
		public ushort Addr;
		public ushort Length;
		public byte Rate;
		public byte CurPhase;
		public int CurPhaseTime;
		public uint HalfwordTimeMask;
	}

	private readonly TXSlot[] TXSlots = new TXSlot[6];
	private readonly byte[] TXBuffer = new byte[0x2000];

	private readonly byte[] RXBuffer = new byte[2048];
	private uint RXBufferPtr;
	private int RXTime;
	private uint RXHalfwordTimeMask;

	private uint ComStatus;
	private uint TXCurSlot;
	private uint RXCounter;

	private int MPReplyTimer;
	private ushort MPClientMask, MPClientFail;

	private readonly byte[] MPClientReplies = new byte[15 * 1024];

	private ushort MPLastSeqno;

	private int USUntilPowerOn;

	private bool IsMP;
	private bool IsMPClient;
	private ulong NextSync;
	private ulong RXTimestamp;

	public Wifi( NDS nds )
	{
		_nds = nds;
	}

	private ref ushort IOPORT( int x ) => ref IO[x >> 1];

	private static ushort R16( byte[] b, int a ) => (ushort)(b[a] | (b[a + 1] << 8));
	private static void W16( byte[] b, int a, ushort v ) { b[a] = (byte)v; b[a + 1] = (byte)(v >> 8); }
	private static uint R32( byte[] b, int a ) => (uint)(b[a] | (b[a + 1] << 8) | (b[a + 2] << 16) | (b[a + 3] << 24));
	private static void W32( byte[] b, int a, uint v ) { b[a] = (byte)v; b[a + 1] = (byte)(v >> 8); b[a + 2] = (byte)(v >> 16); b[a + 3] = (byte)(v >> 24); }
	private static ulong R64( byte[] b, int a ) => R32( b, a ) | ((ulong)R32( b, a + 4 ) << 32);

	private static bool MACEqual( byte[] a, int ao, byte[] b, int bo )
	{
		for ( int i = 0; i < 6; i++ )
			if ( a[ao + i] != b[bo + i] ) return false;
		return true;
	}

	private byte IOByte( int byteAddr ) => (byte)(IO[byteAddr >> 1] >> ((byteAddr & 1) * 8));

	private bool MACEqualIO( byte[] buf, int off, int ioByteAddr )
	{
		for ( int i = 0; i < 6; i++ )
			if ( buf[off + i] != IOByte( ioByteAddr + i ) ) return false;
		return true;
	}

	public void Reset()
	{
		Array.Clear( RAM, 0, 0x2000 );
		Array.Clear( IO, 0, IO.Length );

		Enabled = false;
		PowerOn = false;

		Random = 1;

		Array.Clear( BBRegs, 0, 0x100 );
		Array.Clear( BBRegsRO, 0, 0x100 );

		void BBRegFixed( int id, byte val ) { BBRegs[id] = val; BBRegsRO[id] = 1; }
		BBRegFixed( 0x00, 0x6D );
		BBRegFixed( 0x0D, 0x00 );
		BBRegFixed( 0x0E, 0x00 );
		BBRegFixed( 0x0F, 0x00 );
		BBRegFixed( 0x10, 0x00 );
		BBRegFixed( 0x11, 0x00 );
		BBRegFixed( 0x12, 0x00 );
		BBRegFixed( 0x16, 0x00 );
		BBRegFixed( 0x17, 0x00 );
		BBRegFixed( 0x18, 0x00 );
		BBRegFixed( 0x19, 0x00 );
		BBRegFixed( 0x1A, 0x00 );
		BBRegFixed( 0x27, 0x00 );
		BBRegFixed( 0x4D, 0x00 );
		BBRegFixed( 0x5D, 0x01 );
		BBRegFixed( 0x5E, 0x00 );
		BBRegFixed( 0x5F, 0x00 );
		BBRegFixed( 0x60, 0x00 );
		BBRegFixed( 0x61, 0x00 );
		BBRegFixed( 0x64, 0xFF );
		BBRegFixed( 0x66, 0x00 );
		for ( int i = 0x69; i < 0x100; i++ )
			BBRegFixed( i, 0x00 );

		byte[] fw = _nds.GetFirmware();
		RFVersion = fw[0x40];
		Array.Clear( RFRegs, 0, RFRegs.Length );

		if ( RFVersion == 3 )
		{
			RFChannelIndex[0] = fw[0x116];
			RFChannelIndex[1] = fw[0x125];
			for ( int i = 0; i < 14; i++ )
			{
				RFChannelData[i, 0] = fw[0x117 + i];
				RFChannelData[i, 1] = fw[0x126 + i];
			}
		}
		else
		{
			int rf56 = 0xF2;
			RFChannelIndex[0] = (uint)(fw[rf56 + 2] >> 2);
			RFChannelIndex[1] = (uint)(fw[rf56 + 5] >> 2);
			for ( int i = 0; i < 14; i++ )
			{
				RFChannelData[i, 0] = (uint)(fw[rf56 + i * 6 + 0] |
					(fw[rf56 + i * 6 + 1] << 8) |
					((fw[rf56 + i * 6 + 2] & 0x03) << 16));
				RFChannelData[i, 1] = (uint)(fw[rf56 + i * 6 + 3] |
					(fw[rf56 + i * 6 + 4] << 8) |
					((fw[rf56 + i * 6 + 5] & 0x03) << 16));
			}
		}

		CurChannel = 0;

		IOPORT( 0x000 ) = 0x1440;

		IO[0x018 >> 1] = 0xFFFF; IO[0x01A >> 1] = 0xFFFF; IO[0x01C >> 1] = 0xFFFF;
		IO[0x020 >> 1] = 0xFFFF; IO[0x022 >> 1] = 0xFFFF; IO[0x024 >> 1] = 0xFFFF;

		IOPORT( W_PowerUS ) = 0x0001;

		USTimestamp = 0;
		USCounter = 0;
		USCompare = 0;
		BlockBeaconIRQ14 = false;

		Array.Clear( TXSlots, 0, TXSlots.Length );
		Array.Clear( TXBuffer, 0, TXBuffer.Length );
		ComStatus = 0;
		TXCurSlot = 0xFFFFFFFF;
		RXCounter = 0;

		Array.Clear( RXBuffer, 0, RXBuffer.Length );
		RXBufferPtr = 0;
		RXTime = 0;
		RXHalfwordTimeMask = 0xFFFFFFFF;

		MPReplyTimer = 0;
		MPClientMask = 0;
		MPClientFail = 0;
		Array.Clear( MPClientReplies, 0, MPClientReplies.Length );

		MPLastSeqno = 0xFFFF;

		CmdCounter = 0;

		USUntilPowerOn = 0;

		IsMP = false;
		IsMPClient = false;
		NextSync = 0;
		RXTimestamp = 0;

		_nds.CancelEvent( SysEvent.Wifi );
	}

	private void ScheduleTimer( bool first )
	{
		if ( first ) TimerError = 0;

		long cycles = 33513982L * kTimerInterval;
		cycles -= TimerError;
		int delay = (int)((cycles + 999999) / 1000000);
		TimerError = (int)((delay * 1000000L) - cycles);

		_nds.ScheduleEvent( SysEvent.Wifi, !first, delay, USTimer );
	}

	private void UpdatePowerOn()
	{
		bool on = Enabled;
		on = on && ((IOPORT( W_PowerUS ) & 0x1) == 0);

		if ( on == PowerOn )
			return;

		PowerOn = on;
		if ( on )
			ScheduleTimer( true );
		else
			_nds.CancelEvent( SysEvent.Wifi );
	}

	public void SetPowerCnt( uint val )
	{
		Enabled = (val & (1 << 1)) != 0;
		UpdatePowerOn();
	}

	private void CheckIRQ( ushort oldflags )
	{
		ushort newflags = (ushort)(IOPORT( W_IF ) & IOPORT( W_IE ));
		if ( (oldflags == 0) && (newflags != 0) )
			_nds.SetIRQ( 1, IRQ.Wifi );
	}

	private void SetIRQ( int irq )
	{
		ushort oldflags = (ushort)(IOPORT( W_IF ) & IOPORT( W_IE ));
		IOPORT( W_IF ) |= (ushort)(1 << irq);
		CheckIRQ( oldflags );
	}

	private void SetIRQ13()
	{
		SetIRQ( 13 );
		if ( (IOPORT( W_ModeWEP ) & 0x7) == 0 )
		{
			if ( (IOPORT( W_PowerTX ) & (1 << 1)) == 0 )
				UpdatePowerStatus( -1 );
		}
	}

	private void SetIRQ14( int source )
	{
		if ( source != 2 )
			IOPORT( W_BeaconCount1 ) = IOPORT( W_BeaconInterval );

		if ( BlockBeaconIRQ14 && source == 1 )
			return;
		if ( (IOPORT( W_USCompareCnt ) & 0x0001) == 0 )
			return;

		SetIRQ( 14 );

		IOPORT( W_BeaconCount2 ) = 0xFFFF;
		IOPORT( W_TXReqRead ) &= 0xFFF2;

		if ( (IOPORT( W_TXSlotBeacon ) & 0x8000) != 0 )
			StartTX_Beacon();

		if ( IOPORT( W_ListenCount ) == 0 )
			IOPORT( W_ListenCount ) = IOPORT( W_ListenInterval );

		IOPORT( W_ListenCount )--;
	}

	private void SetIRQ15()
	{
		SetIRQ( 15 );
		if ( (IOPORT( W_PowerTX ) & (1 << 0)) != 0 )
			UpdatePowerStatus( 1 );
	}

	private void SetStatus( uint status )
	{
		ushort[] rfpins = { 0x04, 0x84, 0, 0x46, 0, 0x84, 0x87, 0, 0x46, 0x04 };
		IOPORT( W_RFStatus ) = (ushort)status;
		IOPORT( W_RFPins ) = rfpins[status];
	}

	private void UpdatePowerStatus( int power )
	{
		int curflags = 0;
		if ( IOPORT( W_TRXPower ) == 1 ) curflags |= 1;
		if ( (IOPORT( W_PowerState ) & (1 << 9)) == 0 ) curflags |= 2;
		int reqflags = curflags;

		if ( (IOPORT( W_PowerForce ) & (1 << 15)) != 0 )
		{
			reqflags = (IOPORT( W_PowerForce ) & (1 << 0)) != 0 ? 0 : 3;
		}
		else if ( (IOPORT( W_ModeReset ) & (1 << 0)) == 0 )
		{
			reqflags = 0;
		}
		else
		{
			if ( power == 0 )
			{
				if ( (IOPORT( W_PowerState ) & 0x0202) == 0x0202 )
					power = 1;
				else if ( (IOPORT( W_PowerState ) & 0x0201) == 0x0001 )
					power = -1;
			}

			if ( (power == -1) && (IOPORT( W_PowerDownCtrl ) & (1 << 0)) != 0 )
				power = 0;

			if ( power == 1 )
				reqflags = 3;
			else if ( power == -1 )
				reqflags = IOPORT( W_PowerDownCtrl ) != 0 ? 3 : 0;
			else if ( (IOPORT( W_PowerDownCtrl ) & (1 << 1)) != 0 )
				reqflags = 3;
		}

		if ( reqflags == curflags )
			return;

		if ( (reqflags & 1) != 0 )
		{
			if ( (curflags & 1) == 0 )
			{
				IOPORT( W_TRXPower ) = 1;
				SetStatus( 1 );
			}
		}
		else
		{
			IOPORT( W_TRXPower ) = 2;
			if ( ComStatus == 0 )
			{
				IOPORT( W_TRXPower ) = 0;
				SetStatus( 9 );
			}
		}

		if ( (reqflags & 2) != 0 )
		{
			IOPORT( W_PowerState ) |= (1 << 8);
			if ( ((curflags & 2) == 0) && (USUntilPowerOn == 0) )
			{
				USUntilPowerOn = -2048;
				SetIRQ( 11 );
			}
		}
		else
		{
			IOPORT( W_PowerState ) &= unchecked((ushort)~(1 << 0));
			IOPORT( W_PowerState ) &= unchecked((ushort)~(1 << 8));
			IOPORT( W_PowerState ) |= (1 << 9);
			USUntilPowerOn = 0;
		}
	}

	private int PreambleLen( int rate )
	{
		if ( rate == 1 ) return 192;
		if ( (IOPORT( W_Preamble ) & 0x0004) != 0 ) return 96;
		return 192;
	}

	private uint NumClients( ushort bitmask )
	{
		uint ret = 0;
		for ( int i = 1; i < 16; i++ )
			if ( (bitmask & (1 << i)) != 0 ) ret++;
		return ret;
	}

	private void IncrementTXCount( ref TXSlot slot )
	{
		byte cnt = RAM[slot.Addr + 0x4];
		if ( cnt < 0xFF ) cnt++;
		W16( RAM, slot.Addr + 0x4, cnt );
	}

	private void ReportMPReplyErrors( ushort clientfail )
	{
		for ( int i = 1; i < 16; i++ )
		{
			if ( (clientfail & (1 << i)) == 0 )
				continue;
			int ba = W_CMDStat0 + i;
			byte v = (byte)(IO[ba >> 1] >> ((ba & 1) * 8));
			v++;
			if ( (ba & 1) != 0 ) IO[ba >> 1] = (ushort)((IO[ba >> 1] & 0x00FF) | (v << 8));
			else IO[ba >> 1] = (ushort)((IO[ba >> 1] & 0xFF00) | v);
		}
	}

	private void TXSendFrame( ref TXSlot slot, int num )
	{
		uint noseqno = 0;

		if ( RAM[slot.Addr + 0x4] != 0 )
			noseqno = 2;
		else if ( num == 1 )
			noseqno = (uint)((IOPORT( W_TXSlotCmd ) & 0x4000) != 0 ? 1 : 0);

		if ( noseqno == 0 )
		{
			if ( (IOPORT( W_TXHeaderCnt ) & (1 << 2)) == 0 )
				W16( RAM, slot.Addr + 0xC + 22, (ushort)(IOPORT( W_TXSeqNo ) << 4) );
			IOPORT( W_TXSeqNo ) = (ushort)((IOPORT( W_TXSeqNo ) + 1) & 0x0FFF);
		}

		ushort framectl = R16( RAM, slot.Addr + 0xC );
		if ( (framectl & (1 << 14)) != 0 )
		{
			if ( (IOPORT( W_WEPCnt ) & (1 << 15)) != 0 )
			{
				int wep_fcs = (slot.Addr + 0xC + slot.Length - 7) & ~0x1;
				W32( RAM, wep_fcs, 0x22334466 );
			}
		}

		int len = slot.Length;
		if ( (slot.Addr + len) > 0x1FF4 )
			len = 0x1FF4 - slot.Addr;

		Array.Copy( RAM, slot.Addr, TXBuffer, 0, 12 + len );

		if ( noseqno == 2 )
			W16( TXBuffer, 0xC, (ushort)(R16( TXBuffer, 0xC ) | (1 << 11)) );

		if ( CurChannel == 0 ) return;
		TXBuffer[9] = (byte)CurChannel;
	}

	private void StartTX_LocN( int nslot, int loc )
	{
		ref TXSlot slot = ref TXSlots[nslot];
		slot.Valid = true;
		slot.Addr = (ushort)((IOPORT( W_TXSlotLoc1 + (loc * 4) ) & 0x0FFF) << 1);
		slot.Length = (ushort)(R16( RAM, slot.Addr + 0xA ) & 0x3FFF);

		byte rate = RAM[slot.Addr + 0x8];
		slot.Rate = (byte)(rate == 0x14 ? 2 : 1);

		slot.CurPhase = 0;
		slot.CurPhaseTime = PreambleLen( slot.Rate );
	}

	private void StartTX_Cmd()
	{
		ref TXSlot slot = ref TXSlots[1];
		slot.Valid = true;
		slot.Addr = (ushort)((IOPORT( W_TXSlotCmd ) & 0x0FFF) << 1);
		slot.Length = (ushort)(R16( RAM, slot.Addr + 0xA ) & 0x3FFF);

		byte rate = RAM[slot.Addr + 0x8];
		slot.Rate = (byte)(rate == 0x14 ? 2 : 1);

		MPClientMask = (ushort)(R16( RAM, slot.Addr + 12 + 24 + 2 ) & MPClientFail);
		MPClientFail &= MPClientMask;

		uint duration = (uint)(PreambleLen( slot.Rate ) + (slot.Length * (slot.Rate == 2 ? 4 : 8)));
		duration += (uint)(112 + ((10 + IOPORT( W_CmdReplyTime )) * NumClients( MPClientMask )));
		duration += (uint)(32 * (slot.Rate == 2 ? 4 : 8));

		if ( CmdCounter > (duration + 100) )
		{
			slot.CurPhase = 0;
			slot.CurPhaseTime = PreambleLen( slot.Rate );
		}
		else
		{
			slot.CurPhase = 13;
			slot.CurPhaseTime = (int)(CmdCounter - 100);
		}

		UpdatePowerStatus( 1 );
	}

	private void StartTX_Beacon()
	{
		ref TXSlot slot = ref TXSlots[4];
		slot.Valid = true;
		slot.Addr = (ushort)((IOPORT( W_TXSlotBeacon ) & 0x0FFF) << 1);
		slot.Length = (ushort)(R16( RAM, slot.Addr + 0xA ) & 0x3FFF);

		byte rate = RAM[slot.Addr + 0x8];
		slot.Rate = (byte)(rate == 0x14 ? 2 : 1);

		slot.CurPhase = 0;
		slot.CurPhaseTime = PreambleLen( slot.Rate );

		IOPORT( W_TXBusy ) |= 0x0010;
	}

	private void FireTX()
	{
		if ( (IOPORT( W_RXCnt ) & 0x8000) == 0 )
			return;

		ushort txbusy = IOPORT( W_TXBusy );

		ushort txreq = IOPORT( W_TXReqRead );
		ushort txstart = 0;
		if ( (IOPORT( W_TXSlotLoc1 ) & 0x8000) != 0 ) txstart |= 0x0001;
		if ( (IOPORT( W_TXSlotCmd ) & 0x8000) != 0 ) txstart |= 0x0002;
		if ( (IOPORT( W_TXSlotLoc2 ) & 0x8000) != 0 ) txstart |= 0x0004;
		if ( (IOPORT( W_TXSlotLoc3 ) & 0x8000) != 0 ) txstart |= 0x0008;

		txstart &= txreq;
		txstart &= (ushort)~txbusy;

		IOPORT( W_TXBusy ) = (ushort)(txbusy | txstart);

		if ( (txstart & 0x0008) != 0 ) { StartTX_LocN( 3, 2 ); return; }
		if ( (txstart & 0x0004) != 0 ) { StartTX_LocN( 2, 1 ); return; }
		if ( (txstart & 0x0002) != 0 ) { MPClientFail = 0xFFFE; StartTX_Cmd(); return; }
		if ( (txstart & 0x0001) != 0 ) { StartTX_LocN( 0, 0 ); return; }
	}

	private void SendMPReply( ushort clienttime, ushort clientmask )
	{
		ref TXSlot slot = ref TXSlots[5];

		if ( (IOPORT( W_TXSlotReply2 ) & 0x8000) != 0 )
			W16( RAM, slot.Addr, 0x0001 );

		slot.Rate = 2;

		IOPORT( W_TXSlotReply2 ) = IOPORT( W_TXSlotReply1 );
		IOPORT( W_TXSlotReply1 ) = 0;

		if ( (IOPORT( W_TXSlotReply2 ) & 0x8000) == 0 )
		{
			slot.Valid = false;
		}
		else
		{
			slot.Valid = true;
			slot.Addr = (ushort)((IOPORT( W_TXSlotReply2 ) & 0x0FFF) << 1);
			slot.Length = (ushort)(R16( RAM, slot.Addr + 0xA ) & 0x3FFF);

			uint duration = (uint)(PreambleLen( slot.Rate ) + (slot.Length * (slot.Rate == 2 ? 4 : 8)));
			if ( duration > clienttime )
				slot.Valid = false;
		}

		if ( slot.Valid )
		{
			slot.CurPhase = 0;
			TXSendFrame( ref slot, 5 );
		}
		else
		{
			slot.CurPhase = 10;
		}

		ushort clientnum = 0;
		for ( int i = 1; i < IOPORT( W_AIDLow ); i++ )
			if ( (clientmask & (1 << i)) != 0 ) clientnum++;

		slot.CurPhaseTime = 16 + ((clienttime + 10) * clientnum) + PreambleLen( slot.Rate );

		IOPORT( W_TXBusy ) |= 0x0080;
	}

	private bool ProcessTX( ref TXSlot slot, int num )
	{
		slot.CurPhaseTime -= kTimerInterval;
		if ( slot.CurPhaseTime > 0 )
		{
			if ( slot.CurPhase == 1 )
			{
				if ( ((uint)slot.CurPhaseTime & slot.HalfwordTimeMask) == 0 )
					IOPORT( W_RXTXAddr )++;
			}
			else if ( slot.CurPhase == 2 )
			{
				MPReplyTimer -= kTimerInterval;
				if ( MPReplyTimer <= 0 && MPClientMask != 0 )
				{
					int nclient = 1;
					while ( (MPClientMask & (1 << nclient)) == 0 ) nclient++;

					uint curclient = 1u << nclient;

					MPReplyTimer += 10 + IOPORT( W_CmdReplyTime );
					MPClientMask &= (ushort)~curclient;
				}
			}

			return false;
		}

		switch ( slot.CurPhase )
		{
			case 0:
				{
					SetIRQ( 7 );
					if ( num == 5 ) SetStatus( 8 ); else SetStatus( 3 );

					uint len = slot.Length;
					if ( slot.Rate == 2 ) { len *= 4; slot.HalfwordTimeMask = 0x7 & kTimeCheckMask; }
					else { len *= 8; slot.HalfwordTimeMask = 0xF & kTimeCheckMask; }

					slot.CurPhase = 1;
					slot.CurPhaseTime = (int)len;

					IOPORT( W_RXTXAddr ) = (ushort)(slot.Addr >> 1);

					if ( num != 5 )
						TXSendFrame( ref slot, num );
				}
				break;

			case 10:
				{
					SetIRQ( 7 );
					SetStatus( 8 );
					slot.CurPhase = 11;
					slot.CurPhaseTime = 28 * 4;
					slot.HalfwordTimeMask = 0xFFFFFFFF;
				}
				break;

			case 1:
				{
					if ( num != 1 && num != 5 )
						W16( RAM, slot.Addr, 0x0001 );
					RAM[slot.Addr + 5] = 0;

					if ( num == 1 )
					{
						if ( (IOPORT( W_TXStatCnt ) & 0x4000) != 0 )
						{
							IOPORT( W_TXStat ) = 0x0800;
							SetIRQ( 1 );
						}
						SetStatus( 5 );

						MPReplyTimer = 16 + PreambleLen( slot.Rate );

						slot.CurPhase = 2;
						slot.CurPhaseTime = (int)(112 + ((10 + IOPORT( W_CmdReplyTime )) * NumClients( MPClientMask )));
						break;
					}
					else if ( num == 5 )
					{
						if ( (IOPORT( W_TXStatCnt ) & 0x1000) != 0 )
						{
							IOPORT( W_TXStat ) = 0x0401;
							SetIRQ( 1 );
						}
						SetStatus( 1 );

						IOPORT( W_TXBusy ) &= unchecked((ushort)~0x80);
						FireTX();
						return true;
					}

					IOPORT( W_TXBusy ) &= (ushort)~(1 << num);

					switch ( num )
					{
						case 0:
						case 2:
						case 3:
							IOPORT( W_TXStat ) = (ushort)(0x0001 | ((num != 0 ? (num - 1) : 0) << 12));
							SetIRQ( 1 );
							IOPORT( W_TXSlotLoc1 + ((num != 0 ? (num - 1) : 0) * 4) ) &= 0x7FFF;
							break;

						case 4:
							if ( (IOPORT( W_TXStatCnt ) & 0x8000) != 0 )
							{
								IOPORT( W_TXStat ) = 0x0301;
								SetIRQ( 1 );
							}
							break;
					}

					SetStatus( 1 );
					FireTX();
				}
				return true;

			case 11:
				{
					IOPORT( W_TXSeqNo ) = (ushort)((IOPORT( W_TXSeqNo ) + 1) & 0x0FFF);
					IOPORT( W_TXBusy ) &= unchecked((ushort)~0x80);
					SetStatus( 1 );
					FireTX();
				}
				return true;

			case 2:
				{
					SetIRQ( 7 );
					SetStatus( 8 );

					IOPORT( W_RXTXAddr ) = 0xFC0;

					if ( slot.Rate == 2 ) slot.CurPhaseTime = 32 * 4;
					else slot.CurPhaseTime = 32 * 8;

					ReportMPReplyErrors( MPClientFail );

					slot.CurPhase = 3;
				}
				break;

			case 3:
				{
					if ( MPClientFail == 0 )
						W16( RAM, slot.Addr, 0x0001 );
					else
						W16( RAM, slot.Addr, 0x0005 );

					W16( RAM, slot.Addr + 0x2, MPClientFail );
					if ( MPClientFail == 0 )
						IncrementTXCount( ref slot );

					IOPORT( W_TXSeqNo ) = (ushort)((IOPORT( W_TXSeqNo ) + 1) & 0x0FFF);

					if ( (IOPORT( W_TXStatCnt ) & 0x2000) != 0 )
					{
						IOPORT( W_TXStat ) = 0x0B01;
						SetIRQ( 1 );
					}

					IOPORT( W_TXBusy ) &= unchecked((ushort)~(1 << 1));
					IOPORT( W_TXSlotCmd ) &= 0x7FFF;

					SetStatus( 1 );
					SetIRQ( 12 );

					FireTX();
				}
				return true;

			case 13:
				{
					IOPORT( W_TXBusy ) &= unchecked((ushort)~(1 << 1));
					IOPORT( W_TXSlotCmd ) &= 0x7FFF;

					W16( RAM, slot.Addr, 0x0005 );

					IOPORT( W_TXSeqNo ) = (ushort)((IOPORT( W_TXSeqNo ) + 1) & 0x0FFF);

					SetStatus( 1 );
					SetIRQ( 12 );

					FireTX();
				}
				return true;
		}

		return false;
	}

	private void IncrementRXAddr( ref ushort addr, ushort inc = 2 )
	{
		for ( uint i = 0; i < inc; i += 2 )
		{
			addr += 2;
			addr &= 0x1FFE;
			if ( addr == (IOPORT( W_RXBufEnd ) & 0x1FFE) )
				addr = (ushort)(IOPORT( W_RXBufBegin ) & 0x1FFE);
		}
	}

	private void StartRX()
	{
		ushort framelen = R16( RXBuffer, 8 );
		RXTime = framelen;

		ushort txrate = R16( RXBuffer, 6 );
		if ( txrate == 0x14 ) { RXTime *= 4; RXHalfwordTimeMask = 0x7 & kTimeCheckMask; }
		else { RXTime *= 8; RXHalfwordTimeMask = 0xF & kTimeCheckMask; }

		ushort addr = (ushort)(IOPORT( W_RXBufWriteCursor ) << 1);
		IncrementRXAddr( ref addr, 12 );
		IOPORT( W_RXTXAddr ) = (ushort)(addr >> 1);

		RXBufferPtr = 12;

		SetIRQ( 6 );
		SetStatus( 6 );
		ComStatus |= 1;
	}

	private void FinishRX()
	{
		ComStatus &= ~0x1u;
		RXCounter = 0;

		if ( ComStatus == 0 )
		{
			if ( (IOPORT( W_PowerState ) & (1 << 9)) != 0 )
			{
				IOPORT( W_TRXPower ) = 0;
				SetStatus( 9 );
			}
			else
				SetStatus( 1 );
		}

		ushort framectl = R16( RXBuffer, 12 );
		ushort seqno = R16( RXBuffer, 12 + 22 );

		byte dstmac0 = RXBuffer[12 + 4];
		if ( (dstmac0 & 0x01) == 0 )
		{
			if ( !MACEqualIO( RXBuffer, 12 + 4, W_MACAddr0 ) )
				return;
		}

		if ( (framectl & (1 << 14)) != 0 )
		{
			if ( (IOPORT( W_WEPCnt ) & (1 << 15)) == 0 )
				return;
		}

		ushort rxflags = 0x0010;
		bool cmd_dupe = false;

		switch ( (framectl >> 2) & 0x3 )
		{
			case 0:
				{
					if ( MACEqualIO( RXBuffer, 12 + 16, W_BSSID0 ) )
						rxflags |= 0x8000;

					int subtype = (framectl >> 4) & 0xF;
					if ( subtype == 0x8 )
					{
						if ( (rxflags & 0x8000) == 0 )
						{
							if ( (IOPORT( W_RXFilter ) & (1 << 0)) == 0 )
								return;
						}
						rxflags |= 0x0001;
					}
					else if ( (subtype <= 0x5) || (subtype >= 0xA && subtype <= 0xC) )
					{
						if ( (rxflags & 0x8000) == 0 )
						{
							if ( (IOPORT( W_RXFilter ) & (3 << 9)) == 0 )
								return;
						}
					}
				}
				break;

			case 1:
				{
					if ( (framectl & 0xF0) == 0xA0 )
					{
						if ( MACEqualIO( RXBuffer, 12 + 4, W_BSSID0 ) )
							rxflags |= 0x8000;

						if ( (rxflags & 0x8000) == 0 )
						{
							if ( (IOPORT( W_RXFilter ) & (1 << 11)) == 0 )
								return;
						}
						rxflags |= 0x0005;
					}
					else
						return;
				}
				break;

			case 2:
				{
					int fromto = (framectl >> 8) & 0x3;
					if ( (IOPORT( W_RXFilter2 ) & (1 << fromto)) != 0 )
						return;

					int[] bssidoffset = { 16, 4, 10, 0 };
					if ( bssidoffset[fromto] != 0 )
					{
						if ( MACEqualIO( RXBuffer, 12 + bssidoffset[fromto], W_BSSID0 ) )
							rxflags |= 0x8000;
					}

					ushort rxfilter = IOPORT( W_RXFilter );

					if ( (rxflags & 0x8000) == 0 )
					{
						if ( (rxfilter & (1 << 11)) == 0 )
							return;
					}

					if ( (framectl & (1 << 11)) != 0 )
					{
						if ( (rxfilter & (1 << 0)) == 0 )
							return;
					}

					if ( MACEqual( RXBuffer, 12 + 16, MPReplyMAC, 0 ) )
					{
						if ( (framectl & 0xF0) == 0x50 ) rxflags |= 0x000F;
						else rxflags |= 0x000E;
					}
					else if ( MACEqual( RXBuffer, 12 + 4, MPCmdMAC, 0 ) )
					{
						if ( seqno == MPLastSeqno ) cmd_dupe = true;
						MPLastSeqno = seqno;
						rxflags |= 0x000C;
					}
					else if ( MACEqual( RXBuffer, 12 + 4, MPAckMAC, 0 ) )
					{
						rxflags |= 0x000D;
					}
					else
					{
						rxflags |= 0x0008;
					}

					switch ( (framectl >> 4) & 0xF )
					{
						case 0x0: break;
						case 0x1:
							if ( (rxflags & 0xF) == 0xD )
							{
								if ( (rxfilter & (1 << 7)) == 0 ) return;
							}
							else if ( (rxflags & 0xF) != 0xE )
							{
								if ( (rxfilter & (1 << 1)) == 0 ) return;
							}
							break;
						case 0x2:
							if ( (rxflags & 0xF) != 0xC )
							{
								if ( (rxfilter & (1 << 2)) == 0 ) return;
							}
							break;
						case 0x3:
							if ( (rxfilter & (1 << 3)) == 0 ) return;
							break;
						case 0x4: break;
						case 0x5:
							if ( (rxflags & 0xF) == 0xF )
							{
								if ( (rxfilter & (1 << 8)) == 0 ) return;
							}
							else
							{
								if ( (rxfilter & (1 << 4)) == 0 ) return;
							}
							break;
						case 0x6:
							if ( (rxfilter & (1 << 5)) == 0 ) return;
							break;
						case 0x7:
							if ( (rxfilter & (1 << 6)) == 0 ) return;
							break;
						default:
							return;
					}
				}
				break;
		}

		if ( !cmd_dupe )
		{
			ushort headeraddr = (ushort)(IOPORT( W_RXBufWriteCursor ) << 1);
			W16( RAM, headeraddr, rxflags );
			IncrementRXAddr( ref headeraddr );
			W16( RAM, headeraddr, 0x0040 );
			IncrementRXAddr( ref headeraddr, 4 );
			W16( RAM, headeraddr, R16( RXBuffer, 6 ) );
			IncrementRXAddr( ref headeraddr );
			W16( RAM, headeraddr, R16( RXBuffer, 8 ) );
			IncrementRXAddr( ref headeraddr );
			W16( RAM, headeraddr, 0x4080 );

			ushort addr = (ushort)(IOPORT( W_RXTXAddr ) << 1);
			if ( (addr & 0x2) != 0 ) IncrementRXAddr( ref addr );
			IOPORT( W_RXBufWriteCursor ) = (ushort)((addr & ~0x3) >> 1);

			SetIRQ( 0 );
		}

		if ( (rxflags & 0x800F) == 0x800C )
		{
			ushort clientmask = R16( RXBuffer, 0xC + 26 );
			if ( IOPORT( W_AIDLow ) != 0 && (clientmask & (1 << IOPORT( W_AIDLow ))) != 0 )
				SendMPReply( R16( RXBuffer, 0xC + 24 ), clientmask );
		}
		else if ( (rxflags & 0x800F) == 0x8001 )
		{
			uint len = R16( RXBuffer, 8 );
			ushort txrate = R16( RXBuffer, 6 );
			len *= (uint)((txrate == 0x14) ? 4 : 8);
			len -= 76;

			ulong timestamp = R64( RXBuffer, 12 + 24 );
			timestamp += len;

			USCounter = timestamp;
		}
	}

	private bool CheckRX( int type )
	{
		return false;
	}

	private void MSTimer()
	{
		if ( IOPORT( W_USCompareCnt ) != 0 )
		{
			if ( (USCounter & ~0x3FFUL) == USCompare )
			{
				BlockBeaconIRQ14 = false;
				SetIRQ14( 0 );
			}
		}

		if ( IOPORT( W_BeaconCount1 ) != 0 )
		{
			IOPORT( W_BeaconCount1 )--;
			if ( IOPORT( W_BeaconCount1 ) == 0 ) SetIRQ14( 1 );
		}
		if ( IOPORT( W_BeaconCount1 ) == 0 )
			IOPORT( W_BeaconCount1 ) = IOPORT( W_BeaconInterval );

		if ( IOPORT( W_BeaconCount2 ) != 0 )
		{
			IOPORT( W_BeaconCount2 )--;
			if ( IOPORT( W_BeaconCount2 ) == 0 ) SetIRQ13();
		}
	}

	private void USTimer( uint param )
	{
		USTimestamp += kTimerInterval;

		if ( IsMPClient && (ComStatus == 0) )
		{
			if ( RXTimestamp != 0 && (USTimestamp >= RXTimestamp) )
			{
				RXTimestamp = 0;
				StartRX();
			}

			if ( USTimestamp >= NextSync )
				CheckRX( 2 );
		}

		if ( USUntilPowerOn < 0 )
		{
			USUntilPowerOn += kTimerInterval;
			if ( USUntilPowerOn >= 0 )
			{
				USUntilPowerOn = 0;
				IOPORT( W_PowerState ) = 0;
				SetStatus( 1 );
				UpdatePowerStatus( 0 );
			}
		}

		if ( IOPORT( W_USCountCnt ) != 0 )
		{
			USCounter += kTimerInterval;
			uint uspart = (uint)(USCounter & 0x3FF);

			if ( IOPORT( W_USCompareCnt ) != 0 )
			{
				uint beaconus = ((uint)IOPORT( W_BeaconCount1 ) << 10) | (0x3FFu - uspart);
				if ( (beaconus & kTimeCheckMask) == (IOPORT( W_PreBeacon ) & kTimeCheckMask) )
					SetIRQ15();
			}

			if ( (uspart & kTimeCheckMask) == 0 )
				MSTimer();
		}

		if ( (IOPORT( W_CmdCountCnt ) & 0x0001) != 0 )
		{
			if ( CmdCounter > 0 )
			{
				if ( CmdCounter < kTimerInterval ) CmdCounter = 0;
				else CmdCounter -= kTimerInterval;
			}
		}

		if ( IOPORT( W_ContentFree ) != 0 )
		{
			if ( IOPORT( W_ContentFree ) < kTimerInterval ) IOPORT( W_ContentFree ) = 0;
			else IOPORT( W_ContentFree ) -= kTimerInterval;
		}

		if ( ComStatus == 0 )
		{
			ushort txbusy = IOPORT( W_TXBusy );
			if ( txbusy != 0 )
			{
				if ( (IOPORT( W_PowerState ) & (1 << 9)) != 0 )
				{
					ComStatus = 0;
					TXCurSlot = 0xFFFFFFFF;
				}
				else
				{
					ComStatus = 0x2;
					if ( (txbusy & 0x0080) != 0 ) TXCurSlot = 5;
					else if ( (txbusy & 0x0010) != 0 ) TXCurSlot = 4;
					else if ( (txbusy & 0x0008) != 0 ) TXCurSlot = 3;
					else if ( (txbusy & 0x0004) != 0 ) TXCurSlot = 2;
					else if ( (txbusy & 0x0002) != 0 ) TXCurSlot = 1;
					else if ( (txbusy & 0x0001) != 0 ) TXCurSlot = 0;
				}
			}
			else
			{
				if ( (!IsMPClient) || (USTimestamp > NextSync) )
				{
					if ( ((RXCounter & 0x1FF & kTimeCheckMask) == 0) && (ComStatus == 0) )
						CheckRX( 0 );
				}

				RXCounter += kTimerInterval;
			}
		}

		if ( (ComStatus & 0x2) != 0 )
		{
			bool finished = ProcessTX( ref TXSlots[TXCurSlot], (int)TXCurSlot );
			if ( finished )
			{
				if ( (IOPORT( W_PowerState ) & (1 << 9)) != 0 )
				{
					IOPORT( W_TXBusy ) = 0;
					IOPORT( W_TRXPower ) = 0;
					SetStatus( 9 );
				}

				ushort txbusy = IOPORT( W_TXBusy );
				if ( (txbusy & 0x0080) != 0 ) TXCurSlot = 5;
				else if ( (txbusy & 0x0010) != 0 ) TXCurSlot = 4;
				else if ( (txbusy & 0x0008) != 0 ) TXCurSlot = 3;
				else if ( (txbusy & 0x0004) != 0 ) TXCurSlot = 2;
				else if ( (txbusy & 0x0002) != 0 ) TXCurSlot = 1;
				else if ( (txbusy & 0x0001) != 0 ) TXCurSlot = 0;
				else
				{
					TXCurSlot = 0xFFFFFFFF;
					ComStatus = 0;
					RXCounter = 0;
				}
			}
		}
		if ( (ComStatus & 0x1) != 0 )
		{
			RXTime -= kTimerInterval;
			if ( ((uint)RXTime & RXHalfwordTimeMask) == 0 )
			{
				ushort addr = (ushort)(IOPORT( W_RXTXAddr ) << 1);
				if ( addr < 0x1FFF ) W16( RAM, addr, R16( RXBuffer, (int)RXBufferPtr ) );

				IncrementRXAddr( ref addr );
				IOPORT( W_RXTXAddr ) = (ushort)(addr >> 1);
				RXBufferPtr += 2;

				if ( RXTime <= 0 )
				{
					FinishRX();
				}
				else if ( addr == (IOPORT( W_RXBufReadCursor ) << 1) )
				{
					RXTime = 0;
					SetStatus( 1 );
					if ( TXCurSlot == 0xFFFFFFFF )
					{
						ComStatus &= ~0x1u;
						RXCounter = 0;
					}
					if ( (ComStatus == 0) && (IOPORT( W_PowerState ) & (1 << 9)) != 0 )
					{
						IOPORT( W_TRXPower ) = 0;
						SetStatus( 9 );
					}
				}
			}
		}

		ScheduleTimer( false );
	}

	private void ChangeChannel()
	{
		uint val1 = RFRegs[RFChannelIndex[0] & 0x3F];
		uint val2 = RFRegs[RFChannelIndex[1] & 0x3F];

		CurChannel = 0;

		for ( int i = 0; i < 14; i++ )
		{
			if ( val1 == RFChannelData[i, 0] && val2 == RFChannelData[i, 1] )
			{
				CurChannel = i + 1;
				break;
			}
		}
	}

	private void RFTransfer_Type2()
	{
		uint id = (uint)((IOPORT( W_RFData2 ) >> 2) & 0x1F);

		if ( (IOPORT( W_RFData2 ) & 0x0080) != 0 )
		{
			uint data = RFRegs[id];
			IOPORT( W_RFData1 ) = (ushort)(data & 0xFFFF);
			IOPORT( W_RFData2 ) = (ushort)((IOPORT( W_RFData2 ) & 0xFFFCu) | ((data >> 16) & 0x3u));
		}
		else
		{
			uint data = (uint)(IOPORT( W_RFData1 ) | ((IOPORT( W_RFData2 ) & 0x0003) << 16));
			RFRegs[id] = data;

			if ( id == (RFChannelIndex[0] & 0x3F) || id == (RFChannelIndex[1] & 0x3F) )
				ChangeChannel();
		}
	}

	private void RFTransfer_Type3()
	{
		uint id = (uint)((IOPORT( W_RFData1 ) >> 8) & 0x3F);

		uint cmd = (uint)(IOPORT( W_RFData2 ) & 0xF);
		if ( cmd == 6 )
		{
			IOPORT( W_RFData1 ) = (ushort)((IOPORT( W_RFData1 ) & 0xFF00u) | (RFRegs[id] & 0xFFu));
		}
		else if ( cmd == 5 )
		{
			uint data = (uint)(IOPORT( W_RFData1 ) & 0xFF);
			RFRegs[id] = data;

			if ( id == (RFChannelIndex[0] & 0x3F) || id == (RFChannelIndex[1] & 0x3F) )
				ChangeChannel();
		}
	}

	public ushort Read( uint addr )
	{
		if ( addr >= 0x04810000 )
			return 0;

		addr &= 0x7FFE;

		if ( addr >= 0x4000 && addr < 0x6000 )
			return R16( RAM, (int)(addr & 0x1FFE) );
		if ( addr >= 0x2000 && addr < 0x4000 )
			return 0xFFFF;

		bool activeread = (addr < 0x1000);

		switch ( addr )
		{
			case W_Random:
				Random = (ushort)((Random & 0x1) ^ (((Random & 0x3FF) << 1) | (Random >> 10)));
				return Random;

			case W_Preamble:
				return (ushort)(IOPORT( W_Preamble ) & 0x0003);

			case W_USCount0: return (ushort)(USCounter & 0xFFFF);
			case W_USCount1: return (ushort)((USCounter >> 16) & 0xFFFF);
			case W_USCount2: return (ushort)((USCounter >> 32) & 0xFFFF);
			case W_USCount3: return (ushort)(USCounter >> 48);

			case W_USCompare0: return (ushort)(USCompare & 0xFFFF);
			case W_USCompare1: return (ushort)((USCompare >> 16) & 0xFFFF);
			case W_USCompare2: return (ushort)((USCompare >> 32) & 0xFFFF);
			case W_USCompare3: return (ushort)(USCompare >> 48);

			case W_CmdCount: return (ushort)((CmdCounter + 9) / 10);

			case W_BBRead:
				if ( (IOPORT( W_BBCnt ) & 0xF000) != 0x6000 )
					return 0;
				return BBRegs[IOPORT( W_BBCnt ) & 0xFF];

			case W_BBBusy:
				return 0;
			case W_RFBusy:
				return 0;

			case W_RXBufDataRead:
				if ( activeread )
				{
					uint rdaddr = IOPORT( W_RXBufReadAddr );
					ushort ret = R16( RAM, (int)rdaddr );

					rdaddr += 2;
					if ( rdaddr == (uint)(IOPORT( W_RXBufEnd ) & 0x1FFE) )
						rdaddr = (uint)(IOPORT( W_RXBufBegin ) & 0x1FFE);
					if ( rdaddr == IOPORT( W_RXBufGapAddr ) )
					{
						rdaddr += (uint)(IOPORT( W_RXBufGapSize ) << 1);
						if ( rdaddr >= (uint)(IOPORT( W_RXBufEnd ) & 0x1FFE) )
							rdaddr = rdaddr + (uint)(IOPORT( W_RXBufBegin ) & 0x1FFE) - (uint)(IOPORT( W_RXBufEnd ) & 0x1FFE);

						if ( IOPORT( 0x000 ) == 0xC340 )
							IOPORT( W_RXBufGapSize ) = 0;
					}

					IOPORT( W_RXBufReadAddr ) = (ushort)(rdaddr & 0x1FFE);
					IOPORT( W_RXBufDataRead ) = ret;

					if ( IOPORT( W_RXBufCount ) > 0 )
					{
						IOPORT( W_RXBufCount )--;
						if ( IOPORT( W_RXBufCount ) == 0 )
							SetIRQ( 9 );
					}
				}
				break;

			case W_TXBusy:
				return (ushort)(IOPORT( W_TXBusy ) & 0x001F);

			case W_CMDStat0:
			case W_CMDStat0 + 2:
			case W_CMDStat0 + 4:
			case W_CMDStat0 + 6:
			case W_CMDStat0 + 8:
			case W_CMDStat0 + 10:
			case W_CMDStat0 + 12:
			case W_CMDStat0 + 14:
				{
					ushort ret = IOPORT( (int)(addr & 0xFFF) );
					IOPORT( (int)(addr & 0xFFF) ) = 0;
					return ret;
				}
		}

		return IOPORT( (int)(addr & 0xFFF) );
	}

	public void Write( uint addr, ushort val )
	{
		if ( addr >= 0x04810000 )
			return;

		addr &= 0x7FFE;

		if ( addr >= 0x4000 && addr < 0x6000 )
		{
			W16( RAM, (int)(addr & 0x1FFE), val );
			return;
		}
		if ( addr >= 0x2000 && addr < 0x4000 )
			return;

		switch ( addr )
		{
			case W_ModeReset:
				{
					ushort oldval = IOPORT( W_ModeReset );
					IOPORT( W_ModeReset ) = (ushort)(val & 0x0001);

					if ( (oldval & 0x0001) == 0 && (val & 0x0001) != 0 )
					{
						IOPORT( 0x27C ) = 0x0005;
						UpdatePowerStatus( 0 );
					}
					else if ( (oldval & 0x0001) != 0 && (val & 0x0001) == 0 )
					{
						IOPORT( 0x27C ) = 0x000A;
						UpdatePowerStatus( 0 );
					}

					if ( (val & 0x2000) != 0 )
					{
						IOPORT( W_RXBufWriteAddr ) = 0;
						IOPORT( W_CmdTotalTime ) = 0;
						IOPORT( W_CmdReplyTime ) = 0;
						IOPORT( 0x1A4 ) = 0;
						IOPORT( 0x278 ) = 0x000F;
					}
					if ( (val & 0x4000) != 0 )
					{
						IOPORT( W_ModeWEP ) = 0;
						IOPORT( W_TXStatCnt ) = 0;
						IOPORT( 0x00A ) = 0;
						IOPORT( W_MACAddr0 ) = 0;
						IOPORT( W_MACAddr1 ) = 0;
						IOPORT( W_MACAddr2 ) = 0;
						IOPORT( W_BSSID0 ) = 0;
						IOPORT( W_BSSID1 ) = 0;
						IOPORT( W_BSSID2 ) = 0;
						IOPORT( W_AIDLow ) = 0;
						IOPORT( W_AIDFull ) = 0;
						IOPORT( W_TXRetryLimit ) = 0x0707;
						IOPORT( 0x02E ) = 0;
						IOPORT( W_RXBufBegin ) = 0x4000;
						IOPORT( W_RXBufEnd ) = 0x4800;
						IOPORT( W_TXBeaconTIM ) = 0;
						IOPORT( W_Preamble ) = 0x0001;
						IOPORT( W_RXFilter ) = 0x0401;
						IOPORT( 0x0D4 ) = 0x0001;
						IOPORT( W_RXFilter2 ) = 0x0008;
						IOPORT( 0x0EC ) = 0x3F03;
						IOPORT( W_TXHeaderCnt ) = 0;
						IOPORT( 0x198 ) = 0;
						IOPORT( 0x1A2 ) = 0x0001;
						IOPORT( 0x224 ) = 0x0003;
						IOPORT( 0x230 ) = 0x0047;
					}
				}
				return;

			case W_ModeWEP:
				val &= 0x007F;
				IOPORT( W_ModeWEP ) = val;
				if ( (IOPORT( W_PowerTX ) & (1 << 1)) != 0 )
				{
					if ( (val & 0x7) == 1 )
						IOPORT( W_PowerDownCtrl ) |= (1 << 1);
					else if ( (val & 0x7) == 2 )
						IOPORT( W_PowerDownCtrl ) = 3;

					if ( (val & 0x7) != 3 )
						IOPORT( W_PowerState ) &= 0x0300;

					UpdatePowerStatus( 0 );
				}
				return;

			case W_IE:
				{
					ushort oldflags = (ushort)(IOPORT( W_IF ) & IOPORT( W_IE ));
					IOPORT( W_IE ) = val;
					CheckIRQ( oldflags );
				}
				return;
			case W_IF:
				IOPORT( W_IF ) &= (ushort)~val;
				return;
			case W_IFSet:
				{
					ushort oldflags = (ushort)(IOPORT( W_IF ) & IOPORT( W_IE ));
					IOPORT( W_IF ) |= (ushort)(val & 0xFBFF);
					CheckIRQ( oldflags );
				}
				return;

			case W_AIDLow:
				IOPORT( W_AIDLow ) = (ushort)(val & 0x000F);
				return;
			case W_AIDFull:
				IOPORT( W_AIDFull ) = (ushort)(val & 0x07FF);
				return;

			case W_PowerUS:
				IOPORT( W_PowerUS ) = (ushort)(val & 0x0003);
				UpdatePowerOn();
				return;

			case W_PowerTX:
				IOPORT( W_PowerTX ) = (ushort)(val & 0x0003);
				if ( (val & (1 << 1)) != 0 )
				{
					if ( (IOPORT( W_ModeWEP ) & 0x7) == 1 )
						IOPORT( W_PowerDownCtrl ) |= (1 << 1);
					else if ( (IOPORT( W_ModeWEP ) & 0x7) == 2 )
						IOPORT( W_PowerDownCtrl ) = 3;

					UpdatePowerStatus( 0 );
				}
				return;

			case W_PowerState:
				if ( (IOPORT( W_ModeWEP ) & 0x7) != 3 )
					return;

				val = (ushort)((IOPORT( W_PowerState ) & 0x0300) | (val & 0x0003));
				if ( (val & 0x0300) == 0x0200 )
					val &= unchecked((ushort)~(1 << 0));
				else
					val &= unchecked((ushort)~(1 << 1));

				if ( (val & (1 << 9)) == 0 )
					val &= unchecked((ushort)~(1 << 8));

				IOPORT( W_PowerState ) = val;
				UpdatePowerStatus( 0 );
				return;

			case W_PowerForce:
				val &= 0x8001;
				IOPORT( W_PowerForce ) = val;
				UpdatePowerStatus( 0 );
				return;

			case W_PowerDownCtrl:
				IOPORT( W_PowerDownCtrl ) = (ushort)(val & 0x0003);
				if ( (IOPORT( W_PowerTX ) & (1 << 1)) != 0 )
				{
					if ( (IOPORT( W_ModeWEP ) & 0x7) == 1 )
						IOPORT( W_PowerDownCtrl ) |= (1 << 1);
					else if ( (IOPORT( W_ModeWEP ) & 0x7) == 2 )
						IOPORT( W_PowerDownCtrl ) = 3;
				}
				UpdatePowerStatus( 0 );
				return;

			case W_USCountCnt: val &= 0x0001; break;
			case W_USCompareCnt:
				if ( (val & 0x0002) != 0 ) SetIRQ14( 2 );
				val &= 0x0001;
				break;

			case W_USCount0: USCounter = (USCounter & 0xFFFFFFFFFFFF0000) | val; return;
			case W_USCount1: USCounter = (USCounter & 0xFFFFFFFF0000FFFF) | ((ulong)val << 16); return;
			case W_USCount2: USCounter = (USCounter & 0xFFFF0000FFFFFFFF) | ((ulong)val << 32); return;
			case W_USCount3: USCounter = (USCounter & 0x0000FFFFFFFFFFFF) | ((ulong)val << 48); return;

			case W_USCompare0:
				USCompare = (USCompare & 0xFFFFFFFFFFFF0000) | ((ulong)val & 0xFC00);
				if ( (val & 0x0001) != 0 ) BlockBeaconIRQ14 = true;
				return;
			case W_USCompare1: USCompare = (USCompare & 0xFFFFFFFF0000FFFF) | ((ulong)val << 16); return;
			case W_USCompare2: USCompare = (USCompare & 0xFFFF0000FFFFFFFF) | ((ulong)val << 32); return;
			case W_USCompare3: USCompare = (USCompare & 0x0000FFFFFFFFFFFF) | ((ulong)val << 48); return;

			case W_CmdCount: CmdCounter = (uint)(val * 10); return;

			case W_BBCnt:
				IOPORT( W_BBCnt ) = val;
				if ( (IOPORT( W_BBCnt ) & 0xF000) == 0x5000 )
				{
					int regid = IOPORT( W_BBCnt ) & 0xFF;
					if ( BBRegsRO[regid] == 0 )
						BBRegs[regid] = (byte)(IOPORT( W_BBWrite ) & 0xFF);
				}
				return;

			case W_RFData2:
				IOPORT( W_RFData2 ) = val;
				if ( RFVersion == 3 ) RFTransfer_Type3();
				else RFTransfer_Type2();
				return;
			case W_RFCnt:
				val &= 0x413F;
				break;

			case W_RXCnt:
				if ( (val & 0x0001) != 0 )
					IOPORT( W_RXBufWriteCursor ) = IOPORT( W_RXBufWriteAddr );
				if ( (val & 0x0080) != 0 )
				{
					IOPORT( W_TXSlotReply2 ) = IOPORT( W_TXSlotReply1 );
					IOPORT( W_TXSlotReply1 ) = 0;
				}
				if ( (val & 0x8000) != 0 )
					FireTX();
				val &= 0xFF0E;
				break;

			case W_RXBufDataRead:
				if ( IOPORT( W_RXBufCount ) > 0 )
				{
					IOPORT( W_RXBufCount )--;
					if ( IOPORT( W_RXBufCount ) == 0 )
						SetIRQ( 9 );
				}
				return;

			case W_RXBufReadAddr:
			case W_RXBufGapAddr:
				val &= 0x1FFE;
				break;
			case W_RXBufGapSize:
			case W_RXBufCount:
			case W_RXBufWriteAddr:
			case W_RXBufReadCursor:
				val &= 0x0FFF;
				break;

			case W_TXReqReset:
				IOPORT( W_TXReqRead ) &= (ushort)~val;
				return;
			case W_TXReqSet:
				IOPORT( W_TXReqRead ) |= val;
				FireTX();
				return;

			case W_TXSlotReset:
				if ( (val & 0x0001) != 0 ) IOPORT( W_TXSlotLoc1 ) &= 0x7FFF;
				if ( (val & 0x0002) != 0 ) IOPORT( W_TXSlotCmd ) &= 0x7FFF;
				if ( (val & 0x0004) != 0 ) IOPORT( W_TXSlotLoc2 ) &= 0x7FFF;
				if ( (val & 0x0008) != 0 ) IOPORT( W_TXSlotLoc3 ) &= 0x7FFF;
				if ( (val & 0x0040) != 0 ) IOPORT( W_TXSlotReply2 ) &= 0x7FFF;
				if ( (val & 0x0080) != 0 ) IOPORT( W_TXSlotReply1 ) &= 0x7FFF;
				val = 0;
				break;

			case W_TXBufDataWrite:
				{
					uint wraddr = IOPORT( W_TXBufWriteAddr );
					W16( RAM, (int)wraddr, val );

					wraddr += 2;
					if ( wraddr == IOPORT( W_TXBufGapAddr ) )
						wraddr += (uint)(IOPORT( W_TXBufGapSize ) << 1);

					IOPORT( W_TXBufWriteAddr ) = (ushort)(wraddr & 0x1FFE);

					if ( IOPORT( W_TXBufCount ) > 0 )
					{
						IOPORT( W_TXBufCount )--;
						if ( IOPORT( W_TXBufCount ) == 0 )
							SetIRQ( 8 );
					}
				}
				return;

			case W_TXBufWriteAddr:
			case W_TXBufGapAddr:
				val &= 0x1FFE;
				break;
			case W_TXBufGapSize:
			case W_TXBufCount:
				val &= 0x0FFF;
				break;

			case W_TXSlotBeacon:
				IsMP = (val & 0x8000) != 0;
				break;

			case W_TXSlotCmd:
				if ( CmdCounter == 0 )
					val = (ushort)((val & 0x7FFF) | (IOPORT( W_TXSlotCmd ) & 0x8000));
				IOPORT( (int)(addr & 0xFFF) ) = val;
				FireTX();
				return;
			case W_TXSlotLoc1:
			case W_TXSlotLoc2:
			case W_TXSlotLoc3:
				IOPORT( (int)(addr & 0xFFF) ) = val;
				FireTX();
				return;

			case 0x228:
			case 0x244:
				break;

			case 0x000:
			case 0x034:
			case 0x044:
			case 0x054:
			case 0x098:
			case 0x0B0:
			case 0x0B6:
			case 0x0B8:
			case 0x15C:
			case 0x15E:
			case 0x180:
			case 0x19C:
			case 0x1A8:
			case 0x1AC:
			case 0x1C4:
			case 0x210:
			case 0x214:
			case 0x268:
				return;

			default:
				break;
		}

		IOPORT( (int)(addr & 0xFFF) ) = val;
	}
}
