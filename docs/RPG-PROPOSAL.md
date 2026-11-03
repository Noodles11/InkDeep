# The open reef: Ink Deep as a roguelike RPG (proposal)

A proposal for branch `m4-rpg`: the dive becomes an open reef to explore from a town, with quests, NPCs freed from the
Leak, and one purpose: close every leak and give the ocean back its freedom. Nothing here is built. It maps each idea
onto what the game already has, and lists what the Theme Bible would have to change (§10) and what is left to decide (§11).

## 1. The game in one breath

Clementine lives in Lantern Bay, the last clean water of the reef. Out in the reef, every leak bleeds tank-water from
the dead end of its own cave. She swims out of the bay onto the reef, rides its currents further, swims down the caves through what the Leak has made of the
reef's creatures, and seals the leaks with her inner light. Each seal gives her experience, which is the only
experience there is, and each one makes the reef visibly cleaner. The creatures she frees go home to the bay and ask
for her help. What she keeps is her level, her active pearls and her name. What she risks on every trip is her
passive pearls and the shells she is carrying.

The two runs in the brief share one spine, and the whole design serves it: **find a leak, fight your way to it, seal
it, and the reef remembers.**

## 2. The world

```
   ┌──────────────────────────────┐            ┌────────────┐            ┌────────────┐
   │ SHALLOWS: the reef map       │  currents  │   KELP     │  currents  │  GALLEON … │  regions: static maps,
   │  (nine times the bay)        │────────────│ region map │────────────│            │  one biome each
   │ ┌───────────┐                │            └──┬────┬────┘            └────────────┘
   │ │LANTERN BAY│ haven corner:  │               │    │
   │ │shop · fan │ safe, no ink   │
   │ │mirror     │                │
   │ └───────────┘                │
   └──┬────┬──────────────────────┘
      │    │ cave mouths            │    │
   ┌──┴┐ ┌─┴─┐                   ┌──┴┐ ┌─┴──┐
   │cave│ │cave│  seeded         │cave│ │crown│  great leak: authored,
   │leak│ │leak│  sub-levels     │leak│ │leak │  boss-guarded, closes for good
   └────┘ └────┘                  └────┘ └─────┘
```

### 2.1 Lantern Bay (static)

- **Where every session starts.** After loading her profile, she wakes here (idea 13). The bay has no ink, no
  creatures to fight and no Menace.
- **A corner of the reef.** The bay is not a map of its own: it is the haven in a corner of the Shallows' map, which
  is nine times its size. She leaves town by swimming out of it onto the reef, where the cave mouths are; past its
  edge she is vulnerable again, and coming back into it heals her whole (§13).
- **Places in the bay:**
  - The shell shop, which buys and sells pearls.
  - The **stash**, where she keeps her pearls (§5.2).
  - The **Tide Fan**, a great sea fan in the commune where the bay ties its requests as kelp ribbons (§6;
    docs/QUESTS-PROPOSAL.md). No signboard: no human things in the reef before Depth 4.
  - The **mirror**, where she changes her look (§8).
  - A **home for each NPC** she has freed (§7).
  - Its **gates** out onto the reef.
- **Bay cleanse meter.** A single meter shows how much of the reef is free. It is the sum of the great leaks sealed in
  every region.

### 2.2 Regions (static)

- **One biome per region,** following the depth order already in the game: Shallows, Kelp, Galleon, Carnival, Trench,
  Abyss.
- **A fixed Menace per region.** Menace still drives everything it drives today: light, ink coverage, creature
  aggression and colour (Bible §3 Pillar 5). Going further out still feels like going deeper.
- **A region map is hand-made** (idea 14). It holds:
  - The arrival point of its current.
  - Cave mouths.
  - Lantern posts, which are respawn points.
  - Secrets: fake walls, Crab Deals.
  - Fountains.
  - The crown cave of its great leak.
- **Ink on a region map.** Free-roaming creatures and corrupted fish live on it (ADVENTURE.md §3–4). How much of the
  map is inked follows the region's cleanse level, not the run.

### 2.3 Leak caves (seeded)

These cover idea 5.

- **One leak per cave,** at the dead end of the main cavern path.
- **The generator.** Caves come from the current generator (`TopDownGenerator` with `CaveLayout`). The one change is
  that the way-down chamber becomes the leak chamber, with no hole.
- **Mobs.** Side chambers off the main path hold lurking mobs. The biome decides which mobs and the cave's level
  decides how strong (§9). The whole cave is corrupted: ink coverage is fixed high, and the start is the only clean
  water.
- **After the seal.** No current carries her back: she swims out to the mouth on her own, looting the side chambers
  in clean water. Sealing a leak dissolves its ink (already true today), so the way out is the safe half of the cave.
- **Two kinds of leak:**
  - **Minor leaks.** Infinite and seeded (idea 12). A region always keeps three open: when she seals one, a new cave
    mouth opens somewhere else on the region map the next time she visits. This is the repeatable "close the leak"
    content.
  - **Great leaks.** One per region, a fixed cave with a fixed layout and a boss guarding the leak chamber (the boss
    generator). They close **for good**: the region map clears, its currents turn blue, and the bay's meter rises.
- **The Crack** is the last great leak, at the bottom of the Abyss region. It leads into the Tank finale.

## 3. Currents as travel

This covers idea 1, and almost all of it already exists:

1. **Boarding.** She swims into a **current station**, a glowing stream at a region's edge or at its arrival point. A travel menu opens (the map), and she picks a destination she has unlocked.
2. **The ride is the loading screen.** The existing dive transition is reused with the camera kept top-down (no new
   camera break, Bible §6.1). She is swept along in the main current's streaming particles, snow streaking past and
   ribbons trailing, while the palette slides toward the target region's water. The target level loads underneath, the
   same way the dive loads the level below today.
3. **Arrival.** She is pushed in from the **top** edge of the target map and drifts down until she settles into the
   current's moving layer. That layer is the main current that already exists in a level (DESIGN-TOPDOWN §4.7). It
   carries her a little way and then lets her go.
4. **Unlocking routes.** Sealing a region's great leak unclogs the current network around it. This is the "northern
   run turned blue" beat from the brief.

## 4. Experience comes from leaks and requests

This covers ideas 2, 9 and 12.

- **The sources.** Experience comes from sealing leaks and from the Tide Fan's requests. A minor leak is worth
  `25 × caveLevel^0.8`; a great leak is worth ten times that. An untied request pays as much as a leak of its level,
  on top of whatever leak it took her to (docs/QUESTS-PROPOSAL.md §4.2). Freeing creatures and bosses gives none, and
  key quests pay in shells, pearls or NPCs.
- **What levels do.** Levels are **the only way her stats change** (shot speed, swim speed, damage, max HP, glow, dash
  cooldown, bubbles). Each level gives skill points to spend in the existing skill screen, and each point still adds
  6 max HP, so her arms still grow with her (ADVENTURE.md §7).
- **A level-up waits for calm.** It lands when the room is quiet, as it does now.
- **No level cap.** Each level costs more experience, and minor-leak experience rises with cave level, so the curve
  holds.

## 5. Pearls

### 5.1 Passive pearls: behaviour only

This covers ideas 7 and 9.

- **What they do.** A passive changes **how** she plays: more shots, piercing, explosions, homing, bubbles that merge,
  and so on.
- **Where they come from.** Shops, bosses, secret rooms, Crab Deals and treasure chambers in caves.
- **No limit.** She can absorb as many as she finds. The bell shows 16 motifs, and the rest glow inside, as now.
- **The stat pearls have to go or change.** 25 of the 64 passives today only change stats (Coral Crown, Squid
  Ink Espresso, Shark Tooth, Dolphin Fin, Cuttlebone and others). Each one either becomes a behaviour or is dropped.
  The transformations (Shark Mode, Pirate and the rest) lose their stat parts the same way.
- **Lost on death.** When she dims, every passive she has absorbed comes out of her bell as **lost light**, a single
  glowing pearl-cluster where she fell, together with the shells she was carrying. If she reaches it, she gets
  everything back. If she dims again before reaching it, it is gone. This is the corpse-run in the brief, and it keeps
  the roguelike stakes.

### 5.2 Active pearls: slots and the stash

This covers idea 6.

- **Where they come from.** Key quests give actives (the 13 that exist today: Tide Pulse, Conch Horn, Bubble Shield
  and the rest). They are **never lost**.
- **Slots.** She has **three active slots** with three buttons. The bell's centre halo shows the active in slot 1, and
  slots 2 and 3 appear as small halos to either side. In the bay she can swap actives between her slots and the stash.

### 5.3 Selling

This covers idea 8. Any pearl can be sold at the shell shop for **silver shells**:

- **An active** from the stash sells for its price.
- **An absorbed passive** sells at half its price. It is drawn back out of the bell, and its motif fades: the
  absorption ritual run in reverse.

Silver shells are the one currency: the shells of today, renamed.

## 6. Quests

- **Key quests.** An authored chain, one per region, given by the NPCs freed there. They lead to the region's great
  leak, and their steps reward active pearls. Quest steps are small: chart a chamber, free a named creature, seal a
  minor leak, bring back a pearl.
- **Requests** (docs/QUESTS-PROPOSAL.md). Repeatable side quests tied to the Tide Fan by the bay's folk, seeded from
  her jelly's seed and a count: seal the leak behind a named mouth (which reopens it if shut), free a murkling prime
  (its swallowed school swims home), brave a cave's curse den, wash the ash off the caves' walls. Each pays shells and
  as much experience as a leak of its level, and most offer a passive pearl shown on its ribbon. She carries two at a
  time, tracked on the HUD's kelp ribbons under the minimap. Freed NPCs tie their own requests there later.
- **The quest log.** It lives in the pause screen. The map marks the next step.

## 7. Freed NPCs

This covers idea 3.

- **Named creatures.** Some corrupted creatures are named: the target of a quest step, or one per great-leak region
  waiting in its caves. Freeing one plays the usual freeing (Bible §6.5), then a short cutscene:
  - The camera stays where it is.
  - The creature turns, looks at her, and swims up and away, trailing gold.
  - A caption reads "Kip has gone home to Lantern Bay".
- **In the bay.** The NPC has a home there. She swims close to talk (a speech bubble with a portrait).
- **Clementine never speaks.** She answers with her body: a bob means yes, a turn away means no (Bible §5.3).
- **Bosses come too.** Freed bosses become NPCs, so the Drippus can take up residence by the Tide Fan.

## 8. Character creation

This covers idea 10.

- **When.** On first launch, over the live sea (the title screen that exists), before she wakes in the bay.
- **What can be chosen:**
  - **Gonad symbols:** the four gonad rings, picked from a set of shapes.
  - **Glow colour.**
  - **Oral arm colour.**
  - **Tentacle tip colour.**
  - **A name.**
- **Changing it later.** Everything except the name can be changed at the bay's mirror.
- **What already supports this.** The bell and arm shaders already take colours as parameters. The gonads are drawn
  as the HUD gauge and on her bell, so a symbol set needs new shapes in both places.

## 9. Mobs: biomes and levels

This covers idea 11.

- **Biome rosters.** Each biome has its own roster. The Depth 1 bestiary becomes the Shallows roster, and later
  biomes get their own.
- **Mob level.** A mob's level is `region tier + cave level offset`. Each level scales its HP, damage and corruption
  meter. Its look already scales with Menace, and that stays.
- **Where levels show.** Levels are shown on the Tide Fan's requests and the region map (which caves are which level), not
  floating over creatures, so the Bible's diegetic-first rule holds.

## 10. What changes in the Theme Bible

I haven't changed the Bible: it's yours to decide. These parts of the proposal contradict it.

| Bible | What it says | What the proposal does |
|---|---|---|
| §1, §4 | Clementine, alone, dives after her family; it ends at the Crack | A jelly of the Clementine bloom, named by the player, cleans an open reef region by region. The Crack is still the end. |
| §4.5, §12 Commitment | Runs end in rifts or death; descent is one-way | No runs. She returns to the bay, and only death has stakes (lost light). |
| §3 Pillar 3, §13 | Absorption is forever; no dropping pearls | Passives can be sold back out of her bell, and she loses them all when she dims |
| §5.2 | One active, at the centre halo | Three slots |
| §5.2 grid | "Stat up" rings are a motif family | No stat pearls, so the ring motif needs a new meaning |
| §6.5, §13 | Freed creatures swim off and never help her | Named ones go home and give quests. Do freed fish help in fights? The brief says yes (§11 Q5) |
| §6.1 | Only two camera breaks | The current ride keeps the top-down camera, so the rule holds |
| §6.3, §3 Pillar 1 | Clementine is always tangerine and gold, the only hue that never changes | The player picks her glow and colours freely, and she is no longer always the warmest light |
| §13 | No hub, no free-swim Tide Pool | Lantern Bay is a free-swim hub |
| §7 | Each depth is three seeded reefs | Each region is a static map with seeded caves |

## 11. Decisions and open questions

Decided:

- **Local profiles.** "Logging in" means picking a profile. The name is unique among the save slots on one machine.
  There are no accounts or server.
- **One of the bloom.** "Clementine" is the bloom's family name. The player names their own jelly, who is one of the
  bloom, so the story holds as written.
- **The glow takes any colour.** So do the arms and tentacle tips. The Warmth Test (Bible §12) no longer holds for
  her glow.
- **Absorbed on pickup, sold at half.** Passives are absorbed as today, and the shop can draw one back out for half
  its price (§5.3).
- **Freed fish only eat ink.** They clean the water and never fight for her.
- **She seals a leak by staying near it,** with no shots (ADVENTURE.md §1).
- **One leak per cave.**

## 12. Inconsistencies in the brief

These are settled in this proposal as noted:

- **One leak per cave.** The first run finds three leaks in one cave, but idea 5 gives every leak its own cave. The
  proposal has one leak per cave.
- **Shells and skill points.** The brief has "3 shells-to-go-until-a-skill-point", but skill points come from
  experience, not shells.
- **When a half-tied leak heals.** In the brief it heals between visits. In the proposal it heals within seconds of
  her leaving (as now), so leaving a leak half-tied never pays.
- **Offline change.** "Overnight the simulation unclogged the northern run": the proposal has no offline world change.
  Currents unclog when she seals a great leak.
- **The fusion catalyst** is a new system that isn't in the ideas. It is left out for now.

## 13. How it would be built

Each step is playable on its own:

1. **The profile and the bay.**
   - A profile save: name, look, level, experience, shells, actives, stash, quests, freed NPCs, sealed great leaks.
   - A loader for static maps. The bay is drawn as data in the generator's own terms (chambers and tunnels), so
     `LevelView`, `TopoMap` and the post pass draw it unchanged.
   - Lantern Bay with the shop, a haven in the corner of the reef's map (Shallows).
2. **Leak caves and current travel.**
   - A cave mode for the generator, with the leak at the dead end and no hole.
   - The ride through a cave's opening, into its cave and back out.
   - Experience from leaks.
3. **The progression rework.**
   - Stats from levels only.
   - Rework or remove the stat pearls.
   - Three active slots and the stash.
   - Selling.
   - Death and lost light.
4. **Character creation and the mirror.**
5. **NPCs and quests:**
   - First, the Tide Fan and its repeatable requests (docs/QUESTS-PROPOSAL.md).
   - Then named creatures, the go-home cutscene, and the first key-quest chain (Shallows).
6. **Regions.**
   - Static maps for the other five biomes.
   - Biome rosters and mob levels.
   - Great leaks with bosses.
   - The region cleanse and the current network.
7. **The ending:** the Crack as the last great leak, and the Tank.

**What steps 1 to 4, and step 5's requests, have so far:**

- **Hand-drawn maps.** `data/maps/<name>.json` (`HandLayout`) names chambers, tunnels, cave rooms, open places and a
  region's cave mouths. `TopDownGenerator.FromHand` builds the level with the seeded level's own stages (rock, rooms,
  arches and passes, decoration, kelp); the map's seed shapes only the details. A hand-drawn map (`LevelMap.Station`)
  holds no leaks and no main current. Its square is its own (`HandLayout.Size`; a seeded level's is 140 m,
  `LevelMap.Seeded`), and its exit is a current station only if it says so (`HandLayout.Station`, none yet).
- **The Sunlit Shallows** (`data/maps/shallows.json`), the first region and her home: one map, 420 m a side, nine
  times Lantern Bay, with the bay in its south-west corner. Out on the reef: the landing outside the bay's gate, the
  kelp, a glade, the spire, the ridge, the flats, a crossroads, the meadow and the east, joined by long tunnels, and
  seven cave mouths (kelp, north-west, spire, ridge, flats, east, south-east), openings in its walls (below). Out of
  the bay it takes the Sunken Galleon's look (its map's `look`, `LevelMap.LookDepth`), not the open Shallows' glare.
  Made once a session and kept (`TopDownMain.ShapeRegion`): it takes seconds to make, and coming back out of a cave
  is quick.
- **Lantern Bay** (the reef map's `haven` box, `LevelMap.Haven`, 140 m a side): the lantern post where she wakes, the
  lagoon with the mirror, the commune with the Tide Fan, and the market with the shell shop's cave. Its gate (north)
  and the commune's way east lead out onto the reef. It is her haven:
  - **Safe:** nothing harms her in its water (`PlaneWorld.InHaven`); out of it she is on the open reef, and vulnerable
    again.
  - **Home:** swimming back into it heals her whole (`PlaneWorld.StepHaven`, `HavenEntered`), and the Tide Fan ties
    new ribbons; so it has no light spring (`LightSprings.Of` places none in it). Its name shows as she crosses in
    ("Lantern Bay · Safe water"), the region's as she leaves ("Sunlit Shallows · The open reef").
  - **Its night:** it lies under a summer night (`ReefLook.Bay`): moon-blue water, silvered sand, a low moon's soft
    caustics, and Clementine's light the warmest and brightest thing in it, pooling round her wherever she swims.
    Swimming out, the water turns to the reef's look over the 24 m past its edge (`TopDownMain.StepHavenLook`).
  - **Known:** its whole chart and every place in it show on the minimap and the Tab map from the start
    (`FogOfWar.RevealBox`); the rest of the reef is charted as she swims it.
  - **Its shop** sells only while she is in it (the Pearls tab's Change and Sell).
- **Cave openings** (`Doorway`, `TopDownGenerator.CutDoorway`): a region's cave mouths and a leak cave's way out are
  openings in a wall, as a real cave's are, not spots on the floor.
  - **Where:** from a drawn mouth's point (or a leak cave's start), the nearest stretch of wall with rock enough round
    it. A wall north of the water is sought first, so the opening's face looks toward the camera; then one to the side;
    then any. A leak cave whose start chamber has no room takes the nearest wall along its way in.
  - **The rock:** the rock round it is raised into a rough mound (only where rock already stands, so no water is ever
    closed off), and a passage 4.5 m deep and 3.4 m wide is cut into it from a short notch at the water's edge. The
    mound's rock over the passage stays as its roof (`LevelMap.Roof`), drawn in the reef's own ground, and its face
    stands at the threshold with a round-topped arch cut in it, ringed with rough rock (`LevelView.AddDoors`).
  - **The dark:** the passage darkens the deeper in it goes until it is black (`topdown_ground`'s `door_dark`). At its
    far end a region mouth shows the Leak's own black, its violet ink stirring; a leak cave's way out shows the water
    outside shining in (`PortalView`, `cave_throat.gdshader`).
  - **Through it:** swimming 1.4 m into a passage (`ReefTuning.MouthIn`) takes her through. She swims on into its dark
    as the place dissolves round her, and comes out of the next place's opening, swimming out of its dark into the
    water (`TopDownMain.StepRide`, `StepArrive`). In a leak cave she always starts at the back of its way out.
- **A region's chart:** what she has charted of a region (its map's fog, a byte a grid point) and the places she has
  been to there are kept on her jelly (`Jelly.Charts`, written with the rest of her), so trips into its caves and home
  never wipe its map.
- **Leak caves** (`TopDownGenerator.GenerateCave`, `LevelPlan.ForCave`, `LevelMap.LeakCave`):
  - **Shape:** a seeded level with its one leak at the dead end of the main path where the way down would be, and no
    shaft. Now and then it has a secret or a curse den off the path (35% each), and maybe a light spring as any level
    does; never a clam room, a shell cache, a shop or a crab.
  - **Ink:** at least 90% of the water starts inked (`ReefTuning.CaveInk`).
  - **Creatures:** only murklings and murkling primes (docs/CORRUPTION.md). No fish live in a cave, so nothing turns.
    - **At the start:** 4 + 3 × Menace murklings wait along its path, 9 m apart and at least 14 m from its start. A
      prime waits 9 m short of the leak (`PlaneWorld.PlaceCaveMurklings`).
    - **While the leak is open:** one buds out of the ink 8–18 m from her every 10 s ÷ (1 + 0.6 × Menace), drinking
      the dots round it, while fewer than 4 + 2 × Menace are within 24 m of her. Now and then it is a prime (the
      usual chance, at most two a cave with the waiting one). Sealed, the cave buds no more (`StepCaveMurklings`).
    - **The swallowed school:** a prime freed in a cave lets out the school it swallowed, 18 healthy chromis that
      stream away to the cave's way out, going home (`PlaneWorld.ReleaseSchool`).
  - **Dark:** no light reaches it but hers, her shots' and its fluorescent plankton (docs/ADVENTURE.md §8); a cool dim
    shade (`ReefLook.Cave`) keeps its rock and what swims in it readable outside her light.
  - **Ash:** its corruption clings to its walls as washed-purple ash, cleared by her light and her shots, and
    dissolved by sealing its leak (docs/ADVENTURE.md §8).
  - **Held:** while its leak is open, the Leak's current pours in through its way out, from the back of the passage
    6 m on into the cave at 11 m/s, faster than she swims (`PlaneCurrent.Holds`, `CurrentTuning.HoldSpeed`): it
    pushes her back inside, and says so when she reaches the threshold ("The Leak's current holds the way out · seal
    the leak first", `PlaneWorld.AtCaveMouth`). It shows as moving water, as any current does (`CurrentView`).
  - **Sealed:** the current dies away over 1.5 s, and no other wakes in it; she swims back herself. Swimming into the
    way out, once she has been 10 m from it, takes her out to the region at that mouth (`PlaneWorld.LeavingCave`).
- **Behind each mouth** (`OpenReef`): a cave, grown from her jelly's own seed.
  - **Sealed, it shuts:** sealing a cave shuts its mouth in the region (`Jelly.Shut`, `PlaneWorld.ShutMouths`): rock
    fills its arch (`LevelView.ShutDoors`) and stops her at its threshold, so she cannot go in by it. Coming out of
    the cave she has just sealed, she swims out through it first, and it closes behind her.
  - **It corrupts again:** after 15 minutes of her play time on the open reef (`ReefTuning.Recorrupt`,
    `Jelly.PlaySeconds`) the Leak seeps back and the mouth opens on a fresh cave. If she is in the region then, it
    opens before her ("The Leak seeps back: a cave mouth opens").
  - **Levels:** a region's caves go up a level every three sealed there, starting at the region's tier.
- **Experience** (`PlaneOptions.Reef`): on the open reef only a sealed leak gives any, `25 × cave level^0.8`
  (`ReefTuning.LeakXp`), and an untied request as much again for its level (`PlaneWorld.GiveXp`). No level-up is
  held back.
- **The current ride** (`TopDownMain.BeginRide`, `StepRide`, `StepArrive`):
  - **Where it goes:** a mouth leads to its cave, and a cave's way out leads back out of that mouth. (A current station
    would lead to another region's map; there is none yet.)
  - **Leaving:** she is swept off and lifted. The place dissolves round where she was (the dive's iris) until only
    water and streaming snow are left, and the next place is made meanwhile; the ride lasts at least 2.4 s.
  - **Arriving:** she comes down into the next place from a level's height above, still carried the way the current
    took her. The place opens out round her, and she settles in its water.
  - **No bounce-back:** a way through takes her only once she has swum clear of the one she came out of.
- **Jellies** (`SaveFile.Jellies`, `Jelly`, `JellyLook`, `JellyNames`; the title's Play drawer, `JellyCard`):
  - **Save slots:** each jelly is a save of her own, at most three on a machine (`JellyNames.MaxJellies`). Play opens
    them in the title's sea, no drawer: the camera draws in close and the jellies float side by side where Clementine
    was, each drawn as herself (a `BellView` of her own wearing her look, `BellView.Puppet`, `BellView.Wear`), drifting
    slowly about her place. Under each, a card: her name, level, shells and pearls, leaks sealed and when last played.
  - **Choosing:** pointing at a jelly or her card (or moving the focus to it) chooses her slot: she lights up, and the
    others are switched off like lamps (their light out, bell and strands dim, `BellView.Lit`). The chosen card is
    ruled in gold and holds Dive (wake her in the bay; she swims off from her place) and Delete (pressed twice). The one
    played last is chosen first. Continue on the title wakes her directly.
  - **A new jelly:** an empty slot shows a round, see-through (+) button where a jelly would float. Pressed, a new jelly
    floats into the first empty slot wearing the form in her card as it is set, live: a name unique on this machine, a
    gonad shape (horseshoe, ring, clover, crescent, star), and glow, arm and tip colours. Dive makes her and wakes her
    in the bay; Back (or Esc, B) returns to the slots. With every slot taken there is no (+): one jelly must be deleted
    first.
  - **What is kept:** level, experience, skill points, shells, passive and active pearls, and the caves sealed per
    region. It is saved on every way through, on leaving to the title, and on every seal.
  - **Her look** (`HerColor.SetLook`): her arm colour is her bell and arms; her glow is her light, shots and the glow
    she casts; her tips glow their colour (Clementine's twinkle in random hues); her gonads take their shape on her bell
    and on the HUD's gauge (`gonad_shape.gdshaderinc`).
- **The mirror** (`HandLayout.Mirror`, `PlaneWorld.AtMirror`, `MirrorView`, `MirrorMenu`):
  - **The pool:** a still pool of polished nacre on the lagoon's floor, in a low pearl rim. It mirrors the bright water
    above, thin-film nacre colours round its lip, and her own glow under her as she swims over it
    (`mirror_pool.gdshader`). Bright where a cave mouth is dark, so it never reads as the Leak's; while she is over it,
    it dims to a deep nacre so she shows clearly in it. Nothing grows on it.
  - **Swimming onto it** opens its drawer as the pause menu's opens, the camera closing in on her and the sea held. It
    opens once each time she comes onto it.
  - **The drawer:** her gonads' shape and her glow, arm and tip colours (the jelly card's own form, `LookForm`), shown on
    her as they change. Her name stays. Keep this look keeps it on her jelly and saves it; As she was (Esc, B) puts
    back the look she came with.
- **Stats from levels only** (`PlaneRun.Reef`, `Loadout.Build(pearlStats: false)`): on the open reef no pearl changes
  a stat, only what she does. The pearls that do nothing but change stats (`PlaneRun.OnlyStats`: 25 of them, such as
  Coral Crown, Dolphin Fin, Shark Tooth and Cuttlebone) are not offered there at all, and nor are actives (they come
  from key quests). Reworking the stat pearls into behaviours is still a design pass to do.
- **Three active slots** (`PlaneRun.Slots`, `OwnedActives`): an active pearl is owned, not absorbed, and goes into the
  first empty slot. Each slot has its own charge and button: F, Q and E (Y, LB and RB on a controller). The first
  slot's pearl fills her HUD bell as before; the second and third show as small pearls beside it, their charge round
  them and their key under.
- **The Pearls tab** of the pause menu, on the open reef: her three slots, the actives in her stash (owned but in no
  slot), and the passives in her bell. In Lantern Bay a slot's Change button cycles through the actives she owns
  (`CycleSlot`), and any pearl can be sold to the shell shop for silver shells (`PlaneRun.Sell`): an active for its
  worth, `20 + 20 × quality` (`ReefTuning.PearlWorth`), a passive for half. Elsewhere the tab only shows them.
- **Dimming and lost light** (`Jelly.Lost`, `PlaneLostLight`, `LostLightView`): when she dims, her passive pearls and
  the shells on her are left where she fell as her lost light, a cluster of small lights in her glow; her level,
  actives and name are kept, and she wakes at the lantern post. Swimming into it gives everything back. Dimming again
  first, it is gone (only the newest is kept). In a cave she had already sealed, it waits at that cave's mouth in the
  region instead.
- **Requests** (docs/QUESTS-PROPOSAL.md): the Tide Fan in the commune and its drawer, seeded ribbons for each region,
  two carried at a time, their progress from the caves, untying for a pearl, shells and experience, the HUD's kelp
  ribbons under the minimap (toggled in the pause menu's Requests tab), and named mouths ringed on the maps.
- **Verification starts:** `--reef=bay|shallows|cave` starts there with a jelly that is never saved (`bay` and `shallows`: the reef map, at the lantern post), `--at=x,y` sets her down at a point, `--at=mouth`
  puts her 6 m out from a region's first mouth (`--at=way`, a leak cave's way out; `--dbg-steer` swims her in), `--at=mirror` on the bay's mirror (its drawer opens), `--at=mirror-near` just
  south of it, `--at=fan` and `--at=fan-near` the same for the Tide Fan, `--dbg-requests` ties ribbons and carries two
  (the first done), `--dbg-look=gonads,glow,arms,tips` sets the mirror's form once it opens, `--dbg-lost` lays a lost
  light 5 m north of her start, and `--pause-tab=2` opens the pause menu on the Pearls tab.

**Static-map authoring.** This needs a tool. I recommend extending the cave generator page (`tools/cavegen/`) into an
editor: place chambers, tunnels and places by hand, and save to JSON that the game loads.

## 14. Weak points

- **A big rewrite of the run model.** The sim assumes a run: `PlaneRun`, `LevelPlan` and the reflection snapshot.
  The profile has to sit above it, and every level must load from the profile, not from the run.
- **Static maps lose the variety of seeds.** The repeatable content lives in the caves, so cave generation has to
  carry the replay value. Biomes need distinct cave shapes, not only palettes.
- **Quests and NPCs need writing.** The voice is in the Bible (§10). The volume is new.
- **Without stat pearls, a run has fewer kinds of find.** Behaviour pearls have to be varied enough to carry the
  loot.
