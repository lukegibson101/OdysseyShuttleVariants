using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // A transport drone parked on the world map. Because the autonomous drone carries no colonist,
    // a normal shuttle would lose its cargo on an empty tile (vanilla forms a caravan, which needs a
    // pawn owner). Instead, our DroneLand arrival action drops the drone into one of these world
    // objects: it holds the shuttle Thing + its cargo (an ActiveTransporterInfo, exactly how a shuttle
    // is carried in flight), survives save/load, and lives on the world map so abandoning the origin
    // colony map can't strand it. From here the player can recall it home or send it onward.
    // The map a drone "forms a camp" on. Vanilla WorldObjectDefOf.Camp auto-removes its map (and spawns
    // an AbandonedCamp) the instant it has no colonists - fatal for a pawnless drone, which would lose the
    // ship. This custom MapParent instead keeps the map while the drone ship (any player building) or a
    // colonist is present, and only cleans itself up once the map is truly empty (e.g. after the ship
    // re-launches), removing the world object with it (no lingering abandoned camp).
    public class WorldObject_DroneCamp : MapParent
    {
        // Last tick the drone ship was present on the map as a Building. CheckRemoveMapNow runs every tick
        // interval, but the ship is NOT a Building during its transitions: it arrives via a ~200-tick
        // landing skyfaller, and on re-launch it becomes a ~30-tick leaving skyfaller. If we removed the
        // map the instant no ship-building was present, we'd cull it mid-landing or mid-takeoff and lose
        // the ship. So we keep the map for a grace window after the ship was last present; it's only
        // removed once the ship has been gone long enough to have truly departed (become a world object).
        private int lastShipPresentTick = -1;

        public void NotifyFormed()
        {
            lastShipPresentTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastShipPresentTick, "lastShipPresentTick", -1);
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = true;
            if (!HasMap) return false;

            int now = Find.TickManager.TicksGame;
            if (ShipPresent(Map)) { lastShipPresentTick = now; return false; }

            if (Map.mapPawns.AnyPawnBlockingMapRemoval) return false;
            if (TransporterUtility.IncomingTransporterPreventingMapRemoval(Map)) return false;

            // No ship-building right now: keep the map through the transient landing/takeoff windows.
            if (lastShipPresentTick < 0) lastShipPresentTick = now; // covers the initial landing
            if (now - lastShipPresentTick < 1000) return false;
            return true;
        }

        private static bool ShipPresent(Map map)
        {
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is Building_PassengerShuttle) return true;
            }
            return false;
        }

        // Leave an abandoned-camp marker when the site empties, exactly like a vanilla caravan camp
        // (Camp.Notify_MyMapRemoved) - a decaying world object rather than the site silently vanishing.
        public override void Notify_MyMapRemoved(Map map)
        {
            base.Notify_MyMapRemoved(map);
            if (ModsConfig.OdysseyActive && map.TileInfo.Landmark != null)
            {
                WorldObject landmark = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedLandmark);
                landmark.Tile = Tile;
                landmark.SetFaction(Faction);
                Find.WorldObjects.Add(landmark);
            }
            else
            {
                WorldObject abandoned = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedCamp);
                abandoned.Tile = Tile;
                abandoned.SetFaction(Faction);
                abandoned.GetComponent<TimeoutComp>().StartTimeout(1800000);
                Find.WorldObjects.Add(abandoned);
            }
        }
    }

    public class WorldObject_LandedDrone : WorldObject, IThingHolder
    {
        // The shuttle building + cargo. Mirrors TravellingTransporters' per-pod container; reused
        // verbatim when we re-launch (no re-packing needed - the shuttle is already SetShuttle'd).
        private ActiveTransporterInfo contents;

        public Building_PassengerShuttle Shuttle => contents?.GetShuttle() as Building_PassengerShuttle;

        public ActiveTransporterInfo Contents => contents;

        public void SetContents(ActiveTransporterInfo info)
        {
            contents = info;
            if (contents != null) contents.parent = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "contents");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && contents != null)
            {
                contents.parent = this;
            }
        }

        public ThingOwner GetDirectlyHeldThings() => null;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
            if (contents != null) outChildren.Add(contents);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            Building_PassengerShuttle shuttle = Shuttle;
            if (shuttle == null) yield break;

            // Standard shuttle Launch gizmo - the same command + targeting (fuel-range ring, float-menu
            // destination options, camera) a cargo shuttle uses when it sits in a caravan. The actual
            // launch runs through our LaunchTo (the no-map, world-object launch path, since vanilla
            // TryLaunch would try to spawn a skyfaller on a map we don't have). Our Harmony float-menu
            // patch adds the "Land drone here" option for empty tiles; colonies/settlements keep their
            // normal options. (This replaced the old bespoke "Recall home"/"Send to tile" gizmos.)
            Command_Action launch = new Command_Action
            {
                defaultLabel = "CommandLaunchGroup".Translate(),
                defaultDesc = "CommandLaunchGroupDesc".Translate(),
                icon = CompLaunchable.LaunchCommandTex,
                action = delegate { BeginChoosingDestination(shuttle); }
            };
            AcceptanceReport rep = shuttle.LaunchableComp.CanLaunch(shuttle.FuelLevel);
            if (!rep.Accepted) launch.Disable(rep.Reason);
            yield return launch;

            // Form camp - parity with a caravan's "Form camp": spin up a real map at this tile with the
            // ship physically placed on it (so you can see/enter it, unload, and it becomes a normal
            // re-launchable building). Mirrors SettleInEmptyTileUtility.SetupCamp.
            Command_Action camp = new Command_Action
            {
                defaultLabel = "CommandCamp".Translate(),
                defaultDesc = "CommandCampDesc".Translate(),
                icon = SettleUtility.CreateCampCommandTex,
                action = FormCamp
            };
            if (!SettleInEmptyTileUtility.CanCreateMapAt(Tile) || Find.WorldObjects.AnyMapParentAt(Tile))
            {
                camp.Disable("CommandCampFailExistingWorldObject".Translate());
            }
            yield return camp;
        }

        private void FormCamp()
        {
            PlanetTile tile = Tile;
            // Capture now (the long event runs later, outside the synced-command context).
            bool showUi = MultiplayerCompat.ShowUiForThisClient;
            LongEventHandler.QueueLongEvent(delegate
            {
                IntVec3 size = OSV_WorldObjectDefOf.OSV_DroneCamp.overrideMapSize ?? Find.World.info.initialMapSize;
                // Our own persistent camp MapParent (NOT vanilla Camp, which would delete the pawnless map
                // and lose the ship). It keeps the map alive while the drone is parked there.
                Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, size, OSV_WorldObjectDefOf.OSV_DroneCamp);
                if (map.Parent is WorldObject_DroneCamp camp) camp.NotifyFormed();
                map.Parent.SetFaction(Faction.OfPlayer);

                // Detach only now that the map exists, so a map-gen failure leaves the drone intact.
                ActiveTransporterInfo info = contents;
                contents = null;

                // Reuses the same util as recall-to-colony: respawns the shuttle building + dumps cargo
                // into it, on the camp map.
                Thing landed = TransportersArrivalActionUtility.DropShuttle(info, map, map.Center);

                if (!Destroyed) Destroy();

                if (showUi)
                {
                    if (landed != null) CameraJumper.TryJump(landed);
                    else CameraJumper.TryJump(map.Center, map);
                }
            }, "GeneratingMap", doAsynchronously: true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();
            List<string> lines = new List<string>();
            if (!baseStr.NullOrEmpty()) lines.Add(baseStr);

            Building_PassengerShuttle shuttle = Shuttle;
            if (shuttle != null)
            {
                lines.Add("Fuel: " + Mathf.RoundToInt(shuttle.FuelLevel) + " / " + Mathf.RoundToInt(shuttle.MaxFuelLevel));

                int cargo = 0;
                foreach (Thing t in contents.innerContainer)
                {
                    if (t != shuttle) cargo += t.stackCount;
                }
                lines.Add(cargo > 0 ? "Carrying cargo: " + cargo + " items" : "No cargo");
            }
            return string.Join("\n", lines.ToArray());
        }

        // ---- launch flow ----

        // StartChoosingDestination's ChoseWorldTarget bails unless the transporter is "ready to launch"
        // (groupID >= 0). TryLaunch keeps the groupID when it sends a shuttle, so a landed drone normally
        // still has one - but guard it in case it was ever cleared. InitiateLoading just assigns a fresh
        // group id (no map, no hauling lord), so it's safe on the despawned, world-object-held shuttle.
        private static void EnsureLaunchReady(Building_PassengerShuttle shuttle)
        {
            CompTransporter t = shuttle.TransporterComp;
            if (t != null && t.groupID < 0)
            {
                TransporterUtility.InitiateLoading(Gen.YieldSingle(t));
            }
        }

        // Our own copy of CompLaunchable.StartChoosingDestination. The vanilla method forces
        // closeWorldTabWhenFinished = !IsCaravanShuttle, and our drone isn't a caravan shuttle, so it
        // would snap the camera back to a colony map after a destination is picked. We pass
        // closeWorldTabWhenFinished:false so launching keeps the player on the world map (then LaunchTo
        // follows the departing drone). Reuses the vanilla static ChoseWorldTarget (so all the normal
        // destination options + our injected gift/land options + fuel checks still apply), and draws the
        // same max-range / fuel-range rings.
        private void BeginChoosingDestination(Building_PassengerShuttle shuttle)
        {
            EnsureLaunchReady(shuttle);
            CompLaunchable launchable = shuttle.LaunchableComp;
            PlanetTile origin = Tile;
            Find.WorldSelector.ClearSelection();
            IEnumerable<IThingHolder> pods = Gen.YieldSingle<IThingHolder>(shuttle.TransporterComp);

            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo t) => CompLaunchable.ChoseWorldTarget(t, origin, pods,
                    launchable.MaxLaunchDistanceEver(t.Tile.Layer), LaunchTo, launchable),
                canTargetTiles: true,
                CompLaunchable.TargeterMouseAttachment,
                closeWorldTabWhenFinished: false,
                onUpdate: delegate
                {
                    PlanetLayer layer = Find.WorldSelector.SelectedLayer;
                    PlanetTile o = layer.GetClosestTile_NewTemp(origin);
                    int maxEver = launchable.MaxLaunchDistanceEver(layer);
                    GenDraw.DrawWorldRadiusRing(o, maxEver, CompPilotConsole.GetThrusterRadiusMat(o));
                    int maxFuel = launchable.MaxLaunchDistanceAtFuelLevel(shuttle.FuelLevel, layer);
                    if (maxFuel < maxEver)
                    {
                        GenDraw.DrawWorldRadiusRing(o, maxFuel, CompPilotConsole.GetFuelRadiusMat(o));
                    }
                },
                extraLabelGetter: null,
                canSelectTarget: null,
                originForClosest: origin,
                showCancelButton: true);
        }

        // The launch action handed to StartChoosingDestination (mirrors how a caravan passes
        // CaravanShuttleUtility.LaunchShuttle). Re-launches the held shuttle to the chosen destination
        // with the destination's arrival action - our DroneLand for an empty tile, vanilla's land/visit
        // action for a colony/settlement. Consumes fuel; leaves the drone landed ("stranded") if it
        // can't afford the trip. Reuses our existing ActiveTransporterInfo (no re-packing needed).
        private void LaunchTo(PlanetTile destinationTile, TransportersArrivalAction arrivalAction)
        {
            Building_PassengerShuttle shuttle = Shuttle;
            if (shuttle == null) return;

            CompLaunchable launchable = shuttle.LaunchableComp;
            float fuelLevel = shuttle.FuelLevel;

            // In MP this runs on every client; only surface reject messages to the issuing player.
            bool showUi = MultiplayerCompat.ShowUiForThisClient;

            AcceptanceReport canLaunch = launchable.CanLaunch(fuelLevel);
            if (!canLaunch.Accepted)
            {
                if (showUi) Messages.Message(canLaunch.Reason.NullOrEmpty() ? "The transport drone can't launch right now."
                    : canLaunch.Reason, this, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            int dist = Find.WorldGrid.TraversalDistanceBetween(Tile, destinationTile, passImpassable: true, int.MaxValue, canTraverseLayers: true);
            if (dist > launchable.MaxLaunchDistanceAtFuelLevel(fuelLevel, destinationTile.Layer))
            {
                // Strand: not enough fuel/range. The recovery path is Form camp - it needs no fuel, lands
                // the ship as a normal building you can refuel with chemfuel, then re-launch.
                if (showUi) Messages.Message("The transport drone doesn't have enough fuel to reach there. Form a camp to "
                    + "land it as a refuelable ship.", this, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            float amount = Mathf.Max(launchable.FuelNeededToLaunchAtDist(dist, destinationTile.Layer), 1f);
            shuttle.RefuelableComp.ConsumeFuel(amount);

            // Detach our container so Destroy() can't dispose it - it flies off with the travel object.
            ActiveTransporterInfo info = contents;
            contents = null;

            TravellingTransporters travelling = (TravellingTransporters)WorldObjectMaker.MakeWorldObject(launchable.Props.worldObjectDef);
            travelling.SetFaction(Faction.OfPlayer);
            travelling.destinationTile = destinationTile;
            travelling.arrivalAction = arrivalAction;

            PlanetTile origin = Tile;
            if (origin.Layer != destinationTile.Layer)
            {
                origin = destinationTile.Layer.GetClosestTile_NewTemp(origin);
            }
            travelling.Tile = origin;
            travelling.AddTransporter(info, justLeftTheMap: false);
            Find.WorldObjects.Add(travelling);

            launchable.lastLaunchTick = Find.TickManager.TicksGame;
            Destroy();

            // Stay on the world map, following the departing drone (same as a caravan-launched shuttle).
            // Only the issuing client's camera moves in MP.
            if (showUi) CameraJumper.TryJump(travelling);
        }
    }
}
