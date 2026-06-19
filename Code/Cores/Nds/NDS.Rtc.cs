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

	private byte RtcIRQFlag;
	private uint RtcMinuteCount;
	private uint RtcClockCount;
	private int RtcTimerError;
	private ushort RCnt;

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

		RtcIRQFlag = 0;
		RtcMinuteCount = 0;
		RCnt = 0;

		RtcSyncToHost();

		RtcClockCount = 0;
		ScheduleRtcTimer( true );
	}

	private void RtcSyncToHost()
	{
		DateTime now = DateTime.Now;
		int year = now.Year % 100;
		int month = now.Month;
		int day = now.Day;
		int hour = now.Hour;
		int minute = now.Minute;
		int second = now.Second;

		int[] monthdays = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
		if ( (year & 3) == 0 ) monthdays[2] = 29;

		int numdays = (year * 365) + ((year + 3) / 4);
		for ( int m = 1; m < month; m++ )
			numdays += monthdays[m];
		numdays += day - 1;
		int dayofweek = (6 + numdays) % 7;

		int pm = hour >= 12 ? 0x40 : 0;
		if ( (RtcStatusReg1 & (1 << 1)) == 0 && pm != 0 )
			hour -= 12;

		RtcDateTime[0] = BCD( year );
		RtcDateTime[1] = BCD( month );
		RtcDateTime[2] = BCD( day );
		RtcDateTime[3] = (byte)dayofweek;
		RtcDateTime[4] = (byte)(BCD( hour ) | pm);
		RtcDateTime[5] = BCD( minute );
		RtcDateTime[6] = BCD( second );
	}

	private static byte BCD( int val ) => (byte)((val % 10) | ((val / 10) << 4));

	private static byte BCDIncrement( byte val )
	{
		int v = val + 1;
		if ( (v & 0x0F) >= 0x0A ) v += 0x06;
		if ( (v & 0xF0) >= 0xA0 ) v += 0x60;
		return (byte)v;
	}

	private void ScheduleRtcTimer( bool first )
	{
		if ( first ) RtcTimerError = 0;

		int sysclock = 33513982 + RtcTimerError;
		int delay = sysclock >> 15;
		RtcTimerError = sysclock & 0x7FFF;

		ScheduleEvent( SysEvent.Rtc, !first, delay, RtcClockTimer );
	}

	private void RtcClockTimer( uint param )
	{
		RtcClockCount++;

		if ( (RtcClockCount & 0x7FFF) == 0 )
			CountSecond();
		else if ( (RtcClockCount & 0x7FFF) == 4 )
			RtcIRQFlag = (byte)(RtcIRQFlag & ~0x01);

		ProcessRtcIRQ( 1 );

		ScheduleRtcTimer( false );
	}

	private void CountSecond()
	{
		RtcDateTime[6] = BCDIncrement( RtcDateTime[6] );
		if ( RtcDateTime[6] >= 0x60 )
		{
			RtcDateTime[6] = 0;
			CountMinute();
		}
	}

	private void CountMinute()
	{
		RtcMinuteCount++;
		RtcDateTime[5] = BCDIncrement( RtcDateTime[5] );
		if ( RtcDateTime[5] >= 0x60 )
		{
			RtcDateTime[5] = 0;
			CountHour();
		}

		RtcIRQFlag |= 0x01;
		ProcessRtcIRQ( 0 );
	}

	private void CountHour()
	{
		byte hour = BCDIncrement( (byte)(RtcDateTime[4] & 0x3F) );
		byte pm = (byte)(RtcDateTime[4] & 0x40);

		if ( (RtcStatusReg1 & (1 << 1)) != 0 )
		{
			if ( hour >= 0x24 )
			{
				hour = 0;
				CountDay();
			}

			pm = (byte)(hour >= 0x12 ? 0x40 : 0);
		}
		else
		{
			if ( hour >= 0x12 )
			{
				hour = 0;
				if ( pm != 0 ) CountDay();
				pm ^= 0x40;
			}
		}

		RtcDateTime[4] = (byte)(hour | pm);
	}

	private void CountDay()
	{
		RtcDateTime[3]++;
		if ( RtcDateTime[3] >= 7 )
			RtcDateTime[3] = 0;

		RtcDateTime[2] = BCDIncrement( RtcDateTime[2] );
		CheckEndOfMonth();
	}

	private void CheckEndOfMonth()
	{
		if ( RtcDateTime[2] > DaysInMonth() )
		{
			RtcDateTime[2] = 1;
			CountMonth();
		}
	}

	private void CountMonth()
	{
		RtcDateTime[1] = BCDIncrement( RtcDateTime[1] );
		if ( RtcDateTime[1] > 0x12 )
		{
			RtcDateTime[1] = 1;
			CountYear();
		}
	}

	private void CountYear()
	{
		RtcDateTime[0] = BCDIncrement( RtcDateTime[0] );
	}

	private byte DaysInMonth()
	{
		byte numdays;

		switch ( RtcDateTime[1] )
		{
			case 0x01:
			case 0x03:
			case 0x05:
			case 0x07:
			case 0x08:
			case 0x10:
			case 0x12:
				numdays = 0x31;
				break;

			case 0x04:
			case 0x06:
			case 0x09:
			case 0x11:
				numdays = 0x30;
				break;

			case 0x02:
			{
				numdays = 0x28;

				int year = RtcDateTime[0];
				year = (year & 0xF) + ((year >> 4) * 10);
				if ( (year & 3) == 0 )
					numdays = 0x29;
			}
			break;

			default:
				return 0;
		}

		return numdays;
	}

	private void SetRtcIRQ( byte irq )
	{
		byte oldstat = RtcIRQFlag;
		RtcIRQFlag |= irq;
		RtcStatusReg1 |= irq;

		if ( (oldstat & 0x30) == 0 && (RtcIRQFlag & 0x30) != 0 )
		{
			if ( (RCnt & 0xC100) == 0x8100 )
				SetIRQ( 1, IRQ.Rtc );
		}
	}

	private void ClearRtcIRQ( byte irq )
	{
		RtcIRQFlag = (byte)(RtcIRQFlag & ~irq);
	}

	private void ProcessRtcIRQ( int type )
	{
		switch ( RtcStatusReg2 & 0x0F )
		{
			case 0x00:
				if ( type == 2 )
					ClearRtcIRQ( 0x10 );
				break;

			case 0x01:
			case 0x05:
				if ( (type == 1 && (RtcClockCount & 0x3FF) == 0) || type == 2 )
				{
					uint mask = 0;
					if ( (RtcAlarm1[2] & (1 << 0)) != 0 ) mask |= 0x4000;
					if ( (RtcAlarm1[2] & (1 << 1)) != 0 ) mask |= 0x2000;
					if ( (RtcAlarm1[2] & (1 << 2)) != 0 ) mask |= 0x1000;
					if ( (RtcAlarm1[2] & (1 << 3)) != 0 ) mask |= 0x0800;
					if ( (RtcAlarm1[2] & (1 << 4)) != 0 ) mask |= 0x0400;

					if ( mask != 0 && (RtcClockCount & mask) != mask )
						SetRtcIRQ( 0x10 );
					else
						ClearRtcIRQ( 0x10 );
				}
				break;

			case 0x02:
			case 0x06:
				if ( type == 0 || (type == 2 && (RtcIRQFlag & 0x01) != 0) )
					SetRtcIRQ( 0x10 );
				break;

			case 0x03:
				if ( type == 0 || (type == 2 && (RtcIRQFlag & 0x01) != 0) )
					SetRtcIRQ( 0x10 );
				else if ( type == 1 && RtcDateTime[6] == 0x30 && (RtcClockCount & 0x7FFF) == 0 )
					ClearRtcIRQ( 0x10 );
				break;

			case 0x07:
				if ( type == 0 || (type == 2 && (RtcIRQFlag & 0x01) != 0) )
					SetRtcIRQ( 0x10 );
				else if ( type == 1 && RtcDateTime[6] == 0x00 && (RtcClockCount & 0x7FFF) == 256 )
					ClearRtcIRQ( 0x10 );
				break;

			case 0x04:
				if ( type == 0 )
				{
					bool cond = true;
					if ( (RtcAlarm1[0] & (1 << 7)) != 0 )
						cond = cond && ((RtcAlarm1[0] & 0x07) == RtcDateTime[3]);
					if ( (RtcAlarm1[1] & (1 << 7)) != 0 )
						cond = cond && ((RtcAlarm1[1] & 0x7F) == RtcDateTime[4]);
					if ( (RtcAlarm1[2] & (1 << 7)) != 0 )
						cond = cond && ((RtcAlarm1[2] & 0x7F) == RtcDateTime[5]);

					if ( cond )
						SetRtcIRQ( 0x10 );
					else
						ClearRtcIRQ( 0x10 );
				}
				break;

			default:
				if ( type == 1 )
				{
					SetRtcIRQ( 0x10 );
					ClearRtcIRQ( 0x10 );
				}
				break;
		}

		if ( (RtcStatusReg2 & (1 << 6)) != 0 )
		{
			if ( type == 0 )
			{
				bool cond = true;
				if ( (RtcAlarm2[0] & (1 << 7)) != 0 )
					cond = cond && ((RtcAlarm2[0] & 0x07) == RtcDateTime[3]);
				if ( (RtcAlarm2[1] & (1 << 7)) != 0 )
					cond = cond && ((RtcAlarm2[1] & 0x7F) == RtcDateTime[4]);
				if ( (RtcAlarm2[2] & (1 << 7)) != 0 )
					cond = cond && ((RtcAlarm2[2] & 0x7F) == RtcDateTime[5]);

				if ( cond )
					SetRtcIRQ( 0x20 );
				else
					ClearRtcIRQ( 0x20 );
			}
		}
		else
		{
			if ( type == 2 )
				ClearRtcIRQ( 0x20 );
		}
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
				{
					RtcStatusReg2 = val;
					ProcessRtcIRQ( 2 );
				}
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
