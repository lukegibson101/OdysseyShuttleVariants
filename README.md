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
| Mechanitor *(Biotech)* | 1 mechanitor | 6 bandwidth | 500 | 650 | 90 | mech strike carrier |
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

## Roadmap

Feature-complete and stable for singleplayer. Planned next:
- **Drone Biotech-gating** — move the drone defs under `Biotech/` so non-Biotech games don't error (release prep).
- **Per-shuttle upgrade research** — projects that boost a craft's signature stat (cargo / fuel efficiency /
  range / crew-or-mech capacity).
- **Mech recharging** — a landed Mechanitor shuttle acts as a chemfuel-powered field charger for nearby mechs.

See `CLAUDE.md` for the dev guide and the full backlog.

## License / credits
Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon
Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
