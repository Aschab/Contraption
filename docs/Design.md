# Contraption — Design (prototype scope)

Decisions agreed for the first prototype. Parked ideas live in `Ideas.md`.

## Core loop
The player faces a broken alien machine. A manual lists input→output examples. The player picks inputs from a small pool, feeds them, reads the output with their own (human, readable) analyzer, and manipulates controls. The machine is fixed when it reproduces **every** manual example. Unlimited tests. `Z` undo, `R` reset to the starting state.

## Values
Everything internal is an integer 0–4. Players read values as a 2×2 grid of filled boxes (0 = empty … 4 = full); traits have alien symbols. Displays may render values continuously (needles, glow), but the logic is discrete.

## Liquids (only substance in the prototype)
Properties, each 0–4: **Color** (hue index), **Viscosity**, **Temperature**, **Flammability**, **Refraction**.
Each property should be readable in the liquid itself through effects at thresholds (e.g. steam/frost for temperature, flow speed for viscosity, flicker/sparks for flammability, shimmer for refraction). The analyzer always shows exact numbers for input and output.
Multiple inputs are mixed per property with a rounded average before processing.

## Machine model
- **State = stored values only**: internal registers (0–4, clamp or wrap) + control positions. Same stored values ⇒ same displays and same outputs, regardless of history.
- **Controls** act on the state. Button (press), Lever (2 positions), Dial (N positions, wraps), Knob (N positions, no wrap), Joystick (neutral/up/down/left/right; pushing a direction or centring is an action). Every action applies **effects**: register deltas, each optionally gated by conditions on other registers/control positions (evaluated on the state before the action). Actions don't have to be reversible — that is the Rubik-cube quality.
- **Displays** show functions of registers: lights (threshold), bars (levels), gauges (continuous), glyph screens (one distinct symbol per value, never wrapping; one alphabet per machine so a symbol means the same value on every screen; styles: dot matrix, line-drawn rune, 3-letter alien word). A display can combine registers (e.g. +A −B), be inverted, or be redundant, and a display's meaning is fixed within a machine. **No hidden information**: every register combination produces a distinct set of readings, so the full state is always knowable from the displays (the generator enforces it).
- **Transfer pipeline**: ordered operations on the (mixed) input liquid, each parameterized by one register: add, subtract, cap, raise, purge (zero if register ≥ threshold), amplify, siphon (move amount between properties), swap (if register ≥ threshold), exchange (always swaps two properties; register-independent).
- **Labels/glyphs** are random alien symbols per machine; there is no consistent alphabet. Figuring out what each thing does is the puzzle — except what the level's hints reveal (below).
- **Broken** = wrong settings only (for now).

## Hints (learning curve)
Saved per level (`MachineDefinition.Hints`, edited in the lab); early worlds show a lot, late worlds little or nothing.
- **Home display**: each pipeline step lives at the display that shows its register most plainly (fewest terms, not a light, not inverted). Exchange has no register; it lives on the output pipe.
- **Label plates** (per step: none / trait / full): written into the home display's label slot (every display model has a recessed slot along its bottom; the instrument sits above it) as a symbol sentence `[trait] [step] [trait] [threshold]`. Trait level names only the traits; full adds the step mark and the purge/swap threshold (2×2 grid). Siphon reads left → right (source into target). Modifiers: *pair* (the display mixes two registers) and *inverted* (it reads backwards). Symbols: `Resources/Labels` (the books use the same trait symbols).
- **Rear view** (optional, F / R3 turns the machine): the back of every display in a frame, a socket on each one that hosts steps, the start badge (empty ring, by the inputs) and the end badge (full disc, by the output). Cables run start → steps in order → end, each one optional; each test sends one glowing bead from start to end along the cables in order (hidden links take the same time, unlit). Labels appear on the rear nodes too. Controls can't be used while the machine is turned; tests can.

## Generator
Seeded and deterministic. Builds registers → controls/effects → transfer pipeline → input pool + manual (from a hidden solution) → displays → start state. Every candidate is solved exhaustively (BFS over the full state space). It's rejected if unsolvable, too short/long, the state space is too large, or the manual is too permissive. Difficulty is scored from solver metrics: solution length, reachable states, trap states (can't reach the goal), conditional effects.
Saved levels store the full definition (seed + generator version are metadata only).

## Lab scene
Generate (seed + settings) → play/test the machine as the player would → inspect metrics and the hidden solution → tweak → save to `Assets/_Project/Data/Levels`.

## Workshop & books
- The machine stands on a work table in an alien workshop (Share/Workshop, tWorkshop). Each level opens with a camera flight through the workshop that lands on the table; then the background blurs (depth of field).
- **Manual** (book, front-left of the table): four examples per page, vertically centred — input row(s) then the expected output row; a wax seal once that example is proven.
- **Logbook** (book, front-right): every test as IN rows (input liquid icons) + OUT row, four tests per page filled from the top; entries can be deleted. OUT values that differ from the manual's answer for the same inputs are orange.
- Values are written as 2×2 grids (0 empty … 4 full) under five alien trait symbols (fixed for the whole game); each input liquid has a cog icon in its colour carrying its sample symbol.
- **Sample tubes** (racks, left of the machine) are the input pool: each tag has an unmistakable symbol (triangle, ring, square, X, +, diamond, bars, dots) used in the books too. Loaded tubes stand raised; clicking a tube loads it.
- One book at a time can be lifted into view (manual on the left, logbook on the right); the camera leans so the machine stays visible beside it. The table and everything on it stay sharp; only the room behind blurs.

## Platform & input
PC (Steam) first, consoles possible. Mouse is optional: every control must be reachable via keyboard/gamepad focus navigation.

## Visuals
- **Grid layout** (saved with the level, editable in the lab): 2–5 rows, a silhouette (steps, towers, shoulders, arches, notches), parts placed anywhere. Each control/display kind has style variants with footprints 1×1, 2×2, 1×2–1×4, 2×1–4×1; a style never changes rules. Free cells get decorative fillers (vents, grilles, pipes, portholes, 2×2 fans/hatches).
- **Relief** (saved, editable per part): each part/filler is flush, raised on a housing in front of the body, or framed in a deep bezel; most machines also get ledges (cornices) along row boundaries.
- **Chassis** from dual-grid tiles (one per grid corner, 14 masks per kit) so any silhouette gets clean edges; top/base styles (crest, chimney, dome, antenna / feet, plinth, skid) per machine.
- **Liquid flow**: input containers stand on top and drain into the body; the output pours into a container on the ground to the right. Only inputs *or* output are full at a time. Optional per machine: **Show steps** — a rack of vials with the liquid after each pipeline step.
- **Liquid readability (bands)**: colour = hue; viscosity = surface wobble, drain speed, stream width; temperature 0 ice cubes, 1 cold mist, 3 steam, 4 boiling; flammability 2 sparks, 3 flames, 4 big flames; refraction = see-through distortion and gloss, 3 glow light, 4 glints. Exact values stay on the analyzer.
- **Colour schemes** per machine (saved): frame, trim, panel + two alt panel tints, metal, 1–2 glow colours; parts and fillers pick a tint and a glow. Style target: Share/t1–t4 (chamfered cream panels with rivets, diamond studs, octagonal gems, warm workshop, bloom).
- Tests animate (~1.5 s) and can be skipped with any input or turned off.
