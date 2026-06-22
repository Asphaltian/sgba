namespace Emulotl.Nds;

public sealed partial class NDS
{
	private uint RomU16( uint offset )
	{
		if ( _rom == null || offset + 1 >= _rom.Length ) return 0;
		return (uint)(_rom[offset] | (_rom[offset + 1] << 8));
	}

	private uint RomU32( uint offset )
	{
		if ( _rom == null || offset + 3 >= _rom.Length ) return 0;
		return (uint)(_rom[offset] | (_rom[offset + 1] << 8) | (_rom[offset + 2] << 16) | (_rom[offset + 3] << 24));
	}

	private void DirectBoot()
	{
		if ( _rom == null || _rom.Length < NdsConstants.HeaderSize )
			return;

		uint arm9RomOff = RomU32( 0x20 );
		uint arm9Entry = RomU32( 0x24 );
		uint arm9Ram = RomU32( 0x28 );
		uint arm9Size = RomU32( 0x2C );
		uint arm7RomOff = RomU32( 0x30 );
		uint arm7Entry = RomU32( 0x34 );
		uint arm7Ram = RomU32( 0x38 );
		uint arm7Size = RomU32( 0x3C );

		MapSharedWRAM( 3 );

		for ( uint i = 0; i < 0x9C; i++ )
			ARM9BIOS[0x20 + i] = _rom[0xC0 + i];

		for ( uint i = 0; i < 0x170; i += 4 )
			ARM9Write32( 0x027FFE00 + i, RomU32( i ) );

		CmdEncMode = 2;

		uint cartid = ChipID;
		ARM9Write32( 0x027FF800, cartid );
		ARM9Write32( 0x027FF804, cartid );
		ARM9Write16( 0x027FF808, (ushort)RomU16( 0x15E ) );
		ARM9Write16( 0x027FF80A, (ushort)RomU16( 0x6C ) );
		ARM9Write16( 0x027FF850, 0x5835 );
		ARM9Write32( 0x027FFC00, cartid );
		ARM9Write32( 0x027FFC04, cartid );
		ARM9Write16( 0x027FFC08, (ushort)RomU16( 0x15E ) );
		ARM9Write16( 0x027FFC0A, (ushort)RomU16( 0x6C ) );
		ARM9Write16( 0x027FFC10, 0x5835 );
		ARM9Write16( 0x027FFC30, 0xFFFF );
		ARM9Write16( 0x027FFC40, 0x0001 );

		uint arm9start = 0;

		if ( arm9RomOff >= 0x4000 && arm9RomOff < 0x8000 )
		{
			ProcessSecureArea();
			byte[] securearea = new byte[0x800];
			DecryptSecureArea( securearea );

			for ( uint i = 0; i < 0x800; i += 4 )
			{
				ARM9Write32( arm9Ram + i, Read32( securearea, i ) );
				arm9start += 4;
			}
		}

		for ( uint i = arm9start; i < arm9Size; i += 4 )
			ARM9Write32( arm9Ram + i, RomU32( arm9RomOff + i ) );

		for ( uint i = 0; i < arm7Size; i += 4 )
			ARM7Write32( arm7Ram + i, RomU32( arm7RomOff + i ) );

		ARM7BIOSProt = 0x1204;

		SetupFirmwareDirectBoot();

		ARM9.CP15Write( 0x100, 0x00052078 );
		ARM9.CP15Write( 0x200, 0x00000042 );
		ARM9.CP15Write( 0x201, 0x00000042 );
		ARM9.CP15Write( 0x300, 0x00000002 );
		ARM9.CP15Write( 0x502, 0x15111011 );
		ARM9.CP15Write( 0x503, 0x05100011 );
		ARM9.CP15Write( 0x600, 0x04000033 );
		ARM9.CP15Write( 0x601, 0x04000033 );
		ARM9.CP15Write( 0x610, 0x0200002B );
		ARM9.CP15Write( 0x611, 0x0200002B );
		ARM9.CP15Write( 0x620, 0x00000000 );
		ARM9.CP15Write( 0x621, 0x00000000 );
		ARM9.CP15Write( 0x630, 0x08000035 );
		ARM9.CP15Write( 0x631, 0x08000035 );
		ARM9.CP15Write( 0x640, 0x0300001B );
		ARM9.CP15Write( 0x641, 0x0300001B );
		ARM9.CP15Write( 0x650, 0x00000000 );
		ARM9.CP15Write( 0x651, 0x00000000 );
		ARM9.CP15Write( 0x660, 0xFFFF001D );
		ARM9.CP15Write( 0x661, 0xFFFF001D );
		ARM9.CP15Write( 0x670, 0x027FF017 );
		ARM9.CP15Write( 0x671, 0x027FF017 );
		ARM9.CP15Write( 0x910, 0x0300000A );
		ARM9.CP15Write( 0x911, 0x00000020 );

		ARM9.R[12] = arm9Entry;
		ARM9.R[13] = 0x03002F7C;
		ARM9.R[14] = arm9Entry;
		ARM9.R_IRQ[0] = 0x03003F80;
		ARM9.R_SVC[0] = 0x03003FC0;

		ARM7.R[12] = arm7Entry;
		ARM7.R[13] = 0x0380FD80;
		ARM7.R[14] = arm7Entry;
		ARM7.R_IRQ[0] = 0x0380FF80;
		ARM7.R_SVC[0] = 0x0380FFC0;

		ARM9.JumpTo( arm9Entry );
		ARM7.JumpTo( arm7Entry );

		ExMemCnt[0] = 0xE880;
		ExMemCnt[1] = 0x0080;

		PostFlag9 = 0x01;
		PostFlag7 = 0x01;

		PowerControl9 = 0x820F;
		GPU.SetPowerCnt( PowerControl9 );

		PowerControl7 = 0x0001;
		SPU.SetPowerCnt( PowerControl7 & 0x0001 );

		WriteAuxSpiCnt( 0, 0x8000, 0xFFFF );
		WriteAuxSpiCnt( 1, 0x8000, 0xFFFF );
	}
}
