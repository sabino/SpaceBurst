# Architecture

## Solution Layout

- `SpaceBurst/`: main desktop game
- `SpaceBurst.Android/`: Android host that compiles the shared game code
- `SpaceBurst.Runtime/`: shared runtime content contracts and validation types
- `SpaceBurst.Runtime.Tests/`: runtime tests
- `Levels/`: authored stage and archetype JSON

## Runtime Structure

- `CampaignDirector` owns high-level game flow, tutorial, transitions, save slots, and progression.
- `Player1`, enemies, projectiles, and feedback systems run inside the shared gameplay simulation.
- `SpaceBurst.Runtime` defines the serializable contracts used by gameplay code, validation, and tooling.

## Documentation Scope

Public product docs cover the game, runtime contracts, build flow, and legal information. The internal level editor is intentionally excluded from release downloads and public documentation for now.
