using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    // Typed-capacity layer for our buildable shuttles. FIRST BUILD scope: crew cap + autonomous
    // (no-pawn) drone, plus an inspect line. The mech bandwidth budget and mechanitor-only /
    // cargo-mass-split enforcement are the next layer (Harmony on the load logic); their data
    // fields live here now so the XML is already forward-compatible.
    public class CompProperties_TypedShuttleCapacity : CompProperties
    {
        // Max colonist passengers (seats). -1 leaves the vanilla default (unlimited, mass-gated).
        public int maxColonists = -1;

        // Mech "slots" as a bandwidth budget: sum of each aboard mech's vanilla BandwidthCost must
        // stay <= this. 0 = not a mech carrier. (Enforcement: next build.)
        public int mechBandwidthCapacity = 0;

        // The single allowed passenger must be a mechanitor. (Enforcement: next build.)
        public bool requireMechanitor = false;

        // Autonomous craft: refuse ALL pawns/animals - items only.
        public bool noPawns = false;

        public CompProperties_TypedShuttleCapacity()
        {
            compClass = typeof(CompTypedShuttleCapacity);
        }
    }

    // DefOf for the mech-bay expansion research (Biotech-only). Each finished tier raises the
    // mechanitor shuttle's carried-mech bandwidth budget - see CompTypedShuttleCapacity.EffectiveMechBandwidth.
    [DefOf]
    public static class OSV_ResearchDefOf
    {
        [MayRequireBiotech] public static ResearchProjectDef OSV_MechBayExpansion1;
        [MayRequireBiotech] public static ResearchProjectDef OSV_MechBayExpansion2;

        static OSV_ResearchDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OSV_ResearchDefOf));
        }
    }

    public class CompTypedShuttleCapacity : ThingComp
    {
        public CompProperties_TypedShuttleCapacity Props => (CompProperties_TypedShuttleCapacity)props;

        // Mech bandwidth budget as actually enforced: the def's base plus any completed mech-bay
        // expansion research (+3 per tier). Research is per-save/global, so every mech shuttle shares
        // the same upgraded budget. Returns the base (0) unchanged for non-mech craft. All cap reads
        // (inspect line, info card, load-dialog readout + accept check, enter-job budget) use this.
        public int EffectiveMechBandwidth
        {
            get
            {
                int cap = Props.mechBandwidthCapacity;
                return cap > 0 ? cap + MechBandwidthBonus() : cap;
            }
        }

        // +6 bandwidth per finished mech-bay expansion tier.
        public static int MechBandwidthBonus() => MechBayExpansionTiers() * 6;

        // Number of finished mech-bay expansion tiers (0-2). Also drives how many mechs can charge at a
        // landed mech shuttle at once (see CompMechChargerShuttle.EffectiveMaxSimultaneous).
        // Guarded against a null game context: ResearchProjectDef.IsFinished reads Find.ResearchManager,
        // which is null when this comp's stats are shown outside a live game (e.g. the architect/def
        // info card) - without this guard that NREs the whole info-card stats list.
        public static int MechBayExpansionTiers()
        {
            if (Current.Game == null) return 0;
            int tiers = 0;
            ResearchProjectDef r1 = OSV_ResearchDefOf.OSV_MechBayExpansion1;
            ResearchProjectDef r2 = OSV_ResearchDefOf.OSV_MechBayExpansion2;
            if (r1 != null && r1.IsFinished) tiers++;
            if (r2 != null && r2.IsFinished) tiers++;
            return tiers;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Caps are scribed by CompShuttle, so we only set them once, on first spawn. This comp
            // is only ever on our own buildable craft (never quest shuttles), so no faction guard.
            if (!respawningAfterLoad)
            {
                CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
                if (shuttle != null)
                {
                    // Runs after CompShuttle.PostSpawnSetup (listed after CompProperties_Shuttle in
                    // the def), which force-sets accept* true for player shuttles - so our values win.
                    if (Props.noPawns)
                    {
                        shuttle.acceptColonists = false;
                        shuttle.acceptChildren = false;
                        shuttle.acceptColonyPrisoners = false;
                        shuttle.allowSlaves = false;
                    }
                    else if (Props.maxColonists >= 1)
                    {
                        shuttle.maxColonistCount = Props.maxColonists;
                    }
                }

                // Make the launch cooldown count from LANDING, not launch. Vanilla stamps
                // CompLaunchable.lastLaunchTick at launch, so a long flight burns the cooldown mid-air
                // (the "ready to launch again" message can fire before the shuttle even lands). A
                // shuttle spawning fresh (not a save reload) with lastLaunchTick > 0 is landing from a
                // flight - not freshly constructed (that's -1) - so restart the cooldown from now.
                CompLaunchable launchable = parent.TryGetComp<CompLaunchable>();
                if (launchable != null && launchable.lastLaunchTick > 0)
                {
                    launchable.lastLaunchTick = Find.TickManager.TicksGame;
                }
            }

            EnsureLaunchReady();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            EnsureLaunchReady();
        }

        // The autonomous drone has no crew/cargo to "load", but vanilla blocks launch until a load
        // group is initiated (groupID >= 0 -> LoadingInProgressOrReadyToLaunch). Keep an empty group
        // assigned so the drone is always ready to take off, with or without cargo. (Self-heals if a
        // load is cancelled.) InitiateLoading only assigns a groupID - it creates no hauling lord.
        private void EnsureLaunchReady()
        {
            if (!Props.noPawns || !parent.Spawned) return;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null && transporter.groupID < 0)
            {
                TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));
            }
        }

        private int LoadedColonists()
        {
            int n = 0;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing t in transporter.innerContainer)
                {
                    if (t is Pawn p && p.IsColonist) n++;
                }
            }
            return n;
        }

        private int LoadedMechBandwidth()
        {
            float total = 0f;
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing t in transporter.innerContainer)
                {
                    if (t is Pawn p && p.RaceProps.IsMechanoid) total += p.GetStatValue(StatDefOf.BandwidthCost);
                }
            }
            return Mathf.RoundToInt(total);
        }

        public override string CompInspectStringExtra()
        {
            if (Props.noPawns)
            {
                return "Crew: none (autonomous)";
            }

            List<string> lines = new List<string>();

            if (Props.requireMechanitor)
            {
                lines.Add("Crew (mechanitor): " + LoadedColonists() + " / " + Props.maxColonists);
            }
            else if (Props.maxColonists >= 1)
            {
                lines.Add("Passengers: " + LoadedColonists() + " / " + Props.maxColonists);
            }

            if (Props.mechBandwidthCapacity > 0)
            {
                lines.Add("Mechs: " + LoadedMechBandwidth() + " / " + EffectiveMechBandwidth + " bandwidth");
            }

            return lines.Count > 0 ? string.Join("\n", lines.ToArray()) : null;
        }

        // Shown in the info card ("i" window) under Building.
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (StatDrawEntry e in base.SpecialDisplayStats())
            {
                yield return e;
            }

            if (Props.noPawns)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Passenger capacity",
                    "None (autonomous)",
                    "This craft is autonomous and carries no crew - it launches and returns on its own.", 4905);
            }
            else if (Props.requireMechanitor)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Crew capacity",
                    Props.maxColonists + " (mechanitor only)",
                    "Only a mechanitor may pilot this craft.", 4905);
            }
            else if (Props.maxColonists >= 1)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Passenger capacity",
                    Props.maxColonists.ToString(),
                    "The maximum number of colonist passengers this shuttle can carry.", 4905);
            }

            if (Props.mechBandwidthCapacity > 0)
            {
                string val = EffectiveMechBandwidth + " bandwidth";
                int bonus = MechBandwidthBonus();
                if (bonus > 0) val += " (base " + Props.mechBandwidthCapacity + " + " + bonus + " researched)";
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "Mech bay capacity",
                    val,
                    "Mechs are carried against a bandwidth budget - each mech's bandwidth cost "
                    + "(e.g. cleansweeper 1, tunneler 3). The total carried must not exceed this. "
                    + "Mech-bay expansion research raises this budget.", 4904);
            }
        }
    }
}
