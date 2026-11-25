# Ideas backlog (revisit after the first prototype)

## Substances
- **Tablets**: blank tablet → symbols printed; symbol tablet → transformed symbols (substitution, shift, reverse, stamp ops).
- **Solids**: input a solid, get it in a specific shape (sides, size, holes, material).
- Machines mixing substance types (liquid in → tablet out).

## Puzzle mechanics
- Physically broken parts: jammed lever, dead or lying display, missing part to swap in. Good twist for a later world.
- Limited tests / limited input supply → stars or scoring.
- Consistent alien alphabet across the game so players learn to read it (Chants of Sennaar style). Currently rejected: glyphs are random per machine. Possible middle ground: a few glyphs that stay consistent per World.
- Hints driven by the solver (distance-to-goal is already computed).
- Player notebook to annotate glyphs and controls.

## Content & progression
- Worlds of levels grouped by generator difficulty score; hand-curated from the lab.
- Per-World themes: palette, chassis kit, which control/display types are introduced.

## Tech / tools
- Difficulty score tuning from playtest data (time-to-solve vs predicted score).
- Batch generation: generate N machines overnight, keep the top ones per difficulty bucket.
- Machine visual layout generator: extendable chassis pieces that snap together.
