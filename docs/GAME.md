# Ink Deep — the game

The one current design of the whole game, as it is built, and the decided plan for what is not built yet. Start here.
Every other document goes into one part of it in detail. Where two documents disagree, the order of trust is: **this
page**, then the system documents (§0), then DESIGN-TOPDOWN.md, then anything in `archive/`.

> **The `inkdeep-crossover` branch** is rebuilding the game as the crossover of
> [proposals/CROSSOVER.md](proposals/CROSSOVER.md). What is built of it so far is in §14; the rest of this page still
> describes the dive the crossover starts from.

## 0. The documents

| Document | What it holds |
|---|---|
| **GAME.md** (this page) | The whole game in brief, and the decisions that bind the rest. |
| [THEME-BIBLE.md](THEME-BIBLE.md) | The guideline: theme, pillars, lore, look, sound, voice, and the theme tests every idea must pass. |
| [LIGHT-AND-GRAPH.md](LIGHT-AND-GRAPH.md) | Light as life: her aura and beam, what light does, charge and prism stones, murklings hunting light; the dive as a graph of reefs; places and sub-levels. |
| [DIVE.md](DIVE.md) | Jellies (save slots), what she keeps and loses, the camera, havens, folk as depth bosses, leak caves, labyrinths, requests and chains. |
| [ADVENTURE.md](ADVENTURE.md) | Leaks, the ink, fish that turn, corrupted fish, the abyss reef, her oral arm and the HUD's gauge, Crab Deals. |
| [CORRUPTION.md](CORRUPTION.md) | The corruption war: every corrupted creature, meters and corruption rings, Drippus Prime, Queen Clam, light springs, her corruption meter. |
| [FOLK.md](FOLK.md) | The five neighbours: who they are, their boss fights, their homecoming, what each opens in the haven, their chains. |
| [PEARLS.md](PEARLS.md) | Every pearl, synergy and transformation, how a pearl's look is derived, the crab's pearls. |
| [BOSS-GENERATOR.md](BOSS-GENERATOR.md) | The rules every boss keeps, and the generator that rolls bosses. |
| [TITLE-MENU.md](TITLE-MENU.md) | The title screen, its cards, statistics, the Sea-pedia, saves and settings. |
| [VISUAL-SHOWCASE.md](VISUAL-SHOWCASE.md), [ART-LOOKS.md](ART-LOOKS.md) | The painted look (the Blue Hollow), her beam's light, and the earlier looks. |
| [DESIGN-TOPDOWN.md](DESIGN-TOPDOWN.md) | The detailed rules and numbers of the plane: movement, camera, the level generator, lighting, UI. Parts are out of date (§13). |
| `proposals/` | Ideas not built: the deeper fish, the reef redo, the beam pearls plan, the project review. |
| `archive/` | Superseded designs and the reasoning behind built features (the 2D and 3D designs, the open-reef RPG, the old proposals). Read for history, never for rules. |

## 1. The game in one breath

A top-down action roguelite in a darkening ocean. Clementine, a small bioluminescent jellyfish, the last of her bloom,
dives after her family through a reef poisoned by the Leak. Her light is everything: it is how she sees, how she frees
the corrupted, how she seals the leaks, and it is her life. Every pearl she swallows becomes part of her light; every
creature she defeats is freed, not killed; every depth ends with a neighbour to bring home.

The pillars, the tests and the voice are in the Theme Bible. The game in numbers: a dive is a tutorial and five depths
of five rows of reefs; a reef takes a few minutes; a dive is saved at every reef.

## 2. The story

Told in full in THEME-BIBLE §4. In brief: the reef's oldest tale is of the jellyfish who failed the Dragon King's errand
and lost her bones; jellyfish have drifted ever since. When the Crack opened and warm light rose out of it, the reef
believed the Dragon Palace was lit again, and its folk swam to it and came back corrupted. Clementine's bloom went in one
current. She dives to finish the errand. The truth at the bottom is a human home aquarium joined to the sea by a pipe:
the reef was never being called home. It was being kept.

## 3. Clementine

- **Her body** (THEME-BIBLE §5): a clear glass bell with four glowing gonads and a glowing rim, four oral arms and eight
  hair-thin tentacles. She swims in pulses, lurch then glide, and drifts and bobs when left alone.
- **Moves** (DESIGN-TOPDOWN §2.2): free swimming on her plane; the **ink dash** (a burst, briefly untouchable, leaving a
  slowing ink cloud), and the **fast swim** while the dash is held.
- **Her light** (LIGHT-AND-GRAPH §2):
  - her **aura**, always on round her: it shows the corrupted near her and seals a leak she stays close to. **Not in
    the crossover** (since 0.57.0, `PlaneOptions.Glow`): no glow round her, her bell casts no light; her beam is her light;
  - her **beam**, a flashlight cone along her aim while the beam is held: it burns the corrupted, dazzles them, seals
    leaks, opens seals, clears ink and ash, charges stones and opens bosses, by how much of its light reaches. It is a
    real light: rock, the seabed and every creature's body stop it and cast shadows (VISUAL-SHOWCASE). In the
    crossover it is mostly a light to see by: it shows what the glow showed (the creatures in the dark, what a bubble
    vault holds), dazzles what it falls on and burns creatures at a fifth of its strength (`PlaneOptions.BeamBurn`,
    `HerLightTuning.LightOnlyBurn`), but burns a boss's open pearl in full;
  - her **bubble shots** (the right mouse button, the right bumper; back since 0.57.0, as before 0.13.0): thrown
    bubbles of light that ease out, rest and merge, each a small lantern burning the corrupted it lights; her attack;
  - her **glow can be put out**: dark, she is hidden from the Leak's creatures and sees almost nothing (not in the
    crossover, which has no glow: its toggle has no default button).
- **Her life is her light.** What hurts her corrupts her; her corruption meter is the minimap's bezel and the gauge on
  the HUD. Murklings drink her light. It comes back from cores (the light freed creatures leave), light orbs, light
  springs, healing pearls, havens and a sealed leak cave. Full of corruption, she dims and the dive ends (DIVE §2).
- **Her active pearl** is used with its own button and recharges; she carries one at a time.
- **Controls:** move, aim, beam, shoot, glow (unbound by default), dash, dive, active pearl, review pearls, pause. Every one can be rebound, on the
  keyboard and mouse and on a controller (`InputSetup`). Twin-stick aim on a controller.

## 4. The dive

- **A jelly is a save slot** (three on the title). Choosing one continues her dive or starts a new one (DIVE §1).
- **The tutorial**, Depth 0, the Sunlit Shallows: two set reefs holding every kind of place, nothing explained in words.
  Passed once, dives start in Depth 1.
- **The depths:**

  | Depth | Reef | Its boss | State |
  |---|---|---|---|
  | 0 | Sunlit Shallows | none (the open rift) | built (the tutorial) |
  | 1 | Kelp Jungle | Grandmother Tull | built |
  | 2 | Sunken Galleon | Kip | built |
  | 3 | Coral Carnival | Fennel | built |
  | 4 | Twilight Trench | Ottilie | built |
  | 5 | The Abyss | Bubbles | built; its own creatures and the keystone's boss planned (§12) |
  | 6 | The Tank | The Hand | **planned**: the finale and the ending (§12) |

  Once a neighbour is freed, her depth's boss from then on is the Leak's own, Drippus Prime. Until the Tank is built,
  the dive loops after Depth 5 into the next cycle, Menace still rising; after the ending, the loop stays as the endless
  mode.
- **A depth is a graph of reefs** (LIGHT-AND-GRAPH §6): four rows of two to three reefs, then the boss's reef. Each reef
  has two to three holes down, each ringed in the colour of the reef it leads to (a plain reef, a clam room, a curse
  den, the boss). The pause menu's chart shows what she has seen of the graph.
- **Havens** (DIVE §4): small safe sub-levels behind a third of the reefs' mouths, always bright, each with its shop
  and a pick of the services she has unlocked. Entering one heals her whole and banks everything she carries.
- **What she keeps** (DIVE §2): her level, skills and freed folk always; what is banked (pearls and shells) between
  dives; what she carries since the last haven is lost if she dims.

## 5. A reef

- **Its leak** (ADVENTURE §1): a brittle star of ink over the shut hole, seen only in light. Its ink spreads as dots
  through the water, and murklings bud out of it (ADVENTURE §4). Sealing it with her light opens every hole of the reef, sends
  a wave of light out that dissolves the ink and briefly shows the corrupted it touches, and pays her experience.
- **Its places** (LIGHT-AND-GRAPH §7): secrets (sealed walls with labyrinths behind), shops, shell caches, Crab Deals,
  clam rooms, leak caves (sub-levels with their own leak, harmful until sealed) and the holes. A template gives each reef
  its subject and cast (DESIGN-TOPDOWN §4.8).
- **Its light**: light springs where she heals, charge stones her beam lights (lures, lamps, and a trio that hides a
  cache), prism stones she pushes into line to colour her light for coloured seals.
- **Its life**: healthy reef fish (clownfish in anemones, blue tangs, schools of chromis, pufferfish, flounders) going
  about their day; the ink never turns them; currents and geysers; the reef director that keeps the reef acting when she is not
  there.
- **Where she is shown**: the minimap (her surroundings, the unseen filled as rock), the Tab map (the whole level under
  fog), the chart (the dive).

## 6. The corrupted

The Leak's creatures give off no light: they are seen only where light falls on them. Each carries a corruption meter;
emptied, it is freed, leaving a core of light.

- **Murklings only** (ADVENTURE §4): on every reef, at every depth, the only creatures are murklings budded out of the
  leaks' ink: basic ones that cling and drink her light, throwers, shoals (a whole school at once, ringing her) and
  primes (which break into murklings). The deeper (the Menace), the bigger and the more of them. Their size is their
  corruption; they wear no ring.
- **Kept, switched off** (CORRUPTION; ADVENTURE §3–4): the war's creatures (Blightroots, Blotters, Thumpsquirts,
  Inkwardens, Lashjaws, their dens) and the fish that turn (spiked, tadpole and shoal murklings, Hearthguards).
- **Planned** (proposals/DEEP-FISH-PROPOSAL.md): two fish of their own for each depth.

## 7. Bosses

- **Every boss holds a corrupted pearl**, its only weak spot; beating it means finding the pearl and lighting it, often
  only at certain moments. On defeat the pearl is cleansed, the arena washed clean, and the pearl becomes hers
  (BOSS-GENERATOR §0, CLAUDE.md).
- **The folk** (FOLK.md): the five neighbours as the Leak made them, one a depth, each fight built round finding her
  pearl (Tull's sigh, Kip's flank, Fennel hiding in the kelp, Ottilie shielding hers, Bubbles's ink sacs). Freed, each
  goes home to the haven and opens something there.
- **Drippus Prime** (CORRUPTION): the Leak's own boss, a murkling prime grown huge; every birth costs it some of its body.
- **The boss generator** (BOSS-GENERATOR): rolls bosses within hard fairness budgets, with Queen Clam as its reference.
- Each boss is met in a short cut-in (`BossIntro`), real animals, no cartoon faces.

## 8. Pearls and builds

- **Pearls are swallowed, for good**, in a short ritual, and circle her bell (PEARLS). 72 are designed and 41 work on the
  plane; 13 are actives (she carries one). A pearl looks like what it does: its colour from its strongest effect, an
  icon engraved for its kind (`PearlKey`).
- **Found** in clam rooms, shops, labyrinths, boss pearls, Crab Deals (paid in health), request rewards and the folk's
  chains. **Achievements** unlock pearls into the pools (DESIGN-TOPDOWN §8).
- **On her beam**: fire rate is its tick rate, shot speed its carry, knockback its light pressure; pearls split it into
  rays, a helix, a floodlight (proposals/BEAM-PEARLS-PLAN.md has the rest of the carry-over).
- **Her light's colours** (LIGHT-AND-GRAPH §10): Fire Coral, Glowing Moss and Frost Kelp tint her light ember, bloom and
  frost; a split beam deals them across its rays, and where they meet on a creature they mix as light does (sunfire,
  shatter, lull, daybreak), each mix doing more. Coloured light opens coloured seals and colours charge stones.
- **Her light's paths on rock** (LIGHT-AND-GRAPH §11): Mirror Scale bounces it, Mitosis forks it, Boomerang Shrimp
  sends it back along its way, Ghost Jelly takes it through thin walls; the rays born take each other's words where
  they land (Cathedral, Hall of Mirrors), always with less light than arrived.
- **Her light's rhythms** (LIGHT-AND-GRAPH §12): held steady her beam focuses (Pearl Diver), held long it spins up
  (Sardine Run), held on a creature it snaps (Pistol Shrimp Claw), and Glow Burst makes it beat; Pearl Necklace, Depth
  Charge and String of Pearls name where they meet.
- **Her light's paths in the open** (LIGHT-AND-GRAPH §13): it leans toward creatures (Anglerfish Lure), flares
  (Starfish Arm), leaves pools of light that burn on, painting the reef (Bubble Coral), and arcs (Electric Eel Tail);
  Will-o'-Wisp, Pinball Storm and Aurora name where they meet.
- **Her light's echoes** (LIGHT-AND-GRAPH §14): what her light frees flares (Starburst Gland), shines out spines that
  take her paths (Fire Urchin Spine) or sends its core flying home (Lamprey Mouth), in her colours, one generation
  deep; Supernova names where they meet.
- **Her glow's second voice** (LIGHT-AND-GRAPH §15): fireflies of her light orbit her (Firefly Squid, Bubble Gland,
  Anemone Pump), revealing and burning a little in her colours, lures for the murklings; her glow takes her colours.
- **Her great lights** (LIGHT-AND-GRAPH §16): Sunbeam's Lance, Nautilus Spiral's turning beam and Lighthouse where
  they meet; the lantern forms (three pearls of a tag) re-theme her light: Kraken Form's crown, Neon Rave's seven
  colours in turn, Coral Reef's mending afterlight, Shark Mode's hot beam and Frenzy, Pirate's gilded light; and the
  crab's Black Sun, whose shadows burn.
- **The catalogue** (docs/PEARLS.md, generated from it): 61 pearls in play, each a word of her light's grammar
  (shape, path, colour, rhythm, echo, glow), a plain stat, an active or the crab's; 14 constellations and 5 lantern
  forms, each announcing itself. The pause menu's Stats tab reads her light back in those words above her numbers
  (beam reach and width, tick rate, carry); the Sea-pedia lists the pearls by kind.

## 9. Growth and the jelly

- **Experience** (DESIGN-TOPDOWN §2.6) comes from setting things right: cleansing a level's corruption (a step every
  tenth), freeing fish, freeing bosses. Each level is a **skill point**, laid on one of her modifiers in the pause menu.
- **Her look**: a new jelly is made on the title's jelly card (`LookForm`): her name, the shape of her four gonads, and
  the colours of her glow, her bell and arms, and her tentacle tips (`JellyLook`). Everything but the name can be changed
  at the haven's mirror, once Kip is home.

## 10. The folk, the requests, the chains

- **The folk** (FOLK.md): Grandmother Tull opens the Tide Fan, Kip the mirror, Fennel the Brood Bed (shells left with him
  are never lost), Ottilie the shell shop, Bubbles the Dusk Dance (skill points laid anew). Freed folk never fight for
  her. Each speaks a line or two from her home; Clementine never speaks.
- **Requests** (DIVE §7): the Tide Fan ties asks for the depth she is diving (seal leaks, free a prime, seal a
  leak cave, wash ash); she carries two and unties them at the next haven.
- **Chains**: each freed neighbour's short quest, its last step paying her active pearl.

## 11. On the screen

- **The HUD** (DESIGN-TOPDOWN §9, ADVENTURE §6): top left, Clementine seen from above with her four oral arms as her
  stats (health, inner light, the active pearl's charge, experience) and her shells beside her; top right the minimap,
  its bezel her corruption meter; top centre a dot per leak. Messages slide down from the top edge. Nothing else stays on
  the screen.
- **On the reef**: corruption rings round the corrupted she can see; the pearl she is absorbing on its own card.
- **Readable at a glance** (VISUAL-SHOWCASE, "The order of the eye"): outside her light the reef is grey and quiet and
  never outshines her; the camera draws in on her in a fight; what she
  cannot see near her troubles the water and is heard (CORRUPTION, "Sensing the unseen"). A wholly cleansed reef gets
  its full colour back.
- **The pause menu**: Level up, Stats, Pearls, Achievements, Requests, Chart.
- **The title** (TITLE-MENU): the three jellies over a live sea; statistics; the Sea-pedia; settings.

## 12. Decided and not yet built

- **The ending.** Depth 6, the Tank: flat fluorescent light, neon gravel, plastic plants that do not sway, and The Hand
  in three phases; the bloom found there; the true ending of THEME-BIBLE §4.5. The Abyss's own content before it: its
  creatures, and the old catfish of the keystone as the last boss of the sea.
- **The lore's folklore frame** (THEME-BIBLE §4): the folk's lines, the Sea-pedia and the bosses' cut-ins carry the tale
  of the jellyfish's errand and the Palace, piece by piece across many dives.
- **The rest of the pearls** on the beam, status effects, transformations (proposals/BEAM-PEARLS-PLAN.md).
- **The next steps of the project review** (proposals/MASTERPIECE-REVIEW.md §9): playtests, the light grammar for
  pearls, the tide clock and a 30-minute mode, sound as a system, co-op.

## 13. Known gaps between the documents and the game

- **DESIGN-TOPDOWN.md** still carries rules from before the beam and the dive: bubbles as her attack and their ammo
  orbs (§2.3), sand dollars and hearts (§7), the inherited item and hub rules, seven depths counted to the Tank as built
  (§8). The beam, cores, light orbs, shells and the dive in this page and the system documents replace them. It should be
  rewritten into the system documents and then archived.
- **Code from the open reef** is still in the game (`PortalView`, the region and current-ride code in `TopDownMain`,
  `PlaneReef`, the three active slots in `PlaneRun`); its comments point to archive/RPG-PROPOSAL.md. It should be removed
  or brought under the dive.

## 14. The crossover, built so far

The crossover (proposals/CROSSOVER.md) is being built step by step (its §15). The game runs with its switch
(`Run.Crossover.On`); the sim takes it as a parameter (`LevelPlan.For`, `TopDownGenerator.Generate`: `crossover`), so
the tests of the dive still test the dive.

- **Step 1 (0.49.0), shelving:** the game's camera is straight down and orthographic (`CameraRig.Flat`), framing
  the same ground the tilted one did at her (`--dbg-tilted` brings the tilted view back for comparison); the title's
  sea keeps the tilted view. Its levels have no havens and no leak caves or curse dens (and so no Tide Fan,
  requests, mirror or folk homes to reach). Skill points are shelved: no level-up message, no pulse on her bell or the
  HUD, and the pause menu has no Level up, Requests or Chart tab. Still to shelve: experience itself, and the hole
  rims that name a curse den's reef (the dive graph goes in step 2).
- **Step 2 (0.50.0), template caves and the dive clock:** every level but the tutorial's and a boss's is built from
  cell templates (`LevelPlan.Templated`, `CaveLayout.FromTemplates`, `CaveTemplates`): a 5 × 5 grid of 18 m cells
  (18, not the proposal's 22, so the grid fits inside the level's rim), a Spelunky walk from one row to the far one,
  side cells up to two deep and the odd loop, the whole grid turned or mirrored any of eight ways. Each used cell takes
  a template that opens where it is joined (sockets 6 m wide in the middle of a side), turned and mirrored, its chunk
  slots filled at random; then the rock edges are eroded with noise, and the generator's own stages (walls, passes,
  arches, decoration) build on it. Ten Sunlit Reef cells and one room cell so far. A room cell holds a place that
  lives in a cave (a treasure clam, a Crab Deal) behind a short spur; an open place (a shell cache) takes a POI mark;
  a place that finds neither is left out. Mob marks fill the spawn table by tag (lone, pack, thrower, ambusher,
  guard), and the marks are kept on the map (`LevelMap.MobMarks`, `PoiMarks`) for the rosters and places of later
  steps; the ink creatures still come from the corruption's Blightroots as before. No secret rooms (a labyrinth is a
  second layer). A template level has no shaft and no holes: after **2 minutes** on it (`DiveClock`) she may dive
  anywhere, and nothing else is asked (not even that no fight is on). A ring round her bell on the HUD fills over the
  two minutes; when it is full a soft chime and a message say so, the prompt shows for a few seconds and the ring
  breathes. The level below is shown under her only once she dives. `--dbg-diveclock=s` starts each level's clock
  that far on (verification).
- **Step 3, first part (0.51.0), shells, bubble vaults, shops:**
  - **Shells by worth** (`ShellKind`, `ShellWorth`): cowrie 1, scallop 3, conch 8, nautilus 25. On a template level
    each shell of a find is rolled by the find's richness (a freed creature's, a pot's, a cache's, a vault's) and the
    depth (the menace), so the rarer shells lie deeper and better hidden; elsewhere every shell is a cowrie, as before.
    A shell's size and glow grow with its worth (one shell model for all four until the ink look); the counter counts
    each in by its worth as it lands.
  - **Bubble vaults** (`PlaneVault`, `VaultView`): templates carry vault marks by their walls (`v`, `LevelMap.VaultMarks`);
    a level takes most of them (at most 6), each a big air bubble snug against its rock holding 2–4 shells (rolled
    as a vault's), a healing orb 40% of the time and a pearl 8%. What it holds shows inside it only in her aura; her
    beam fills it with her light till it bursts (about a second), and her dash bursts it too; its contents tumble out
    away from the wall. It is fixed to the rock. `--at=vault --dbg-vault` shines at one (verification).
  - **Shops:** with no havens, a template level from its second row on holds a shop 45% of the time
    (`LevelPlan.CrossoverShop`), in a room cell like a treasure clam, with the shop's stands as before.
- **Step 3, second part (0.52.0), light pips, pools by place, the light shrine:**
  - **Light pips** (`LightPips`, `PlaneRun.Pips`, on for the crossover's runs): her health counts in half pips of
    100/12, so 6 pips at the start (more as pearls raise her most). A hit takes whole half pips, one to four by how
    hard it was after the menace (`HitHalves`: one per 12), so hits read and count; a drain (a murkling clinging, the
    curse's dark) still drinks steadily, and her pips show it rounded up. A healing orb gives back a whole pip, the
    shop's top-up two. On the HUD the pips are beads of her warm light strung down her first arm, a half pip a lit
    half bead, the empty ones dark; the last pip breathes, and the arm flashes as one is lost.
  - **Pools by place** (`PlaneRun.Offer`): on a template level a clam offers from the treasure pool, a shop from
    the shop pool (its pearl priced by quality, 50 + 25 a quality step: 50 to 150 shells), a bubble vault from the
    secret pool with the plainer treasures (quality 2 and under), a light shrine the finest (quality 3 and up). A
    pool with nothing left falls back to every pearl she may be offered.
  - **The light shrine** (`PoiKind.Shrine`, `StandKind.Shrine`; the proposal named it but not what it does, so this
    is a first design): an old pale stone on a POI mark of a template level (35% of them, `LevelPlan.CrossoverShrine`),
    a fine pearl resting on it. Touching it gives two pips of her light for the pearl, which she begins to absorb; it
    never takes her last light (with two pips or less it refuses: "Too little light to give"). It differs from the
    Crab Deal, which cuts her most health. `--at=shrine` / `--at=shrine-on` (verification).
  - Step 3 is now built but for the active pearl charging by freeing creatures (§6), which comes with the rosters.
- **Step 4 (0.53.0), the boss level:** a boss level is a template level too (`LevelPlan.Templated` now holds for
  every crossover level but the tutorial's). Its walk ends in the **arena**: the 2 × 2 block of the last two rows
  round where the walk would end, carved as one round cave (`CaveLayout.ArenaRadius`, 15 m, its wall wavering a
  little) with a doorway to each cell joined to it; the walk's cells past where it first enters the block are left
  out. The generator's own boss stages work on it unchanged: the sealed boss walls across each doorway, the Crack in
  the middle. Its clock opens no dive: when it has run, **the current rises** (`PlaneTide.cs`, `TideTuning`): the
  water flows toward the arena from everywhere along the level's own ways (down the distance through the water to
  the arena's middle), 1 m/s at first and 0.5 m/s more every 5 s, to 9 (faster than she swims within about a
  minute). It carries her, the free-swimming creatures (murklings not budding or clinging, mobs, Thumpsquirts,
  Blotters), the reef's fish, shells, orbs, freed light, loose pearls and ink clouds; rooted things stay. Inside the
  arena it is still. A creature it brings within 3 m of the arena's rim **dissolves into ink** there (a dark bloom
  and a gurgle) and adds 4% of the boss's own light to its corruption, to at most 60% more; a fish just leaves. It
  stops when the boss is freed; the Crack then opens as before and is the way down (the **vortex**, restyled with the
  ink look). The HUD's ring turns to a swirl of ink once the current runs, and she is told ("The current rises").
  **Queen Clam** is the first depth's boss (`BossRoster.For(…, crossover)`; no neighbour waits there), so the first
  depth plays through: four template levels with the dive clock, then her arena, then the next depth. Not done: the
  current is not drawn yet (only what it carries moves), and the second vortex at a branch waits for the depths'
  branches (step 7).
- **Step 5 (0.54.0), treasures:** the treasures are listed in `data/treasures.json` (`TreasureCatalog`, checked on
  load like the achievements: ids unique and known to the game, `Treasures.Known`, one a depth). The first three:
  the **Pearl Altar** (depth 1, the Sunlit Reef), the **Spare Bell** (the Kelp Forest) and the **Shell Urn** (the
  Shipwreck Graveyard; since step 7 both of depth 2, one for each branch, found by biome). Each depth hides its
  treasure on one of its levels from the second to the one before the boss's (`LevelPlan.RelicLevel`, from the
  depth's own stream): a **relic place** (`PoiKind.Relic`) on the POI mark farthest from the walk's own cells. It
  pushes out a shell cache or the Crab Deal if the level is full; it is never named under the minimap and never shown
  by the Brass Compass. There the treasure lies as the real old object (a worn stone block with three hollows, a
  ship's bronze bell gone dull green, a sealed clay urn), glinting now and then with a small warm light. She takes it
  by touching it: its banner ("Treasure", what it does every run), and it is the **jelly's for good**
  (`Jelly.Treasures`, saved at once). Once she has it, its place holds a pearl from the secret pool instead. From the
  next run on, the treasures she has wait in the **start cave**, round where the run's first template level starts
  (`PlaneRun.BeginWithTreasures`, `PlaneWorld.PlaceStartCave`): the **Pearl Altar** offers three pearls of quality 1
  to 3 on the altar ("altar" pool); touching one fades the other two (`PlanePearl.Choice`). The **Shell Urn** lies on
  its side with 15 shells spilled before it (a conch, two scallops, a cowrie). The **Spare Bell** gives her one more
  light pip for the run (`PlaneRun.ExtraHp`). A run continued from a save keeps the bell's pip but has no start cave.
  Not done: the guarded and light-puzzle hiding places (a relic lies in a side cell for now), the other treasures of
  §5.2, and treasures on the title screen's jelly card.
- **Step 6 (0.55.0), the ink look** (CROSSOVER.md §12): a stage at the end of the water pass (`topdown_post`'s
  `ink_look`, `CameraRig.SetInk`), on for crossover runs (`--no-ink` shows the painted look to compare). It works on
  what is drawn before that pass (the reef, the rock, the floor's things); Clementine, her shots and the particles,
  drawn after it, keep their own look. Outside her light the world is **flat washes**: the rock in ash greys, the water
  and the floor under it near-black blue ink, the light stepped into four tones with a **halftone** (a 45° dot screen)
  at each step's soft edge, so a shadow is a flat fill, never a gradient. **Brush lines** in ink: the caves' outline
  where the rock rises through her plane (its weight wavering along it, fixed to the rock so it never crawls, with
  dry-brush gaps), a lighter floor contour a few metres down, and a line round whatever lies at her plane. In **her
  light** (and what she has cleansed) the world keeps its colour, stepped a little, and the caves' outline is traced
  in **coral and magenta light**; only the rock's is (the terrain's height from the sun's map tells it apart): a
  creature or a boss in her light keeps its ink line, never a neon one. Her glow in the water is a **sunset**: white at
  her, gold, hot coral, a magenta rim at the edge of her reach. The **paper's grain** (a fine tooth, fibres, a sheet's
  unevenness) lies over the whole picture, her included, at 7% (`Fisheye.InkGrain`). This uses magenta as the warm end
  of her light, the accent CROSSOVER.md §12 allows on this branch. Not done: her bell's own colours, the cyan of the
  deep biomes' machines, and the interface's ink frames.
- **Step 7, first part (0.56.0), the route, the first branch, bosses and rosters by biome** (CROSSOVER.md §8–10):
  - **Biomes** (`Run.Biome`, `Biomes.Of`): depth 1 is the **Sunlit Reef**; depth 2 the **Kelp Forest** or the
    **Shipwreck Graveyard**; depth 3 the **Sunken Ruins** (and the depths below it too, until step 8 builds theirs).
    Every crossover level carries its biome (`LevelPlan.Biome`, `LevelMap.Biome`), and its name is the place's name
    on arriving.
  - **The first branch:** Queen Clam's arena opens **two ways down**: the Crack across it is two vortices end to end
    (`TopDownGenerator.SplitCrack`), both opening when she is freed; the prompt over each names the biome below ("dive ·
    the Kelp Forest below"), and each glows in that biome's colour (green, rust; only in the painted look so far: the ink
    look keeps colour only in her light). The way she takes is kept down the whole depth (`LevelId.Branch`, saved with a
    run, `SuspendedRun.Branch`); the two branches are different levels (their own streams), and both bosses' arenas
    lead on to the Ruins. The crossover's way on is its own route (`Biomes.Exits`): an ordinary level's next is the
    graph's first exit with her branch kept, a boss's the next depth's first level, once or one way for each biome below.
  - **Bosses by biome** (`BossRoster.For(…, biome)`): the neighbours the Leak made move in as the biomes' bosses,
    **Sleepy Old Tull** (the Kelp Forest), **Nine-Fin Kip** (the Shipwreck Graveyard) and **Many-Armed Ottilie** (the
    Sunken Ruins), each with her corrupted pearl as before, at her depth's Menace; in the crossover she is nobody's
    neighbour (freeing her sends no one home: the folk's homes are shelved).
  - **Rosters by biome** (`Biomes.Roster`): each mob mark's tag takes the biome's own creatures: the Sunlit Reef's
    the common reef's; the Kelp Forest's barracuda, moon jellies and nudibranchs, its urchins in wait; the Shipwreck's
    morays and crabs in its holds; the Ruins' morays in its walls. A place's guardians come from the roster too. The
    Kelp Forest has the kelp's dense beds (the Sunlit Reef now the common few).
  - **Treasures by biome** (`TreasureCatalog.InBiome`): a biome's levels hide its own treasure, so the Spare Bell lies
    in the Kelp Forest and the Shell Urn in the Shipwreck Graveyard, both at depth 2; the Ruins have none yet.
    `data/treasures.json` is checked for known biomes, one treasure a biome.
  - `--branch=1` (with `--depth=`) starts on the second branch (verification).
  - Not done: the biomes' own cell templates, chunks, hazards and looks (every biome still uses the Sunlit Reef's
    cells and the depth's palette); the war's creatures in the rosters (the ink creatures still come from the
    Blightroots); the vortices' colour in the ink look; the Ruins' treasure. Tull's, Kip's and Ottilie's fights are their
    folk fights as they were, not tuned for the crossover's arena.
- **Her light and her shots (0.57.0):** her **glow is gone** in the crossover (`PlaneOptions.Glow`): no aura, her bell
  casts no light round her (no bulb, no halo, no warm pool, no glow in the water: `BellView.CastsLight`, the post pass's
  `her_glow_on`), and the glow's toggle has no default button. Her **beam is her light**: it shows the creatures in the
  dark and what a bubble vault holds, dazzles, and burns creatures at a fifth (`HerLightTuning.LightOnlyBurn`), a boss's
  open pearl in full. Her **bubble shots are back** as her attack (right mouse, right bumper; `--shoot` for
  verification). In the ink look **her light is clean**: what her beam lights shows in its own colour, smooth, never
  stepped or halftoned, coming in quickly at the beam's edge; outside it the washes and dots are as they were. (The
  caves' outline is still traced in coral and magenta light where she lights it.)
- **Her light's afterimage, her shots' light, the vaults in the dark (0.58.0):**
  - **The afterimage** (`LightMemory`): where her beam and her shots' light have fallen, the reef keeps its colour
    for 0.3 s after the light has moved on, then fades back to the muted ash over 1.3 s, its edge softened over about a metre. A grid of quarter-metre cells
    over the level, each the light it last had and how long ago, traced as the post pass draws her beam (its fan to
    where it stops, cut by rock) and her shots' light; the post pass takes the larger of it and her light now
    (`her_after`, `her_after_rect`).
  - **Her shots' light** shows the reef round them in its colour, as her beam does: the harder a shot hits, the wider
    and stronger (their lights' cap raised, `CombatView.LightMax`, so a hard shot outshines a plain one).
  - **Bubble vaults lie in the water's dark**: out of her light (her beam, her shots', their afterimage) a vault's film
    all but sinks from sight (`plane_bubble`'s `dark`), only a faint cold-white rim and a faint pale light of its own
    (`VaultView.OwnGlow`) showing it from afar; lit, it shows as before, and what it holds.
  - `--dbg-sweep` turns the auto-fire's aim round her (verification of the afterimage).
  - (0.58.1) Her beam lights its cone alone, right up to her bell: no lit disc round her (reef_show's `rs_beam`).
- **Step 7, second part (0.59.0), the biomes' cells and looks:**
  - **Cells by biome** (`CaveTemplates.KelpForest`, `Shipwreck`, `SunkenRuins`, `VariantsOf`, `ChunksOf`): five cells
    each, one of every shape a cell can take (a cross, a T, a straight, a bend, a dead end; turned and mirrored they open
    every way), with chunk lists of their own. The Kelp Forest's are wide open water among a few rounded rocks (its kelp
    beds stand in them); the Shipwreck's square holds and decks, a beam or a stack of crates in them, a keel down a long
    hull and ribs along its walls; the Ruins' a forum, an atrium round its pool, a colonnade, a temple's cella over its
    steps, a shrine's niche. Every passage is at least 4 m wide (narrower ones close up under the reef's walls and its
    corridors), so their levels are made as readily as the Sunlit Reef's (about 1.1–1.6 attempts). A level takes its
    biome's cells (`CaveLayout.FromTemplates(…, biome)`); a shape a biome lacks falls back to the Sunlit Reef's.
  - **Looks by biome** (`ReefLook.Of`, `ForBiome`): a biome's look is its depth's painted look blended halfway to its own
    palette, the Kelp Forest's greens, the Shipwreck's rust and old timber (the drawn Galleon's), the Ruins' marble,
    warm sand, terracotta and faded fresco blue (`ReefLook.Ruins`); so her light finds each biome's own colours. Used for
    the level's environment, its flora, the dive's look from one to the next, and the map.
  - A pickup placed along a template level's straight two-point corridor no longer divides by zero
    (`PlacePickups`), and a failed attempt is logged with its reason.
  - Not done (then): the biomes' own dressing and hazards (the third part, below), and the Ruins' treasure.
- **Step 7, third part (0.60.0), the biomes' dressing and hazards:**
  - **Gardens by biome** (`LevelView.Gardens`): the Kelp Forest grows sea grass and a few sponges under its kelp, no
    reef corals; the Shipwreck and the Ruins only a thin growth of sponges, fans and sea grass (a third and two fifths of
    the reef's).
  - **Their own things** (`BiomePropView`, for the look only): the Shipwreck's planks, barrels, crates and now and then a
    rusted cannon, on its floor and its wall tops; the Ruins' standing column drums, fallen columns, cut blocks and
    amphorae lying on their sides. A scatter from the level's seed, off the start, the ways down and the places; drawn in
    the scene, so the ink look greys them out of her light like the reef.
  - **Hazards** (`PlaneHazards.cs`, `HazardTuning`), one a biome, none in a boss's arena, none the corruption's (her
    light does nothing to them):
    - the Kelp Forest's **tangles**: inside a kelp bed (within 85% of its radius) she swims at 60% of her pace;
    - the Shipwreck's **falling debris**: every 3.5–6.5 s a timber falls from the wreck above where she is heading
      (her velocity a second on, up to 3 m astray); its shadow and a pale ring show for 1.3 s, then it lands across her
      plane (a thud, a small shake) and sinks; under it (within 1.2 m) she is hit (14, before the menace);
    - the Ruins' **dart traps**: six a level, set in walls (a carved stone face, its mouth a faint pale glow) facing a
      clear run of water; when she crosses one's line (within 0.9 m of it, up to 11 m out, in its sight) it clicks,
      glints warm and 0.35 s later fires a bronze dart at 12 m/s down its line (10 on a hit), then rests 2.5 s.
  - The traps' mouths, the darts and the debris ring are drawn after the ink pass with a faint light of their own, so a
    hazard shows in the dark as a vault does. `--at=dart` stands in front of a trap (verification).
  - Not done: kelp walls her beam burns through, urchin beds as a hazard of their own, the Shipwreck's cannons that
    fire when a creature passes, amphorae to break, light-locked doors; the biomes' rock itself (its shapes and its
    surface are still the reef's); the Ruins' treasure.
- **The biomes' ink (0.61.0):** in the ink look each biome's muted reef (out of her light) is washed in its own colour,
  always dark, never quite black (`CameraRig.SetInkBiome`, topdown_post's `ink_tint`): the Sunlit Reef blue, the Kelp
  Forest green, the Shipwreck red, the Ruins yellow; its water leans the same way. Each has its own pattern
  (`ink_pattern`), fixed to the reef so it never crawls: a **halftone screen** at the soft edge of each tone (the reef's
  45° dots, the kelp's steep lines, the wreck's grain-wavering lines, the ruins' squares, `ink_screen`) and a **motif**
  drawn darker into its unlit rock and faintly on its floor (`ink_motif`): the reef's scattered polyps, the kelp's
  wavering strands, the wreck's planks with their seams, butt joints and grain, the ruins' mosaic with its grout and a
  tile gone here and there. Her light shows the reef in its own colours over all of it.
  (0.61.1) The wash kept as dark as the plain ink look (its tint a lean, not a lift). Every crossover level records the
  cells it was built from (`LevelMap.Cells`), and each biome's levels are checked to be built only from its own (and the
  room cell): no two biomes share a cell.
- **Step 7, fourth part (0.62.0), the rest of the biomes' hazards and the Ruins' treasure** (`PlaneHazards.cs`):
  - the Shipwreck's **rusted cannons** (four a level, against walls, a faint glow at the muzzle): anything that swims
    across the line before one sets it off, her or a creature; 0.6 s later it booms (a shake) and fires a heavy ball at
    7 m/s, 14 m out; the ball hits whatever it meets first, her (16) or a creature (40), and rests 4 s. Lure the
    creatures across them;
  - the Kelp Forest's **kelp curtains** (two a level, across the middle of side passages): a close row of tall strands,
    a faint green glow along it; it stops her, her shots and her light, till her beam holds on it 0.6 s: then it parts
    (its strands sink aside) for 3.5 s and closes again behind her (never on her);
  - the Ruins' **amphorae** (its own pots, the breakable vases of old, holding shells) and a **light-locked door** on a
    side passage (a seal her light opens, `TopDownGenerator.RuinsDoors`);
  - the Ruins' treasure, the **Oil Lamp** (`Treasures.OilLamp`, a bronze temple lamp): her beam reaches a quarter
    farther every run (`PlaneRun.BeamReach`).
  - `--at=curtain` stands before a curtain (verification).
  - Not done: urchin beds as a hazard of their own (urchins are the Kelp Forest's ambushers), the doors' own look (a seal
    looks like the reef wall), and the war's ink creatures in the rosters.

