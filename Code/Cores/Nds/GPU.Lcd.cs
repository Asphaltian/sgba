namespace Emulotl.Nds;

public sealed partial class GPU
{
	private const int LineCycles = 355 * 6;
	private const int HBlankCycles = 48 + (256 * 6);

	public ushort VCount;
	public ushort[] DispStat = new ushort[2];
	public ushort[] VMatch = new ushort[2];
	public ushort TotalScanlines;

	public bool ScreensEnabled;
	public bool ScreenSwap;

	private ushort NextVCount;
	private bool VCountOverride;

	public uint CaptureCnt;
	public bool CaptureEnable;

	private void ResetLcd()
	{
		VCount = 0;
		DispStat[0] = DispStat[1] = 0;
		VMatch[0] = VMatch[1] = 0;
		TotalScanlines = 0;
		ScreensEnabled = false;
		ScreenSwap = false;
		CaptureCnt = 0;
		CaptureEnable = false;
		NextVCount = 0;
		VCountOverride = false;
	}

	private void SetDispStatIRQ( int cpu, int num )
	{
		ushort irqmask = (ushort)(1 << num);
		ushort enablemask = (ushort)(1 << (num + 3));

		if ( (DispStat[cpu] & irqmask) != 0 )
			return;

		DispStat[cpu] |= irqmask;
		if ( (DispStat[cpu] & enablemask) != 0 )
			NDS.SetIRQ( (uint)cpu, num );
	}

	public void SetDispStat( int cpu, ushort val, ushort mask )
	{
		const ushort ro_mask = 0x0047;

		val &= (ushort)(mask & ~ro_mask);
		DispStat[cpu] &= (ushort)(~mask | ro_mask);
		DispStat[cpu] |= val;

		VMatch[cpu] = (ushort)((DispStat[cpu] >> 8) | ((DispStat[cpu] & 0x80) << 1));
	}

	public void SetVCount( ushort val, ushort mask )
	{
		val &= (ushort)(mask & 0x1FF);
		NextVCount = (ushort)((NextVCount & ~mask) | (val & mask));
		VCountOverride = true;
	}

	public void StartFrame()
	{
		ScreensEnabled = (NDS.PowerControl9 & (1 << 0)) != 0;
		RebuildVRAMFlat();
		TotalScanlines = 0;
		StartScanline( 0 );
	}

	private void StartScanline( uint line )
	{
		DispStat[0] &= unchecked((ushort)~(1 << 1));
		DispStat[1] &= unchecked((ushort)~(1 << 1));

		if ( line == 0 )
			VCount = 0;
		else
		{
			VCount++;
			VCount &= 0x1FF;
		}

		GPU2D_A.UpdateWindows( VCount );
		GPU2D_B.UpdateWindows( VCount );

		if ( VCount == 0 )
		{
			if ( (CaptureCnt & (1u << 31)) != 0 )
			{
				CaptureEnable = true;
				CheckCaptureStart();
			}
		}

		if ( VCount >= 2 && VCount < 194 )
			NDS.CheckDMAs( 0, 0x03 );
		else if ( VCount == 194 )
			NDS.StopDMAs( 0, 0x03 );

		if ( VCount == 192 )
		{
			if ( CaptureEnable )
				CheckCaptureEnd();

			NDS.GPU3D.VBlank();

			NDS.Render2DA.VBlank();
			NDS.Render2DB.VBlank();

			if ( CaptureEnable )
			{
				CaptureCnt &= ~(1u << 31);
				CaptureEnable = false;
			}

			SetDispStatIRQ( 0, 0 );
			SetDispStatIRQ( 1, 0 );

			DispStat[0] |= 1 << 0;
			DispStat[1] |= 1 << 0;

			NDS.StopDMAs( 0, 0x04 );

			NDS.CheckDMAs( 0, 0x01 );
			NDS.CheckDMAs( 1, 0x11 );
		}
		else if ( VCount == 262 )
		{
			DispStat[0] &= unchecked((ushort)~(1 << 0));
			DispStat[1] &= unchecked((ushort)~(1 << 0));
		}

		if ( VCountOverride )
		{
			VCount = NextVCount;
			VCountOverride = false;
		}

		if ( VCount == VMatch[0] ) SetDispStatIRQ( 0, 2 );
		else DispStat[0] &= unchecked((ushort)~(1 << 2));

		if ( VCount == VMatch[1] ) SetDispStatIRQ( 1, 2 );
		else DispStat[1] &= unchecked((ushort)~(1 << 2));

		NDS.ScheduleEvent( SysEvent.Lcd, true, HBlankCycles, OnLcdStartHBlank, line );
	}

	private void StartHBlank( uint line )
	{
		DispStat[0] |= 1 << 1;
		DispStat[1] |= 1 << 1;

		bool resetregs = VCount == 262;

		GPU2D_A.UpdateRegistersPreDraw( resetregs );
		GPU2D_B.UpdateRegistersPreDraw( resetregs );

		if ( VCount < 192 )
		{
			NDS.Render2DA.DrawScanline( VCount );
			NDS.Render2DB.DrawScanline( VCount );

			NDS.CheckDMAs( 0, 0x02 );
		}

		GPU2D_A.UpdateRegistersPostDraw( resetregs );
		GPU2D_B.UpdateRegistersPostDraw( resetregs );

		if ( (DispStat[0] & (1 << 4)) != 0 ) NDS.SetIRQ( 0, IRQ.HBlank );
		if ( (DispStat[1] & (1 << 4)) != 0 ) NDS.SetIRQ( 1, IRQ.HBlank );

		if ( VCount == 262 || VCount == 511 )
			NDS.ScheduleEvent( SysEvent.Lcd, true, LineCycles - HBlankCycles, OnLcdFinishFrame, line + 1 );
		else
			NDS.ScheduleEvent( SysEvent.Lcd, true, LineCycles - HBlankCycles, OnLcdStartScanline, line + 1 );
	}

	private void FinishFrame( uint lines )
	{
		TotalScanlines = (ushort)lines;
	}

	private void OnLcdStartScanline( uint line ) => StartScanline( line );
	private void OnLcdStartHBlank( uint line ) => StartHBlank( line );
	private void OnLcdFinishFrame( uint line ) => FinishFrame( line );
}
