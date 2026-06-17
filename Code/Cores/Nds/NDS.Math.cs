namespace Emulotl.Nds;

public sealed partial class NDS
{
	public ushort DivCnt;
	public uint[] DivNumerator = new uint[2];
	public uint[] DivDenominator = new uint[2];
	public uint[] DivQuotient = new uint[2];
	public uint[] DivRemainder = new uint[2];

	public ushort SqrtCnt;
	public uint[] SqrtVal = new uint[2];
	public uint SqrtRes;

	private void ResetMath()
	{
		DivCnt = 0;
		SqrtCnt = 0;
		Array.Clear( DivNumerator );
		Array.Clear( DivDenominator );
		Array.Clear( DivQuotient );
		Array.Clear( DivRemainder );
		Array.Clear( SqrtVal );
		SqrtRes = 0;
	}

	private long DivNum64() => (long)(((ulong)DivNumerator[1] << 32) | DivNumerator[0]);
	private long DivDen64() => (long)(((ulong)DivDenominator[1] << 32) | DivDenominator[0]);

	private void SetQuotient( long v )
	{
		DivQuotient[0] = (uint)v;
		DivQuotient[1] = (uint)((ulong)v >> 32);
	}

	private void SetRemainder( long v )
	{
		DivRemainder[0] = (uint)v;
		DivRemainder[1] = (uint)((ulong)v >> 32);
	}

	public void StartDiv()
	{
		CancelEvent( SysEvent.Div );
		DivCnt |= 0x8000;
		ScheduleEvent( SysEvent.Div, false, (DivCnt & 0x3) == 0 ? 18 : 34, OnDivDone );
	}

	private void OnDivDone( uint param ) => DivDone();

	private void DivDone()
	{
		DivCnt &= 0x3FFF;

		switch ( DivCnt & 0x0003 )
		{
			case 0x0000:
				{
					int num = (int)DivNumerator[0];
					int den = (int)DivDenominator[0];
					if ( den == 0 )
					{
						DivQuotient[0] = (uint)(num < 0 ? 1 : -1);
						DivQuotient[1] = (uint)(num < 0 ? -1 : 0);
						SetRemainder( num );
					}
					else if ( num == int.MinValue && den == -1 )
					{
						SetQuotient( 0x80000000L );
					}
					else
					{
						SetQuotient( num / den );
						SetRemainder( num % den );
					}
					break;
				}

			case 0x0001:
			case 0x0003:
				{
					long num = DivNum64();
					int den = (int)DivDenominator[0];
					if ( den == 0 )
					{
						SetQuotient( num < 0 ? 1 : -1 );
						SetRemainder( num );
					}
					else if ( num == long.MinValue && den == -1 )
					{
						SetQuotient( long.MinValue );
						SetRemainder( 0 );
					}
					else
					{
						SetQuotient( num / den );
						SetRemainder( num % den );
					}
					break;
				}

			case 0x0002:
				{
					long num = DivNum64();
					long den = DivDen64();
					if ( den == 0 )
					{
						SetQuotient( num < 0 ? 1 : -1 );
						SetRemainder( num );
					}
					else if ( num == long.MinValue && den == -1 )
					{
						SetQuotient( long.MinValue );
						SetRemainder( 0 );
					}
					else
					{
						SetQuotient( num / den );
						SetRemainder( num % den );
					}
					break;
				}
		}

		if ( (DivDenominator[0] | DivDenominator[1]) == 0 )
			DivCnt |= 0x4000;
	}

	public void StartSqrt()
	{
		CancelEvent( SysEvent.Sqrt );
		SqrtCnt |= 0x8000;
		ScheduleEvent( SysEvent.Sqrt, false, 13, OnSqrtDone );
	}

	private void OnSqrtDone( uint param ) => SqrtDone();

	private void SqrtDone()
	{
		SqrtCnt &= 0x7FFF;

		ulong val;
		uint res = 0;
		ulong rem = 0;
		int nbits, topshift;

		if ( (SqrtCnt & 0x0001) != 0 )
		{
			val = ((ulong)SqrtVal[1] << 32) | SqrtVal[0];
			nbits = 32;
			topshift = 62;
		}
		else
		{
			val = SqrtVal[0];
			nbits = 16;
			topshift = 30;
		}

		for ( int i = 0; i < nbits; i++ )
		{
			rem = (rem << 2) + ((val >> topshift) & 0x3);
			val <<= 2;
			res <<= 1;

			uint prod = (res << 1) + 1;
			if ( rem >= prod )
			{
				rem -= prod;
				res++;
			}
		}

		SqrtRes = res;
	}
}
