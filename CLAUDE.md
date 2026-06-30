# CLAUDE.md — Odyssey Shuttle Variants

A RimWorld 1.6 mod adding buildable transport-shuttle variants to the **Odyssey** shuttle system.
This file is mod-specific; the **workspace** `../CLAUDE.md` (global RimWorld rules) also applies — on
conflict, this file wins.

## What this mod is
- Five buildable craft — **Cargo / Troop / Heavy Cargo / Mechanitor (Biotech) / Transport Drone (Biotech)** —
  plus the vanilla **Passenger Shuttle** folded into our capacity system. Research-gated off vanilla
  `Shuttles`, built in the Odyssey architect category, each costs a rare `ShuttleEngine`.
- **Headline feature (done):** a **typed-capacity system** — colonists (seat count), mechs (bandwidth budget),
  and cargo (mass, with crew + mech weight excluded) are independent pools, not vanilla's single mass limit.
- **Autonomous drone (done):** 0-crew craft that lands & holds cargo on any tile via a custom world object,
  with recall / send-onward / form-camp / gift-to-settlement, fuel-gated stranding, and camp-refuel recovery.
- **Multiplayer compatibility is a hard requirement** (and is implemented — see MP section).
- NOTE: the old "tiered engine research/crafting tree" idea was **dropped**; engines are a rare build
  ingredient (trader/reward only), and differentiation comes from the typed-capacity system + drone autonomy.

## Reference FIRST — don't re-derive
- `../Docs/` modder's-map (`90-modding-patterns.md` index; `10-royalty.md §3` for `CompShuttle` /
  `Building_PassengerShuttle` / `TransportShipDef`; `14-odyssey.md`).
- `../../RimWorld-Decompiled/Assembly-CSharp/` — decompiled game source (read-only; never commit).
- Real Def XML: `D:\SteamLibrary\steamapps\common\RimWorld\Data\<Expansion>\Defs\`.

## Conventions
- **defName prefix `OSV_`**; texPaths under `Things/Building/OSV/...`. Prefix everything (global namespace).
- Built on vanilla classes (`Building_PassengerShuttle` + `CompProperties_Shuttle/Launchable/Transporter/
  Refuelable` + `TransportShipDef` + `ShuttleSkyfallerBase`). Prefer extension points; Harmony where needed.
- **Save/MP discipline:** once published, don't rename defNames or `Scribe` keys (breaks saves / the
  WorldObject/arrival-action save data). Keep sim code deterministic — no `UnityEngine.Random` / `DateTime`.

## Architecture (C#, `Source/`)
- `CompTypedShuttleCapacity` — per-craft caps (maxColonists / mechBandwidthCapacity / requireMechanitor /
  noPawns). Sets `CompShuttle.maxColonistCount` etc. on spawn; also resets the launch cooldown to the
  **landing** tick so a long flight doesn't burn it mid-air.
- `HarmonyPatches.cs` — load-dialog readouts + caps (CheckForErrors, IsAllowed-which-pawn, the enter-job
  count/bandwidth fail conditions), `HasPilot` for the pilotless drone / mechanitor, the **cargo mass-split**
  (3 postfixes excluding humanlike + mech mass: `CompTransporter.MassUsage`, `Dialog_LoadTransporters.MassUsage`,
  `CaravanShuttleUtility.GetCaravanShuttleMass`), the drone launch float-menu injection, and the "jump to
  location" menu addition. (`DialogAccess` = AccessTools field-refs into the load dialog's privates.)
- `WorldObject_LandedDrone` / `WorldObject_DroneCamp` + `TransportersArrivalAction_DroneLand` /
  `_DroneGift` — the autonomous-drone land/hold/camp/gift system. Full design + decompile anchors:
  `Source/DRONE_LANDING_BUILD.md`.
- `MultiplayerCompat.cs` — `if (MP.enabled)` registers sync methods for the world-object commands
  (`LaunchTo`, `FormCamp`); targeting stays local, UI side-effects guarded to the issuing client. The
  `0MultiplayerAPI.dll` stub is shipped in `Assemblies/` (see csproj) so the mod loads with or without MP.

## Build / deploy / test
- `cd Source && dotnet build -c Release` (.NET 10 SDK → net48). CopyToMod drops both DLLs into `Assemblies/`.
- Deploy: mirror-copy `Source/`-excluded into `<RimWorld>\Mods\OdysseyShuttleVariants` (the workspace
  `tools/deploy.ps1`, or robocopy `/MIR /XD Source obj bin .git`). It's a COPY, not a junction — redeploy
  after every change. **C# changes need a full game restart; XML reloads via Dev Mode → Reload Defs.**
- **MP:** identical mod + load order on all clients; re-share after any rebuild.

## Roadmap / backlog
Done: ✅ 5 craft + passenger integration · ✅ typed capacity + cargo mass-split · ✅ autonomous drone
(land/hold/recall/camp/gift) · ✅ MP sync · ✅ fuel/cooldown balance · ✅ research tree · ✅ directional art.

Remaining:
1. **Drone Biotech-gating** (release prep) — `Defs/ThingDefs_Drone.xml` references Biotech-only research
   (`HighMechtech`, `OSV_MechShuttleTech`) but loads from root → red errors without Biotech. Move the drone
   defs into `Biotech/` (like the Mech shuttle) to gate cleanly.
2. **MP test** of the drone/camp/gift actions (troop/cargo launches verified clean).
3. **Per-shuttle upgrade research trees** — projects that boost a craft's signature stat (cargo / fuel
   efficiency / range / crew-or-mech cap, e.g. mech bandwidth 6→9→12). Stats already read off the comp.
   DECISION (Luke 2026-07-01): upgrades **boost the EXISTING craft's stat** (comp reads research level),
   **NOT** new bigger-footprint variants per tier — avoids a per-tier art/def explosion (art is the
   bottleneck), benefits already-built ships, keeps the architect menu clean. The distinct craft
   (Troop/Cargo/Heavy/Mech/Drone) already provide the big size/role tiers; research just fine-tunes each.
4. **Mech recharging at the Mechanitor shuttle** (chemfuel-powered, landed-only). Design: the parked
   (spawned) shuttle acts like a mobile `Building_MechCharger` — deployed mechs walk up and charge at
   spots around the hull, fuelled by the shuttle's chemfuel at ~50% efficiency; adjacency throttles how many
   charge at once. A default-off **"Mech charging" toggle** controls it (fuel safety — charging burns the
   same fuel needed to fly home). **The hull is the charger** — do NOT spawn real charger buildings
   (electrical / lifecycle / stray-building mess); use a comp on the shuttle, optional charge-spot marker
   overlay. Reuse `Building_MechCharger` / `JobDriver_MechCharge` / `WorkGiver_HaulMechToCharger` /
   `JobGiver_GetEnergy_Charger` / `Need_MechEnergy`. Moderate C#, MP-deterministic.

## Related (separate future mods)
- **Crew Presets** — named groups of pawns (colonists/animals/mechs) applied across the vanilla pawn-selection
  dialogs (per-save). Design: `../ModIdeas/CrewPresets-DESIGN.md`.
- **Transporter Loadouts** — named item manifests (e.g. 200 chemfuel + 100 meals) applied in cargo dialogs
  (global/cross-save, since they're item-defs not pawns). Design: `../ModIdeas/TransporterLoadouts-DESIGN.md`.
- Both standalone (no Odyssey dep); they pair if installed together.
