namespace Emulotl.Nds;

public enum SysEvent
{
	Lcd,
	Spu,
	Wifi,
	DisplayFifo,
	RomTransfer,
	RomSpiTransfer,
	SpiTransfer,
	Div,
	Sqrt,
	Rtc,
	Timer9,
	Timer7,
	Dma9,
	Dma7,

	Count
}

public sealed partial class NDS
{
	private struct SchedEvent
	{
		public bool Active;
		public long Timestamp;
		public Action<uint> Func;
		public uint Param;
	}

	private readonly SchedEvent[] _schedList = new SchedEvent[(int)SysEvent.Count];
	private uint _schedMask;

	public int CurCPU;

	public long SysTimestamp { get; private set; }

	private void ResetScheduler()
	{
		SysTimestamp = 0;
		CurCPU = 0;
		Array.Clear( _schedList );
		_schedMask = 0;
	}

	public void ScheduleEvent( SysEvent id, bool periodic, long delay, Action<uint> func, uint param = 0 )
	{
		ref SchedEvent ev = ref _schedList[(int)id];

		if ( periodic )
			ev.Timestamp += delay;
		else
			ev.Timestamp = (CurCPU == 0 ? (ARM9Timestamp >> 1) : ARM7Timestamp) + delay;

		ev.Active = true;
		ev.Func = func;
		ev.Param = param;
		_schedMask |= 1u << (int)id;

		Reschedule( ev.Timestamp );
	}

	public void Reschedule( long target )
	{
		if ( CurCPU == 0 )
		{
			if ( target < (ARM9Target >> 1) )
				ARM9Target = target << 1;
		}
		else
		{
			if ( target < ARM7Target )
				ARM7Target = target;
		}
	}

	public void CancelEvent( SysEvent id )
	{
		_schedList[(int)id].Active = false;
		_schedMask &= ~(1u << (int)id);
	}

	public bool EventScheduled( SysEvent id ) => _schedList[(int)id].Active;

	public long NextEventTimestamp()
	{
		long min = long.MaxValue;
		uint mask = _schedMask;
		for ( int i = 0; mask != 0; i++, mask >>= 1 )
		{
			if ( (mask & 1) != 0 && _schedList[i].Timestamp < min )
				min = _schedList[i].Timestamp;
		}
		return min;
	}

	public void RunSystem( long target )
	{
		SysTimestamp = target;

		uint mask = _schedMask;
		for ( int i = 0; mask != 0; i++, mask >>= 1 )
		{
			if ( (mask & 1) != 0 )
			{
				ref SchedEvent ev = ref _schedList[i];
				if ( ev.Timestamp <= SysTimestamp )
				{
					ev.Active = false;
					_schedMask &= ~(1u << i);
					ev.Func?.Invoke( ev.Param );
				}
			}
		}
	}
}
