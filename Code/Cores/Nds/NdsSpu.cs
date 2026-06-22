namespace Emulotl.Nds;

public enum AudioInterpolation
{
	None,
	Linear,
	Cosine,
	Cubic,
	SNESGaussian
}

public sealed class SPUChannel
{
	public static readonly sbyte[] ADPCMIndexTable = [-1, -1, -1, -1, 2, 4, 6, 8];

	public static readonly ushort[] ADPCMTable =
	[
		0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E,
		0x0010, 0x0011, 0x0013, 0x0015, 0x0017, 0x0019, 0x001C, 0x001F,
		0x0022, 0x0025, 0x0029, 0x002D, 0x0032, 0x0037, 0x003C, 0x0042,
		0x0049, 0x0050, 0x0058, 0x0061, 0x006B, 0x0076, 0x0082, 0x008F,
		0x009D, 0x00AD, 0x00BE, 0x00D1, 0x00E6, 0x00FD, 0x0117, 0x0133,
		0x0151, 0x0173, 0x0198, 0x01C1, 0x01EE, 0x0220, 0x0256, 0x0292,
		0x02D4, 0x031C, 0x036C, 0x03C3, 0x0424, 0x048E, 0x0502, 0x0583,
		0x0610, 0x06AB, 0x0756, 0x0812, 0x08E0, 0x09C3, 0x0ABD, 0x0BD0,
		0x0CFF, 0x0E4C, 0x0FBA, 0x114C, 0x1307, 0x14EE, 0x1706, 0x1954,
		0x1BDC, 0x1EA5, 0x21B6, 0x2515, 0x28CA, 0x2CDF, 0x315B, 0x364B,
		0x3BB9, 0x41B2, 0x4844, 0x4F7E, 0x5771, 0x602F, 0x69CE, 0x7462,
		0x7FFF
	];

	public static readonly short[,] PSGTable =
	{
		{ -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF, -0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF,  0x7FFF },
		{ -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF, -0x7FFF }
	};

	private static readonly byte[] VolShift = [4, 3, 2, 0];

	public AudioInterpolation InterpType = AudioInterpolation.None;

	public readonly uint Num;

	public uint Cnt;
	public uint SrcAddr;
	public ushort TimerReload;
	public uint LoopPos;
	public uint Length;

	public byte Volume;
	public byte VolumeShift;
	public byte Pan;

	public bool KeyOn;
	public uint Timer;
	public int Pos;
	public short[] PrevSample = new short[3];
	public short CurSample;
	public ushort NoiseVal;

	public int ADPCMVal;
	public int ADPCMIndex;
	public int ADPCMValLoop;
	public int ADPCMIndexLoop;
	public byte ADPCMCurByte;

	private readonly byte[] FIFO = new byte[32];
	public uint FIFOReadPos;
	public uint FIFOWritePos;
	public uint FIFOReadOffset;
	public uint FIFOLevel;

	private readonly NDS NDS;

	public SPUChannel( uint num, NDS nds, AudioInterpolation interpolation )
	{
		Num = num;
		NDS = nds;
		InterpType = interpolation;
	}

	public void Reset()
	{
		KeyOn = false;

		SetCnt( 0 );
		SrcAddr = 0;
		TimerReload = 0;
		LoopPos = 0;
		Length = 0;

		Timer = 0;

		Pos = 0;
		FIFOReadPos = 0;
		FIFOWritePos = 0;
		FIFOReadOffset = 0;
		FIFOLevel = 0;
	}

	public void SetCnt( uint val )
	{
		uint oldcnt = Cnt;
		Cnt = val & 0xFF7F837F;

		Volume = (byte)(Cnt & 0x7F);
		if ( Volume == 127 ) Volume++;

		VolumeShift = VolShift[(Cnt >> 8) & 0x3];

		Pan = (byte)((Cnt >> 16) & 0x7F);
		if ( Pan == 127 ) Pan++;

		if ( (val & (1u << 31)) != 0 && (oldcnt & (1u << 31)) == 0 )
			KeyOn = true;
	}

	public void SetSrcAddr( uint val ) { SrcAddr = val & 0x07FFFFFC; }
	public void SetTimerReload( uint val ) { TimerReload = (ushort)(val & 0xFFFF); }
	public void SetLoopPos( uint val ) { LoopPos = (val & 0xFFFF) << 2; }
	public void SetLength( uint val ) { Length = (val & 0x001FFFFF) << 2; }

	private void FifoWriteU32( uint idx, uint val )
	{
		int b = (int)(idx * 4);
		FIFO[b] = (byte)val;
		FIFO[b + 1] = (byte)(val >> 8);
		FIFO[b + 2] = (byte)(val >> 16);
		FIFO[b + 3] = (byte)(val >> 24);
	}

	private uint FifoReadU32At( uint b )
	{
		return (uint)(FIFO[b] | (FIFO[b + 1] << 8) | (FIFO[b + 2] << 16) | (FIFO[b + 3] << 24));
	}

	private void FifoConsume( uint size )
	{
		FIFOReadPos += size;
		FIFOReadPos &= 0x1F;
		FIFOLevel -= size;

		if ( FIFOLevel <= 16 )
			FIFO_BufferData();
	}

	private sbyte FifoReadS8() { sbyte v = (sbyte)FIFO[FIFOReadPos]; FifoConsume( 1 ); return v; }
	private short FifoReadS16() { short v = (short)(FIFO[FIFOReadPos] | (FIFO[FIFOReadPos + 1] << 8)); FifoConsume( 2 ); return v; }
	private byte FifoReadU8() { byte v = FIFO[FIFOReadPos]; FifoConsume( 1 ); return v; }
	private uint FifoReadU32() { uint v = FifoReadU32At( FIFOReadPos ); FifoConsume( 4 ); return v; }

	public void FIFO_BufferData()
	{
		uint totallen = LoopPos + Length;

		if ( FIFOReadOffset >= totallen )
		{
			uint repeatmode = (Cnt >> 27) & 0x3;
			if ( (repeatmode & 1) != 0 ) FIFOReadOffset = LoopPos;
			else if ( (repeatmode & 2) != 0 ) return;
		}

		uint burstlen = 16;
		if ( (FIFOReadOffset + 16) > totallen )
			burstlen = totallen - FIFOReadOffset;

		if ( (SrcAddr + FIFOReadOffset) >= 0x00004000 )
		{
			for ( uint i = 0; i < burstlen; i += 4 )
			{
				FifoWriteU32( FIFOWritePos, NDS.ARM7Read32( SrcAddr + FIFOReadOffset ) );
				FIFOReadOffset += 4;
				FIFOWritePos++;
				FIFOWritePos &= 0x7;
			}
		}
		else
		{
			for ( uint i = 0; i < burstlen; i += 4 )
			{
				FifoWriteU32( FIFOWritePos, 0 );
				FIFOReadOffset += 4;
				FIFOWritePos++;
				FIFOWritePos &= 0x7;
			}
		}

		FIFOLevel += burstlen;
	}

	public void Start()
	{
		Timer = TimerReload;

		if ( ((Cnt >> 29) & 0x3) == 3 )
			Pos = -1;
		else
			Pos = -3;

		NoiseVal = 0x7FFF;
		PrevSample[0] = 0;
		PrevSample[1] = 0;
		PrevSample[2] = 0;
		CurSample = 0;

		FIFOReadPos = 0;
		FIFOWritePos = 0;
		FIFOReadOffset = 0;
		FIFOLevel = 0;

		if ( ((Cnt >> 29) & 0x3) != 3 )
		{
			FIFO_BufferData();
			FIFO_BufferData();
		}
	}

	public void NextSample_PCM8()
	{
		Pos++;
		if ( Pos < 0 ) return;
		if ( (uint)Pos >= (LoopPos + Length) )
		{
			uint repeat = (Cnt >> 27) & 0x3;
			if ( (repeat & 1) != 0 )
			{
				Pos = (int)LoopPos;
			}
			else if ( (repeat & 2) != 0 )
			{
				CurSample = 0;
				Cnt &= ~(1u << 31);
				return;
			}
		}

		sbyte val = FifoReadS8();
		CurSample = (short)(val << 8);
	}

	public void NextSample_PCM16()
	{
		Pos++;
		if ( Pos < 0 ) return;
		if ( (uint)(Pos << 1) >= (LoopPos + Length) )
		{
			uint repeat = (Cnt >> 27) & 0x3;
			if ( (repeat & 1) != 0 )
			{
				Pos = (int)(LoopPos >> 1);
			}
			else if ( (repeat & 2) != 0 )
			{
				CurSample = 0;
				Cnt &= ~(1u << 31);
				return;
			}
		}

		short val = FifoReadS16();
		CurSample = val;
	}

	public void NextSample_ADPCM()
	{
		Pos++;
		if ( Pos < 8 )
		{
			if ( Pos == 0 )
			{
				uint header = FifoReadU32();
				ADPCMVal = (short)(header & 0xFFFF);
				ADPCMIndex = (int)((header >> 16) & 0x7F);
				if ( ADPCMIndex > 88 ) ADPCMIndex = 88;

				ADPCMValLoop = ADPCMVal;
				ADPCMIndexLoop = ADPCMIndex;
			}

			return;
		}

		if ( (uint)(Pos >> 1) >= (LoopPos + Length) )
		{
			uint repeat = (Cnt >> 27) & 0x3;
			if ( (repeat & 1) != 0 )
			{
				Pos = (int)(LoopPos << 1);
				ADPCMVal = ADPCMValLoop;
				ADPCMIndex = ADPCMIndexLoop;
				ADPCMCurByte = FifoReadU8();
			}
			else if ( (repeat & 2) != 0 )
			{
				CurSample = 0;
				Cnt &= ~(1u << 31);
				return;
			}
		}
		else
		{
			if ( (Pos & 0x1) == 0 )
				ADPCMCurByte = FifoReadU8();
			else
				ADPCMCurByte >>= 4;

			ushort val = ADPCMTable[ADPCMIndex];
			ushort diff = (ushort)(val >> 3);
			if ( (ADPCMCurByte & 0x1) != 0 ) diff += (ushort)(val >> 2);
			if ( (ADPCMCurByte & 0x2) != 0 ) diff += (ushort)(val >> 1);
			if ( (ADPCMCurByte & 0x4) != 0 ) diff += val;

			if ( (ADPCMCurByte & 0x8) != 0 )
			{
				ADPCMVal -= diff;
				if ( ADPCMVal < -0x7FFF ) ADPCMVal = -0x7FFF;
			}
			else
			{
				ADPCMVal += diff;
				if ( ADPCMVal > 0x7FFF ) ADPCMVal = 0x7FFF;
			}

			ADPCMIndex += ADPCMIndexTable[ADPCMCurByte & 0x7];
			if ( ADPCMIndex < 0 ) ADPCMIndex = 0;
			else if ( ADPCMIndex > 88 ) ADPCMIndex = 88;

			if ( Pos == (int)(LoopPos << 1) )
			{
				ADPCMValLoop = ADPCMVal;
				ADPCMIndexLoop = ADPCMIndex;
			}
		}

		CurSample = (short)ADPCMVal;
	}

	public void NextSample_PSG()
	{
		Pos++;
		CurSample = PSGTable[(Cnt >> 24) & 0x7, Pos & 0x7];
	}

	public void NextSample_Noise()
	{
		if ( (NoiseVal & 0x1) != 0 )
		{
			NoiseVal = (ushort)((NoiseVal >> 1) ^ 0x6000);
			CurSample = -0x7FFF;
		}
		else
		{
			NoiseVal >>= 1;
			CurSample = 0x7FFF;
		}
	}

	private int Run( uint cycles, int type )
	{
		if ( (Cnt & (1u << 31)) == 0 ) return 0;

		if ( (type < 3) && ((Length + LoopPos) < 16) ) return 0;

		if ( KeyOn )
		{
			Start();
			KeyOn = false;
		}

		Timer += cycles;

		while ( (Timer >> 16) != 0 )
		{
			Timer = TimerReload + (Timer - 0x10000);

			if ( (type < 3) && (InterpType != AudioInterpolation.None) )
			{
				PrevSample[2] = PrevSample[1];
				PrevSample[1] = PrevSample[0];
				PrevSample[0] = CurSample;
			}

			switch ( type )
			{
				case 0: NextSample_PCM8(); break;
				case 1: NextSample_PCM16(); break;
				case 2: NextSample_ADPCM(); break;
				case 3: NextSample_PSG(); break;
				case 4: NextSample_Noise(); break;
			}

			if ( (Cnt & (1u << 31)) == 0 ) break;
		}

		int val = CurSample;

		if ( (type < 3) && (InterpType != AudioInterpolation.None) )
		{
			int samplepos = (int)((Timer - TimerReload) * 0x100 / (0x10000 - TimerReload));
			if ( samplepos > 0xFF ) samplepos = 0xFF;

			switch ( InterpType )
			{
				case AudioInterpolation.Linear:
					val = ((val * samplepos) +
						   (PrevSample[0] * (0xFF - samplepos))) >> 8;
					break;

				case AudioInterpolation.Cosine:
					val = ((val * SPU.InterpCos[samplepos]) +
						   (PrevSample[0] * SPU.InterpCos[0xFF - samplepos])) >> 14;
					break;

				case AudioInterpolation.Cubic:
					val = ((PrevSample[2] * SPU.InterpCubic[samplepos, 0]) +
						   (PrevSample[1] * SPU.InterpCubic[samplepos, 1]) +
						   (PrevSample[0] * SPU.InterpCubic[samplepos, 2]) +
						   (val * SPU.InterpCubic[samplepos, 3])) >> 14;
					break;

				case AudioInterpolation.SNESGaussian:
					{
						int out_ = (SPU.InterpSNESGauss[0x0FF - samplepos] * GaussClamp( PrevSample[2] )) >> 10;
						out_ = out_ + ((SPU.InterpSNESGauss[0x1FF - samplepos] * GaussClamp( PrevSample[1] )) >> 10);
						out_ = out_ + ((SPU.InterpSNESGauss[0x100 + samplepos] * GaussClamp( PrevSample[0] )) >> 10);
						out_ = out_ + ((SPU.InterpSNESGauss[0x000 + samplepos] * GaussClamp( val )) >> 10);
						val = Math.Clamp( out_, -0x8000, 0x7FFF );
						break;
					}
			}
		}

		val <<= VolumeShift;
		val *= Volume;
		return val;
	}

	private static int GaussClamp( int s ) => Math.Clamp( s >> 1, -0x3FFA, 0x3FF8 );

	public int DoRun( uint cycles )
	{
		switch ( (Cnt >> 29) & 0x3 )
		{
			case 0: return Run( cycles, 0 );
			case 1: return Run( cycles, 1 );
			case 2: return Run( cycles, 2 );
			case 3:
				if ( Num >= 14 )
					return Run( cycles, 4 );
				else if ( Num >= 8 )
					return Run( cycles, 3 );
				return 0;
			default:
				return 0;
		}
	}

	public void PanOutput( int in_, ref int left, ref int right )
	{
		left += (int)(((long)in_ * (128 - Pan)) >> 10);
		right += (int)(((long)in_ * Pan) >> 10);
	}
}

public sealed class SPUCaptureUnit
{
	public readonly uint Num;

	public byte Cnt;
	public uint DstAddr;
	public ushort TimerReload;
	public uint Length;

	public uint Timer;
	public int Pos;

	private readonly byte[] FIFO = new byte[16];
	public uint FIFOReadPos;
	public uint FIFOWritePos;
	public uint FIFOWriteOffset;
	public uint FIFOLevel;

	private readonly NDS NDS;

	public SPUCaptureUnit( uint num, NDS nds )
	{
		Num = num;
		NDS = nds;
	}

	public void Reset()
	{
		SetCnt( 0 );
		DstAddr = 0;
		TimerReload = 0;
		Length = 0;

		Timer = 0;

		Pos = 0;
		FIFOReadPos = 0;
		FIFOWritePos = 0;
		FIFOWriteOffset = 0;
		FIFOLevel = 0;
	}

	public void SetCnt( byte val )
	{
		if ( (val & 0x80) != 0 && (Cnt & 0x80) == 0 )
			Start();

		val &= 0x8F;
		if ( (val & 0x80) == 0 ) val &= unchecked((byte)~0x01);
		Cnt = val;
	}

	public void SetDstAddr( uint val ) { DstAddr = val & 0x07FFFFFC; }
	public void SetTimerReload( uint val ) { TimerReload = (ushort)(val & 0xFFFF); }
	public void SetLength( uint val ) { Length = val << 2; if ( Length == 0 ) Length = 4; }

	public void Start()
	{
		Timer = TimerReload;
		Pos = 0;
		FIFOReadPos = 0;
		FIFOWritePos = 0;
		FIFOWriteOffset = 0;
		FIFOLevel = 0;
	}

	private uint FifoReadU32At( uint b )
	{
		return (uint)(FIFO[b] | (FIFO[b + 1] << 8) | (FIFO[b + 2] << 16) | (FIFO[b + 3] << 24));
	}

	public void FIFO_FlushData()
	{
		for ( uint i = 0; i < 4; i++ )
		{
			NDS.ARM7Write32( DstAddr + FIFOWriteOffset, FifoReadU32At( FIFOReadPos * 4 ) );

			FIFOReadPos++;
			FIFOReadPos &= 0x3;
			FIFOLevel -= 4;

			FIFOWriteOffset += 4;
			if ( FIFOWriteOffset >= Length )
			{
				FIFOWriteOffset = 0;
				break;
			}
		}
	}

	private void FIFO_WriteS8( sbyte val )
	{
		FIFO[FIFOWritePos] = (byte)val;
		FIFOWritePos += 1;
		FIFOWritePos &= 0xF;
		FIFOLevel += 1;
		if ( FIFOLevel >= 16 ) FIFO_FlushData();
	}

	private void FIFO_WriteS16( short val )
	{
		FIFO[FIFOWritePos] = (byte)val;
		FIFO[(FIFOWritePos + 1) & 0xF] = (byte)(val >> 8);
		FIFOWritePos += 2;
		FIFOWritePos &= 0xF;
		FIFOLevel += 2;
		if ( FIFOLevel >= 16 ) FIFO_FlushData();
	}

	public void Run( uint cycles, int sample )
	{
		Timer += cycles;

		if ( (Cnt & 0x08) != 0 )
		{
			while ( (Timer >> 16) != 0 )
			{
				Timer = TimerReload + (Timer - 0x10000);

				FIFO_WriteS8( (sbyte)(sample >> 8) );
				Pos++;
				if ( Pos >= Length )
				{
					if ( FIFOLevel >= 4 )
						FIFO_FlushData();

					if ( (Cnt & 0x04) != 0 )
					{
						Cnt &= 0x7F;
						return;
					}
					else
						Pos = 0;
				}
			}
		}
		else
		{
			while ( (Timer >> 16) != 0 )
			{
				Timer = TimerReload + (Timer - 0x10000);

				FIFO_WriteS16( (short)sample );
				Pos += 2;
				if ( Pos >= Length )
				{
					if ( FIFOLevel >= 4 )
						FIFO_FlushData();

					if ( (Cnt & 0x04) != 0 )
					{
						Cnt &= 0x7F;
						return;
					}
					else
						Pos = 0;
				}
			}
		}
	}
}

public sealed class SPU
{
	public static readonly short[] InterpCos = new short[0x100];
	public static readonly short[,] InterpCubic = new short[0x100, 4];
	public static readonly short[] InterpSNESGauss;

	static SPU()
	{
		for ( int i = 0; i < 0x100; i++ )
		{
			double ratio = i * Math.PI / 255.0;
			ratio = 1.0 - Math.Cos( ratio );
			InterpCos[i] = (short)(ratio * 0x2000);
		}

		for ( int i = 0; i < 0x100; i++ )
		{
			int i1 = i << 6;
			int i2 = (i * i) >> 2;
			int i3 = (i * i * i) >> 10;

			InterpCubic[i, 0] = (short)(-i3 + 2 * i2 - i1);
			InterpCubic[i, 1] = (short)(i3 - 2 * i2 + 0x4000);
			InterpCubic[i, 2] = (short)(-i3 + i2 + i1);
			InterpCubic[i, 3] = (short)(i3 - i2);
		}

		InterpSNESGauss = SnesGaussTable();
	}

	private uint Cnt;
	private byte MasterVolume;
	private ushort Bias;
	private bool ApplyBias = true;
	private bool Degrade10Bit;
	private bool Mute;

	private uint MixInterval;

	private readonly short[] OutputLastSamples = new short[2];

	private readonly SPUChannel[] Channels = new SPUChannel[16];
	private readonly SPUCaptureUnit[] Capture = new SPUCaptureUnit[2];

	private readonly NDS NDS;
	private readonly NdsAudio Output;

	public SPU( NDS nds, NdsAudio output )
	{
		NDS = nds;
		Output = output;

		for ( uint i = 0; i < 16; i++ )
			Channels[i] = new SPUChannel( i, nds, AudioInterpolation.None );

		Capture[0] = new SPUCaptureUnit( 0, nds );
		Capture[1] = new SPUCaptureUnit( 1, nds );

		ApplyBias = true;
		Degrade10Bit = false;

		SetSampleRate( false );
	}

	public void Reset()
	{
		Cnt = 0;
		MasterVolume = 0;
		Bias = 0;
		Mute = true;

		for ( int i = 0; i < 16; i++ )
			Channels[i].Reset();

		Capture[0].Reset();
		Capture[1].Reset();

		NDS.ScheduleEvent( SysEvent.Spu, false, 1024, Mix, 0 );
	}

	public void SetPowerCnt( uint val )
	{
		Mute = (val & (1 << 0)) == 0;
	}

	public void SetSampleRate( bool fast )
	{
		MixInterval = fast ? 704u : 1024u;
		OutputLastSamples[0] = 0;
		OutputLastSamples[1] = 0;
	}

	public void SetInterpolation( AudioInterpolation type )
	{
		foreach ( SPUChannel channel in Channels )
			channel.InterpType = type;
	}

	public void SetBias( ushort bias ) { Bias = bias; }
	public void SetApplyBias( bool enable ) { ApplyBias = enable; }
	public void SetDegrade10Bit( bool enable ) { Degrade10Bit = enable; }

	public void Mix( uint spucycles )
	{
		int left = 0, right = 0;
		int leftoutput = 0, rightoutput = 0;

		if ( (Cnt & (1 << 15)) != 0 )
		{
			int ch0 = Channels[0].DoRun( spucycles );
			int ch1 = Channels[1].DoRun( spucycles );
			int ch2 = Channels[2].DoRun( spucycles );
			int ch3 = Channels[3].DoRun( spucycles );

			Channels[0].PanOutput( ch0, ref left, ref right );
			Channels[2].PanOutput( ch2, ref left, ref right );

			if ( (Cnt & (1 << 12)) == 0 ) Channels[1].PanOutput( ch1, ref left, ref right );
			if ( (Cnt & (1 << 13)) == 0 ) Channels[3].PanOutput( ch3, ref left, ref right );

			for ( int i = 4; i < 16; i++ )
			{
				SPUChannel chan = Channels[i];
				int channel = chan.DoRun( spucycles );
				chan.PanOutput( channel, ref left, ref right );
			}

			if ( (Capture[0].Cnt & (1 << 7)) != 0 )
			{
				int val = left;
				val >>= 8;
				if ( val < -0x8000 ) val = -0x8000;
				else if ( val > 0x7FFF ) val = 0x7FFF;
				Capture[0].Run( spucycles, val );
			}

			if ( (Capture[1].Cnt & (1 << 7)) != 0 )
			{
				int val = right;
				val >>= 8;
				if ( val < -0x8000 ) val = -0x8000;
				else if ( val > 0x7FFF ) val = 0x7FFF;
				Capture[1].Run( spucycles, val );
			}

			switch ( Cnt & 0x0300 )
			{
				case 0x0000:
					leftoutput = left;
					break;
				case 0x0100:
					{
						int pan = 128 - Channels[1].Pan;
						leftoutput = (int)(((long)ch1 * pan) >> 10);
						break;
					}
				case 0x0200:
					{
						int pan = 128 - Channels[3].Pan;
						leftoutput = (int)(((long)ch3 * pan) >> 10);
						break;
					}
				case 0x0300:
					{
						int pan1 = 128 - Channels[1].Pan;
						int pan3 = 128 - Channels[3].Pan;
						leftoutput = (int)((((long)ch1 * pan1) >> 10) + (((long)ch3 * pan3) >> 10));
						break;
					}
			}

			switch ( Cnt & 0x0C00 )
			{
				case 0x0000:
					rightoutput = right;
					break;
				case 0x0400:
					{
						int pan = Channels[1].Pan;
						rightoutput = (int)(((long)ch1 * pan) >> 10);
						break;
					}
				case 0x0800:
					{
						int pan = Channels[3].Pan;
						rightoutput = (int)(((long)ch3 * pan) >> 10);
						break;
					}
				case 0x0C00:
					{
						int pan1 = Channels[1].Pan;
						int pan3 = Channels[3].Pan;
						rightoutput = (int)((((long)ch1 * pan1) >> 10) + (((long)ch3 * pan3) >> 10));
						break;
					}
			}
		}

		leftoutput = (int)(((long)leftoutput * MasterVolume) >> 7);
		rightoutput = (int)(((long)rightoutput * MasterVolume) >> 7);

		leftoutput >>= 8;
		rightoutput >>= 8;

		if ( ApplyBias )
		{
			leftoutput += (Bias << 6) - 0x8000;
			rightoutput += (Bias << 6) - 0x8000;
		}

		short outL, outR;
		if ( Mute )
		{
			outL = 0;
			outR = 0;
		}
		else
		{
			outL = (short)Math.Clamp( leftoutput, -0x8000, 0x7FFF );
			outR = (short)Math.Clamp( rightoutput, -0x8000, 0x7FFF );
		}

		if ( Degrade10Bit )
		{
			outL = (short)(outL & unchecked((short)0xFFC0));
			outR = (short)(outR & unchecked((short)0xFFC0));
		}

		OutputLastSamples[0] = outL;
		OutputLastSamples[1] = outR;

		Output.PushSample( outL, outR );

		NDS.ScheduleEvent( SysEvent.Spu, true, MixInterval, Mix, MixInterval >> 1 );
	}

	public byte Read8( uint addr )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: return (byte)(chan.Cnt & 0xFF);
				case 0x1: return (byte)((chan.Cnt >> 8) & 0xFF);
				case 0x2: return (byte)((chan.Cnt >> 16) & 0xFF);
				case 0x3: return (byte)(chan.Cnt >> 24);
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500: return (byte)(Cnt & 0x7F);
				case 0x04000501: return (byte)(Cnt >> 8);

				case 0x04000508: return Capture[0].Cnt;
				case 0x04000509: return Capture[1].Cnt;
			}
		}

		return 0;
	}

	public ushort Read16( uint addr )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: return (ushort)(chan.Cnt & 0xFFFF);
				case 0x2: return (ushort)(chan.Cnt >> 16);
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500: return (ushort)Cnt;
				case 0x04000504: return Bias;

				case 0x04000508: return (ushort)(Capture[0].Cnt | (Capture[1].Cnt << 8));
			}
		}

		return 0;
	}

	public uint Read32( uint addr )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: return chan.Cnt;
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500: return Cnt;
				case 0x04000504: return Bias;

				case 0x04000508: return (uint)(Capture[0].Cnt | (Capture[1].Cnt << 8));

				case 0x04000510: return Capture[0].DstAddr;
				case 0x04000518: return Capture[1].DstAddr;
			}
		}

		return 0;
	}

	public void Write8( uint addr, byte val )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: chan.SetCnt( (chan.Cnt & 0xFFFFFF00) | val ); return;
				case 0x1: chan.SetCnt( (chan.Cnt & 0xFFFF00FF) | ((uint)val << 8) ); return;
				case 0x2: chan.SetCnt( (chan.Cnt & 0xFF00FFFF) | ((uint)val << 16) ); return;
				case 0x3: chan.SetCnt( (chan.Cnt & 0x00FFFFFF) | ((uint)val << 24) ); return;
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500:
					Cnt = (Cnt & 0xBF00) | (uint)(val & 0x7F);
					MasterVolume = (byte)(Cnt & 0x7F);
					if ( MasterVolume == 127 ) MasterVolume++;
					return;
				case 0x04000501:
					Cnt = (Cnt & 0x007F) | (uint)((val & 0xBF) << 8);
					return;

				case 0x04000508:
					Capture[0].SetCnt( val );
					return;
				case 0x04000509:
					Capture[1].SetCnt( val );
					return;
			}
		}
	}

	public void Write16( uint addr, ushort val )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: chan.SetCnt( (chan.Cnt & 0xFFFF0000) | val ); return;
				case 0x2: chan.SetCnt( (chan.Cnt & 0x0000FFFF) | ((uint)val << 16) ); return;
				case 0x8:
					chan.SetTimerReload( val );
					if ( (addr & 0xF0) == 0x10 ) Capture[0].SetTimerReload( val );
					else if ( (addr & 0xF0) == 0x30 ) Capture[1].SetTimerReload( val );
					return;
				case 0xA: chan.SetLoopPos( val ); return;

				case 0xC: chan.SetLength( ((chan.Length >> 2) & 0xFFFF0000) | val ); return;
				case 0xE: chan.SetLength( ((chan.Length >> 2) & 0x0000FFFF) | ((uint)val << 16) ); return;
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500:
					Cnt = (uint)(val & 0xBF7F);
					MasterVolume = (byte)(Cnt & 0x7F);
					if ( MasterVolume == 127 ) MasterVolume++;
					return;

				case 0x04000504:
					Bias = (ushort)(val & 0x3FF);
					return;

				case 0x04000508:
					Capture[0].SetCnt( (byte)(val & 0xFF) );
					Capture[1].SetCnt( (byte)(val >> 8) );
					return;

				case 0x04000514: Capture[0].SetLength( val ); return;
				case 0x0400051C: Capture[1].SetLength( val ); return;
			}
		}
	}

	public void Write32( uint addr, uint val )
	{
		if ( addr < 0x04000500 )
		{
			SPUChannel chan = Channels[(addr >> 4) & 0xF];

			switch ( addr & 0xF )
			{
				case 0x0: chan.SetCnt( val ); return;
				case 0x4: chan.SetSrcAddr( val ); return;
				case 0x8:
					chan.SetLoopPos( val >> 16 );
					val &= 0xFFFF;
					chan.SetTimerReload( val );
					if ( (addr & 0xF0) == 0x10 ) Capture[0].SetTimerReload( val );
					else if ( (addr & 0xF0) == 0x30 ) Capture[1].SetTimerReload( val );
					return;
				case 0xC: chan.SetLength( val ); return;
			}
		}
		else
		{
			switch ( addr )
			{
				case 0x04000500:
					Cnt = val & 0xBF7F;
					MasterVolume = (byte)(Cnt & 0x7F);
					if ( MasterVolume == 127 ) MasterVolume++;
					return;

				case 0x04000504:
					Bias = (ushort)(val & 0x3FF);
					return;

				case 0x04000508:
					Capture[0].SetCnt( (byte)(val & 0xFF) );
					Capture[1].SetCnt( (byte)(val >> 8) );
					return;

				case 0x04000510: Capture[0].SetDstAddr( val ); return;
				case 0x04000514: Capture[0].SetLength( val & 0xFFFF ); return;
				case 0x04000518: Capture[1].SetDstAddr( val ); return;
				case 0x0400051C: Capture[1].SetLength( val & 0xFFFF ); return;
			}
		}
	}

	private static short[] SnesGaussTable()
	{
		return
		[
			0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000,
			0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x002, 0x002, 0x002, 0x002, 0x002,
			0x002, 0x002, 0x003, 0x003, 0x003, 0x003, 0x003, 0x004, 0x004, 0x004, 0x004, 0x004, 0x005, 0x005, 0x005, 0x005,
			0x006, 0x006, 0x006, 0x006, 0x007, 0x007, 0x007, 0x008, 0x008, 0x008, 0x009, 0x009, 0x009, 0x00A, 0x00A, 0x00A,
			0x00B, 0x00B, 0x00B, 0x00C, 0x00C, 0x00D, 0x00D, 0x00E, 0x00E, 0x00F, 0x00F, 0x00F, 0x010, 0x010, 0x011, 0x011,
			0x012, 0x013, 0x013, 0x014, 0x014, 0x015, 0x015, 0x016, 0x017, 0x017, 0x018, 0x018, 0x019, 0x01A, 0x01B, 0x01B,
			0x01C, 0x01D, 0x01D, 0x01E, 0x01F, 0x020, 0x020, 0x021, 0x022, 0x023, 0x024, 0x024, 0x025, 0x026, 0x027, 0x028,
			0x029, 0x02A, 0x02B, 0x02C, 0x02D, 0x02E, 0x02F, 0x030, 0x031, 0x032, 0x033, 0x034, 0x035, 0x036, 0x037, 0x038,
			0x03A, 0x03B, 0x03C, 0x03D, 0x03E, 0x040, 0x041, 0x042, 0x043, 0x045, 0x046, 0x047, 0x049, 0x04A, 0x04C, 0x04D,
			0x04E, 0x050, 0x051, 0x053, 0x054, 0x056, 0x057, 0x059, 0x05A, 0x05C, 0x05E, 0x05F, 0x061, 0x063, 0x064, 0x066,
			0x068, 0x06A, 0x06B, 0x06D, 0x06F, 0x071, 0x073, 0x075, 0x076, 0x078, 0x07A, 0x07C, 0x07E, 0x080, 0x082, 0x084,
			0x086, 0x089, 0x08B, 0x08D, 0x08F, 0x091, 0x093, 0x096, 0x098, 0x09A, 0x09C, 0x09F, 0x0A1, 0x0A3, 0x0A6, 0x0A8,
			0x0AB, 0x0AD, 0x0AF, 0x0B2, 0x0B4, 0x0B7, 0x0BA, 0x0BC, 0x0BF, 0x0C1, 0x0C4, 0x0C7, 0x0C9, 0x0CC, 0x0CF, 0x0D2,
			0x0D4, 0x0D7, 0x0DA, 0x0DD, 0x0E0, 0x0E3, 0x0E6, 0x0E9, 0x0EC, 0x0EF, 0x0F2, 0x0F5, 0x0F8, 0x0FB, 0x0FE, 0x101,
			0x104, 0x107, 0x10B, 0x10E, 0x111, 0x114, 0x118, 0x11B, 0x11E, 0x122, 0x125, 0x129, 0x12C, 0x130, 0x133, 0x137,
			0x13A, 0x13E, 0x141, 0x145, 0x148, 0x14C, 0x150, 0x153, 0x157, 0x15B, 0x15F, 0x162, 0x166, 0x16A, 0x16E, 0x172,
			0x176, 0x17A, 0x17D, 0x181, 0x185, 0x189, 0x18D, 0x191, 0x195, 0x19A, 0x19E, 0x1A2, 0x1A6, 0x1AA, 0x1AE, 0x1B2,
			0x1B7, 0x1BB, 0x1BF, 0x1C3, 0x1C8, 0x1CC, 0x1D0, 0x1D5, 0x1D9, 0x1DD, 0x1E2, 0x1E6, 0x1EB, 0x1EF, 0x1F3, 0x1F8,
			0x1FC, 0x201, 0x205, 0x20A, 0x20F, 0x213, 0x218, 0x21C, 0x221, 0x226, 0x22A, 0x22F, 0x233, 0x238, 0x23D, 0x241,
			0x246, 0x24B, 0x250, 0x254, 0x259, 0x25E, 0x263, 0x267, 0x26C, 0x271, 0x276, 0x27B, 0x280, 0x284, 0x289, 0x28E,
			0x293, 0x298, 0x29D, 0x2A2, 0x2A6, 0x2AB, 0x2B0, 0x2B5, 0x2BA, 0x2BF, 0x2C4, 0x2C9, 0x2CE, 0x2D3, 0x2D8, 0x2DC,
			0x2E1, 0x2E6, 0x2EB, 0x2F0, 0x2F5, 0x2FA, 0x2FF, 0x304, 0x309, 0x30E, 0x313, 0x318, 0x31D, 0x322, 0x326, 0x32B,
			0x330, 0x335, 0x33A, 0x33F, 0x344, 0x349, 0x34E, 0x353, 0x357, 0x35C, 0x361, 0x366, 0x36B, 0x370, 0x374, 0x379,
			0x37E, 0x383, 0x388, 0x38C, 0x391, 0x396, 0x39B, 0x39F, 0x3A4, 0x3A9, 0x3AD, 0x3B2, 0x3B7, 0x3BB, 0x3C0, 0x3C5,
			0x3C9, 0x3CE, 0x3D2, 0x3D7, 0x3DC, 0x3E0, 0x3E5, 0x3E9, 0x3ED, 0x3F2, 0x3F6, 0x3FB, 0x3FF, 0x403, 0x408, 0x40C,
			0x410, 0x415, 0x419, 0x41D, 0x421, 0x425, 0x42A, 0x42E, 0x432, 0x436, 0x43A, 0x43E, 0x442, 0x446, 0x44A, 0x44E,
			0x452, 0x455, 0x459, 0x45D, 0x461, 0x465, 0x468, 0x46C, 0x470, 0x473, 0x477, 0x47A, 0x47E, 0x481, 0x485, 0x488,
			0x48C, 0x48F, 0x492, 0x496, 0x499, 0x49C, 0x49F, 0x4A2, 0x4A6, 0x4A9, 0x4AC, 0x4AF, 0x4B2, 0x4B5, 0x4B7, 0x4BA,
			0x4BD, 0x4C0, 0x4C3, 0x4C5, 0x4C8, 0x4CB, 0x4CD, 0x4D0, 0x4D2, 0x4D5, 0x4D7, 0x4D9, 0x4DC, 0x4DE, 0x4E0, 0x4E3,
			0x4E5, 0x4E7, 0x4E9, 0x4EB, 0x4ED, 0x4EF, 0x4F1, 0x4F3, 0x4F5, 0x4F6, 0x4F8, 0x4FA, 0x4FB, 0x4FD, 0x4FF, 0x500,
			0x502, 0x503, 0x504, 0x506, 0x507, 0x508, 0x50A, 0x50B, 0x50C, 0x50D, 0x50E, 0x50F, 0x510, 0x511, 0x511, 0x512,
			0x513, 0x514, 0x514, 0x515, 0x516, 0x516, 0x517, 0x517, 0x517, 0x518, 0x518, 0x518, 0x518, 0x518, 0x519, 0x519
		];
	}
}
