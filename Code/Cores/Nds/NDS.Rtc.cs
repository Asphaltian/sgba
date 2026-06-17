namespace Emulotl.Nds;

public sealed partial class NDS
{
	private ushort RtcIO;

	private byte RtcInput;
	private int RtcInputBit;
	private int RtcInputPos;

	private readonly byte[] RtcOutput = new byte[8];
	private int RtcOutputBit;
	private int RtcOutputPos;

	private byte RtcCurCmd;

	private readonly byte[] RtcDateTime = new byte[7];
	private byte RtcStatusReg1;
	private byte RtcStatusReg2;
	private readonly byte[] RtcAlarm1 = new byte[3];
	private readonly byte[] RtcAlarm2 = new byte[3];
	private byte RtcClockAdjust;
	private byte RtcFreeReg;

	private void ResetRtc()
	{
		RtcIO = 0;
		RtcInput = 0;
		RtcInputBit = 0;
		RtcInputPos = 0;
		Array.Clear( RtcOutput );
		RtcOutputBit = 0;
		RtcOutputPos = 0;
		RtcCurCmd = 0;

		RtcStatusReg1 = 0x02;
		RtcStatusReg2 = 0;
		Array.Clear( RtcAlarm1 );
		Array.Clear( RtcAlarm2 );
		RtcClockAdjust = 0;
		RtcFreeReg = 0;

		RtcDateTime[0] = 0x26; // year (BCD 2026)
		RtcDateTime[1] = 0x06; // month
		RtcDateTime[2] = 0x16; // day
		RtcDateTime[3] = 0x02; // day of week
		RtcDateTime[4] = 0x12; // hour (24h)
		RtcDateTime[5] = 0x00; // minute
		RtcDateTime[6] = 0x00; // second
	}

	private static byte BCDSanitize( byte val, byte min, byte max )
	{
		if ( val > max ) val = min;
		else if ( (val & 0x0F) >= 0x0A ) val = (byte)((val & 0xF0) + 0x10);
		return val;
	}

	private void RtcWriteDateTime( int num, byte val )
	{
		switch ( num )
		{
			case 1: RtcDateTime[0] = BCDSanitize( val, 0x00, 0x99 ); break;
			case 2: RtcDateTime[1] = BCDSanitize( (byte)(val & 0x1F), 0x01, 0x12 ); break;
			case 3: RtcDateTime[2] = BCDSanitize( (byte)(val & 0x3F), 0x01, 0x31 ); break;
			case 4: RtcDateTime[3] = BCDSanitize( (byte)(val & 0x07), 0x00, 0x06 ); break;
			case 5:
				{
					byte hour = (byte)(val & 0x3F);
					byte pm = (byte)(val & 0x40);
					if ( (RtcStatusReg1 & (1 << 1)) != 0 )
					{
						hour = BCDSanitize( hour, 0x00, 0x23 );
						pm = (byte)(hour >= 0x12 ? 0x40 : 0);
					}
					else
					{
						hour = BCDSanitize( hour, 0x00, 0x11 );
					}
					RtcDateTime[4] = (byte)(hour | pm);
				}
				break;
			case 6: RtcDateTime[5] = BCDSanitize( (byte)(val & 0x7F), 0x00, 0x59 ); break;
			case 7: RtcDateTime[6] = BCDSanitize( (byte)(val & 0x7F), 0x00, 0x59 ); break;
		}
	}

	private void RtcCmdRead()
	{
		if ( (RtcCurCmd & 0x0F) == 0x06 )
		{
			switch ( RtcCurCmd & 0x70 )
			{
				case 0x00:
					RtcOutput[0] = RtcStatusReg1;
					RtcStatusReg1 &= 0x0F;
					break;
				case 0x40:
					RtcOutput[0] = RtcStatusReg2;
					break;
				case 0x20:
					Array.Copy( RtcDateTime, 0, RtcOutput, 0, 7 );
					break;
				case 0x60:
					Array.Copy( RtcDateTime, 4, RtcOutput, 0, 3 );
					break;
				case 0x10:
					if ( (RtcStatusReg2 & 0x04) != 0 )
						Array.Copy( RtcAlarm1, 0, RtcOutput, 0, 3 );
					else
						RtcOutput[0] = RtcAlarm1[2];
					break;
				case 0x50:
					Array.Copy( RtcAlarm2, 0, RtcOutput, 0, 3 );
					break;
				case 0x30: RtcOutput[0] = RtcClockAdjust; break;
				case 0x70: RtcOutput[0] = RtcFreeReg; break;
			}
		}
	}

	private void RtcCmdWrite( byte val )
	{
		if ( (RtcCurCmd & 0x0F) != 0x06 )
			return;

		switch ( RtcCurCmd & 0x70 )
		{
			case 0x00:
				if ( RtcInputPos == 1 )
					RtcStatusReg1 = (byte)((RtcStatusReg1 & 0xF0) | (val & 0x0E));
				break;
			case 0x40:
				if ( RtcInputPos == 1 )
					RtcStatusReg2 = val;
				break;
			case 0x20:
				if ( RtcInputPos <= 7 )
					RtcWriteDateTime( RtcInputPos, val );
				break;
			case 0x60:
				if ( RtcInputPos <= 3 )
					RtcWriteDateTime( RtcInputPos + 4, val );
				break;
			case 0x10:
				if ( (RtcStatusReg2 & 0x04) != 0 )
				{
					if ( RtcInputPos <= 3 ) RtcAlarm1[RtcInputPos - 1] = val;
				}
				else
				{
					if ( RtcInputPos == 1 ) RtcAlarm1[2] = val;
				}
				break;
			case 0x50:
				if ( RtcInputPos <= 3 ) RtcAlarm2[RtcInputPos - 1] = val;
				break;
			case 0x30:
				if ( RtcInputPos == 1 ) RtcClockAdjust = val;
				break;
			case 0x70:
				if ( RtcInputPos == 1 ) RtcFreeReg = val;
				break;
		}
	}

	private void RtcByteIn( byte val )
	{
		if ( RtcInputPos == 0 )
		{
			if ( (val & 0xF0) == 0x60 )
			{
				byte[] rev = [0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6, 0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6];
				RtcCurCmd = rev[val & 0xF];
			}
			else
			{
				RtcCurCmd = val;
			}

			if ( (RtcCurCmd & 0x80) != 0 )
				RtcCmdRead();

			return;
		}

		RtcCmdWrite( val );
	}

	public ushort RtcRead() => RtcIO;

	public void RtcWrite( ushort val, bool isByte )
	{
		if ( isByte ) val |= (ushort)(RtcIO & 0xFF00);

		if ( (val & 0x0004) != 0 )
		{
			if ( (RtcIO & 0x0004) == 0 )
			{
				RtcInput = 0;
				RtcInputBit = 0;
				RtcInputPos = 0;
				Array.Clear( RtcOutput );
				RtcOutputBit = 0;
				RtcOutputPos = 0;
			}
			else
			{
				if ( (val & 0x0002) == 0 )
				{
					if ( (val & 0x0010) != 0 )
					{
						if ( (val & 0x0001) != 0 )
							RtcInput |= (byte)(1 << RtcInputBit);

						RtcInputBit++;
						if ( RtcInputBit >= 8 )
						{
							RtcInputBit = 0;
							RtcByteIn( RtcInput );
							RtcInput = 0;
							RtcInputPos++;
						}
					}
					else
					{
						if ( (RtcOutput[RtcOutputPos] & (1 << RtcOutputBit)) != 0 )
							RtcIO |= 0x0001;
						else
							RtcIO &= 0xFFFE;

						RtcOutputBit++;
						if ( RtcOutputBit >= 8 )
						{
							RtcOutputBit = 0;
							if ( RtcOutputPos < 7 )
								RtcOutputPos++;
						}
					}
				}
			}
		}

		if ( (val & 0x0010) != 0 )
			RtcIO = val;
		else
			RtcIO = (ushort)((RtcIO & 0x0001) | (val & 0xFFFE));
	}
}
