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
(land/hold/recall/camp/gift) · ✅ MP sync · ✅ fuel/cooldown balance · ✅ research tree · ✅ directional art ·
✅ drone Biotech-gating (`Biotech/Defs/ThingDefs_Drone.xml`) · ✅ mech-shuttle field charging ·
✅ mech-bay bandwidth upgrade research.

Remaining:
1. **MP test** of the drone/camp/gift actions AND the new mech-charging toggle (troop/cargo launches
   verified clean; the toggle syncs via `CompMechChargerShuttle.SetChargingEnabled` in `MultiplayerCompat`).

Recently landed (implementation notes):
- **Mech charging at the Mechanitor shuttle** (`CompMechChargerShuttle` + `JobDriver_MechChargeAtShuttle` +
  `Patch_JobGiver_GetEnergy_Charger`). The landed hull IS the charger — no real charger buildings. A
  low colony mech that can't reach a real `Building_MechCharger` falls back to the nearest shuttle whose
  **"Mech charging" toggle** (default OFF, opt-in — charging drains flight fuel) is on; it walks to a free adjacent cell and tops up
  at 50/day, paying `fuelPerEnergy` (0.25) chemfuel per point from the shuttle's own tank. `maxSimultaneousCharging`
  (base 2, +1 per mech-bay research tier → 2/3/4 via `EffectiveMaxSimultaneous`) + free-cell reservations
  throttle crowding. Reuses vanilla charging visuals: `Mote_MechCharging` + `MechChargerCharging` sustainer on
  the mech (in the job driver) and a `Other/BundledWires` cable from each mech to the hull centre drawn at
  `AltitudeLayer.SmallWire` (below Building) so the ship renders on top. Discovery reuses vanilla's recharge slot via a
  Harmony **postfix** on `JobGiver_GetEnergy_Charger.TryGiveJob` (no think-tree XML edit; strict fallback).
  Charger lookup iterates a static spawned-charger registry with a deterministic (dist, thingIDNumber)
  tiebreak → MP-safe. Note: can't reuse vanilla `JobDriver_MechCharge`/`Need_MechEnergy.currentCharger`
  directly — both hard-type to `Building_MechCharger`.
- **Per-shuttle upgrade research — DESCOPED to the mech ship only** (Luke 2026-07-01): two Biotech projects
  `OSV_MechBayExpansion1/2` raise the mechanitor shuttle's bandwidth budget 6→12→18 (+6/tier) and each add a
  field-charging slot (2→3→4). No other craft,
  no new variants (avoids the per-tier art/def explosion — art is the bottleneck). `CompTypedShuttleCapacity.
  EffectiveMechBandwidth` = base + finished-research bonus; ALL cap reads (inspect line, info card, load-dialog
  readout + accept, enter-job budget) route through it, so already-built ships benefit live.

## Related (separate future mods)
- **Crew Presets** — named groups of pawns (colonists/animals/mechs) applied across the vanilla pawn-selection
  dialogs (per-save). Design: `../ModIdeas/CrewPresets-DESIGN.md`.
- **Transporter Loadouts** — named item manifests (e.g. 200 chemfuel + 100 meals) applied in cargo dialogs
  (global/cross-save, since they're item-defs not pawns). Design: `../ModIdeas/TransporterLoadouts-DESIGN.md`.
- Both standalone (no Odyssey dep); they pair if installed together.
