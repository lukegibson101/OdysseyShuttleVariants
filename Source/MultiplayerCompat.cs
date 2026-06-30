using Multiplayer.API;
using Verse;

namespace OdysseyShuttleVariants
{
    // Multiplayer support for the drone's world-object player commands. The world targeter and float
    // menus run locally on the issuing client; only the state-mutating methods are synced so every
    // client applies them deterministically. (The camp building's re-launch goes through vanilla
    // CompLaunchable.TryLaunch, which the Multiplayer mod already syncs, and our arrival actions
    // serialize via their IExposable ExposeData through ExposeParameter - no SyncWorker needed.)
    //
    // The 0MultiplayerAPI.dll stub ships in Assemblies/ (see csproj), so this type loads fine even
    // without the Multiplayer mod installed; MP.enabled is then false and nothing is registered.
    [StaticConstructorOnStartup]
    public static class MultiplayerCompat
    {
        static MultiplayerCompat()
        {
            if (!MP.enabled) return;

            // LaunchTo(PlanetTile, TransportersArrivalAction): PlanetTile syncs via MP's built-in
            // serializer; ExposeParameter(1) serializes the arrival action via its ExposeData
            // (DroneGift scribes its settlement; DroneLand has no state).
            MP.RegisterSyncMethod(typeof(WorldObject_LandedDrone), "LaunchTo").ExposeParameter(1);
            MP.RegisterSyncMethod(typeof(WorldObject_LandedDrone), "FormCamp");
        }

        // True when UI side effects (camera jumps, reject messages) should run on this client: always
        // in singleplayer, and in multiplayer only on the client that issued the synced command.
        public static bool ShowUiForThisClient => !MP.enabled || MP.IsExecutingSyncCommandIssuedBySelf;
    }
}
