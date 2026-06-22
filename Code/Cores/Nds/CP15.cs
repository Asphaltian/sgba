namespace Emulotl.Nds;

public sealed partial class ARMv5
{
	private const uint ItcmMask = NdsConstants.Arm9ItcmSize - 1;
	private const uint DtcmMask = NdsConstants.Arm9DtcmSize - 1;
	private const int CodeCacheTiming = 3;

	public void CP15Reset()
	{
		CP15Control = 0x2078;

		RNGSeed = 44203;
		TraceProcessID = 0;

		DTCMSetting = 0;
		ITCMSetting = 0;

		Array.Clear( ITCM );
		Array.Clear( DTCM );

		ITCMSize = 0;
		DTCMBase = 0xFFFFFFFF;
		DTCMMask = 0;

		Array.Clear( ICache );
		Array.Clear( ICacheTags );
		Array.Clear( ICacheCount );

		PU_CodeCacheable = 0;
		PU_DataCacheable = 0;
		PU_DataCacheWrite = 0;
		PU_CodeRW = 0;
		PU_DataRW = 0;

		for ( int i = 0; i < 8; i++ )
			PU_Region[i] = 0;

		UpdateRegionTimings( 0, 0x100000 );

		UpdatePURegions( true );
	}

	public void UpdateRegionTimings( uint addrstart, uint addrend )
	{
		for ( uint i = addrstart; i < addrend; i++ )
		{
			byte pu = PU_Map[i];
			uint bt0 = NDS.ARM9MemTimings[i >> 2, 0];
			uint bt2 = NDS.ARM9MemTimings[i >> 2, 2];
			uint bt3 = NDS.ARM9MemTimings[i >> 2, 3];

			MemTimings[i, 0] = (pu & 0x40) != 0 ? (byte)0xFF : (byte)(bt2 << 1);

			if ( (pu & 0x10) != 0 )
			{
				MemTimings[i, 1] = 3;
				MemTimings[i, 2] = 3;
				MemTimings[i, 3] = 1;
			}
			else
			{
				MemTimings[i, 1] = (byte)(bt0 << 1);
				MemTimings[i, 2] = (byte)(bt2 << 1);
				MemTimings[i, 3] = (byte)(bt3 << 1);
			}
		}
	}

	public void UpdateDTCMSetting()
	{
		uint newDTCMBase;
		uint newDTCMMask;
		uint newDTCMSize;

		if ( (CP15Control & (1 << 16)) != 0 )
		{
			newDTCMSize = 0x200u << (int)((DTCMSetting >> 1) & 0x1F);
			if ( newDTCMSize < 0x1000 ) newDTCMSize = 0x1000;
			newDTCMMask = 0xFFFFF000 & ~(newDTCMSize - 1);
			newDTCMBase = DTCMSetting & newDTCMMask;
		}
		else
		{
			newDTCMSize = 0;
			newDTCMBase = 0xFFFFFFFF;
			newDTCMMask = 0;
		}

		DTCMBase = newDTCMBase;
		DTCMMask = newDTCMMask;
	}

	public void UpdateITCMSetting()
	{
		if ( (CP15Control & (1 << 18)) != 0 )
			ITCMSize = 0x200u << (int)((ITCMSetting >> 1) & 0x1F);
		else
			ITCMSize = 0;
	}

	public void UpdatePURegion( uint n )
	{
		if ( (CP15Control & (1 << 0)) == 0 )
			return;

		uint coderw = (PU_CodeRW >> (int)(4 * n)) & 0xF;
		uint datarw = (PU_DataRW >> (int)(4 * n)) & 0xF;

		uint codecache, datacache, datawrite;

		if ( (CP15Control & (1 << 12)) != 0 )
			codecache = (PU_CodeCacheable >> (int)n) & 0x1;
		else
			codecache = 0;

		if ( (CP15Control & (1 << 2)) != 0 )
		{
			datacache = (PU_DataCacheable >> (int)n) & 0x1;
			datawrite = (PU_DataCacheWrite >> (int)n) & 0x1;
		}
		else
		{
			datacache = 0;
			datawrite = 0;
		}

		uint rgn = PU_Region[n];
		if ( (rgn & (1 << 0)) == 0 )
			return;

		int size = Math.Max( (int)((rgn >> 1) & 0x1F) - 11, 0 );
		uint start = ((rgn >> 12) >> size) << size;
		uint end = start + (1u << size);

		byte usermask = 0;
		byte privmask = 0;

		switch ( datarw )
		{
			case 0: break;
			case 1: privmask |= 0x03; break;
			case 2: privmask |= 0x03; usermask |= 0x01; break;
			case 3: privmask |= 0x03; usermask |= 0x03; break;
			case 5: privmask |= 0x01; break;
			case 6: privmask |= 0x01; usermask |= 0x01; break;
		}

		switch ( coderw )
		{
			case 0: break;
			case 1: privmask |= 0x04; break;
			case 2: privmask |= 0x04; usermask |= 0x04; break;
			case 3: privmask |= 0x04; usermask |= 0x04; break;
			case 5: privmask |= 0x04; break;
			case 6: privmask |= 0x04; usermask |= 0x04; break;
		}

		if ( (datacache & 0x1) != 0 )
		{
			privmask |= 0x10;
			usermask |= 0x10;

			if ( (datawrite & 0x1) != 0 )
			{
				privmask |= 0x20;
				usermask |= 0x20;
			}
		}

		if ( (codecache & 0x1) != 0 )
		{
			privmask |= 0x40;
			usermask |= 0x40;
		}

		for ( uint i = start; i < end; i++ )
		{
			PU_UserMap[i] = usermask;
			PU_PrivMap[i] = privmask;
		}

		UpdateRegionTimings( start, end );
	}

	public void UpdatePURegions( bool updateAll )
	{
		if ( (CP15Control & (1 << 0)) == 0 )
		{
			byte mask = 0x07;
			if ( (CP15Control & (1 << 2)) != 0 ) mask |= 0x30;
			if ( (CP15Control & (1 << 12)) != 0 ) mask |= 0x40;

			Array.Fill( PU_UserMap, mask );
			Array.Fill( PU_PrivMap, mask );

			UpdateRegionTimings( 0x00000, 0x100000 );
		}
		else
		{
			if ( updateAll )
			{
				Array.Clear( PU_UserMap );
				Array.Clear( PU_PrivMap );
			}

			for ( uint n = 0; n < 8; n++ )
				UpdatePURegion( n );

			if ( updateAll )
				UpdateRegionTimings( 0x00000, 0x100000 );
		}

		PU_Map = (CPSR & 0x1F) == 0x10 ? PU_UserMap : PU_PrivMap;
	}

	public void ICacheInvalidateAll() { }
	public void ICacheInvalidateByAddr( uint addr ) { }

	public void CP15Write( uint id, uint val )
	{
		switch ( id )
		{
			case 0x100:
				{
					uint old = CP15Control;
					val &= 0x000FF085;
					CP15Control &= ~0x000FF085u;
					CP15Control |= val;
					UpdateDTCMSetting();
					UpdateITCMSetting();
					if ( (old & 0x1005) != (val & 0x1005) )
						UpdatePURegions( (old & 0x1) != (val & 0x1) );
					ExceptionBase = (val & (1 << 13)) != 0 ? 0xFFFF0000 : 0x00000000;
					return;
				}

			case 0x200: PU_DataCacheable = val; return;
			case 0x201: PU_CodeCacheable = val; return;
			case 0x300: PU_DataCacheWrite = val; return;

			case 0x500:
				PU_DataRW = 0;
				PU_DataRW |= val & 0x0003;
				PU_DataRW |= (val & 0x000C) << 2;
				PU_DataRW |= (val & 0x0030) << 4;
				PU_DataRW |= (val & 0x00C0) << 6;
				PU_DataRW |= (val & 0x0300) << 8;
				PU_DataRW |= (val & 0x0C00) << 10;
				PU_DataRW |= (val & 0x3000) << 12;
				PU_DataRW |= (val & 0xC000) << 14;
				return;

			case 0x501:
				PU_CodeRW = 0;
				PU_CodeRW |= val & 0x0003;
				PU_CodeRW |= (val & 0x000C) << 2;
				PU_CodeRW |= (val & 0x0030) << 4;
				PU_CodeRW |= (val & 0x00C0) << 6;
				PU_CodeRW |= (val & 0x0300) << 8;
				PU_CodeRW |= (val & 0x0C00) << 10;
				PU_CodeRW |= (val & 0x3000) << 12;
				PU_CodeRW |= (val & 0xC000) << 14;
				return;

			case 0x502: PU_DataRW = val; return;
			case 0x503: PU_CodeRW = val; return;

			case 0x600:
			case 0x601:
			case 0x610:
			case 0x611:
			case 0x620:
			case 0x621:
			case 0x630:
			case 0x631:
			case 0x640:
			case 0x641:
			case 0x650:
			case 0x651:
			case 0x660:
			case 0x661:
			case 0x670:
			case 0x671:
				PU_Region[(id >> 4) & 0xF] = val;
				UpdatePURegions( true );
				return;

			case 0x704:
			case 0x782:
				Halt( 1 );
				return;

			case 0x750: ICacheInvalidateAll(); return;
			case 0x751: ICacheInvalidateByAddr( val ); return;

			case 0x910:
				DTCMSetting = val & 0xFFFFF03E;
				UpdateDTCMSetting();
				return;

			case 0x911:
				ITCMSetting = val & 0x0000003E;
				UpdateITCMSetting();
				return;

			case 0xD01:
				TraceProcessID = val;
				return;
		}
	}

	public uint CP15Read( uint id )
	{
		switch ( id )
		{
			case 0x000:
			case 0x003:
			case 0x004:
			case 0x005:
			case 0x006:
			case 0x007: return 0x41059461;

			case 0x001: return 0x0F0D2112;
			case 0x002: return (6 << 6) | (5 << 18);

			case 0x100: return CP15Control;

			case 0x200: return PU_DataCacheable;
			case 0x201: return PU_CodeCacheable;
			case 0x300: return PU_DataCacheWrite;

			case 0x500:
				{
					uint ret = 0;
					ret |= PU_DataRW & 0x00000003;
					ret |= (PU_DataRW & 0x00000030) >> 2;
					ret |= (PU_DataRW & 0x00000300) >> 4;
					ret |= (PU_DataRW & 0x00003000) >> 6;
					ret |= (PU_DataRW & 0x00030000) >> 8;
					ret |= (PU_DataRW & 0x00300000) >> 10;
					ret |= (PU_DataRW & 0x03000000) >> 12;
					ret |= (PU_DataRW & 0x30000000) >> 14;
					return ret;
				}
			case 0x501:
				{
					uint ret = 0;
					ret |= PU_CodeRW & 0x00000003;
					ret |= (PU_CodeRW & 0x00000030) >> 2;
					ret |= (PU_CodeRW & 0x00000300) >> 4;
					ret |= (PU_CodeRW & 0x00003000) >> 6;
					ret |= (PU_CodeRW & 0x00030000) >> 8;
					ret |= (PU_CodeRW & 0x00300000) >> 10;
					ret |= (PU_CodeRW & 0x03000000) >> 12;
					ret |= (PU_CodeRW & 0x30000000) >> 14;
					return ret;
				}
			case 0x502: return PU_DataRW;
			case 0x503: return PU_CodeRW;

			case 0x600:
			case 0x601:
			case 0x610:
			case 0x611:
			case 0x620:
			case 0x621:
			case 0x630:
			case 0x631:
			case 0x640:
			case 0x641:
			case 0x650:
			case 0x651:
			case 0x660:
			case 0x661:
			case 0x670:
			case 0x671: return PU_Region[(id >> 4) & 0xF];

			case 0x910: return DTCMSetting;
			case 0x911: return ITCMSetting;
			case 0xD01: return TraceProcessID;
		}

		return 0;
	}

	private static ushort RdU16( byte[] m, uint o ) => (ushort)(m[o] | (m[o + 1] << 8));
	private static uint RdU32( byte[] m, uint o ) => (uint)(m[o] | (m[o + 1] << 8) | (m[o + 2] << 16) | (m[o + 3] << 24));
	private static void WrU16( byte[] m, uint o, ushort v ) { m[o] = (byte)v; m[o + 1] = (byte)(v >> 8); }
	private static void WrU32( byte[] m, uint o, uint v ) { m[o] = (byte)v; m[o + 1] = (byte)(v >> 8); m[o + 2] = (byte)(v >> 16); m[o + 3] = (byte)(v >> 24); }

	public uint CodeRead32( uint addr, bool branch )
	{
		if ( addr < ITCMSize )
		{
			CodeCycles = 1;
			return RdU32( ITCM, addr & ItcmMask );
		}

		CodeCycles = RegionCodeCycles;
		if ( CodeCycles == 0xFF )
			CodeCycles = branch || (addr & 0x1F) == 0 ? CodeCacheTiming : 1;

		if ( (addr & 0xFF800000) != CodeMemBase )
			SetupCodeMem( addr );

		if ( CodeMem.Mem != null )
			return RdU32( CodeMem.Mem, CodeMem.Offset + (addr & CodeMem.Mask) );

		return BusRead32( addr );
	}

	public override void DataRead8( uint addr, ref uint val )
	{
		if ( (PU_Map[addr >> 12] & 0x01) == 0 ) { DataAbort(); return; }
		DataRegion = addr;

		if ( addr < ITCMSize ) { DataCycles = 1; val = ITCM[addr & ItcmMask]; return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; val = DTCM[addr & DtcmMask]; return; }

		val = BusRead8( addr );
		DataCycles = MemTimings[addr >> 12, 1];
	}

	public override void DataRead16( uint addr, ref uint val )
	{
		if ( (PU_Map[addr >> 12] & 0x01) == 0 ) { DataAbort(); return; }
		DataRegion = addr;
		addr &= ~1u;

		if ( addr < ITCMSize ) { DataCycles = 1; val = RdU16( ITCM, addr & ItcmMask ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; val = RdU16( DTCM, addr & DtcmMask ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) val = RdU16( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead16( addr );
		DataCycles = MemTimings[addr >> 12, 1];
	}

	public override void DataRead32( uint addr, ref uint val )
	{
		if ( (PU_Map[addr >> 12] & 0x01) == 0 ) { DataAbort(); return; }
		DataRegion = addr;
		addr &= ~3u;

		if ( addr < ITCMSize ) { DataCycles = 1; val = RdU32( ITCM, addr & ItcmMask ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; val = RdU32( DTCM, addr & DtcmMask ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) val = RdU32( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead32( addr );
		DataCycles = MemTimings[addr >> 12, 2];
	}

	public override void DataRead32S( uint addr, ref uint val )
	{
		addr &= ~3u;

		if ( addr < ITCMSize ) { DataCycles += 1; val = RdU32( ITCM, addr & ItcmMask ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles += 1; val = RdU32( DTCM, addr & DtcmMask ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) val = RdU32( NDS.MainRAM, addr & NDS.MainRAMMask );
		else val = BusRead32( addr );
		DataCycles += MemTimings[addr >> 12, 3];
	}

	public override void DataWrite8( uint addr, byte val )
	{
		if ( (PU_Map[addr >> 12] & 0x02) == 0 ) { DataAbort(); return; }
		DataRegion = addr;

		if ( addr < ITCMSize ) { DataCycles = 1; ITCM[addr & ItcmMask] = val; return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; DTCM[addr & DtcmMask] = val; return; }

		BusWrite8( addr, val );
		DataCycles = MemTimings[addr >> 12, 1];
	}

	public override void DataWrite16( uint addr, ushort val )
	{
		if ( (PU_Map[addr >> 12] & 0x02) == 0 ) { DataAbort(); return; }
		DataRegion = addr;
		addr &= ~1u;

		if ( addr < ITCMSize ) { DataCycles = 1; WrU16( ITCM, addr & ItcmMask, val ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; WrU16( DTCM, addr & DtcmMask, val ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) WrU16( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite16( addr, val );
		DataCycles = MemTimings[addr >> 12, 1];
	}

	public override void DataWrite32( uint addr, uint val )
	{
		if ( (PU_Map[addr >> 12] & 0x02) == 0 ) { DataAbort(); return; }
		DataRegion = addr;
		addr &= ~3u;

		if ( addr < ITCMSize ) { DataCycles = 1; WrU32( ITCM, addr & ItcmMask, val ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles = 1; WrU32( DTCM, addr & DtcmMask, val ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) WrU32( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite32( addr, val );
		DataCycles = MemTimings[addr >> 12, 2];
	}

	public override void DataWrite32S( uint addr, uint val )
	{
		addr &= ~3u;

		if ( addr < ITCMSize ) { DataCycles += 1; WrU32( ITCM, addr & ItcmMask, val ); return; }
		if ( (addr & DTCMMask) == DTCMBase ) { DataCycles += 1; WrU32( DTCM, addr & DtcmMask, val ); return; }

		if ( (addr & 0xFF000000) == 0x02000000 ) WrU32( NDS.MainRAM, addr & NDS.MainRAMMask, val );
		else BusWrite32( addr, val );
		DataCycles += MemTimings[addr >> 12, 3];
	}

	public void GetCodeMemRegion( uint addr, ref MemRegion region )
	{
		NDS.GetARM9CodeMem( addr, ref region );
	}
}
