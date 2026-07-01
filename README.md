# Odyssey Shuttle Variants

Expands RimWorld 1.6's **Odyssey** transport-shuttle system with a family of buildable, role-distinct
craft and a **typed-capacity system** — each shuttle carries colonists, mechs and cargo as *independent*
pools, instead of vanilla's single shared mass limit. Includes a fully **autonomous transport drone** and
is **multiplayer-compatible**.

## The craft

All are research-gated off the vanilla `Shuttles` project, built in the **Odyssey** architect category, and
cost a rare `ShuttleEngine` (trader/reward only — engine count scales with size). The vanilla **Passenger
Shuttle** is folded into the same capacity system as the baseline all-rounder.

| Craft | Crew | Mechs | Cargo (kg) | Fuel | Range | Role |
|---|--:|--:|--:|--:|--:|---|
| Passenger *(vanilla)* | 4 | – | 250 | 400 | 62 | balanced all-rounder |
| Troop | 12 | – | 100 | 550 | 120 | fast squad insertion/extraction |
| Cargo | 2 | – | 1250 | 600 | 85 | dedicated hauler |
| Heavy Cargo | 4 | – | 2500 | 800 | 70 | flagship hauler — slow, thirsty, short range |
| Mechanitor *(Biotech)* | 1 mechanitor | 6 bandwidth | 50 | 650 | 90 | mech strike carrier |
| Transport Drone *(Biotech)* | 0 | – | 1000 | 650 | 200 | autonomous courier |

*Crew = seat count; Mechs = a bandwidth budget (sum of each mech's BandwidthCost); Cargo = item/animal mass
with crew and mechs excluded. Fuel is sized so a full tank covers a comfortable round trip; range is the hard
launch-distance cap.*

## Headline features

- **Typed capacity** — colonists (by count), mechs (by bandwidth), and cargo (by mass, *excluding* crew and
  mech weight) are tracked and capped independently, with clear load-dialog readouts and over-cap warnings.
- **Autonomous transport drone** — no pilot or crew. It can be sent to any tile and **lands & holds its cargo**
  in the field (a persistent world object that survives the origin map being abandoned), then **recalled**,
  **sent onward**, or told to **form a camp** (spins up a real map with the ship on it). At a friendly
  settlement it **gifts its cargo** for goodwill and stays put to fly again. Strands (and can be camp-refuelled)
  if it can't afford a trip.
- **Multiplayer-compatible** — the world-object commands are MP-synced; the API stub ships with the mod, so it
  also runs fine without the Multiplayer mod installed.

## Research

Rooted on the vanilla `Shuttles` project: `Shuttles → Troop → Cargo → Heavy`, and a Biotech branch
`Shuttles + StandardMechtech → Mechanitor → Drone (also needs HighMechtech)`. All in a dedicated **Shuttles**
research tab.

## Art

Original ¾ directional art per craft (`Textures/Things/Building/OSV/<Craft>/`, the Biotech one under
`Biotech/Textures/...`), rotatable + paintable (`Graphic_Multi` + `CutoutComplex` + `_m` paint masks).

## Mech support (Biotech)

- **Field charging** — a landed Mechanitor shuttle doubles as a **chemfuel-powered mech charger** with no
  power grid. Flip on its **"Mech charging"** toggle (off by default — it drains the fuel the shuttle needs to
  fly home) and low colony mechs that can't reach a real charger walk up to the hull and top up from its tank.
  A real `Building_MechCharger` always wins; the shuttle is the field fallback.
- **Mech-bay expansion research** — two Biotech projects raise the Mechanitor shuttle's carried-mech
  **bandwidth budget 6 → 12 → 18** and widen its **field-charging line 2 → 3 → 4** mechs at once. It boosts the
  *existing* craft (no new variants), so ships you already built benefit the moment the research finishes.

## Roadmap

Feature-complete and stable for singleplayer. Remaining before release: a **multiplayer test** of the
autonomous-drone world commands and the mech-charging toggle (troop/cargo launches are already MP-verified).

See `CLAUDE.md` for the dev guide and the full backlog.

## License / credits
Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon
Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
