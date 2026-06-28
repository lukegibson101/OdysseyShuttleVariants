# CLAUDE.md — Odyssey Shuttle Variants

A RimWorld 1.6 mod adding buildable transport-shuttle variants to the **Odyssey** shuttle system.
This file is mod-specific; the **workspace** `../CLAUDE.md` (global RimWorld rules) also applies — on
conflict, this file wins.

## What this mod is
- Three buildable shuttles — **Cargo / Troop / Mechanitor** (Biotech) — research-gated off the vanilla
  `Shuttles` project, built in the Odyssey architect category; each needs a `ShuttleEngine`.
- **Differentiators (the point of the mod):** independent **crew/cargo capacity** + a tiered **engine
  research/crafting tree**. Neither exists in vanilla or the comparable "Odyssey Transport Shuttle" mod.
- **Multiplayer compatibility is a hard requirement.**

## Reference FIRST — don't re-derive
- `../Docs/` modder's-map. START at `90-modding-patterns.md` (goal→recipe + symbol→doc index).
  Shuttle internals: `10-royalty.md §3` (`CompShuttle` / `Building_PassengerShuttle` / `TransportShipDef`).
  Odyssey: `14-odyssey.md`.
- `../../RimWorld-Decompiled/Assembly-CSharp/` — decompiled game source (read-only; never commit).
- Real Def XML examples: `D:\SteamLibrary\steamapps\common\RimWorld\Data\<Expansion>\Defs\`.

## Conventions
- **defName prefix `OSV_`**; texPaths under `Things/Building/OSV/...`. Prefix everything (defNames share a
  global namespace).
- Built on vanilla classes (`Building_PassengerShuttle` + `CompProperties_Shuttle/Launchable/Transporter/
  Refuelable` + `TransportShipDef` + `ShuttleSkyfallerBase`). **Prefer extension points over Harmony.**
- **Save/MP discipline:** once published, don't rename defNames or change `Scribe` keys (breaks saves).
  Keep sim code deterministic — no `UnityEngine.Random` / `DateTime` (see workspace CLAUDE.md).

## Art — read before touching `Textures/`
- All shuttle art is currently **cloned from the vanilla Odyssey PassengerShuttle** (placeholder, identical
  across the three). The RimWorld EULA permits vanilla art as a *basis* inside a mod **provided the Ludeon
  disclaimer ships** (it's in `About.xml`).
- These placeholder clones are **gitignored** (throwaway, regenerable, and avoids redistributing Ludeon art).
  Regenerate locally from the game install via `../tools/extract_textures.py` then clone into
  `Textures/Things/Building/OSV/<Shuttle>/`.
- Real **original** ¾-directional art (made with `../tools/make_directional.py`) replaces the clones later and
  **is** committed. Workflow + prompts: `../art/generation-prompts.md`.

## Build / deploy / test
- Pure XML right now — no DLL needed to run. `Source/` holds the `net48` csproj skeleton for the C# phases.
- Deploy: `powershell -File ../tools/deploy.ps1` (mirror-copy) or add `-Junction` for a live link.
- Test: enable in Dev Mode, watch the dev log for def errors; instant-research a shuttle + spawn a
  `ShuttleEngine`, then build from the Odyssey architect tab.

## Roadmap
1. ✅ XML scaffold — three buildable shuttles.
2. **Engine-tier tree** — engine items + recipes + branch research (XML).
3. **Crew/cargo split** (C#) — subclass `CompTransporter` (cargo = item-mass vs crew = pawn-count) + patch
   `Dialog_LoadTransporters` to show/enforce two pools + MultiplayerAPI sync. Scoped; most-invasive piece.
