namespace Emulotl.Nds;

public sealed partial class GPU
{
	private const int FlatPad = 8;

	public byte[] VRAMFlat_ABG = new byte[0x80000 + FlatPad];
	public byte[] VRAMFlat_BBG = new byte[0x20000 + FlatPad];
	public byte[] VRAMFlat_AOBJ = new byte[0x40000 + FlatPad];
	public byte[] VRAMFlat_BOBJ = new byte[0x20000 + FlatPad];

	public byte[] VRAMFlat_ABGExtPal = new byte[0x8000 + FlatPad];
	public byte[] VRAMFlat_BBGExtPal = new byte[0x8000 + FlatPad];
	public byte[] VRAMFlat_AOBJExtPal = new byte[0x2000 + FlatPad];
	public byte[] VRAMFlat_BOBJExtPal = new byte[0x2000 + FlatPad];

	public void RebuildVRAMFlat()
	{
		UpdateTexVRAMTracking();

		FlattenRegion( VRAMFlat_ABG, VRAMMap_ABG, 14 );
		FlattenRegion( VRAMFlat_BBG, VRAMMap_BBG, 14 );
		FlattenRegion( VRAMFlat_AOBJ, VRAMMap_AOBJ, 14 );
		FlattenRegion( VRAMFlat_BOBJ, VRAMMap_BOBJ, 14 );

		FlattenRegion( VRAMFlat_ABGExtPal, VRAMMap_ABGExtPal, 13 );
		FlattenRegion( VRAMFlat_BBGExtPal, VRAMMap_BBGExtPal, 13 );
		FlattenSlot( VRAMFlat_AOBJExtPal, VRAMMap_AOBJExtPal, 0x2000 );
		FlattenSlot( VRAMFlat_BOBJExtPal, VRAMMap_BOBJExtPal, 0x2000 );
	}

	private void UpdateTexVRAMTracking()
	{
		uint mask = 0;
		ulong sig = 1469598103934665603UL;
		for ( int s = 0; s < VRAMMap_Texture.Length; s++ )
		{
			mask |= VRAMMap_Texture[s];
			sig = (sig ^ VRAMMap_Texture[s]) * 1099511628211UL;
		}
		for ( int s = 0; s < VRAMMap_TexPal.Length; s++ )
		{
			mask |= VRAMMap_TexPal[s];
			sig = (sig ^ VRAMMap_TexPal[s]) * 1099511628211UL;
		}

		TexBankMask = mask;

		if ( sig != _lastTexMapSig )
		{
			_lastTexMapSig = sig;
			TexVRAMVersion++;
		}
	}

	private void FlattenRegion( byte[] flat, uint[] map, int slotShift )
	{
		int slotSize = 1 << slotShift;
		for ( int s = 0; s < map.Length; s++ )
			FlattenSlotAt( flat, s << slotShift, map[s], slotSize );
	}

	private void FlattenSlot( byte[] flat, uint mask, int slotSize )
	{
		FlattenSlotAt( flat, 0, mask, slotSize );
	}

	private void FlattenSlotAt( byte[] flat, int dst, uint mask, int slotSize )
	{
		if ( mask == 0 )
		{
			Array.Clear( flat, dst, slotSize );
			return;
		}

		bool first = true;
		for ( int b = 0; b < 9; b++ )
		{
			if ( (mask & (1u << b)) == 0 )
				continue;

			byte[] bank = VRAM[b];
			int src = (int)((uint)dst & VRAMMask[b]);

			if ( first )
			{
				Array.Copy( bank, src, flat, dst, slotSize );
				first = false;
			}
			else
			{
				for ( int i = 0; i < slotSize; i++ )
					flat[dst + i] |= bank[src + i];
			}
		}
	}
}
