# Architecture

## Solution Layout

- `SpaceBurst/`: main desktop game
- `SpaceBurst.Android/`: Android host that compiles the shared game code
- `SpaceBurst.Runtime/`: shared runtime content contracts and validation types
- `SpaceBurst.Runtime.Tests/`: runtime tests
- `SpaceBurst.Tests/`: gameplay progression, difficulty, loadout, and save-integrity tests
- `SpaceBurst.Web/`: the Blazor WebAssembly host and browser render loop
- `Levels/`: authored stage and archetype JSON

## Runtime Structure

- `CampaignDirector` owns high-level game flow, tutorial, transitions, save slots, and progression.
- `SpawnDirector` owns horde packets, elite bursts, kill-chain rewards, and presentation cues; stage completion waits for its combat timeline.
- `Player1`, enemies, projectiles, and feedback systems run inside the shared gameplay simulation.
- `SpaceBurst.Runtime` defines the serializable contracts used by gameplay code, validation, and tooling.
- `WeaponProgressionCatalog` defines chapter unlocks and the complete one-evolution-per-style contract.
- `RunSaveIntegrity` versions and seals run snapshots before the platform storage backend performs atomic writes and backup rotation.

## Documentation Scope

Public product docs cover the game, runtime contracts, build flow, and legal information. The internal level editor is intentionally excluded from release downloads and public documentation for now.
