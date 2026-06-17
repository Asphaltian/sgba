namespace Emulotl.Nds;

public sealed partial class NDS
{
	public uint ROMCnt;
	public ushort[] AuxSpiCnt = new ushort[2];
	public byte[] AuxSpiData = new byte[2];

	public byte[] ROMCommand = new byte[8];
	private readonly byte[] ROMCmd = new byte[8];

	private uint ROMTransferPos, ROMTransferLen;

	private readonly uint[] ROMData = new uint[2];
	private uint ROMDataPosCPU, ROMDataPosCart, ROMDataCount;
	private bool ROMDataLate;

	private uint ROMAddr;
	private uint ChipID;
	private uint ROMMask;
	private uint CmdEncMode;
	private bool CartInserted;
	private uint _cartCpu;

	private void ResetCart()
	{
		ROMCnt = 0;
		AuxSpiCnt[0] = AuxSpiCnt[1] = 0;
		AuxSpiData[0] = AuxSpiData[1] = 0;
		Array.Clear( ROMCommand );
		Array.Clear( ROMCmd );
		ROMTransferPos = 0;
		ROMTransferLen = 0;
		ROMAddr = 0;
		CmdEncMode = 0;
		_cartCpu = 0;

		CartInserted = _rom != null && _rom.Length >= 0x200;

		uint cartromsize = 0x200;
		if ( _rom != null )
			while ( cartromsize < _rom.Length ) cartromsize <<= 1;

		ROMMask = cartromsize - 1;

		ChipID = 0x000000C2;
		if ( cartromsize >= 1024 * 1024 && cartromsize <= 128 * 1024 * 1024 )
			ChipID |= ((cartromsize >> 20) - 1) << 8;
		else
			ChipID |= (0x100 - (cartromsize >> 28)) << 8;
	}

	private byte RomByte( uint addr ) => addr < (uint)_rom.Length ? _rom[addr] : (byte)0xFF;

	private uint ROMRead32()
	{
		uint addrhi = ROMAddr & ROMMask & ~0xFFFu;
		uint addrlo = ROMAddr & 0xFFF;

		uint ret = RomByte( addrhi | addrlo );
		addrlo = (addrlo + 1) & 0xFFF;
		ret |= (uint)RomByte( addrhi | addrlo ) << 8;
		addrlo = (addrlo + 1) & 0xFFF;
		ret |= (uint)RomByte( addrhi | addrlo ) << 16;
		addrlo = (addrlo + 1) & 0xFFF;
		ret |= (uint)RomByte( addrhi | addrlo ) << 24;
		addrlo = (addrlo + 1) & 0xFFF;

		ROMAddr = addrhi | addrlo;
		return ret;
	}

	private void ROMCommandStart()
	{
		if ( CmdEncMode == 0 )
		{
			Array.Copy( ROMCommand, ROMCmd, 8 );

			switch ( ROMCmd[0] )
			{
				case 0x00:
					ROMAddr = (uint)((ROMCmd[1] << 24) | (ROMCmd[2] << 16) | (ROMCmd[3] << 8) | ROMCmd[4]);
					ROMAddr &= 0xFFF;
					return;
				case 0x3C:
					CmdEncMode = 1;
					Key1_InitKeycode( RomU32( 0x0C ), 2, 2 );
					return;
				default:
					return;
			}
		}
		else if ( CmdEncMode == 1 )
		{
			uint[] cmddec =
			[
				ByteSwap( ReadCmdU32( 4 ) ),
				ByteSwap( ReadCmdU32( 0 ) ),
			];
			Key1_Decrypt( cmddec, 0 );
			uint tmp = ByteSwap( cmddec[1] );
			cmddec[1] = ByteSwap( cmddec[0] );
			cmddec[0] = tmp;

			ROMCmd[0] = (byte)cmddec[0];
			ROMCmd[1] = (byte)(cmddec[0] >> 8);
			ROMCmd[2] = (byte)(cmddec[0] >> 16);
			ROMCmd[3] = (byte)(cmddec[0] >> 24);
			ROMCmd[4] = (byte)cmddec[1];
			ROMCmd[5] = (byte)(cmddec[1] >> 8);
			ROMCmd[6] = (byte)(cmddec[1] >> 16);
			ROMCmd[7] = (byte)(cmddec[1] >> 24);

			switch ( ROMCmd[0] & 0xF0 )
			{
				case 0x40:
					return;
				case 0x20:
					ROMAddr = (uint)((ROMCmd[2] & 0xF0) << 8);
					return;
				case 0xA0:
					CmdEncMode = 2;
					return;
				default:
					return;
			}
		}
		else if ( CmdEncMode == 2 )
		{
			Array.Copy( ROMCommand, ROMCmd, 8 );

			if ( ROMCmd[0] == 0xB7 )
			{
				ROMAddr = (uint)((ROMCmd[1] << 24) | (ROMCmd[2] << 16) | (ROMCmd[3] << 8) | ROMCmd[4]) & ROMMask;
				if ( ROMAddr < 0x8000 )
					ROMAddr = 0x8000 + (ROMAddr & 0x1FF);
			}
		}
	}

	private uint ReadCmdU32( int off )
		=> (uint)(ROMCommand[off] | (ROMCommand[off + 1] << 8) | (ROMCommand[off + 2] << 16) | (ROMCommand[off + 3] << 24));

	private uint ROMCommandReceive()
	{
		if ( CmdEncMode == 0 )
		{
			switch ( ROMCmd[0] )
			{
				case 0x9F: return 0xFFFFFFFF;
				case 0x00: return ROMRead32();
				case 0x90: return ChipID;
			}
		}
		else if ( CmdEncMode == 1 )
		{
			switch ( ROMCmd[0] & 0xF0 )
			{
				case 0x10: return ChipID;
				case 0x20: return ROMRead32();
			}
		}
		else if ( CmdEncMode == 2 )
		{
			switch ( ROMCmd[0] )
			{
				case 0xB7: return ROMRead32();
				case 0xB8: return ChipID;
			}
		}

		return 0;
	}

	public void WriteROMCnt( uint cpu, uint val, uint mask )
	{
		_cartCpu = cpu;
		val &= mask;
		uint xferstart = val & ~ROMCnt & (1u << 31);
		ROMCnt = (ROMCnt & (~mask | 0x20800000)) | (val & 0xFF7F7FFF);

		if ( (AuxSpiCnt[cpu] & (1 << 15)) == 0 ) return;
		if ( (AuxSpiCnt[cpu] & (1 << 13)) != 0 ) return;
		if ( xferstart == 0 ) return;

		uint datasize = (ROMCnt >> 24) & 0x7;
		if ( datasize == 7 )
			datasize = 4;
		else if ( datasize > 0 )
			datasize = 0x100u << (int)datasize;

		ROMTransferPos = 0;
		ROMTransferLen = datasize;

		if ( CartInserted )
			ROMCommandStart();

		ROMDataPosCPU = 0;
		ROMDataPosCart = 0;
		ROMDataCount = 0;
		ROMDataLate = false;

		uint xfercycle = (ROMCnt & (1 << 27)) != 0 ? 8u : 5u;
		uint cmddelay = 8 + (ROMCnt & 0x1FFF);
		if ( datasize != 0 )
			cmddelay += (ROMCnt >> 16) & 0x3F;

		if ( datasize == 0 )
			ScheduleEvent( SysEvent.RomTransfer, false, xfercycle * cmddelay, OnRomEnd );
		else
			ScheduleEvent( SysEvent.RomTransfer, false, xfercycle * (cmddelay + 4), OnRomReceiveData );
	}

	private void OnRomEnd( uint param ) => ROMEndTransfer();
	private void OnRomReceiveData( uint param ) => ROMReceiveData();

	private void ROMReceiveData()
	{
		uint data = CartInserted ? ROMCommandReceive() : 0;

		ROMData[ROMDataPosCart] = data;
		ROMDataPosCart ^= 1;
		ROMDataCount++;
		ROMTransferPos += 4;

		RaiseDRQ();

		if ( ROMDataCount < 2 )
			ROMAdvanceReceive();
		else
			ROMDataLate = true;
	}

	private void ROMAdvanceReceive()
	{
		if ( ROMTransferPos >= ROMTransferLen )
			return;

		uint xfercycle = (ROMCnt & (1 << 27)) != 0 ? 8u : 5u;
		uint delay = 4;
		if ( (ROMTransferPos & 0x1FF) == 0 )
			delay += (ROMCnt >> 16) & 0x3F;

		ScheduleEvent( SysEvent.RomTransfer, false, xfercycle * delay, OnRomReceiveData );
	}

	private void ROMEndTransfer()
	{
		ROMCnt &= ~(1u << 31);

		ROMTransferPos = 0;
		ROMTransferLen = 0;

		if ( (AuxSpiCnt[_cartCpu] & (1 << 14)) != 0 )
			SetIRQ( _cartCpu, IRQ.CartXferDone );
	}

	private void RaiseDRQ()
	{
		ROMCnt |= 1u << 23;
		CheckCartDMA();
	}

	public void CartCheckDMA() => CheckCartDMA();

	private void CheckCartDMA()
	{
		if ( (ROMCnt & (1 << 23)) == 0 )
			return;

		if ( _cartCpu != 0 )
			CheckDMAs( 1, 0x12 );
		else
			CheckDMAs( 0, 0x05 );
	}

	public uint ReadROMData()
	{
		uint ret = ROMData[ROMDataPosCPU];
		if ( (ROMCnt & (1 << 30)) != 0 )
			return ret;

		ROMDataPosCPU ^= 1;
		if ( ROMDataCount > 0 )
			ROMDataCount--;

		ROMCnt &= ~(1u << 23);

		if ( ROMTransferPos < ROMTransferLen )
		{
			if ( ROMDataLate )
			{
				ROMDataLate = false;
				ROMAdvanceReceive();
			}
		}
		else
		{
			if ( ROMDataCount == 0 )
				ROMEndTransfer();
			else
				RaiseDRQ();
		}

		return ret;
	}

	public void WriteROMData( uint val, uint mask )
	{
		if ( (ROMCnt & (1 << 30)) == 0 )
			return;

		ROMTransferPos += 4;
		if ( ROMTransferPos >= ROMTransferLen )
			ROMEndTransfer();
	}

	public void WriteAuxSpiCnt( uint cpu, ushort val, ushort mask )
	{
		val &= mask;
		AuxSpiCnt[cpu] = (ushort)((AuxSpiCnt[cpu] & (~mask | 0x0080)) | (val & 0xE043));
	}

	public void WriteROMCommand( int pos, byte val ) => ROMCommand[pos] = val;
}
