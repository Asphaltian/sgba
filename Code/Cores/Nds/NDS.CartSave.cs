namespace Emulotl.Nds;

public sealed partial class NDS
{
	private byte[] SRAM;
	private uint SRAMLength;
	private int SRAMType;

	private byte SRAMStatus;
	private byte SRAMCmd;
	private uint SRAMAddr;
	private uint SRAMPos;
	private bool SRAMSelected;

	private void ResetCartSave()
	{
		int type = NdsSaveDatabase.TypeForRom( _rom );
		int len;
		if ( type >= 0 )
			len = NdsSaveDatabase.Length( type );
		else if ( _save.LoadedLength > 0 )
			len = _save.LoadedLength;
		else
			len = NdsSaveDatabase.DefaultLength;

		_save.Setup( len );
		SRAM = _save.Data;
		SRAMLength = (uint)_save.Data.Length;
		SRAMType = _save.Type;

		SRAMStatus = 0;
		SRAMCmd = 0;
		SRAMAddr = 0;
		SRAMPos = 0;
		SRAMSelected = false;
	}

	private void SaveSPISelect()
	{
		SRAMPos = 0;
	}

	private byte SaveSPITransmitReceive( byte val )
	{
		if ( SRAMType == 0 ) return 0;

		byte ret = 0xFF;

		if ( SRAMPos == 0 )
		{
			switch ( val )
			{
				case 0x04: SRAMStatus &= unchecked((byte)~(1 << 1)); return 0;
				case 0x06: SRAMStatus |= 1 << 1; return 0;
				default:
					SRAMCmd = val;
					SRAMAddr = 0;
					break;
			}
		}
		else
		{
			ret = SRAMType switch
			{
				1 => SRAMWrite_EEPROMTiny( val ),
				2 => SRAMWrite_EEPROM( val ),
				3 => SRAMWrite_FLASH( val ),
				_ => (byte)0xFF,
			};
		}

		if ( SRAMType == 3 && (SRAMCmd == 0xD8 || SRAMCmd == 0xDB) )
			_save.MarkDirty();

		SRAMPos++;
		return ret;
	}

	private byte SRAMWrite_EEPROMTiny( byte val )
	{
		switch ( SRAMCmd )
		{
			case 0x01:
				if ( SRAMPos == 1 )
					SRAMStatus = (byte)((SRAMStatus & 0x01) | (val & 0x0C));
				return 0;

			case 0x05:
				return (byte)(SRAMStatus | 0xF0);

			case 0x02:
			case 0x0A:
				if ( SRAMPos < 2 )
				{
					SRAMAddr = val;
				}
				else
				{
					if ( (SRAMStatus & (1 << 1)) != 0 )
					{
						SRAM[(SRAMAddr + (SRAMCmd == 0x0A ? 0x100u : 0)) & 0x1FF] = val;
						_save.MarkDirty();
					}
					SRAMAddr++;
				}
				return 0;

			case 0x03:
			case 0x0B:
				if ( SRAMPos < 2 )
				{
					SRAMAddr = val;
					return 0;
				}
				else
				{
					byte ret = SRAM[(SRAMAddr + (SRAMCmd == 0x0B ? 0x100u : 0)) & 0x1FF];
					SRAMAddr++;
					return ret;
				}

			case 0x9F:
				return 0xFF;

			default:
				return 0xFF;
		}
	}

	private byte SRAMWrite_EEPROM( byte val )
	{
		uint addrsize = 2;
		if ( SRAMLength > 65536 ) addrsize++;

		switch ( SRAMCmd )
		{
			case 0x01:
				if ( SRAMPos == 1 )
					SRAMStatus = (byte)((SRAMStatus & 0x01) | (val & 0x0C));
				return 0;

			case 0x05:
				return SRAMStatus;

			case 0x02:
				if ( SRAMPos <= addrsize )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
				}
				else
				{
					if ( (SRAMStatus & (1 << 1)) != 0 )
					{
						SRAM[SRAMAddr & (SRAMLength - 1)] = val;
						_save.MarkDirty();
					}
					SRAMAddr++;
				}
				return 0;

			case 0x03:
				if ( SRAMPos <= addrsize )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
					return 0;
				}
				else
				{
					byte ret = SRAM[SRAMAddr & (SRAMLength - 1)];
					SRAMAddr++;
					return ret;
				}

			case 0x9F:
				return 0xFF;

			default:
				return 0xFF;
		}
	}

	private byte SRAMWrite_FLASH( byte val )
	{
		switch ( SRAMCmd )
		{
			case 0x05:
				return SRAMStatus;

			case 0x02:
			case 0x0A:
				if ( SRAMPos <= 3 )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
				}
				else
				{
					if ( (SRAMStatus & (1 << 1)) != 0 )
					{
						SRAM[SRAMAddr & (SRAMLength - 1)] = SRAMCmd == 0x0A ? val : (byte)0;
						_save.MarkDirty();
					}
					SRAMAddr++;
				}
				return 0;

			case 0x03:
				if ( SRAMPos <= 3 )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
					return 0;
				}
				else
				{
					byte ret = SRAM[SRAMAddr & (SRAMLength - 1)];
					SRAMAddr++;
					return ret;
				}

			case 0x0B:
				if ( SRAMPos <= 3 )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
					return 0;
				}
				else if ( SRAMPos == 4 )
				{
					return 0;
				}
				else
				{
					byte ret = SRAM[SRAMAddr & (SRAMLength - 1)];
					SRAMAddr++;
					return ret;
				}

			case 0x9F:
				return 0xFF;

			case 0xD8:
				if ( SRAMPos <= 3 )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
				}
				if ( SRAMPos == 3 && (SRAMStatus & (1 << 1)) != 0 )
				{
					for ( uint i = 0; i < 0x10000; i++ )
					{
						SRAM[SRAMAddr & (SRAMLength - 1)] = 0;
						SRAMAddr++;
					}
				}
				return 0;

			case 0xDB:
				if ( SRAMPos <= 3 )
				{
					SRAMAddr <<= 8;
					SRAMAddr |= val;
				}
				if ( SRAMPos == 3 && (SRAMStatus & (1 << 1)) != 0 )
				{
					for ( uint i = 0; i < 0x100; i++ )
					{
						SRAM[SRAMAddr & (SRAMLength - 1)] = 0;
						SRAMAddr++;
					}
				}
				return 0;

			default:
				return 0xFF;
		}
	}

	public byte ReadAuxSpiData( uint cpu )
	{
		if ( (AuxSpiCnt[cpu] & (1 << 15)) == 0 ) return 0;
		if ( (AuxSpiCnt[cpu] & (1 << 13)) == 0 ) return 0;
		if ( (AuxSpiCnt[cpu] & (1 << 7)) != 0 ) return 0;

		return AuxSpiData[cpu];
	}

	public void WriteAuxSpiData( uint cpu, byte val )
	{
		if ( (AuxSpiCnt[cpu] & (1 << 15)) == 0 ) return;
		if ( (AuxSpiCnt[cpu] & (1 << 13)) == 0 ) return;
		if ( (AuxSpiCnt[cpu] & (1 << 7)) != 0 ) return;

		bool hold = (AuxSpiCnt[cpu] & (1 << 6)) != 0;

		if ( CartInserted )
		{
			if ( !SRAMSelected )
				SaveSPISelect();

			AuxSpiData[cpu] = SaveSPITransmitReceive( val );
		}
		else
		{
			AuxSpiData[cpu] = 0;
		}

		SRAMSelected = hold;
	}
}
