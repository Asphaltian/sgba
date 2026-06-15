namespace Emulotl.Gba;

public sealed class GbaTimingEvent
{
	public GbaTimingEvent( Action<long> callback, int priority, string name )
	{
		Callback = callback;
		Priority = priority;
		Name = name;
	}

	public Action<long> Callback { get; }
	public int Priority { get; }
	public string Name { get; }

	public long When { get; internal set; }
	public bool Scheduled { get; internal set; }
	internal GbaTimingEvent Next;
}

public sealed class GbaTiming
{
	private GbaTimingEvent _root;

	public long NextEvent => _root?.When ?? long.MaxValue;

	public void Schedule( GbaTimingEvent ev, long when )
	{
		if ( ev.Scheduled )
			Deschedule( ev );

		ev.When = when;
		ev.Scheduled = true;

		GbaTimingEvent previous = null;
		GbaTimingEvent next = _root;
		while ( next != null )
		{
			if ( next.When > when || (next.When == when && next.Priority > ev.Priority) )
				break;

			previous = next;
			next = next.Next;
		}

		ev.Next = next;
		if ( previous == null )
			_root = ev;
		else
			previous.Next = ev;
	}

	public void Deschedule( GbaTimingEvent ev )
	{
		if ( !ev.Scheduled )
			return;

		GbaTimingEvent previous = null;
		GbaTimingEvent node = _root;
		while ( node != null )
		{
			if ( ReferenceEquals( node, ev ) )
			{
				if ( previous == null )
					_root = node.Next;
				else
					previous.Next = node.Next;
				break;
			}

			previous = node;
			node = node.Next;
		}

		ev.Next = null;
		ev.Scheduled = false;
	}

	public bool IsScheduled( GbaTimingEvent ev ) => ev.Scheduled;

	public long Tick( long currentCycle )
	{
		while ( _root != null && _root.When <= currentCycle )
		{
			GbaTimingEvent ev = _root;
			_root = ev.Next;
			ev.Next = null;
			ev.Scheduled = false;

			ev.Callback( currentCycle - ev.When );
		}

		return NextEvent;
	}

	public void Clear()
	{
		GbaTimingEvent node = _root;
		while ( node != null )
		{
			GbaTimingEvent next = node.Next;
			node.Next = null;
			node.Scheduled = false;
			node = next;
		}
		_root = null;
	}
}
