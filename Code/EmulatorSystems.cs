using Emulotl.Gba;
using Emulotl.Nds;

namespace Emulotl;

public static class EmulatorSystems
{
	private static bool _registered;

	public static void EnsureRegistered()
	{
		if ( _registered )
			return;

		_registered = true;
		SystemRegistry.Register( GbaSystem.Profile );
		SystemRegistry.Register( NdsSystem.Profile );
	}
}
