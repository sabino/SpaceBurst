# Gameplay

SpaceBurst is built as a forward-scrolling arcade shooter with a deterministic campaign loop rather than an endless survival arena.

## Core Loop

1. Clear authored stage sections while the world scrolls forward.
2. Collect style-specific power cores from destroyed enemies.
3. Survive with `Ships` for in-place respawns and `Lives` for full stage restarts.
4. Use stage transitions to spend stored upgrade charges on draft cards.
5. Push through five chapter bands and defeat a boss on every 10th stage.

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

Each style has level `0` through `3`, then continues into diminishing-return rank growth for long runs. Stored charges are tracked by style so the upgrade drafts stay readable.

## Feedback Systems

- Deterministic rewind with a slow-to-fast acceleration curve.
- Stage transition FTL effects instead of hard level-complete cutaways.
- Procedural audio and chapter-aware music transitions.
- Procedural visuals, parallax backgrounds, ripples, and impact feedback.
