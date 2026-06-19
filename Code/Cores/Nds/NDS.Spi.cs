namespace Emulotl.Nds;

public sealed partial class NDS
{
	private const int FirmwareLength = 0x20000;
	private const uint FirmwareMask = FirmwareLength - 1;
	private const uint UserDataAddr = 0x1FE00;

	private byte[] _firmware;
	private readonly byte[] _userData = new byte[0x100];

	public byte[] GetFirmware() => _firmware;

	private static readonly byte[] BBINIT =
	[
		0x03, 0x17, 0x40, 0x00, 0x1B, 0x6C, 0x48, 0x80, 0x38, 0x00, 0x35, 0x07, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x00, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC7, 0xBB, 0x01, 0x24, 0x7F,
		0x5A, 0x01, 0x3F, 0x01, 0x3F, 0x36, 0x1D, 0x00, 0x78, 0x35, 0x55, 0x12, 0x34, 0x1C, 0x00, 0x01,
		0x0E, 0x38, 0x03, 0x70, 0xC5, 0x2A, 0x0A, 0x08, 0x04, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFE,
		0xFE, 0xFE, 0xFE, 0xFC, 0xFC, 0xFA, 0xFA, 0xFA, 0xFA, 0xFA, 0xF8, 0xF8, 0xF6, 0x00, 0x12, 0x14,
		0x12, 0x41, 0x23, 0x03, 0x04, 0x70, 0x35, 0x0E, 0x2C, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		0x00, 0x00, 0x00, 0x0E, 0x00, 0x00, 0x12, 0x28, 0x1C
	];

	private static readonly byte[] RFINIT =
	[
		0x31, 0x4C, 0x4F, 0x21, 0x00, 0x10, 0xB0, 0x08, 0xFA, 0x15, 0x26, 0xE6, 0xC1, 0x01, 0x0E, 0x50,
		0x05, 0x00, 0x6D, 0x12, 0x00, 0x00, 0x01, 0xFF, 0x0E, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x06,
		0x06, 0x00, 0x00, 0x00, 0x18, 0x00, 0x02, 0x00, 0x00
	];

	private static readonly byte[] CHANDATA =
	[
		0x1E, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x16,
		0x26, 0x1C, 0x1C, 0x1C, 0x1D, 0x1D, 0x1D, 0x1E, 0x1E, 0x1E, 0x1E, 0x1F, 0x1E, 0x1F, 0x18,
		0x01, 0x4B, 0x4B, 0x4B, 0x4B, 0x4C, 0x4C, 0x4C, 0x4C, 0x4C, 0x4C, 0x4C, 0x4D, 0x4D, 0x4D,
		0x02, 0x6C, 0x71, 0x76, 0x5B, 0x40, 0x45, 0x4A, 0x2F, 0x34, 0x39, 0x3E, 0x03, 0x08, 0x14
	];

	private static readonly byte[] DEFAULT_UNUSED3 = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

	public ushort SpiCnt;

	private byte _fwCmd;
	private bool _fwHold;
	private uint _fwAddr;
	private uint _fwDataPos;
	private byte _fwStatus;
	private byte _fwData;

	private byte _pmIndex;
	private bool _pmHold;
	private uint _pmDataPos;
	private byte _pmData;
	private readonly byte[] _pmRegisters = new byte[8];
	private static readonly byte[] PmRegMasks = [0x7F, 0x00, 0x01, 0x03, 0x0F, 0x00, 0x00, 0x00];

	private byte _tscData;
	private byte _tscControlByte;
	private int _tscDataPos;
	private ushort _tscConvResult;
	private ushort TouchX, TouchY = 0xFFF;

	private static ushort CRC16( byte[] data, int offset, int len, ushort start )
	{
		ushort[] blarg = [0xC0C1, 0xC181, 0xC301, 0xC601, 0xCC01, 0xD801, 0xF001, 0xA001];
		uint s = start;

		for ( int i = 0; i < len; i++ )
		{
			s ^= data[offset + i];
			for ( int j = 0; j < 8; j++ )
			{
				if ( (s & 0x1) != 0 )
				{
					s >>= 1;
					s ^= (uint)(blarg[j] << (7 - j));
				}
				else
				{
					s >>= 1;
				}
			}
		}

		return (ushort)(s & 0xFFFF);
	}

	private void BuildUserData()
	{
		Array.Clear( _userData );

		Write16( _userData, 0x00, 5 );
		_userData[0x03] = 1;
		_userData[0x04] = 1;

		ReadOnlySpan<char> nick = "Emulotl";
		for ( int i = 0; i < nick.Length; i++ )
			Write16( _userData, (uint)(0x06 + i * 2), nick[i] );
		Write16( _userData, 0x1A, (ushort)nick.Length );

		Write16( _userData, 0x64, 0x0031 );

		Write16( _userData, 0x58, 0 );
		Write16( _userData, 0x5A, 0 );
		_userData[0x5C] = 0;
		_userData[0x5D] = 0;
		Write16( _userData, 0x5E, 255 << 4 );
		Write16( _userData, 0x60, 191 << 4 );
		_userData[0x62] = 255;
		_userData[0x63] = 191;

		ushort crc = CRC16( _userData, 0, 0x70, 0xFFFF );
		Write16( _userData, 0x72, crc );
	}

	private void ResetSpi()
	{
		BuildUserData();

		_firmware ??= new byte[FirmwareLength];
		Array.Fill( _firmware, (byte)0xFF );

		Array.Clear( _firmware, 0, 0x200 );

		_firmware[0x08] = (byte)'M';
		_firmware[0x09] = (byte)'E';
		_firmware[0x0A] = (byte)'L';
		_firmware[0x0B] = (byte)'N';

		_firmware[0x1D] = 0x20;
		Write16( _firmware, 0x20, (ushort)(UserDataAddr >> 3) );

		Write16( _firmware, 0x2C, 0x0138 );
		_firmware[0x2E] = 0x00;
		_firmware[0x2F] = 0x06;
		Array.Copy( DEFAULT_UNUSED3, 0, _firmware, 0x30, DEFAULT_UNUSED3.Length );
		_firmware[0x36] = 0x00; _firmware[0x37] = 0x09; _firmware[0x38] = 0xBF;
		_firmware[0x39] = 0x11; _firmware[0x3A] = 0x22; _firmware[0x3B] = 0x33;
		Write16( _firmware, 0x3C, 0x3FFE );
		_firmware[0x3E] = 0xFF; _firmware[0x3F] = 0xFF;
		_firmware[0x40] = 0x03;
		_firmware[0x41] = 0x94;
		_firmware[0x42] = 0x29;
		_firmware[0x43] = 0x02;

		Write16( _firmware, 0x44, 0x0002 );
		Write16( _firmware, 0x46, 0x0017 );
		Write16( _firmware, 0x48, 0x0026 );
		Write16( _firmware, 0x4A, 0x1818 );
		Write16( _firmware, 0x4C, 0x0048 );
		Write16( _firmware, 0x4E, 0x4840 );
		Write16( _firmware, 0x50, 0x0058 );
		Write16( _firmware, 0x52, 0x0042 );
		Write16( _firmware, 0x54, 0x0146 );
		Write16( _firmware, 0x56, 0x8064 );
		Write16( _firmware, 0x58, 0xE6E6 );
		Write16( _firmware, 0x5A, 0x2443 );
		Write16( _firmware, 0x5C, 0x000E );
		Write16( _firmware, 0x5E, 0x0001 );
		Write16( _firmware, 0x60, 0x0001 );
		Write16( _firmware, 0x62, 0x0402 );

		Array.Copy( BBINIT, 0, _firmware, 0x64, BBINIT.Length );
		_firmware[0xCD] = 0x00;
		Array.Copy( RFINIT, 0, _firmware, 0xCE, RFINIT.Length );
		_firmware[0xF7] = 0x02;
		Array.Copy( CHANDATA, 0, _firmware, 0xF8, CHANDATA.Length );
		Array.Fill( _firmware, (byte)0xFF, 0x134, 46 );

		Write16( _firmware, 0x2A, CRC16( _firmware, 0x2C, 0x138, 0x0000 ) );

		_firmware[0x2FF] = 0x80;

		Array.Copy( _userData, 0, _firmware, UserDataAddr, 0x100 );
		Array.Copy( _userData, 0, _firmware, UserDataAddr + 0x100, 0x100 );

		for ( uint ap = 0; ap < 3; ap++ )
		{
			uint b = UserDataAddr - 0x400 + ap * 0x100;
			Array.Clear( _firmware, (int)b, 0x100 );

			if ( ap == 0 )
			{
				ReadOnlySpan<char> ssid = "EmulotlAP";
				for ( int i = 0; i < ssid.Length; i++ )
					_firmware[b + 0x40 + i] = (byte)ssid[i];
				_firmware[b + 0xE7] = 0x00;
			}
			else
			{
				_firmware[b + 0xE7] = 0xFF;
			}

			_firmware[b + 0xEF] = 0x01;
			Write16( _firmware, b + 0xFE, CRC16( _firmware, (int)b, 0xFE, 0x0000 ) );
		}

		SpiCnt = 0;

		_fwCmd = 0;
		_fwHold = false;
		_fwAddr = 0;
		_fwDataPos = 0;
		_fwStatus = 0;
		_fwData = 0;

		_pmIndex = 0;
		_pmHold = false;
		_pmDataPos = 0;
		_pmData = 0;
		Array.Clear( _pmRegisters );
		_pmRegisters[4] = 0x40;

		_tscData = 0;
		_tscControlByte = 0;
		_tscDataPos = 0;
		_tscConvResult = 0;
		TouchX = 0;
		TouchY = 0xFFF;
	}

	public void SetupFirmwareDirectBoot()
	{
		ARM9Write32( 0x027FF864, 0 );
		ARM9Write32( 0x027FF868, UserDataAddr );

		ARM9Write16( 0x027FF874, 0 );
		ARM9Write16( 0x027FF876, 0 );

		for ( uint i = 0; i < 0x70; i += 4 )
			ARM9Write32( 0x027FFC80 + i, Read32( _userData, i ) );
	}

	public void WriteSpiCnt( ushort val )
	{
		if ( (SpiCnt & (1 << 15)) != 0 && (val & (1 << 15)) == 0 )
			SpiRelease( (SpiCnt >> 8) & 0x3 );

		SpiCnt = (ushort)((SpiCnt & 0x0080) | (val & 0xCF03));
	}

	public byte ReadSpiData()
	{
		if ( (SpiCnt & (1 << 15)) == 0 ) return 0;
		if ( (SpiCnt & (1 << 7)) != 0 ) return 0;

		return ((SpiCnt >> 8) & 0x3) switch
		{
			0 => _pmData,
			1 => _fwData,
			2 => _tscData,
			_ => (byte)0,
		};
	}

	public void WriteSpiData( byte val )
	{
		if ( (SpiCnt & (1 << 15)) == 0 ) return;
		if ( (SpiCnt & (1 << 7)) != 0 ) return;

		SpiCnt |= 1 << 7;

		int dev = (SpiCnt >> 8) & 0x3;
		switch ( dev )
		{
			case 0: PmWrite( val ); break;
			case 1: FwWrite( val ); break;
			case 2: TscWrite( val ); break;
		}

		if ( (SpiCnt & (1 << 11)) == 0 )
			SpiRelease( dev );

		uint delay = (uint)(8 * (8 << (SpiCnt & 0x3)));
		ScheduleEvent( SysEvent.SpiTransfer, false, delay, OnSpiTransferDone );
	}

	private void OnSpiTransferDone( uint param )
	{
		SpiCnt = (ushort)(SpiCnt & ~(1 << 7));
		if ( (SpiCnt & (1 << 14)) != 0 )
			SetIRQ( 1, IRQ.SPI );
	}

	private void SpiRelease( int dev )
	{
		switch ( dev )
		{
			case 0: _pmHold = false; break;
			case 1: _fwHold = false; _fwCmd = 0; break;
			case 2: break;
		}
	}

	private void FwWrite( byte val )
	{
		if ( !_fwHold )
		{
			_fwCmd = val;
			_fwHold = true;
			_fwData = 0;
			_fwDataPos = 1;
			_fwAddr = 0;

			switch ( _fwCmd )
			{
				case 0x04: _fwStatus &= 0xFD; break;
				case 0x06: _fwStatus |= 0x02; break;
			}
			return;
		}

		switch ( _fwCmd )
		{
			case 0x03:
				if ( _fwDataPos < 4 )
				{
					_fwAddr = (_fwAddr << 8) | val;
					_fwData = 0;
				}
				else
				{
					_fwData = _firmware[_fwAddr & FirmwareMask];
					_fwAddr++;
				}
				_fwDataPos++;
				break;

			case 0x05:
				_fwData = _fwStatus;
				break;

			case 0x0A:
				if ( _fwDataPos < 4 )
				{
					_fwAddr = (_fwAddr << 8) | val;
					_fwData = 0;
				}
				else
				{
					_firmware[_fwAddr & FirmwareMask] = val;
					_fwData = val;
					_fwAddr++;
				}
				_fwDataPos++;
				break;

			case 0x9F:
				_fwData = _fwDataPos switch { 1 => 0x20, 2 => 0x40, 3 => 0x12, _ => 0 };
				_fwDataPos++;
				break;

			default:
				_fwData = 0xFF;
				break;
		}
	}

	private void PmWrite( byte val )
	{
		if ( !_pmHold )
		{
			_pmIndex = val;
			_pmHold = true;
			_pmData = 0;
			_pmDataPos = 1;
			return;
		}

		if ( _pmDataPos == 1 )
		{
			int regid = _pmIndex & 0x07;
			if ( (_pmIndex & 0x80) != 0 )
			{
				_pmData = _pmRegisters[regid];
			}
			else
			{
				_pmRegisters[regid] = (byte)((_pmRegisters[regid] & ~PmRegMasks[regid]) | (val & PmRegMasks[regid]));
			}
		}

		_pmDataPos++;
	}

	private void TscWrite( byte val )
	{
		if ( _tscDataPos == 1 )
			_tscData = (byte)((_tscConvResult >> 5) & 0x7F);
		else if ( _tscDataPos == 2 )
			_tscData = (byte)((_tscConvResult << 3) & 0xFF);
		else
			_tscData = 0;

		if ( (val & 0x80) != 0 )
		{
			_tscControlByte = val;
			_tscDataPos = 1;

			switch ( _tscControlByte & 0x70 )
			{
				case 0x10: _tscConvResult = TouchY; break;
				case 0x50: _tscConvResult = TouchX; break;
				default: _tscConvResult = 0xFFF; break;
			}

			if ( (_tscControlByte & 0x08) != 0 )
				_tscConvResult &= 0x0FF0;
		}
		else
		{
			_tscDataPos++;
		}
	}

	public void SetTouchCoords( ushort x, ushort y )
	{
		TouchX = x;
		TouchY = y;

		if ( y == 0xFFF )
		{
			ExtKeyIn = (ushort)(ExtKeyIn | 0x40);
			return;
		}

		TouchX <<= 4;
		TouchY <<= 4;
		ExtKeyIn = (ushort)(ExtKeyIn & ~0x40);
	}

	public void TouchScreen( int x, int y ) => SetTouchCoords( (ushort)x, (ushort)y );
	public void ReleaseScreen() => SetTouchCoords( 0, 0xFFF );
}
