using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace OdysseyShuttleVariants
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            new Harmony("luke.odysseyshuttlevariants").PatchAll();
        }
    }

    // Reflective access to the private bits of Dialog_LoadTransporters we need.
    internal static class DialogAccess
    {
        public static readonly AccessTools.FieldRef<Dialog_LoadTransporters, List<CompTransporter>> Transporters =
            AccessTools.FieldRefAccess<Dialog_LoadTransporters, List<CompTransporter>>("transporters");

        public static readonly AccessTools.FieldRef<Dialog_LoadTransporters, List<TransferableOneWay>> Transferables =
            AccessTools.FieldRefAccess<Dialog_LoadTransporters, List<TransferableOneWay>>("transferables");

        public static CompTypedShuttleCapacity GetComp(Dialog_LoadTransporters dlg)
        {
            List<CompTransporter> t = Transporters(dlg);
            if (t == null || t.Count == 0 || t[0].parent == null) return null;
            return t[0].parent.TryGetComp<CompTypedShuttleCapacity>();
        }

        // Colonists currently ticked to load.
        public static int SelectedColonists(Dialog_LoadTransporters dlg)
        {
            int n = 0;
            List<TransferableOneWay> list = Transferables(dlg);
            if (list != null)
            {
                foreach (TransferableOneWay tr in list)
                {
                    if (tr.CountToTransfer > 0 && tr.AnyThing is Pawn p && p.IsColonist) n++;
                }
            }
            return n;
        }

        // Any pawns (colonists or animals) currently ticked to load.
        public static int SelectedPawns(Dialog_LoadTransporters dlg)
        {
            int n = 0;
            List<TransferableOneWay> list = Transferables(dlg);
            if (list != null)
            {
                foreach (TransferableOneWay tr in list)
                {
                    if (tr.CountToTransfer > 0 && tr.AnyThing is Pawn) n += tr.CountToTransfer;
                }
            }
            return n;
        }

        // Total bandwidth cost of the mechs currently ticked to load.
        public static float SelectedMechBandwidth(Dialog_LoadTransporters dlg)
        {
            float total = 0f;
            List<TransferableOneWay> list = Transferables(dlg);
            if (list != null)
            {
                foreach (TransferableOneWay tr in list)
                {
                    if (tr.CountToTransfer > 0 && tr.AnyThing is Pawn p && p.RaceProps.IsMechanoid)
                    {
                        total += p.GetStatValue(StatDefOf.BandwidthCost) * tr.CountToTransfer;
                    }
                }
            }
            return total;
        }
    }

    // Live passenger readout in the load dialog header (top-left), red when over the cap.
    [HarmonyPatch(typeof(Dialog_LoadTransporters), "DoWindowContents")]
    public static class Patch_Dialog_DoWindowContents
    {
        public static void Postfix(Dialog_LoadTransporters __instance)
        {
            CompTypedShuttleCapacity comp = DialogAccess.GetComp(__instance);
            if (comp == null) return;

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.UpperLeft;
            float y = 6f;

            if (comp.Props.noPawns)
            {
                int pawns = DialogAccess.SelectedPawns(__instance);
                bool over = pawns > 0;
                DrawLine(ref y, over, over ? "No crew allowed (cargo only)" : "Autonomous: cargo only");
            }
            else if (comp.Props.maxColonists >= 0)
            {
                int n = DialogAccess.SelectedColonists(__instance);
                string lbl = comp.Props.requireMechanitor ? "Mechanitor: " : "Passengers: ";
                DrawLine(ref y, n > comp.Props.maxColonists, lbl + n + " / " + comp.Props.maxColonists);
            }

            if (comp.Props.mechBandwidthCapacity > 0)
            {
                int used = Mathf.RoundToInt(DialogAccess.SelectedMechBandwidth(__instance));
                DrawLine(ref y, used > comp.Props.mechBandwidthCapacity,
                    "Mechs: " + used + " / " + comp.Props.mechBandwidthCapacity + " bandwidth");
            }

            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
        }

        private static void DrawLine(ref float y, bool over, string text)
        {
            GUI.color = over ? ColorLibrary.RedReadable : Color.white;
            Widgets.Label(new Rect(12f, y, 360f, 25f), text);
            y += 22f;
        }
    }

    // Block Accept when over the cap (or any pawn on a cargo-only drone), with a message.
    [HarmonyPatch(typeof(Dialog_LoadTransporters), "CheckForErrors")]
    public static class Patch_Dialog_CheckForErrors
    {
        public static void Postfix(Dialog_LoadTransporters __instance, List<Pawn> pawns, ref bool __result)
        {
            if (!__result) return; // already failing for another reason
            CompTypedShuttleCapacity comp = DialogAccess.GetComp(__instance);
            if (comp == null) return;

            if (comp.Props.noPawns)
            {
                if (pawns != null && pawns.Count > 0)
                {
                    Messages.Message("This drone is autonomous and carries cargo only - no crew.",
                        MessageTypeDefOf.RejectInput, historical: false);
                    __result = false;
                }
                return;
            }

            if (comp.Props.requireMechanitor && pawns != null)
            {
                foreach (Pawn p in pawns)
                {
                    if (p.IsColonist && p.mechanitor == null)
                    {
                        Messages.Message("This craft can only be piloted by a mechanitor.",
                            MessageTypeDefOf.RejectInput, historical: false);
                        __result = false;
                        return;
                    }
                }
            }

            if (comp.Props.maxColonists >= 0 && pawns != null)
            {
                int colonists = 0;
                foreach (Pawn p in pawns)
                {
                    if (p.IsColonist) colonists++;
                }
                if (colonists > comp.Props.maxColonists)
                {
                    Messages.Message("This shuttle can carry at most " + comp.Props.maxColonists + " passengers.",
                        MessageTypeDefOf.RejectInput, historical: false);
                    __result = false;
                }
            }

            if (comp.Props.mechBandwidthCapacity > 0 && pawns != null)
            {
                float used = 0f;
                foreach (Pawn p in pawns)
                {
                    if (p.RaceProps.IsMechanoid) used += p.GetStatValue(StatDefOf.BandwidthCost);
                }
                if (used > comp.Props.mechBandwidthCapacity)
                {
                    Messages.Message("This shuttle's mech bay holds at most " + comp.Props.mechBandwidthCapacity
                        + " bandwidth of mechs.", MessageTypeDefOf.RejectInput, historical: false);
                    __result = false;
                }
            }
        }
    }

    // The catch-all enforcement point. IsAllowed feeds the enter float-menu (via IsAllowedNow),
    // JobDriver_EnterTransporter's FailOn, and auto-loading - so capping it here covers EVERY load
    // path (right-click "enter", multi-select orders, the dialog's actual loading), not just the
    // dialog's Accept button. Colonists over the cap have their enter-job fail as others board.
    [HarmonyPatch(typeof(CompShuttle), "IsAllowed")]
    public static class Patch_CompShuttle_IsAllowed
    {
        // Drone (no pawns) and mech-ship (mechanitor-only) legitimately filter WHICH pawns may board,
        // so they belong here - this also cleanly hides invalid pawns from the load dialog's list.
        // The colonist COUNT cap is deliberately NOT here: IsAllowed also builds the dialog's
        // available-pawn list (TransporterUtility.AllSendablePawns), so capping it would make the
        // other colonists vanish once full and block swapping. The count is enforced on the
        // enter-job instead (see Patch_JobDriver_EnterTransporter).
        public static void Postfix(CompShuttle __instance, Thing t, ref bool __result)
        {
            if (!__result) return;
            CompTypedShuttleCapacity comp = __instance.parent.TryGetComp<CompTypedShuttleCapacity>();
            if (comp == null || !(t is Pawn pawn)) return;

            // Drone: no pawns of any kind.
            if (comp.Props.noPawns)
            {
                __result = false;
                return;
            }

            // Mechs may only board a craft that has a mech bay (mechBandwidthCapacity > 0). The
            // budget itself is enforced on the enter-job; here we just keep them off non-mech craft.
            if (pawn.RaceProps.IsMechanoid)
            {
                if (comp.Props.mechBandwidthCapacity <= 0) __result = false;
                return;
            }

            // Mech ship: a colonist passenger must be a mechanitor.
            if (comp.Props.requireMechanitor && pawn.IsColonist && pawn.mechanitor == null)
            {
                __result = false;
            }
        }
    }

    // Colonist seat-cap enforcement on the actual enter-job, so it catches EVERY load path (dialog
    // loading and map right-click / multi-select orders) without filtering the dialog's pawn list.
    // The over-cap colonist's enter-job fails as the others fill the seats, with a throttled alert.
    [HarmonyPatch(typeof(JobDriver_EnterTransporter), "MakeNewToils")]
    public static class Patch_JobDriver_EnterTransporter
    {
        private static int lastMsgTick = -9999;

        public static void Postfix(JobDriver_EnterTransporter __instance)
        {
            CompShuttle shuttle = __instance.Shuttle;
            if (shuttle == null) return;
            ThingWithComps parent = shuttle.parent;
            CompTypedShuttleCapacity comp = parent.TryGetComp<CompTypedShuttleCapacity>();
            if (comp == null || comp.Props.noPawns) return;

            Pawn pawn = __instance.pawn;

            // Colonist seat cap.
            if (comp.Props.maxColonists >= 0)
            {
                __instance.AddFailCondition(delegate
                {
                    if (pawn == null || !pawn.IsColonist) return false;
                    if (ColonistsAboard(parent, pawn) < comp.Props.maxColonists) return false;
                    Alert(parent, parent.LabelShortCap + " is at maximum occupancy (" + comp.Props.maxColonists + " passengers).");
                    return true;
                });
            }

            // Mech bay bandwidth budget.
            if (comp.Props.mechBandwidthCapacity > 0)
            {
                __instance.AddFailCondition(delegate
                {
                    if (pawn == null || !pawn.RaceProps.IsMechanoid) return false;
                    float cost = pawn.GetStatValue(StatDefOf.BandwidthCost);
                    if (MechBandwidthAboard(parent, pawn) + cost <= comp.Props.mechBandwidthCapacity) return false;
                    Alert(parent, parent.LabelShortCap + " mech bay is full (" + comp.Props.mechBandwidthCapacity + " bandwidth).");
                    return true;
                });
            }
        }

        private static int ColonistsAboard(ThingWithComps shuttle, Pawn excluding)
        {
            int n = 0;
            CompTransporter transporter = shuttle.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing th in transporter.innerContainer)
                {
                    if (th != excluding && th is Pawn p && p.IsColonist) n++;
                }
            }
            return n;
        }

        private static float MechBandwidthAboard(ThingWithComps shuttle, Pawn excluding)
        {
            float total = 0f;
            CompTransporter transporter = shuttle.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                foreach (Thing th in transporter.innerContainer)
                {
                    if (th != excluding && th is Pawn p && p.RaceProps.IsMechanoid)
                    {
                        total += p.GetStatValue(StatDefOf.BandwidthCost);
                    }
                }
            }
            return total;
        }

        // One global cooldown so the per-tick fail condition doesn't spam the alert.
        private static void Alert(ThingWithComps shuttle, string text)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            int now = Find.TickManager.TicksGame;
            if (now - lastMsgTick > 90)
            {
                lastMsgTick = now;
                Messages.Message(text, shuttle, MessageTypeDefOf.RejectInput, historical: false);
            }
        }
    }

    // Vanilla shuttles refuse to launch without a free colonist of PilotingAbility > 0.1. Two of ours
    // legitimately have no such pilot: the autonomous drone (no crew at all) and the mech ship (its
    // mechanitor interfaces with the ship systems rather than flying it by skill).
    [HarmonyPatch(typeof(CompShuttle), "HasPilot", MethodType.Getter)]
    public static class Patch_CompShuttle_HasPilot
    {
        public static void Postfix(CompShuttle __instance, ref bool __result)
        {
            if (__result) return;
            CompTypedShuttleCapacity comp = __instance.parent.TryGetComp<CompTypedShuttleCapacity>();
            if (comp == null) return;

            // Autonomous drone: flies itself, no pilot needed.
            if (comp.Props.noPawns)
            {
                __result = true;
                return;
            }

            // Mech ship: a mechanitor aboard counts as the pilot (it still won't launch empty,
            // which is intended - the mech ship needs its mechanitor).
            if (comp.Props.requireMechanitor)
            {
                CompTransporter transporter = __instance.parent.TryGetComp<CompTransporter>();
                if (transporter != null)
                {
                    foreach (Thing th in transporter.innerContainer)
                    {
                        if (th is Pawn p && p.mechanitor != null)
                        {
                            __result = true;
                            return;
                        }
                    }
                }
            }
        }
    }

    // The autonomous drone has no colonist, so vanilla's launch options either don't apply (a caravan
    // can't form without a pawn owner; a settlement only offers crewed "visit") or would lose it (the
    // "contents will be lost" fallback). For our no-pawn drone we therefore curate the destination menu
    // so it can NEVER lose its cargo: strip the loss option, offer "gift cargo" at friendly settlements
    // (keeping the drone), and "land & hold" on any mapless passable tile. Own-colony / map tiles keep
    // their normal vanilla land options.
    [HarmonyPatch(typeof(CompLaunchable), "GetTransportersFloatMenuOptionsAt")]
    public static class Patch_CompLaunchable_FloatMenu
    {
        public static void Postfix(CompLaunchable __instance, PlanetTile tile,
            Action<PlanetTile, TransportersArrivalAction> launchAction, ref IEnumerable<FloatMenuOption> __result)
        {
            CompTypedShuttleCapacity comp = __instance.parent.TryGetComp<CompTypedShuttleCapacity>();
            if (comp == null || !comp.Props.noPawns) return;

            List<FloatMenuOption> opts = (__result != null)
                ? new List<FloatMenuOption>(__result)
                : new List<FloatMenuOption>();

            // The drone never loses its cargo: drop vanilla's "contents will be lost" option.
            string lossLabel = "TransportPodsContentsWillBeLost".Translate();
            opts.RemoveAll(o => o.Label == lossLabel);

            Settlement settlement = Find.WorldObjects.WorldObjectAt<Settlement>(tile);
            if (settlement != null && settlement.Faction != null && !settlement.Faction.IsPlayer
                && !settlement.Faction.HostileTo(Faction.OfPlayer))
            {
                // Gift cargo to the settlement faction (goodwill), drone holds at the tile afterward.
                opts.Add(new FloatMenuOption("Gift cargo to " + settlement.Label, delegate
                {
                    launchAction(tile, new TransportersArrivalAction_DroneGift(settlement));
                }));
            }
            else if (!Find.World.Impassable(tile) && !Find.WorldObjects.AnyMapParentAt(tile)
                && !Find.WorldObjects.AnyWorldObjectAt<WorldObject_LandedDrone>(tile))
            {
                // Mapless passable tile (incl. empty wilderness): land & hold the cargo.
                opts.Add(new FloatMenuOption("Land drone here (hold cargo)", delegate
                {
                    launchAction(tile, new TransportersArrivalAction_DroneLand());
                }));
            }

            __result = opts;
        }
    }

    // Add landed transport drones to the world map's "Jump to location..." menu, which vanilla only
    // populates with player maps, quest objects, and caravans (our drone is a bespoke world object, so
    // it's otherwise missing). A camped drone is a Map and already appears via the maps loop. We can't
    // append to the list the vanilla action builds in its own closure, so we replace the action with a
    // faithful copy of WorldGizmoUtility.GetJumpToGizmo plus our landed drones.
    [HarmonyPatch(typeof(WorldGizmoUtility), "GetJumpToGizmo")]
    public static class Patch_WorldGizmoUtility_JumpTo
    {
        public static void Postfix(ref Gizmo __result)
        {
            if (__result is Command_Action cmd) cmd.action = BuildMenu;
        }

        private static void BuildMenu()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            HashSet<WorldObject> seen = new HashSet<WorldObject>();

            foreach (Map m in Find.Maps)
            {
                if (m.IsPocketMap || seen.Contains(m.Parent)) continue;
                string text = m.Parent.LabelCap;
                if (GravshipUtility.TryGetNameOfGravshipOnMap(m, out string name)) text = text + " (" + name + ")";
                MapParent parentLocal = m.Parent;
                list.Add(new FloatMenuOption(text, delegate { CameraJumper.TryJumpAndSelect(parentLocal); },
                    parentLocal.ExpandingIcon, parentLocal.ExpandingIconColor));
                seen.Add(parentLocal);
            }
            foreach (Quest quest in Find.QuestManager.questsInDisplayOrder)
            {
                foreach (QuestPart part in quest.PartsListForReading)
                {
                    if (part is QuestPart_SpawnWorldObject sp && sp.worldObject != null
                        && !sp.worldObject.Destroyed && sp.worldObject.Spawned && !seen.Contains(sp.worldObject))
                    {
                        WorldObject woLocal = sp.worldObject;
                        list.Add(new FloatMenuOption(woLocal.LabelCap, delegate { CameraJumper.TryJumpAndSelect(woLocal); },
                            woLocal.ExpandingIcon, woLocal.ExpandingIconColor));
                        seen.Add(woLocal);
                    }
                }
            }
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                if (seen.Contains(caravan)) continue;
                Caravan caravanLocal = caravan;
                list.Add(new FloatMenuOption(caravanLocal.LabelCap, delegate { CameraJumper.TryJumpAndSelect(caravanLocal); },
                    caravanLocal.ExpandingIcon, caravanLocal.ExpandingIconColor));
                seen.Add(caravanLocal);
            }
            // Our addition: landed transport drones (world-map objects with no map of their own).
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (wo is WorldObject_LandedDrone && !seen.Contains(wo))
                {
                    WorldObject woLocal = wo;
                    list.Add(new FloatMenuOption(woLocal.LabelCap, delegate { CameraJumper.TryJumpAndSelect(woLocal); },
                        woLocal.ExpandingIcon, woLocal.ExpandingIconColor));
                    seen.Add(woLocal);
                }
            }
            if (list.Count == 0) list.Add(new FloatMenuOption("NothingToJumpTo".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
