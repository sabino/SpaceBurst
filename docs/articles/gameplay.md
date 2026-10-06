# Gameplay

SpaceBurst is built as a forward-scrolling arcade shooter with a deterministic campaign loop rather than an endless survival arena.

## Core Loop

1. Clear authored stage sections while the world scrolls forward.
2. Collect XP shards from destroyed enemies and salvage from elites and kill-chain rewards.
3. Survive with `Ships` for in-place respawns and `Lives` for full stage restarts.
4. Earn XP level-ups and choose draft cards while the action is paused. Manual drafts wait for an explicit choice; the optional auto mode chooses randomly on its countdown. You can pause and save a pending draft.
5. Build multiplier chains at `8`, `14`, and `20` for XP, scrap, and rewind rewards.
6. Push through five named chapters and defeat a boss on every 10th stage.

## Campaign Chapters

| Stages | Chapter | New draft styles |
| --- | --- | --- |
| 1-10 | Overdrive | Pulse, Spread, Missile |
| 11-20 | Fracture | Laser, Arc |
| 21-30 | Crucible | Plasma, Drone |
| 31-40 | Parallax | Rail, Blade |
| 41-50 | Event Horizon | Fortress |

Every stage carries authored horde packets and presentation cues. Elite bursts recur across each chapter, while encounter density rises from Fracture through Event Horizon. A stage cannot finish before its authored combat timeline has resolved.

## Weapon System

- `Pulse`
- `Spread`
- `Laser`
- `Plasma`
- `Missile`
- `Rail`
- `Arc`
- `Blade`
- `Drone`
- `Fortress`

Each style has level `0` through `3`, then continues into capped diminishing-return rank growth for long runs. One core weapon and up to four support weapons fire together. Swapping styles exchanges the selected support with the previous core, so a weapon can never fire twice from duplicate loadout slots. Once the support stack is full, later chapter unlocks appear as core-swap drafts instead of disappearing from progression; the previous core stays in the arsenal and remains reachable with the style controls.

Every weapon has one unique evolution. Evolution cards appear only after the weapon reaches its required level and the linked passive reactor is installed; the same requirements are checked again when the card is applied.

## Rewards and survival

XP opens a paused three-card choice on level-up. Salvage caches and kill-chain rewards add scrap; every **five scrap** immediately builds one spare ship for an in-place respawn. The HUD shows `SHIP SCRAP 0/5` through `4/5`; the run summary reports total salvage. Partial scrap progress carries between stages and through saves. Spare ships are current-stage reserves; entering a new stage refills ships to the run's reserve allowance, including armor/reserve upgrades.

The tutorial uses a style core and stored charge to teach a practice draft. In campaign play, a core matching the active weapon upgrades it immediately through level 3; any other core grants **two XP**. Legacy banked charges convert to XP once at the next stage boundary, without unlocking weapons ahead of their chapter. Loading a save does not retroactively award ships for its historical scrap total.

## Feedback Systems

- Deterministic rewind with a slow-to-fast acceleration curve.
- Stage transition FTL effects instead of hard level-complete cutaways.
- Procedural audio and chapter-aware music transitions.
- Procedural visuals, parallax backgrounds, ripples, and impact feedback. Friendly fire is quieter than threats; side view uses crisp hostile-shot contours and corner markers to locate the player. The markers indicate ship position, not a smaller hitbox.
- A run-summary screen reports final score, difficulty, level, scrap, arsenal completion, evolution completion, and medal status.
