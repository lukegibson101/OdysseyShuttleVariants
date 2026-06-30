# Build plan — Transport Drone "land & hold cargo" (drone autonomy)

Status: **DONE + VERIFIED IN-GAME 2026-06-30 (Approach A).** Land-and-hold, standard Launch (stay on
world map), gift-to-settlement (keeps drone), Form camp (persistent custom MapParent), re-launch from
camp, jump-to-location, settlement icon-z — all confirmed working by Luke. Remaining: MP sync pass
(singleplayer-only until then) + optional polish. See "BUILT" section at the bottom for the final design.

This was the biggest single C# piece in the mod. The original plan + spike + decision are below.

## The spec (Luke, 2026-06-30)
The drone must **behave exactly like a normal shuttle that has a colonist aboard**:
1. Sendable to **any** world tile, including empty wilderness (no occupied-tile restriction).
2. On arrival it **lands and HOLDS its cargo** — never the vanilla "no colonists → contents lost".
3. Player can **form a camp** from the landed drone (spin up a map to unload / settle).
4. Must **survive abandoning a colony map** — i.e. it lives on the WORLD map as its own object,
   not as a building on a colony map you could leave behind by accident.
5. Also wants: **re-launch** the landed drone onward, and **recall** it home.

The drone is our `OSV_DroneShuttle` (ThingDefs_Drone.xml), carries the comp
`CompTypedShuttleCapacity` with `<noPawns>true</noPawns>`.

## Why we can't reuse a vanilla caravan (researched in decompile)
- `TransportersArrivalAction_FormCaravan.CanFormCaravanAt` → `TransportersArrivalActionUtility
  .AnyPotentialCaravanOwner` requires a **pawn owner** OR `CaravanShuttleUtility.IsCaravanShuttle`
  (= a shuttle already nested inside a MANNED caravan — not our standalone case).
- `Caravan.cs` ~1055-1075: once a caravan has **no owner pawns** it **`Destroy()`s and drops/loses
  contents**. So a pawnless caravan is torn down — can't hold the drone's cargo.
- Conclusion: must build a **custom persistent WorldObject**, not a vanilla caravan.

## Rejected shortcut — "trap a pawn aboard so it acts manned" (Luke idea, 2026-06-30)
Considered permanently embedding a pawn (mech) in the drone so `AnyPotentialCaravanOwner`
passes and vanilla manned-shuttle behavior handles everything. **Does NOT work:**
- `CaravanUtility.IsOwner` = `!pawn.NonHumanlikeOrWildMan() && pawn.Faction==caravanFaction
  && pawn.HostFaction==null && !pawn.IsSlave`. A **mech is non-humanlike → IsOwner false**, so a
  mech aboard does NOT make a caravan form or persist. Drone still hits the loss path.
- Worse: the trapped mech is in the same inner container → it'd be **lost with the cargo**.
- Only a **humanlike colonist** satisfies IsOwner — a "trapped colonist" would work mechanically
  but shows in the colonist bar with needs/mood and sacrifices a colonist. Not viable.
- Also: a lone friendly **mech does not keep a temporary map alive** (only colonists do; home
  maps persist regardless). So this doesn't solve the map-abandon case either.
=> custom WorldObject remains the only clean solution.

## Vanilla anchors (RimWorld-Decompiled/Assembly-CSharp/…)
- `RimWorld/CompLaunchable.cs`
  - `StartChoosingDestination` (≈265), instance `ChoseWorldTarget` (≈296), static
    `ChoseWorldTarget` (≈455) — targeting flow.
  - `GetTransportersFloatMenuOptionsAt` (private) — builds the per-tile options; the
    **loss path** is the final `if (!anything …) yield return "TransportPodsContentsWillBeLost"
    … launchAction(tile, null)`. This is where we inject a **"Land drone here"** option.
  - `TryLaunch` (≈308) — builds the `ActiveTransporter`, the `FlyShipLeaving` skyfaller, sets
    `worldObjectDef` (= our `OSV_TravelingShuttle` while in transit) + `arrivalAction`.
- `RimWorld.Planet/TransportersArrivalAction.cs` — abstract base to subclass. Members:
  `bool GeneratesMap { get; }`, `StillValid(...)`, `ShouldUseLongEvent(...)`,
  `void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)`, `ExposeData()`.
- `RimWorld.Planet/TravellingTransporters.cs` — the in-transit world object; `Arrived()` (≈206)
  resolves a null/invalid arrivalAction (lands on a map at the tile → form caravan → give to
  caravan → else lost). Our custom arrivalAction short-circuits all that.
- `RimWorld.Planet/TransportersArrivalAction_FormCaravan.cs` `.Arrived` — reference pattern for
  pulling things out of `transporters[i].innerContainer` and spawning a world object.
- Camp: `RimWorld.Planet/Camp.cs`, `SettleInEmptyTileUtility.cs`, `SettleInExistingMapUtility.cs`.

## TWO CANDIDATE APPROACHES — decide first next session
### Approach A — custom "landed drone" WorldObject (detailed below)
Purpose-built; full control; no stray pawn. But must reimplement movement/camp/settle/recall.

### Approach B — hidden "ghost pilot" pawn (Luke idea, 2026-06-30) — STRONG CONTENDER
Permanently keep an **invisible, inert humanlike pawn** aboard the drone so `IsOwner` passes and
the drone reuses ALL vanilla manned-shuttle behavior **for free**: form caravan, MOVE, CAMP, SETTLE,
recall. Much less code than A, and more capable.
- The pawn MUST be humanlike (IsOwner needs `!NonHumanlikeOrWildMan()`), player faction, not slave.
- Make it inert: no sleep/needs (disable needs or a custom always-satisfied hediff/comp), no mood,
  not draftable, not a population/incident target, never abandoned.
- **Visibility nuance:**
  - WHILE STORED in the drone's `CompTransporter.innerContainer` (never spawned on a map) it is
    **already invisible** — not on the colonist bar, no needs tick, untargetable. So in-transit /
    parked = hidden for free.
  - WHEN A CARAVAN FORMS the pawn becomes a caravan member → it would SHOW in the caravan contents
    UI and could trigger colonist-targeting incidents / be drafted / left behind. THIS is the part
    to suppress (hide from caravan UI, block incident targeting, lock it to the drone).
- Generation must be deterministic (no `Rand` without push/pop seed) for MP.
- Risk: hiding a humanlike pawn perfectly is a known-fiddly problem (alerts, caravan UI, raids,
  health). Feasibility hinges on how clean the suppression can be.
- KEY QUESTION to resolve first: can we keep the ghost pawn ALWAYS in a container (so it's never a
  visible caravan member) while STILL getting vanilla caravan/camp/settle? If a real caravan must
  expose its pawns, B's "free" win shrinks and A may win after all.

Recommendation: spike Approach B first (cheap to test — generate a hidden human, stuff it in the
drone, send to empty tile, see if a caravan forms & persists with cargo). If the caravan-UI/
incident suppression is clean, ship B. Else fall back to A.

## Architecture (Approach A detail)
1. **`WorldObject_LandedDrone : WorldObject, IThingHolder`** (new)
   - Holds an `ActiveTransporterInfo` (or directly a `ThingOwner`) = the drone + its cargo.
     Mirror how `TravellingTransporters` stores `List<ActiveTransporterInfo> transporters` and
     scribes them (`Scribe_Deep`/`Scribe_Collections`). `GetDirectlyHeldThings`/`GetChildHolders`.
   - Save via `ExposeData`. Player faction. Sits on the world map → immune to colony-map abandon.
   - `WorldObjectDef OSV_LandedDrone` (new Def): texture = the drone sprite, `expandingIcon`,
     `useDynamicDrawer` / select etc. Model on a Caravan/Settlement WorldObjectDef.
   - Gizmos (`GetGizmos`):
     - **Re-launch / send onward** — reuse `CompLaunchable`-style targeting to fly the drone to a
       new tile (re-create the travelling object + our arrival action). Fuel math reuse.
     - **Form camp** — generate a map at this tile and place the drone+cargo there so the player
       can unload/settle. Look at how Odyssey/Royalty camps spawn a map (`Camp` / pocket-map
       utilities, `MapGenerator.GenerateMap`, `CaravanEnterMapUtility`-equivalent for placing).
     - **Recall home** — fly back to the nearest player colony (re-launch to home tile).
     - (maybe) **Abandon** — destroy + drop nothing / salvage.
   - Fuel: the drone has `CompRefuelable`; decide whether the landed object tracks/needs fuel to
     re-launch (design says fuel-gated; can stub v1 = always allow, refine later).
2. **`TransportersArrivalAction_DroneLand : TransportersArrivalAction`** (new)
   - `GeneratesMap => false`. `Arrived(...)` = create the `WorldObject_LandedDrone` at `tile`,
     move the transporters' inner containers into it (do NOT form a caravan, do NOT drop pods).
   - `StillValid` = true for any passable tile.
3. **Harmony injection** (HarmonyPatches.cs)
   - Patch `CompLaunchable.GetTransportersFloatMenuOptionsAt` (or the static `ChoseWorldTarget`)
     so that, when the launchable's parent has our `noPawns` comp, the tile offers
     **"Land drone here"** → `launchAction(tile, new TransportersArrivalAction_DroneLand())`,
     and the **"contents will be lost"** option is suppressed for the drone.
   - Keep delivery options that already work (gift to settlement, give to caravan, land on your
     own map) — only replace the empty-tile loss path.

## Save / MP / determinism
- New `WorldObjectDef` + `WorldObject` subclass + arrival action are all save data → once shipped,
  **don't rename** their defNames/class names or break `ExposeData` keys (save-compat).
- MP: world-object gizmo actions are player commands → likely need `MultiplayerAPI`
  `RegisterSyncMethod` on the re-launch / form-camp / recall actions (the mod is MP-compat by
  requirement). Check how MP handles caravan/world-object commands.
- No `Rand`/`DateTime` in any sim path; if picking drop cells use `Rand.Push/Pop` with a seed.

## Build order (suggested)
1. WorldObjectDef + `WorldObject_LandedDrone` (holds cargo, saves, renders on map, selectable).
2. `TransportersArrivalAction_DroneLand` + Harmony float-menu injection → drone lands & persists
   instead of being lost. **Test: send drone to empty tile → landed-drone object appears, cargo
   intact, survives save/load and abandoning the origin map.**
3. Gizmo: **Recall home** (re-launch to nearest colony) — simplest launch reuse.
4. Gizmo: **Form camp** (generate/enter a map, place drone+cargo) — heaviest part.
5. Gizmo: **Re-launch onward**.
6. MP sync pass on all gizmo actions.
7. Fuel-gating polish (round-trip fuel checks, strand/rescue if desired).

## Test plan
- Send drone to empty tile → lands, cargo held, no loss letter.
- Save/load with a landed drone → persists with cargo.
- Abandon the colony map the drone launched from → drone unaffected (world-object).
- Form camp → map spawns, cargo accessible, can settle/unload.
- Recall → flies to home colony, lands on map, cargo delivered.
- Multiplayer (if testable) → no desync on the gizmo actions.

## Open questions for Luke
- Form-camp: temporary map you must pack up (like a quest site) or a full settle?
- Fuel: does a landed drone need fuel to re-launch, and can it strand if it can't afford the trip
  (the old "fuel-gated auto-return / strand & rescue" idea)? v1 could ignore fuel.
- Recall target: nearest colony auto, or player-chosen?

---

## SPIKE RESULT (2026-06-30) — Approach B (ghost pilot) REJECTED, A chosen

Decompile spike (no prototype needed — the code was conclusive):
- **B's reuse really exists:** a caravan DOES form on an empty tile and the **shuttle + cargo survive**
  (`TransportersArrivalAction_FormCaravan.Arrived` lines 55-58 `RemoveShuttle()` → `GiveThing(caravan)`),
  and vanilla has a full caravan-shuttle re-launch system (`CaravanShuttleUtility.LaunchShuttle`).
- **But the ghost pawn CANNOT be hidden/inert:** `IsOwner` (CaravanUtility.cs:9) ≈ `IsColonist`
  (Pawn.cs:518) — same predicate, so any pawn that triggers caravan formation also shows on the
  colonist bar (`ColonistBar.cs:276-280` filters caravan pawns only by `IsColonist`). A caravan can't
  be pawnless (inventory is stored on pawns; `Caravan.Notify_MemberDied` ~1049 destroys it + abandons
  cargo with no owner). And `Caravan_NeedsTracker` ticks food/rest → the "inert" pawn starves/alerts.
  Suppressing all of that (bar + 6 WITab_Caravan_* + needs + incidents + selection) = huge fragile
  MP-risky surface. **B is not viable cleanly.**
- => **Approach A.** Decisions (Luke 2026-06-30): pivot to A; form-camp **decide later**; fuel
  **gated + strand**.

## BUILT (2026-06-30, Approach A) — files + what works

New files (Source/): `WorldObject_LandedDrone.cs`, `TransportersArrivalAction_DroneLand.cs`;
patch added to `HarmonyPatches.cs`; def `OSV_LandedDrone` in `Defs/Shuttles_Common.xml`.

- **`WorldObject_LandedDrone : WorldObject, IThingHolder`** — holds ONE `ActiveTransporterInfo`
  (the shuttle Thing + cargo, reused verbatim from flight — the shuttle is already `SetShuttle`'d).
  `Scribe_Deep` round-trips it; `GetChildHolders` exposes the container. Lives on the world map →
  survives abandoning the origin colony map. Inspect string shows fuel + cargo count. Gizmos:
  - **Launch** (REWORKED 2026-06-30 per Luke — replaced the old bespoke "Recall home"/"Send to tile"
    with the *standard* shuttle launch, same as a caravan-borne cargo ship): the command calls
    `shuttle.LaunchableComp.StartChoosingDestination(LaunchTo)` so it gets the **fuel-range ring**,
    the full **destination float-menu** (land at colony, visit settlement, + our injected "Land drone
    here" on empty tiles), and proper camera. `EnsureLaunchReady` guards `groupID >= 0` first.
    `LaunchTo` is the launch action (mirrors `CaravanShuttleUtility.LaunchShuttle`): fuel/range check →
    **strand** (stay landed + msg) if it can't afford the trip; else consume fuel, build
    `TravellingTransporters`, `AddTransporter`, `Destroy()` self, then **jump camera to nearest colony**
    (Luke's fix — don't linger on the world map).
  - **Form camp** (ADDED 2026-06-30 per Luke "do everything a normal shuttle can, incl make camp"):
    mirrors `SettleInEmptyTileUtility.SetupCamp` — `GetOrGenerateMapUtility.GetOrGenerateMap(tile,
    WorldObjectDefOf.Camp)` → `TransportersArrivalActionUtility.DropShuttle(info, map, map.Center)`
    (respawns the ship building + dumps cargo on the camp map) → `TimedDetectionRaids` clock →
    Destroy self → jump to the ship. The ship is then a normal re-launchable building on a real map
    (also makes it visible/jumpable on a map). Disabled if `!CanCreateMapAt` / `AnyMapParentAt`.
- **`TransportersArrivalAction_DroneLand : TransportersArrivalAction`** — `GeneratesMap=false`;
  `Arrived` creates the landed-drone world object at the tile and moves the transporter into it.
- **`Patch_CompLaunchable_FloatMenu`** (postfix on private `CompLaunchable.GetTransportersFloatMenuOptionsAt`)
  — for our `noPawns` drone only, on an **empty passable tile** (`!Impassable && !AnyWorldObjectAt`)
  replaces vanilla's "contents will be lost" with **"Land drone here (hold cargo)"** →
  `launchAction(tile, new TransportersArrivalAction_DroneLand())`. Single-option result auto-invokes,
  so clicking an empty tile lands the drone immediately. Other shuttles/quest shuttles untouched.
- **`LaunchTo(tile, action)`** (shared by recall + send) mirrors `CaravanShuttleUtility.LaunchShuttle`:
  `CanLaunch` (enforces cooldown) + distance-vs-`MaxLaunchDistanceAtFuelLevel` → **strands** (stays
  landed + message) if it can't afford the trip; else `ConsumeFuel`, build `TravellingTransporters`
  (the drone's `OSV_TravelingShuttle` def), `AddTransporter`, add to world, `Destroy()` self.

Build: `cd Source && dotnet build -c Release` (clean, 0 warn). DLL → Assemblies/. Deployed (copy,
NOT junction) to `D:\...\RimWorld\Mods\OdysseyShuttleVariants` — redeploy after every C# change.

## IN-GAME TEST PLAN (do after a full restart — Luke)
1. Build a drone, load cargo, launch → click an **empty wilderness tile** → expect "Land drone here",
   drone lands as a world object, cargo intact, "has landed" message (NOT "contents lost").
2. Select the landed drone → **Recall home** → flies to nearest colony, shuttle re-materializes on the
   map with cargo.
3. **Send to tile** → world targeter; empty tile re-lands as drone; own colony lands there; reject else.
4. **Save/load** with a landed drone → persists with cargo + fuel.
5. **Abandon the origin colony map** while a drone is landed elsewhere → drone unaffected.
6. **Strand:** drain fuel low, try recall to a far colony → "not enough fuel … remains landed".

## STILL TODO (next increments)
- **MP sync** (task): uncomment `RimWorld.MultiplayerAPI` in csproj; `RegisterSyncMethod` on the
  gizmo actions (`RecallHome`, the `ChoseTarget`→`LaunchTo` path) — world-object commands need sync.
- **Form-camp** gizmo (deferred per Luke): generate/enter a map to unload/settle.
- **Fuel rescue — RESOLVED (Luke 2026-06-30): Form camp IS the rescue.** A stranded drone can always
  Form camp (no fuel needed) → the ship lands as a normal building you refuel with chemfuel → re-launch.
  No separate rescue mechanism. The strand message now points the player at Form camp.
- Cosmetic: dedicated world-map texture/expanding icon (currently reuses Caravan / PassengerShuttle).
