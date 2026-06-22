namespace Emulotl.Nds;

public sealed partial class NDS
{
	private const uint CPUStop_DMA9 = 0xFFF;
	private const uint CPUStop_DMA7 = 0xFFF << 16;
	private const uint CPUStop_GXStall = 1u << 31;

	public DMA[] DMAs;
	public uint[] DMA9Fill = new uint[4];

	public uint CPUStop;

	private void InitDmas()
	{
		DMAs = new DMA[8];
		DMAs[0] = new DMA( 0, 0, this );
		DMAs[1] = new DMA( 0, 1, this );
		DMAs[2] = new DMA( 0, 2, this );
		DMAs[3] = new DMA( 0, 3, this );
		DMAs[4] = new DMA( 1, 0, this );
		DMAs[5] = new DMA( 1, 1, this );
		DMAs[6] = new DMA( 1, 2, this );
		DMAs[7] = new DMA( 1, 3, this );
	}

	private void ResetDmas()
	{
		CPUStop = 0;

		for ( int i = 0; i < 8; i++ )
			DMAs[i].Reset();

		Array.Clear( DMA9Fill );
	}

	public void StopCPU( uint cpu, uint mask )
	{
		if ( cpu != 0 )
		{
			CPUStop |= mask << 16;
			ARM7.Halt( 2 );
		}
		else
		{
			CPUStop |= mask;
			ARM9.Halt( 2 );
		}
	}

	public void ResumeCPU( uint cpu, uint mask )
	{
		if ( cpu != 0 ) mask <<= 16;
		CPUStop &= ~mask;
	}

	public bool DMAsInMode( uint cpu, uint mode )
	{
		cpu <<= 2;
		if ( DMAs[cpu + 0].IsInMode( mode ) ) return true;
		if ( DMAs[cpu + 1].IsInMode( mode ) ) return true;
		if ( DMAs[cpu + 2].IsInMode( mode ) ) return true;
		if ( DMAs[cpu + 3].IsInMode( mode ) ) return true;

		return false;
	}

	public bool DMAsRunning( uint cpu )
	{
		cpu <<= 2;
		if ( DMAs[cpu + 0].IsRunning() ) return true;
		if ( DMAs[cpu + 1].IsRunning() ) return true;
		if ( DMAs[cpu + 2].IsRunning() ) return true;
		if ( DMAs[cpu + 3].IsRunning() ) return true;

		return false;
	}

	public void GXFIFOStall()
	{
		if ( (CPUStop & CPUStop_GXStall) != 0 ) return;

		CPUStop |= CPUStop_GXStall;

		if ( CurCPU == 1 )
		{
			ARM9.Halt( 2 );
		}
		else
		{
			DMAs[0].StallIfRunning();
			DMAs[1].StallIfRunning();
			DMAs[2].StallIfRunning();
			DMAs[3].StallIfRunning();
		}
	}

	public void GXFIFOUnstall()
	{
		CPUStop &= ~CPUStop_GXStall;
	}

	public void CheckDMAs( uint cpu, uint mode )
	{
		cpu <<= 2;
		DMAs[cpu + 0].StartIfNeeded( mode );
		DMAs[cpu + 1].StartIfNeeded( mode );
		DMAs[cpu + 2].StartIfNeeded( mode );
		DMAs[cpu + 3].StartIfNeeded( mode );
	}

	public void StopDMAs( uint cpu, uint mode )
	{
		cpu <<= 2;
		DMAs[cpu + 0].StopIfNeeded( mode );
		DMAs[cpu + 1].StopIfNeeded( mode );
		DMAs[cpu + 2].StopIfNeeded( mode );
		DMAs[cpu + 3].StopIfNeeded( mode );
	}

	private void RunDMAs( uint cpu )
	{
		uint b = cpu << 2;
		DMAs[b + 0].Run();
		DMAs[b + 1].Run();
		DMAs[b + 2].Run();
		DMAs[b + 3].Run();
	}
}
