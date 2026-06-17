namespace Emulotl.Nds;

public sealed partial class NDS
{
	private readonly uint[] Key1_KeyBuf = new uint[0x412];

	public bool IsNativeArm7Bios;

	private byte[] _nativeArm9Bios;
	private byte[] _nativeArm7Bios;

	public void LoadBios9( byte[] data ) => _nativeArm9Bios = data;
	public void LoadBios7( byte[] data ) => _nativeArm7Bios = data;

	private static uint ByteSwap( uint v )
		=> ((v >> 24) & 0xFF) | ((v >> 8) & 0xFF00) | ((v << 8) & 0xFF0000) | ((v << 24) & 0xFF000000);

	private void Key1_Encrypt( uint[] data, int o )
	{
		uint y = data[o];
		uint x = data[o + 1];

		for ( uint i = 0x0; i <= 0xF; i++ )
		{
			uint z = Key1_KeyBuf[i] ^ x;
			x = Key1_KeyBuf[0x012 + (z >> 24)];
			x += Key1_KeyBuf[0x112 + ((z >> 16) & 0xFF)];
			x ^= Key1_KeyBuf[0x212 + ((z >> 8) & 0xFF)];
			x += Key1_KeyBuf[0x312 + (z & 0xFF)];
			x ^= y;
			y = z;
		}

		data[o] = x ^ Key1_KeyBuf[0x10];
		data[o + 1] = y ^ Key1_KeyBuf[0x11];
	}

	private void Key1_Decrypt( uint[] data, int o )
	{
		uint y = data[o];
		uint x = data[o + 1];

		for ( uint i = 0x11; i >= 0x2; i-- )
		{
			uint z = Key1_KeyBuf[i] ^ x;
			x = Key1_KeyBuf[0x012 + (z >> 24)];
			x += Key1_KeyBuf[0x112 + ((z >> 16) & 0xFF)];
			x ^= Key1_KeyBuf[0x212 + ((z >> 8) & 0xFF)];
			x += Key1_KeyBuf[0x312 + (z & 0xFF)];
			x ^= y;
			y = z;
		}

		data[o] = x ^ Key1_KeyBuf[0x1];
		data[o + 1] = y ^ Key1_KeyBuf[0x0];
	}

	private void Key1_ApplyKeycode( uint[] keycode, uint mod )
	{
		Key1_Encrypt( keycode, 1 );
		Key1_Encrypt( keycode, 0 );

		uint[] temp = [0, 0];

		for ( uint i = 0; i <= 0x11; i++ )
			Key1_KeyBuf[i] ^= ByteSwap( keycode[i % mod] );

		for ( uint i = 0; i <= 0x410; i += 2 )
		{
			Key1_Encrypt( temp, 0 );
			Key1_KeyBuf[i] = temp[1];
			Key1_KeyBuf[i + 1] = temp[0];
		}
	}

	private void Key1_LoadKeyBuf()
	{
		if ( IsNativeArm7Bios )
		{
			for ( int i = 0; i < 0x412; i++ )
				Key1_KeyBuf[i] = Read32( ARM7BIOS, (uint)(0x0030 + i * 4) );
		}
		else
		{
			Array.Clear( Key1_KeyBuf );
		}
	}

	private void Key1_InitKeycode( uint idcode, uint level, uint mod )
	{
		Key1_LoadKeyBuf();

		uint[] keycode = [idcode, idcode >> 1, idcode << 1];
		if ( level >= 1 ) Key1_ApplyKeycode( keycode, mod );
		if ( level >= 2 ) Key1_ApplyKeycode( keycode, mod );
		if ( level >= 3 )
		{
			keycode[1] <<= 1;
			keycode[2] >>= 1;
			Key1_ApplyKeycode( keycode, mod );
		}
	}

	private void Key1_DecryptAt( byte[] buf, int off )
	{
		uint[] d = [Read32( buf, (uint)off ), Read32( buf, (uint)(off + 4) )];
		Key1_Decrypt( d, 0 );
		Write32( buf, (uint)off, d[0] );
		Write32( buf, (uint)(off + 4), d[1] );
	}

	private void Key1_EncryptAt( byte[] buf, int off )
	{
		uint[] d = [Read32( buf, (uint)off ), Read32( buf, (uint)(off + 4) )];
		Key1_Encrypt( d, 0 );
		Write32( buf, (uint)off, d[0] );
		Write32( buf, (uint)(off + 4), d[1] );
	}

	private bool _secureAreaProcessed;

	private void ProcessSecureArea()
	{
		if ( _secureAreaProcessed )
			return;
		_secureAreaProcessed = true;

		uint arm9base = RomU32( 0x20 );
		if ( !(arm9base >= 0x4000 && arm9base < 0x8000) )
			return;
		if ( _rom == null || arm9base + 0x800 > _rom.Length )
			return;

		if ( Read32( _rom, arm9base ) == 0xE7FFDEFF && Read32( _rom, arm9base + 0x10 ) != 0xE7FFDEFF )
		{
			uint gamecode = RomU32( 0x0C );

			_rom[arm9base + 0] = (byte)'e';
			_rom[arm9base + 1] = (byte)'n';
			_rom[arm9base + 2] = (byte)'c';
			_rom[arm9base + 3] = (byte)'r';
			_rom[arm9base + 4] = (byte)'y';
			_rom[arm9base + 5] = (byte)'O';
			_rom[arm9base + 6] = (byte)'b';
			_rom[arm9base + 7] = (byte)'j';

			Key1_InitKeycode( gamecode, 3, 2 );
			for ( uint i = 0; i < 0x800; i += 8 )
				Key1_EncryptAt( _rom, (int)(arm9base + i) );

			Key1_InitKeycode( gamecode, 2, 2 );
			Key1_EncryptAt( _rom, (int)arm9base );
		}
	}

	private void DecryptSecureArea( byte[] outBuf )
	{
		uint gamecode = RomU32( 0x0C );
		uint arm9base = RomU32( 0x20 );

		for ( int i = 0; i < 0x800; i++ )
			outBuf[i] = _rom[arm9base + i];

		Key1_InitKeycode( gamecode, 2, 2 );
		Key1_DecryptAt( outBuf, 0 );

		Key1_InitKeycode( gamecode, 3, 2 );
		for ( int i = 0; i < 0x800; i += 8 )
			Key1_DecryptAt( outBuf, i );

		bool ok = outBuf[0] == 'e' && outBuf[1] == 'n' && outBuf[2] == 'c' && outBuf[3] == 'r'
			&& outBuf[4] == 'y' && outBuf[5] == 'O' && outBuf[6] == 'b' && outBuf[7] == 'j';

		if ( ok )
		{
			Platform.Log( LogLevel.Info, "Secure area decryption OK" );
			Write32( outBuf, 0, 0xE7FFDEFF );
			Write32( outBuf, 4, 0xE7FFDEFF );
		}
		else
		{
			Platform.Log( LogLevel.Warn, "Secure area decryption failed" );
			for ( int i = 0; i < 0x800; i += 4 )
				Write32( outBuf, (uint)i, 0xE7FFDEFF );
		}
	}
}
