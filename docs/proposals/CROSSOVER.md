# Ink Deep: Crossover — the template-cave roguelite

A proposal for a new version of the game, on the `inkdeep-crossover` branch: *The Binding of Isaac* meets
*Spelunky 2*, set on a reef sinking into darkness. It starts the design again from scratch and builds it out of as
much of the existing code as can be kept, so that a playable build comes as quickly as possible.

Each section says **what changes, how, and why**. §14 is the full keep / adapt / replace / shelve table, and §15 the
build order. Numbers marked *(tune)* are first guesses to settle in play.

---

## 1. The game in one breath

Clementine, a small bioluminescent jellyfish, dives through a reef the darkness is swallowing. She fights it with her
light, swallows pearls that boost and change what her light does, gathers shells, and finds the old treasures that
make every later dive stronger. She goes ever deeper: living reefs, drowned ruins, the leftovers of something not from
this world, and finally Atlantis. She never goes back up.

- **From Isaac:** a boss at the bottom of every depth; pearls that stack into wild builds; shops, treasure pearls,
  deals and shrines; a run of about 40 minutes.
- **From Spelunky 2:** levels stitched from hand-made templates with random chunks; a clock that pushes you on
  (the ghost, as a current); branching choices of biome; treasures found once that change every run after.
- **Kept from Ink Deep:** the jellyfish, light as weapon and life, pearls as her build, darkness (the ink) that drains
  colour, creatures freed rather than killed, bosses beaten through their corrupted pearl, irregular caves, diving
  down.

## 2. A level: template caves

**Replaces** the cave generator (`CaveLayout`: chambers grown and joined by tunnels) with a **Spelunky-style template
grid**, laid out so the result still reads as one irregular cave.

**The grid** *(tune)*: a level is a grid of 5 × 5 cells, each 18 × 18 m (built: 22 m cells did not fit inside the
level's rim), centred in today's 110 m reef.

1. **The path.** A random walk picks a chain of cells from the start cell to the far side, as Spelunky's path does.
   Side cells hang off it, up to two deep, and now and then a loop closes. The cells left over are solid rock, so the
   level's outline is ragged, not square.
2. **Templates.** Every used cell takes a template made for its openings (which of its four sides join a neighbour)
   and its biome. A template is an ASCII grid on the 1 m map: rock, open water, coral, kelp, pots, hazards, **mob
   marks** and **POI marks**. Openings sit at a few fixed spots on each side (sockets), so any two templates that
   share an open side line up.
3. **Chunks.** Each template has chunk slots (5 × 5 m patches) filled at random from the biome's chunk list, so no
   cell repeats exactly.
4. **Erosion.** A noise pass roughens every rock edge and rounds corners (`TerrainNoise`, `WallStrata`), so the seams
   between cells disappear and the level looks like a natural cave, not a set of boxes.

**What templates hold:**

- **Mob marks:** a spot, a tag (lone, pack, thrower, ambusher, guard) and a weight. The level's roster for its biome
  and depth fills the tag with a creature. Mobs are placed once and do not respawn.
- **POI marks:** a spot where a place can stand: shop, treasure pearl (a clam), Crab Deal, light shrine, light
  spring, bubble vault, sealed secret, treasure (§5). The level's plan (`LevelPlan`, `PlaceRules`) chooses which
  marks become places and which stay empty. Templates made around a place (a shop cave, a clam's grotto) count as
  that place.
- **Features:** charge stones, prism stones, geysers, currents, pots: the systems that exist become template marks.

**Every POI can be skipped.** Nothing in a level is needed to leave it (§3).

**Why.** Templates give hand-made quality in every cell and still make the run random. Fights, puzzles and places
can be designed and tuned one by one, and a template that plays badly is fixed or deleted without touching the
generator. The current cave generator makes beautiful caves but its content is placed after the fact, so a good fight
or a good secret can't be authored.

## 3. Going deeper: the dive clock

**Changes** the way down. Today she seals the reef's leak, holes open, and she chooses one. Instead:

- **Ordinary levels:** after **2 minutes** on the level, she may **dive anywhere**: press dive and she sinks through
  the seabed to the next level's start. There is nothing to seal, find or beat first. A level can be passed in two
  minutes of drifting, or explored for as long as she likes.
- **No going back up.** A level she has left is gone.
- **The clock** is shown as a ring round her bell on the HUD, filling over the two minutes, with a soft chime
  when the dive opens.
- **One layer.** A level is a single swimming plane: no back layer, no sub-levels (leak caves and labyrinths go).

**Why.** It is the simplest possible way down, and it keeps every POI a real choice: the only reason to swim into
the shop, the deal or the secret is that she wants what is there. It also caps how short a run can be (2 minutes a
level), so speed runs stay possible but never trivial.

## 4. The boss level and the current (Spelunky's ghost)

The **last level of each depth** holds the **boss room**: an arena cave set in the template grid like any other cell
(a hand-made template the size of 2 × 2 cells).

- **On the boss level the clock does not open a dive.** At 2 minutes the level's water starts to move: a **current
  rises** from the far edges and grows stronger every few seconds, **pushing everything that is not attached to the
  surface toward the boss room**: her, free-swimming creatures, shells, orbs, loose pearls, the reef's fish and
  drift. What is fixed to rock or seabed stays (rooted creatures such as Blightroots and Lashjaws, clams, anemones,
  pots, bubble vaults under their overhangs). She can fight it for a while, but not forever. This is Spelunky's
  ghost: stay too long and the level makes the decision for you.
- **Creatures caught in the current** (free-swimming ones; the standing rule: no other creature inside the arena during the fight)
  dissolve into ink at the arena's mouth, and every one that does **adds to the boss's corruption**. Dawdling makes
  the fight harder, which is the ghost's lesson in the game's own terms.
- **She can enter the boss room at any time** before the current comes. The arena seals behind her.
- **On defeat** the boss's pearl is cleansed and becomes hers (the standing rule), the current stops, and the
  **vortex** opens in the arena's middle: swimming into it carries her down to the next depth. At a branch (§8) two
  vortices open, each coloured with the biome it leads to.

**How.** `PlaneCurrents` already moves her, creatures and loose things along flow fields; the current becomes a field
that points toward the arena's mouth from everywhere on the level (a flow field built from the level's paths,
`PlanePaths`/`FlowField`), whose strength rises with time. The vortex is the existing hole down, restyled.

## 5. Pickups and treasures

### 5.1 Pickups (all there is during a run)

| Pickup | What it is | Where |
|---|---|---|
| **Shells** | money, in worths: cowrie 1, scallop 3, conch 8, nautilus 25 *(tune)*. The rarer the shell, the deeper and better hidden. | Freed creatures, pots, bubble vaults, the level's corners. Spent at shops. |
| **Healing orbs** | bubbles of her light that give back a pip (half or whole) | Freed creatures, bubble vaults, light springs, the shop. |
| **Bubble vaults** | a big air bubble caught under a rock overhang, holding something: shells, orbs, sometimes a pearl. Her light shows what is inside; her beam bursts it. | Bubble-vault marks in templates. |

That is all: no bombs, keys, ropes or second currency. Terrain is solid (nothing breaks but pots, bubbles and seals).

**Why so few.** Each pickup does one clear thing. A smaller set is quicker to build, quicker to read on screen, and
leaves pearls as the one system of choice in a run.

### 5.2 Treasures (unlockables found once, kept forever)

Treasures are the game's long-term rewards, Spelunky's secret items and Isaac's unlocks rolled together. Each is a
real old object lying somewhere in a biome (in a treasure POI: hidden, guarded or behind a light puzzle). Picking one
up **unlocks it for the jelly for good**, and from then on it appears **at the start of every run**, in the start
cave.

| Treasure | Found in | What it does each run |
|---|---|---|
| **Pearl Altar** | Sunlit Reef | At the start, it offers **3 random pearls**; she takes one. |
| **Shell Urn** | Shipwreck Graveyard | She starts with 15 shells *(tune)*. |
| **Spare Bell** | Kelp Forest | She starts with one more light pip. |
| **Tide Chart** | Sunken Ruins | The level's outline shows on the map from the start. |
| **Lantern Idol** | Drowned Temple | Treasure and shop marks show on the map. |
| **Beacon Coil** | Alien Wreckage | Her first boss of the run starts with less corruption. |
| **Atlantean keystone pieces** | one in each of four biomes | Together, they open the secret route (§8). |
| **New jellies** | the deepest biomes | A new species to start a run as (§11). |

- A treasure appears in a level only until it is found; after that its POI rolls an ordinary reward.
- Some treasures could later be **upgraded** by finding a second copy deeper (the Altar offering 4 pearls).
- They are saved on the jelly (`Jelly`, `SaveData`), as achievements and freed folk are now.
- *(Built in 0.54.0 for the first three, GAME.md §14: a found treasure's place holds a pearl from the secret pool;
  the start cave is the start of the run's first template level.)*

**Why.** It gives every run a reason to explore beyond the two-minute dive: the treasures are where the permanent
progress is. The Altar in particular makes every start different, which is Isaac's best quality.

## 6. Clementine

| | Now | Crossover | Why |
|---|---|---|---|
| Move | free swim, pulse-and-glide | **same** (`PlaneWorld`) | It already feels like a jellyfish, and twin-stick movement is what Isaac needs. |
| Main attack | the beam, a held cone of light | **bubble shots** (built in 0.57.0, the shots from before 0.13.0); the **beam** mostly a light to see by (a fifth of its burn on creatures, all of it on a boss's pearl) | Shots give Isaac's aim-and-fire rhythm and let "tear" pearls (speed, range, homing) read clearly; the beam keeps the light's identity as the way she finds things. |
| Aura | always on; shows the corrupted | **cut** (built in 0.57.0): her beam shows what the aura showed, the vaults' contents too | Light reveals, where she points it. |
| Dash | ink dash, briefly untouchable | **same** | It covers Isaac's dodging. |
| Glow off | hides her | **cut** for now | It works against a fight-and-go loop. It could come back as a pearl. |
| Health | a continuous corruption meter (100) | **light pips** (6, in halves *(tune)*), refilled by healing orbs | Discrete hits make Isaac's and Spelunky's damage readable and fair. |
| Active pearl | one, recharges over time | **one, recharges by freeing creatures** | Fighting charges it; skipping doesn't. |

## 7. Pearls: her items

**Keeps almost everything.** The 61 working pearls, their light grammar (shape, path, colour, rhythm, echo, glow),
constellations (= Isaac transformations), lantern forms, actives, `PearlKey` looks and the swallowing ritual. This is
the largest finished system in the repo and it already works like Isaac's items.

**Changes:**

- **Pools by place:** treasure pearl (clam), shop, secret, deal, shrine, boss and the Altar's pool. *(Built for the
  clam, shop, bubble vault and shrine in 0.52.0. The light shrine, as built: a fine pearl, quality 3 and up, given for
  two pips of her current light, never her last; GAME.md §14.)* `ItemPools`
  already has pools; they get re-tagged.
- **Boost or alter:** every pearl does one or both. *Boosts* are the plain stat pearls (`PlaneStatSheet`). *Alters*
  change how the beam behaves. Each pearl's card says which.
- **Quality 0–4** on every pearl (Isaac's quality), for pools, deals and the Altar.
- **Biome pearls:** a few per biome in its own language, e.g. Ruins *Mosaic Lens* (the beam splits on stone); Alien
  *Lattice Shard* (the beam chains between conduits); Atlantis *Orichalcum Core* (her bolts become short beams).

## 8. Depths, biomes and the route

Five depths of three levels (two ordinary, one boss), then the Crack, a boss level of its own: 16 levels and
40–45 minutes *(tune)*. As in
Spelunky 2, two depths **branch**: the boss's arena opens two vortices and she picks which biome comes next.

| Depth | Biome | Its rock and look | Its hazards and mechanic | Its boss |
|---|---|---|---|---|
| 1 | **Sunlit Reef** | coral, sand, sea grass; the brightest | currents, stinging anemones, clams that snap shut | Queen Clam |
| 2a | **Kelp Forest** | kelp walls her beam burns through for a moment before they close | tangles that slow her; urchin beds | Grandmother Tull (green sea turtle) |
| 2b | **Shipwreck Graveyard** | wood, iron, cargo | falling debris; rusted cannons that fire when a creature passes | Kip (cleaner wrasse) |
| 3 | **Sunken Ruins** | Greco-Roman marble, columns, mosaics | dart traps; amphorae; light-locked doors | Ottilie (common octopus) |
| 4a | **Alien Wreckage** | dark glass, black alloy, cold conduits | **conduits that carry her beam** from one to the next; field pulses that pull her | Bubbles (mandarin dragonet) |
| 4b | **Drowned Temple** (an ancient civilization's flooded cenote temple) | carved stone, terraces, jade | rolling boulders; pressure plates; glyph doors lit in order by her beam (prism stones) | Fennel (leafy seadragon) |
| 5 | **Atlantis** | orichalcum, gold-veined marble, dead light-machines | machines she relights to open the city (charge stones) | a giant animal fused with a light-machine, its pearl in the machine's heart |
| End | **The Crack** | the source of the ink | the last fight | Drippus Prime |

**The secret route** (Spelunky's Sunken City path): the four Atlantean keystone pieces (§5.2), once all found, open
Atlantis's sealed heart and the true ending.

**Every biome adds** about 40 cell templates and a chunk list for the prototype *(tune)*, a roster of 3–5
creatures, its boss, 4–6 pearls and its treasures. Depths 1–3 make the first playable build; 2b, 4a, 4b and 5 follow.

**Why branches.** Choosing the biome is the most Spelunky thing about a run, and gives every replay a different shape.

## 9. The darkness: creatures

**Keeps** the whole corruption language: ink-black bodies that drink light, seen only where her light falls, freed
rather than killed, leaving a core of light (`PlaneCorruption`, `PlaneCores`, `BlightView`).

**Changes:**

- **Rosters by biome** instead of "murklings only". The war's creatures, written and built but switched off
  (Blotters, Thumpsquirts, Inkwardens, Lashjaws, Blightroots, the octopus), come back, each with a home biome.
  Murklings stay as the common creature everywhere.
- **Placed by templates.** Mob marks put each creature where its fight was designed: ambushers in side pockets,
  throwers on ledges across water, guards at treasure.
- **New creatures per biome**, always real animals: moray eels in the ruins, mantis shrimp and nudibranchs in the
  temple, siphonophores and sea pens round the alien wreck, giant isopods deep down.
- **Leaks are gone.** The ink no longer spreads through a level from a leak, and no murklings bud. Creatures are
  only what the templates placed, so a level cannot be farmed.

**Why.** The creature code exists and is tested, and was switched off only because the reef had no place for it.
Templates give each one a designed place to fight.

## 10. Bosses

**Keeps** every standing rule: each boss holds a **corrupted pearl**, its only weak spot; lit, it is cleansed, washes
the arena and becomes the reward, left where it was held (`PlaneBoss.PearlAt`, `Placed`). No cut-in, no faces, no
other creature in the arena. Boss projectiles are ink and her light cannot clear them.

**Changes:**

- **One boss a depth**, in the boss level's arena (§4), with the current as its clock.
- **The folk become the biome bosses** (§8 table). Queen Clam takes the Sunlit Reef, Drippus Prime the Crack.
- **The boss generator** (`BossGenerator`, `BossValidator`) rolls an alternative boss for each depth from the second
  dive on, so a biome isn't always the same fight.

## 11. Between runs

**Cut or shelve** (they belong to a long saved dive, not a 40-minute run): havens, requests, chains, the mirror,
**experience and skill points**, the dive graph, the reef director, leaks and leak caves, labyrinths. The healthy
reef fish stay as decoration in the start cave and shops.

**Keep and adapt:**

- **Jellies** (three save slots) and their looks (`JellyLook`, `LookForm`).
- **A run is saved at each level's start** (Isaac's quit and continue). `Snapshot` and `SaveCodec` stay; a run's
  save shrinks to its seed, level, pearls, pips and shells.
- **Long-term progress** is the treasures (§5.2), achievements that unlock pearls into the pools (built), and new
  jelly species found as treasures (a moon jelly, a comb jelly whose cilia glow in colours), each starting with other
  stats.

**Why.** Levelling with skill points and banked items push against both parent games, where a run starts bare and its
power comes from what you find. Treasures give the long-term goal instead.

## 12. The look: modern ink, retrowave accents

The game's two forces keep their roles; the crossover gives each a sharper, more graphic style.

**The darkness and the world: modern ink.**

- Bold, brush-drawn ink outlines on rock, ruins and creatures, of varying weight; flat washes of grey and ash for the
  world; paper grain over everything.
- The ink itself is black, glossy and light-drinking, as now: wet highlights, drips, blooms that spread like ink in
  water.
- Shadows are flat ink fills with a halftone dot at their soft edge, not gradients.
- Unlit water is near-black blue; the world gets its colour back only where it is cleansed.

**Her light and the cleansed world: retrowave accents.**

- Her light runs as a **sunset gradient**: white at the core, through gold, to hot coral and a magenta rim. She is
  still the brightest thing on screen.
- What she cleanses comes back in **neon-edged colour**: coral rims glow, ruins get thin cyan and magenta outlines,
  as if traced in light.
- The deep biomes carry the retrowave signatures in their *places*, never on the creatures: the Alien Wreckage's
  conduits and the Atlantis machines show perspective grid lines and a cold cyan glow; Atlantis's sky shafts are a
  banded sunset seen through water.
- **The interface:** ink brush frames with a neon edge line; a clean geometric display face for numbers; a faint
  scanline and chromatic fringe only on the UI, never on play.

**The rules this keeps and changes.**

- *Kept:* creatures look real; ink belongs to the corruption and drains colour; she outshines everything but a
  cleansing burst; no faces, no cut-ins.
- *Changed:* CLAUDE.md no longer limits accent colours: any colour is allowed. The ink look uses **magenta** (the
  warm end of her light, and the edges of what she cleansed) and **cyan** (cold, old light: alien and Atlantean
  machines); the corruption's own bodies and projectiles stay ink.

**How.** The shaders in `assets/shaders` already include a watercolour study (`ArtLook.Watercolour`) and the painted
Blue Hollow look. The ink look adds an outline pass (depth and normal edges), a posterised light ramp with a
halftone edge, and the paper grain. Her light's gradient is `HerColor`/`LightColour`. With the straight-down
orthographic camera these are screen-space passes, and they work the same on every biome.

**Why.** Ink is the darkness's own material, so drawing the world in ink makes the theme the style. The retrowave
accents give the light side a modern, sellable identity, and a clear split: neon is always light, never threat.

## 13. View and camera

**Changes.** The tilted perspective camera (60°, 35° FOV) becomes a **straight-down orthographic follow camera**. It
keeps the smooth follow and still draws in a little in a fight. Levels are continuous caves, so there is no room
framing. The boss arena frames the whole arena while the fight lasts.

**How.** `CameraRig` already has a straight-down mode. Set it orthographic, keep the rest. The 3D renderer stays for
now: the game is already 2D in play (the sim is a flat plane), and the beam's real-light shadows (`BeamShadow`,
`BeamView`) are its most expensive picture and already work.

## 14. What happens to the code

| Area | Verdict | What changes |
|---|---|---|
| `PlaneWorld`, movement, dash | **Keep** | Unchanged. |
| `PlaneHerLight`, `PlaneLight`, the beam's pearls (`PlaneSurge`, `PlaneRhythm`, `PlaneEcho`, `PlaneGreatLights`, `PlaneFireflies`, `PlanePrism`, `PlaneColour`) | **Keep** | Add the tap bolt from `PlaneCombat`'s shots. |
| `PlaneCorruption` | **Adapt** | Meter → light pips. |
| `PlaneCombat`, `PlaneKnock`, `PlaneCores`, `PlanePushWave` | **Keep** | Retune. |
| Creatures (`PlaneBlight`, `PlaneBlotters`, `PlaneThumpsquirts`, `PlaneInkwardens`, `PlaneLashjaws`, `PlaneMurk*`, `PlanePufferling`, `PlaneFlounder`) | **Switch on, retune** | Rosters by biome, placed by mob marks. |
| `PlaneBoss`, `Bosses/*` | **Keep** | New biome assignment; the dissolved creatures add to its corruption. |
| `Items/*` | **Keep** | Pools by place, quality, biome pearls, the Altar's pool. |
| `PlaneEconomy`, `Loot/LootModel` | **Adapt** | Shell worths, healing orbs (the existing light orbs), bubble vaults. Bombs, chests and buried coins go. |
| `PlaneVases` | **Adapt** | Bubble vaults are a variant of pots: they burst and drop their content. |
| `PlaneCrab`, `PlaneChargeStones`, `PlanePrismStones`, `PlaneSeals`, `PlaneSprings`, `PlaneGeysers` | **Keep as template marks** | Deals, puzzle stones, sealed secrets, springs, vents. |
| `PlaneCurrents` | **Adapt** | The boss level's rising current toward the arena. |
| `PlaneAdventure` (leaks, ink spread), `PlaneDens`, `PlaneCurse`, labyrinths, leak caves | **Shelve** | Leaks no longer gate the dive. |
| `Gen/TopDown/CaveLayout`, `WovenReef`, `ReefTerrain`, `Mountains`, `AbyssReef` | **Replace** | `TemplateLayout`: the cell walk, templates, chunks. |
| `TerrainNoise`, `WallStrata` | **Keep** | The erosion pass over the stitched cells. |
| `LevelMap` | **Adapt** | Stays the 1 m grid; gains mob and POI marks from templates. |
| `LevelPlan`, `PlaceRules`, `LevelTemplates`, `LevelValidator` | **Adapt** | Choose which POI marks become places; validate that every cell is reachable and that sockets line up. |
| `HandLayout`, `Maze` | **Keep** | HandLayout reads ASCII templates; Maze helps the cell walk. |
| `Run/DiveGraph` | **Replace** | A depth list with two branch points. |
| `Run/RunStreams`, `Rng`, `SeedCode`, `Saves/*` | **Keep** | New save version; treasures on the jelly. |
| `AchievementCatalog` | **Keep** | Plus a `TreasureCatalog` built the same way. |
| `PlaneRequests`, `PlaneChains`, `PlaneFolk` (haven side), `PlaneExperience`, skill points, `ReefDirector`, `PlaneReefFish`'s ecology | **Shelve** | Behind a flag, not deleted. |
| `CameraRig` | **Adapt** | Orthographic, straight down. |
| `LevelView`, `WovenReefView` | **Adapt** | Draw the template caves; the ink look's passes. |
| `BeamView`, `BeamShadow`, `BellView`, `BlightView.*`, `BossView`, `Bosses/*Rig`, `Fx/*` | **Keep** | Restyled by the ink look. |
| `HudView`, `MinimapView`, `PauseMenu` | **Adapt** | Pips, shells, the dive clock ring; the map of the cave. |
| `BossIntroArt`, `PortalView`, `FolkTalk`, `FanMenu`, `MirrorMenu`, `RequestRibbons`, `ChartView`, `HoleEmblems` | **Remove** | Cut-ins break the standing rule; the rest belong to cut systems. |
| `TopDownMain` (4 415 lines) | **Split** | Into `RunMain` (levels, the clock, the dive, the current) and the level loader; most of the rest is code for cut systems. |
| `Title/*` | **Keep** | The jelly card gains species. |

Most of the code stays. The new work is the template generator and its content, the dive clock and the boss level's
current, the pickups and treasures, the ink look and the HUD.

## 15. Build order (fastest path to a playable build)

Each step ends with a build that runs and passing tests.

1. **Strip** (begun in 0.49.0: GAME.md §14): shelve §11's systems and the leaks behind a flag; remove the views in §14's "Remove" row; set the
   camera to orthographic. *(about 1 week)*
2. **Template caves** (begun in 0.50.0: GAME.md §14; 10 cells and a room so far, each used in its eight ways):
   the cell walk, sockets, an ASCII template reader, chunks, the erosion pass; 30 Sunlit Reef templates with mob and
   POI marks; the dive clock. *First playable: endless Sunlit Reef levels.* *(3 weeks)*
3. **Pickups, pips and places** (built in 0.51.0–0.52.0 but for the active's charge by freeing; GAME.md §14): shell worths, healing orbs, bubble vaults, light pips; shop, clam, deal, shrine,
   secret as POI marks; pools by place; the HUD. *(2 weeks)*
4. **The boss level** (built in 0.53.0: GAME.md §14; the current not drawn yet): the arena template, the rising current, dissolving creatures, the vortex; Queen Clam moved in.
   *A full first depth.* *(2 weeks)*
5. **Treasures** (built in 0.54.0: GAME.md §14; depths 2 and 3 stand in for their biomes): `TreasureCatalog`, the start cave, the Pearl Altar, the first three treasures. *(1 week)*
6. **The ink look** (built in 0.55.0: GAME.md §14; her glow's sunset, not her bell's colours; no interface frames yet): outline, halftone, paper grain, her sunset gradient, the neon edges of cleansed things.
   *(2 weeks)*
7. **Depths 2–3** (begun in 0.56.0: GAME.md §14; the route, the first branch, the bosses and rosters by biome; the
   biomes' cells and looks in 0.59.0; their rock, dressing and hazards still to come): Kelp Forest or Shipwreck, then Sunken Ruins: templates, rosters, Tull,
   Kip and Ottilie moved in, the first branch. *A short full run.* *(4–5 weeks)*
8. **Depths 4–5 and the end:** Alien Wreckage, Drowned Temple, Atlantis, the Crack, the keystone pieces, new jellies.
   *(5–6 weeks)*

Steps 1–4 give a game you can play and show in about two months.

## 16. Open questions

1. **Rendering:** keep the 3D renderer with an orthographic camera (recommended for speed), or move to Godot's 2D
   nodes and drawn sprites later?
2. **Health:** light pips (recommended) or the continuous meter?
3. **The story:** keep the Leak, the Dragon Palace and the aquarium ending (THEME-BIBLE §4), or let Atlantis and the
   alien leftovers carry a new one?
4. **Ordinary levels:** should anything move her on if she stays long after the dive opens? As written, nothing does
   (mobs don't respawn, so nothing can be farmed).
5. **CLAUDE.md's art rules:** they need the two accents of §12 written in before the ink look is built.
