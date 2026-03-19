# Save and Rewind

## Save Slots

SpaceBurst keeps three local save slots under the platform-specific app data directory. A saved run stores stage progression, score, owned weapon styles, upgrade charges, deterministic RNG state, and the active simulation snapshot.

## Rewind

- Rewind is deterministic for gameplay state.
- The rewind meter stores up to 8 seconds.
- Holding rewind starts very slowly, then accelerates.
- Rewind refills on stage start, ship respawn, and life-based stage restart.

Using rewind or loading a save disables medal eligibility for the current run.

## Lives and Ships

- `Ships` are immediate in-place respawns.
- If ships reach zero, the next death costs a `Life`.
- Losing a life restarts the stage from the beginning.
