namespace Emulotl.Nds;

public sealed partial class NDS
{
	public struct Timer
	{
		public ushort Reload;
		public ushort Cnt;
		public uint Counter;
		public int CycleShift;
	}

	private const int Arm9ClockShift = 1;

	public Timer[] Timers = new Timer[8];
	public byte[] TimerCheckMask = new byte[2];
	public long[] TimerTimestamp = new long[2];

	private static readonly int[] TimerPrescaler = [0, 6, 8, 10];

	private void ResetTimers()
	{
		Array.Clear( Timers );
		TimerCheckMask[0] = 0;
		TimerCheckMask[1] = 0;
		TimerTimestamp[0] = 0;
		TimerTimestamp[1] = 0;
	}

	private void HandleTimerOverflow( uint tid )
	{
		ref Timer timer = ref Timers[tid];

		timer.Counter += (uint)(timer.Reload << 10);
		if ( (timer.Cnt & (1 << 6)) != 0 )
			SetIRQ( tid >> 2, IRQ.Timer0 + (int)(tid & 0x3) );

		if ( (tid & 0x3) == 3 )
			return;

		for (; ; )
		{
			tid++;

			timer = ref Timers[tid];

			if ( (timer.Cnt & 0x84) != 0x84 )
				break;

			timer.Counter += 1 << 10;
			if ( (timer.Counter >> 26) == 0 )
				break;

			timer.Counter = (uint)(timer.Reload << 10);
			if ( (timer.Cnt & (1 << 6)) != 0 )
				SetIRQ( tid >> 2, IRQ.Timer0 + (int)(tid & 0x3) );

			if ( (tid & 0x3) == 3 )
				break;
		}
	}

	private void RunTimer( uint tid, int cycles )
	{
		ref Timer timer = ref Timers[tid];

		timer.Counter += (uint)(cycles << timer.CycleShift);
		while ( (timer.Counter >> 26) != 0 )
		{
			timer.Counter -= 1 << 26;
			HandleTimerOverflow( tid );
		}
	}

	public void RunTimers( uint cpu )
	{
		uint timermask = TimerCheckMask[cpu];
		int cycles;

		if ( cpu == 0 )
			cycles = (int)((ARM9Timestamp >> Arm9ClockShift) - TimerTimestamp[0]);
		else
			cycles = (int)(ARM7Timestamp - TimerTimestamp[1]);

		if ( (timermask & 0x1) != 0 ) RunTimer( (cpu << 2) + 0, cycles );
		if ( (timermask & 0x2) != 0 ) RunTimer( (cpu << 2) + 1, cycles );
		if ( (timermask & 0x4) != 0 ) RunTimer( (cpu << 2) + 2, cycles );
		if ( (timermask & 0x8) != 0 ) RunTimer( (cpu << 2) + 3, cycles );

		TimerTimestamp[cpu] += cycles;
	}

	public ushort TimerGetCounter( uint timer )
	{
		RunTimers( timer >> 2 );
		uint ret = Timers[timer].Counter;

		return (ushort)(ret >> 10);
	}

	public void TimerStart( uint id, ushort cnt )
	{
		ref Timer timer = ref Timers[id];
		ushort curstart = (ushort)(timer.Cnt & (1 << 7));
		ushort newstart = (ushort)(cnt & (1 << 7));

		RunTimers( id >> 2 );

		timer.Cnt = cnt;
		timer.CycleShift = 10 - TimerPrescaler[cnt & 0x03];

		if ( curstart == 0 && newstart != 0 )
			timer.Counter = (uint)(timer.Reload << 10);

		if ( (cnt & 0x84) == 0x80 )
			TimerCheckMask[id >> 2] |= (byte)(0x01 << (int)(id & 0x3));
		else
			TimerCheckMask[id >> 2] &= (byte)~(0x01 << (int)(id & 0x3));
	}
}
