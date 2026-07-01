using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace OdysseyShuttleVariants
{
    // Turns a LANDED (spawned) mechanitor shuttle into a chemfuel-powered field charger for the
    // player's mechs - no electrical grid, no separate charger building. The hull IS the charger:
    // low mechs walk to a free spot around the shuttle and top up, drawing on the shuttle's own
    // chemfuel. Off by default (opt-in): charging drains the fuel the shuttle needs to fly home.
    //
    // Discovery/routing reuses vanilla's recharge think-tree slot via a Harmony postfix on
    // JobGiver_GetEnergy_Charger.TryGiveJob (see Patch_JobGiver_GetEnergy_Charger), so the shuttle
    // is a FALLBACK: mechs prefer a real Building_MechCharger and only use the hull when none is
    // reachable. The actual energy top-up + fuel burn happens in JobDriver_MechChargeAtShuttle.
    public class CompProperties_MechChargerShuttle : CompProperties
    {
        // Mech energy restored per day while charging (vanilla Building_MechCharger is 50/day).
        public float chargeRatePerDay = 50f;

        // Chemfuel spent per point of mech energy restored. 0.25 => a full 100-energy top-up costs
        // ~25 chemfuel (full-efficiency charging off the shuttle's own fuel; no grid-vs-fuel penalty).
        public float fuelPerEnergy = 0.25f;

        // How many mechs may charge at the hull at once (they crowd the adjacent cells).
        public int maxSimultaneousCharging = 2;

        public CompProperties_MechChargerShuttle()
        {
            compClass = typeof(CompMechChargerShuttle);
        }
    }

    public class CompMechChargerShuttle : ThingComp
    {
        public CompProperties_MechChargerShuttle Props => (CompProperties_MechChargerShuttle)props;

        // Registry of every spawned charger shuttle, so a low mech can find one without relying on a
        // region-stored ThingRequestGroup. In multiplayer all clients spawn/despawn in the same
        // deterministic order, so this list is identical across clients; the charger search still
        // tiebreaks on thingIDNumber to stay order-independent. See Patch_JobGiver_GetEnergy_Charger.
        private static readonly List<CompMechChargerShuttle> spawnedChargers = new List<CompMechChargerShuttle>();
        public static IReadOnlyList<CompMechChargerShuttle> AllSpawned => spawnedChargers;

        // Public view of the ThingComp's parent (protected) for the job giver / driver.
        public Thing Parent => parent;

        // Off by default (charging drains the fuel the shuttle needs to fly home - opt-in). Scribed so
        // it survives save/reload and flights.
        private bool chargingEnabled;

        // Mechs currently pumping energy at this hull. Populated/cleared by the job driver on all
        // clients (jobs are MP-synced), so it stays deterministic. Used only for Count/Contains
        // (throttle + inspect), never iterated to drive sim state.
        private readonly HashSet<Pawn> chargingMechs = new HashSet<Pawn>();

        public bool ChargingEnabled => chargingEnabled;

        public float ChargePerTick => Props.chargeRatePerDay / GenDate.TicksPerDay;

        // How many mechs may charge at the hull at once: the def base plus one per finished mech-bay
        // expansion research tier (base 2 -> 3 -> 4), so upgrades widen the charging line as well as
        // the carried-mech budget.
        public int EffectiveMaxSimultaneous =>
            Props.maxSimultaneousCharging + CompTypedShuttleCapacity.MechBayExpansionTiers();

        private CompRefuelable fuelComp;
        public CompRefuelable Fuel => fuelComp ?? (fuelComp = parent.GetComp<CompRefuelable>());

        // MP-synced state setter (registered in MultiplayerCompat). Toggling the gizmo calls this so
        // every client flips together; in singleplayer it just runs inline.
        public void SetChargingEnabled(bool value)
        {
            chargingEnabled = value;
        }

        // Is the hull able to (start) charging this mech right now? Mirrors the shape of vanilla
        // Building_MechCharger.CanPawnChargeCurrently: enabled, landed, has fuel, a free slot (unless
        // this mech already holds one), and the mech is one of ours.
        public bool CanChargeNow(Pawn mech)
        {
            if (!chargingEnabled || !parent.Spawned) return false;
            if (mech == null || !mech.IsColonyMech) return false;
            if (Fuel == null || !Fuel.HasFuel) return false;
            if (chargingMechs.Contains(mech)) return true;
            return chargingMechs.Count < EffectiveMaxSimultaneous;
        }

        public void RegisterCharging(Pawn mech) => chargingMechs.Add(mech);
        public void DeregisterCharging(Pawn mech) => chargingMechs.Remove(mech);

        // Pump one tick of energy into the mech, paid for in chemfuel. Returns false when the tank
        // runs dry (the job then ends). Called from the charge toil for each charging mech.
        public bool ChargeTick(Pawn mech)
        {
            if (Fuel == null) return false;
            Need_MechEnergy energy = mech.needs?.energy;
            if (energy == null) return false;

            float cap = JobGiver_GetEnergy.GetMaxRechargeLimit(mech);
            float delta = Mathf.Min(ChargePerTick, cap - energy.CurLevel);
            if (delta <= 0f) return true; // already topped up; the driver's stop check ends the job

            float fuelNeeded = delta * Props.fuelPerEnergy;
            if (Fuel.Fuel < fuelNeeded) return false;

            Fuel.ConsumeFuel(fuelNeeded);
            energy.CurLevel += delta;
            return true;
        }

        // Standable, reachable cells hugging the hull that a mech can charge from. Recomputed on
        // demand (cheap; only walked when a low mech is actually looking for a spot).
        public IEnumerable<IntVec3> ChargeCells()
        {
            Map map = parent.Map;
            if (map == null) yield break;
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(parent))
            {
                if (c.InBounds(map) && c.Standable(map))
                {
                    yield return c;
                }
            }
        }

        // Closest free (unreserved, reachable) charge cell for a mech, or IntVec3.Invalid.
        public IntVec3 FreeChargeCellFor(Pawn mech)
        {
            Map map = parent.Map;
            if (map == null) return IntVec3.Invalid;
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            foreach (IntVec3 c in ChargeCells())
            {
                if (!mech.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                float d = (c - mech.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!spawnedChargers.Contains(this)) spawnedChargers.Add(this);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            spawnedChargers.Remove(this);
            chargingMechs.Clear();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref chargingEnabled, "OSV_chargingEnabled", defaultValue: false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (!parent.Spawned) yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Mech charging",
                defaultDesc = "Let this landed shuttle recharge nearby colony mechs, powered by its own "
                    + "chemfuel. This drains the fuel the shuttle needs to fly home.",
                icon = ChargeIcon,
                isActive = () => chargingEnabled,
                toggleAction = delegate
                {
                    // Route through the MP-synced setter so all clients flip together.
                    SetChargingEnabled(!chargingEnabled);
                }
            };
        }

        public override void PostDraw()
        {
            base.PostDraw();
            // Marker overlay: show the charge spots around the hull while it's selected and enabled.
            if (chargingEnabled && parent.Spawned && Find.Selector.IsSelected(parent))
            {
                List<IntVec3> cells = new List<IntVec3>(ChargeCells());
                if (cells.Count > 0) GenDraw.DrawFieldEdges(cells, ChargeCellColor);
            }

            // Charging cable: a bundled-wire line from each charging mech to the hull's centre, drawn at
            // the SmallWire altitude (below Building) so the shuttle renders on top of it. Reuses the
            // vanilla mech-charger wire material. (Draw-only, client-local - safe to iterate the set.)
            if (parent.Spawned && chargingMechs.Count > 0)
            {
                Vector3 center = parent.TrueCenter();
                center.y = AltitudeLayer.SmallWire.AltitudeFor();
                foreach (Pawn m in chargingMechs)
                {
                    if (m == null || !m.Spawned || m.Map != parent.Map) continue;
                    Vector3 mp = m.DrawPos;
                    mp.y = center.y;
                    GenDraw.DrawLineBetween(center, mp, Icons.Wire, 0.3f);
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            if (!chargingEnabled) return "Mech charging: off";
            string s = "Mech charging: on";
            if (chargingMechs.Count > 0)
            {
                s += " (" + chargingMechs.Count + "/" + EffectiveMaxSimultaneous + " charging)";
            }
            if (Fuel != null && !Fuel.HasFuel) s += " - out of chemfuel";
            return s;
        }

        private static readonly Color ChargeCellColor = new Color(0.3f, 0.9f, 0.4f, 0.5f);

        [StaticConstructorOnStartup]
        private static class Icons
        {
            public static readonly Texture2D Charge = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower");
            // Same bundled-wire material the vanilla mech charger draws its cable with.
            public static readonly Material Wire = MaterialPool.MatFrom("Other/BundledWires", ShaderDatabase.Transparent, Color.white);
        }
        private static Texture2D ChargeIcon => Icons.Charge;
    }
}
