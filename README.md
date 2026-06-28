# Odyssey Shuttle Variants

Adds buildable transport-shuttle variants to RimWorld 1.6's **Odyssey** shuttle system, with
distinct roles, a (planned) engine research tree, and (planned) independent crew/cargo capacity.

## Status: XML scaffold (functional)

Three buildable shuttles, each research-gated off the vanilla `Shuttles` project, built in the
**Odyssey** architect category (each needs a `ShuttleEngine`):

| Shuttle | Mass cap | Range | Notes |
|---|---:|---:|---|
| Cargo shuttle | 2500 | 85 | heavy hauler, slow turnaround |
| Troop shuttle | 1200 | 120 | fast, long range |
| Mechanitor shuttle | 2000 | 90 | Biotech-only (costs SubcoreBasic) |

All are rotatable + paintable. Built on vanilla classes (`Building_PassengerShuttle`,
`CompProperties_Shuttle/Launchable/Transporter/Refuelable`) — no C# required yet.

## Art

Per-shuttle art lives in `Textures/Things/Building/OSV/<Shuttle>/` (+ the Biotech one under
`Biotech/Textures/...`). Currently every shuttle ships a **clone of the vanilla Odyssey
PassengerShuttle** frames (so all three are independent items, identical for now). Replace the
Troop/Mech sets with bespoke directional art later — see `../art/generation-prompts.md`.

## Planned (C# phases, after the Odyssey internals are documented)
- **Independent crew/cargo capacity** (the headline feature) — custom comp + load-flow patch + MP sync.
- **Tiered engine crafting/research tree** — craftable engine items gating shuttle stats.

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
