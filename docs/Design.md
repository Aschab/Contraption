# Contraption — Design (prototype scope)

Decisions agreed for the first prototype. Parked ideas live in `Ideas.md`.

## Core loop
The player faces a broken alien machine. A manual lists input→output examples. The player picks inputs from a small pool, feeds them, reads the output with their own (human, readable) analyzer, and manipulates controls. The machine is fixed when it reproduces **every** manual example. Unlimited tests. `Z` undo, `R` reset to the starting state.

## Values
Everything internal is an integer 0–5. Displays may render values continuously (needles, glow), but the logic is discrete.

## Liquids (only substance in the prototype)
Properties, each 0–5: **Color** (hue index), **Viscosity**, **Temperature**, **Flammability**, **Refraction**.
Each property should be readable in the liquid itself through effects at thresholds (e.g. steam/frost for temperature, flow speed for viscosity, flicker/sparks for flammability, shimmer for refraction). The analyzer always shows exact numbers for input and output.
Multiple inputs are mixed per property with a rounded average before processing.

## Machine model
- **State = stored values only**: internal registers (0–5, clamp or wrap) + control positions. Same stored values ⇒ same displays and same outputs, regardless of history.
- **Controls** act on the state. Button (press), Lever (2 positions), Dial (N positions, wraps), Knob (N positions, no wrap), Joystick (neutral/up/down/left/right; pushing a direction or centring is an action). Every action applies **effects**: register deltas, each optionally gated by conditions on other registers/control positions (evaluated on the state before the action). Actions don't have to be reversible — that is the Rubik-cube quality.
- **Displays** show functions of registers: lights (threshold), bars (levels), gauges (continuous), glyph screens. A display can combine registers (e.g. +A −B), be inverted, or be redundant, and a display's meaning is fixed within a machine. **No hidden information**: every register combination produces a distinct set of readings, so the full state is always knowable from the displays (the generator enforces it).
- **Transfer pipeline**: ordered operations on the (mixed) input liquid, each parameterized by one register: add, subtract, cap, raise, purge (zero if register ≥ threshold), amplify, siphon (move amount between properties), swap (if register ≥ threshold), exchange (always swaps two properties; register-independent).
- **Labels/glyphs** are random alien symbols per machine; there is no consistent alphabet. Figuring out what each thing does is the puzzle.
- **Broken** = wrong settings only (for now).

## Generator
Seeded and deterministic. Builds registers → controls/effects → transfer pipeline → input pool + manual (from a hidden solution) → displays → start state. Every candidate is solved exhaustively (BFS over the full state space). It's rejected if unsolvable, too short/long, the state space is too large, or the manual is too permissive. Difficulty is scored from solver metrics: solution length, reachable states, trap states (can't reach the goal), conditional effects.
Saved levels store the full definition (seed + generator version are metadata only).

## Lab scene
Generate (seed + settings) → play/test the machine as the player would → inspect metrics and the hidden solution → tweak → save to `Assets/_Project/Data/Levels`.

## Platform & input
PC (Steam) first, consoles possible. Mouse is optional: every control must be reachable via keyboard/gamepad focus navigation.

## Visuals
- **Grid layout** (saved with the level, editable in the lab): 2–5 rows, a silhouette (steps, towers, shoulders, arches, notches), parts placed anywhere. Each control/display kind has style variants with footprints 1×1, 2×2, 1×2–1×4, 2×1–4×1; a style never changes rules. Free cells get decorative fillers (vents, grilles, pipes, portholes, 2×2 fans/hatches).
- **Chassis** from dual-grid tiles (one per grid corner, 14 masks per kit) so any silhouette gets clean edges; top/base styles (crest, chimney, dome, antenna / feet, plinth, skid) per machine.
- **Liquid flow**: input containers stand on top and drain into the body; the output pours into a container on the ground to the right. Only inputs *or* output are full at a time. Optional per machine: **Show steps** — a rack of vials with the liquid after each pipeline step.
- **Liquid readability (bands)**: colour = hue; viscosity = surface wobble, drain speed, stream width; temperature 0 ice cubes, 1 cold mist, 4 steam, 5 boiling; flammability 3 sparks, 4–5 flames; refraction = see-through distortion and gloss, 4–5 glow light, 5 glints. Exact values stay on the analyzer.
- Tests animate (~1.5 s) and can be skipped with any input or turned off.
