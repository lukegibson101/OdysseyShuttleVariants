# Odyssey Shuttle Variants

Adds buildable transport-shuttle variants to RimWorld 1.6's **Odyssey** shuttle system, with
distinct roles, a (planned) engine research tree, and (planned) independent crew/cargo capacity.

## Status: XML scaffold — NOT yet load-tested

Three buildable shuttles, each research-gated off the vanilla `Shuttles` project, built in the
**Odyssey** architect category (each needs a `ShuttleEngine`):

| Shuttle | Mass cap | Range | Notes |
|---|---:|---:|---|
| Cargo shuttle | 2500 | 85 | heavy hauler, slow turnaround |
| Troop shuttle | 1200 | 120 | fast, long range |
| Mechanitor shuttle | 2000 | 90 | Biotech-only (costs SubcoreBasic) |

Built on vanilla classes (`Building_PassengerShuttle` + `CompProperties_Shuttle/Launchable/
Transporter/Refuelable`) — pure XML, no C# required.

**Unverified — not loaded in-game once.** The defs are *configured* for rotation + paint
(`rotatable=true` + `Graphic_Multi` directional art + `CutoutComplex` + `<paintable>true` + `_m`
masks), mirroring how the comparable "Odyssey Transport Shuttle" mod does it — but whether they
load cleanly, rotate, and paint correctly is **untested**. First load test will confirm or break this.

## Art

Per-shuttle art lives in `Textures/Things/Building/OSV/<Shuttle>/` (+ the Biotech one under
`Biotech/Textures/...`). Currently every shuttle ships a **clone of the vanilla Odyssey
PassengerShuttle** frames (so all three are independent items, identical for now). Replace the
Troop/Mech sets with bespoke directional art later — see `../art/generation-prompts.md`.

## Planned (C# phases, after the Odyssey internals are documented)
- **Typed capacity system** (the headline feature) — each craft holds **colonists / mechs / cargo**
  independently (vs vanilla's single mass pool), with research that upgrades a craft's signature capacity.
- **Autonomous cargo drone** (4th craft) — 0 crew; gifts cargo at settlements, drops at own caravans/bases,
  strands if it can't afford the round trip.
- **Mech shuttle** — 1 mechanitor + research-upgradable mech slots (base 6), no cargo.

Shuttle engines are deliberately **rare** — trader/reward only, **not craftable**; each shuttle costs 1–2 + materials
(Troop/Drone 1, Cargo/Mech 2).

## TODO / polish
- **Paint masks** — refine which areas each craft paints. The auto colour-match (`tools/remask.py`) is approximate;
  hand-edit the `_<facing>m.png` masks (red = paints, black = keeps colour) for precise accent zones.

## Structure
```
About/About.xml              mod metadata + Ludeon disclaimer
LoadFolders.xml              root always; Biotech/ only when Biotech active
Defs/                        base + Cargo + Troop shuttles, skyfallers, world object, research
Biotech/Defs/                mechanitor shuttle (Biotech-gated)
Textures/                    cloned per-shuttle art
Source/                      C# skeleton (no behaviour yet)
```

## Testing
C# changes need a full game restart; XML reloads via Dev Mode → Reload Defs. Check the dev log
on load for any def errors. Build the shuttles via the Odyssey architect tab after researching
each shuttle's project (use Dev Mode to instant-research / spawn a ShuttleEngine).

## License / credits
Portions of the materials used to create this content/mod are trademarks and/or copyrighted works
of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not
endorsed by Ludeon. (Shuttle art is derived from vanilla RimWorld assets, used as a basis per
Ludeon's modding terms.)
