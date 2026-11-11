# The bay's folk: five neighbours who come home

The five neighbours of Lantern Bay, as the Leak made them and as they are at home. **Every neighbour is a boss first:**
each is the boss of one depth of the dive (docs/DIVE.md §5) until she is freed; freed, she goes home to Lantern Bay (the
havens, docs/DIVE.md §4) and opens something there. This page holds each one's design; §12 is how they are built. They
keep the boss rules of docs/BOSS-GENERATOR.md §0 and the Theme Bible's tests (§12).

## 1. The rule

- **Bosses are the bay's folk.** Each depth of the dive ends in its neighbour's boss fight, till she is freed. Every
  boss added later follows the same rule: a new boss is a new neighbour.
- **They are authored**, as Drippus Prime is (docs/CORRUPTION.md), not rolled: each has its own body and fight, the
  same for every jelly.
- **Once each.** Freeing one saves her as freed on the jelly for good; from then on her depth's boss is the Leak's own,
  Drippus Prime. If Clementine dims in the fight, the neighbour waits there, whole again, for her next dive.
- **The homecoming.** The freeing plays as every boss's does (Bible §6.5): the pearl bursts white, the mutations melt,
  the drip turns gold, true colours flood the arena. Then the camera holds while the true body turns, looks at her
  and swims up and away, trailing gold. The caption reads "Grandmother Tull has gone home to Lantern Bay". Her
  cleansed pearl stays where it sat, as every boss's does.
- **In the haven.** Each has a home there and is always in it. She swims close to talk: a speech bubble with a
  portrait. Clementine never speaks; she bobs for yes and turns away for no (Bible §5.3).
- **Each one opens something.** Lantern Bay starts empty: the last clean water, with nobody left in it. The Tide Fan
  hangs bare, the mirror is clouded with silt, the shell shop's cave is dark. Each freed neighbour brings one of them
  back to life (§3). What a haven always has: healing her whole and banking what she carries (docs/DIVE.md §4).
- **Each gives a key quest chain**, a few small steps naming depths (dive to a depth, free primes or seal leaks in it,
  untie a request, find something in a leak cave, bring it home), its last step paying an active pearl
  (docs/DIVE.md §7).
- **Each knows a piece of what happened to the bloom,** and of the old tale of the jellyfish's errand (Bible §4). The
  pieces come nearer the truth the deeper the neighbour was found, and none goes past the foreshadowing the Bible
  allows for its depth (§4.4).
- **Not fighters.** Freed folk never fight for her.

## 2. Where they lie

| Depth | Reef | Neighbour | Her arena | Her level |
|---|---|---|---|---|
| 1 | Kelp Jungle | Grandmother Tull, green sea turtle | The Sleeping Ledge | 2 |
| 2 | Sunken Galleon | Kip, cleaner wrasse | The Cleaning Station | 3 |
| 3 | Coral Carnival | Fennel, leafy seadragon | The Still Bed | 6 |
| 4 | Twilight Trench | Ottilie, common octopus | The Captain's Hold | 9 |
| 5 | The Abyss | Bubbles, mandarin dragonet | The Empty Stage | 12 |

Each fight is in the arena of her depth's boss reef, round the Crack (`Dives.FolkBossOf`, `FolkBosses`). The arenas'
names and looks below are their design; as built, each fight is in her depth's boss arena.

## 3. What each one opens

| Neighbour | Opens | What it does |
|---|---|---|
| Grandmother Tull | **The Tide Fan** | Requests (docs/DIVE.md §7): she ties the reef's troubles to it. Before her the fan hangs bare. |
| Kip | **The mirror** | Changing her look (docs/GAME.md §9). Kip cleans the silt off it and tends it. |
| Fennel | **The Brood Bed** | A shell keeper: shells left with him are never lost when she dims. New. |
| Ottilie | **The pearl collector** | The shell shop: buying pearls. Her den is the market's cave. |
| Bubbles | **The Dusk Dance** | Re-laying her skill points: Bubbles teaches her to move anew. New. |

---

## 4. Grandmother Tull: Sleepy Old Tull

### 4.1 Who she is

- **A green sea turtle,** over a hundred years old, slow and mossy. Her shell carries a garden of barnacles and algae,
  with a few tiny shrimp living in it: the healthy kind of overgrowth.
- **Her story.** She has swum the whole ocean and came back to this bay every year. This year she stayed. She was
  grazing by the bay's gate when the current took the bloom, all of them in one breath, and she is far too wide to
  follow anyone into the Crack. The ink found her sleeping under her ledge that night.
- **Her voice:** unhurried, fond and dry. She calls Clementine "little lamp".
- **The bloom:** she saw the current take them.

### 4.2 The boss


- **Body:** the turtle 5 m long, her flippers 2 m. True form 3.2 m.
- **Mutation, an overgrown shell:** plate upon plate piled past her, crusted over her garden, so heavy she can barely
  lift it. Ink stains spread from the plates' seams; her flippers and head are drained to ash. The drip rises from the
  seams.
- **Her pearl:** at her throat, under the shell's front lip. Shut away while her head is drawn in.
- **Concept, Slam and Sigh:** "dodge the slam, punish the rest". Exposure: *stagger*. After each slam she sighs,
  stretching her head out under the lip for 2 s, and the pearl shows.
- **Senses:** *vibration*. Half asleep, she feels the water and aims where Clementine last fired or dashed.
- **Movement:** she creeps toward her at 1.2 m/s and turns slowly.
- **Attacks:**

  | Attack | The tell | What it does |
  |---|---|---|
  | **Shell slam** (signature) | 1.0 s: she draws in her flippers and head and lifts the shell | She drops it: a landing ring of ink drops with a 3-drop gap, and a shove of water. The sigh follows. |
  | **Flipper sweep** | 0.9 s: a front flipper draws back | A fan of 5 drops across 60°, from that side only. |

- **Phases (2):** at 50% the overgrowth cracks open along one seam and the slams come in pairs, the sigh after the
  second.
- **The arena:** a seagrass meadow under a long overhanging ledge, its canopy over the north half. The grass is ash
  until it is freed.
- **Freed:** the plates melt back into one shell, her garden comes back green, the shrimp scatter and return. She
  swims up and away more slowly than anything in the game.

### 4.3 In the bay

- **Home:** the commune, her shell against the Tide Fan's foot.
- **Opens:** the Tide Fan's requests. She ties the ribbons with her beak.
- **Her chain, "Find Kip":** dive down to Depth 2; free two murkling primes there; find Kip's lost comb in a Depth 2
  leak cave. Pays **Bubble Shield**: *"Every turtle knows: carry your house with you."*
- **Lines:**
  - *"I watched your whole bloom go down in one breath. I'm too wide for the Crack, little lamp. You aren't."*
  - *"You found me asleep under all that. Don't tell the others how long it took."*
- **Later (after the Carnival's cave is sealed):** turtles breathe air, so Tull has been to the surface.
  *"At night the shore has lights that don't move like stars. I never liked them."*

## 5. Kip: Nine-Fin Kip

### 5.1 Who she is

- **A cleaner wrasse,** slim, black with an electric-blue stripe. She ran the busiest cleaning station on the reef and
  never met a stain she couldn't pick off.
- **Her story.** The ink was the first stain she couldn't clean. A murkling came to her station, and she tried. Her
  clients stopped coming after that, all but the corrupted ones.
- **Her voice:** quick, fussy, kind underneath. She is ashamed of what she did as a boss and has turned it into fussing
  over Clementine.
- **The bloom:** she groomed them the morning before. She remembers every sibling by the colour of their rim, and she
  names the one who glowed the player's own chosen colour.

### 5.2 The boss


- **Body:** the wrasse 4 m long. True form 2.4 m.
- **Mutation, extra fins:** fins crowding along her back and belly, each sprouting another, so she is a ragged fan of
  them. Ink spreads from her blue stripe; the rest of her is ash. The drip rises from the fins' roots.
- **Her pearl:** on her stripe, at mid-body. It shows only from the side.
- **Concept, the cleaning dance (new, a Fish plan's):** "make her overshoot, then hit her side". Exposure: *open
  moments*. After each dart she overshoots and turns side-on for 1.4 s, as if waiting for a client to hold still.
- **Senses:** *water*. She aims at Clementine.
- **Movement:** she bobs over her station in the arena's middle, darts out and circles back.
- **Attacks:**

  | Attack | The tell | What it does |
  |---|---|---|
  | **The dart** (signature) | 0.9 s: the bobbing stops, she tucks her fins, her stripe brightens | A straight charge along her aim, 14 m. Contact harm. Then the side-on window. |
  | **Fin flick** | 0.7 s: the extra fins rattle and flare | A ring of 14 drops from her fins, with a 3-drop gap. |

- **Phases (2):** at 50% she darts twice in a row, and her **queue** comes: six lesser murklings, the fish she used to
  clean, leave the station to cling to Clementine. Each is freed by one shot and swims home.
- **The arena:** her old station, a round coral head in a bright chamber. The coral is ash until she is freed.
- **Freed:** her fins melt back to a wrasse's, her stripe flashes blue, and she darts off, then comes back once to look
  at Clementine before she goes.

### 5.3 In the bay

- **Home:** the lagoon, beside the mirror.
- **Opens:** the mirror. She cleans the silt off it, and she is there each time Clementine uses it.
- **Her chain, "My kelp clients stopped coming":** untie a request at the Tide Fan; dive down to Depth 3; free a
  murkling prime there. Pays **Whale Song**: *"Hold still. This is the good part."*
- **Lines:**
  - *"Hold still. You've got Leak in your frills. …There. Family colour, all the way through."*
  - *"Your sister glowed just like that. I did her rim twice, she was so proud of it."*

## 6. Fennel: Papa Fennel

### 6.1 Who he is

- **A leafy seadragon,** his body grown all over with leaf-shaped fronds, so he drifts like a loose piece of weed. In
  seadragons the father carries the eggs: Fennel has a clutch of pink eggs on his tail.
- **His story.** The Leak reached him while he was carrying his brood. He spent the whole corruption curled around the
  eggs, and the ink never touched them.
- **His voice:** whispery, shy, easily startled. He hides when she swims at him too fast.
- **The bloom:** he saw them pass through the kelp. *"In a line, all facing down. None of them were pulsing. You don't
  drift like that. Something was drinking them."*

### 6.2 The boss


- **Body:** the seadragon 5 m long. True form 3 m. The eggs glow faintly pink on his tail, untouched.
- **Mutation, fronds grown inward:** his leafy fronds have hardened into thorns curling back into his body. Ink stains
  spread from their roots; the rest is ash. The drip rises from the thorns.
- **His pearl:** at his chest, behind the thorns. It shows when he unfurls.
- **Concept, the hidden one (new):** "find him, light him, then strike". He hides among the arena's kelp, as still as
  the weed. **The Leak gives him away:** his drip rises from the kelp where he is. When her light reaches him, he
  unfurls. Exposure: *stagger*. After his thorn lash his chest is open for 1.6 s.
- **Senses:** *vibration*. Firing gives her away too.
- **Movement:** he drifts from one stand of kelp to another while hidden, slowly, and holds still to strike.
- **Attacks:**

  | Attack | The tell | What it does |
  |---|---|---|
  | **Thorn lash** (signature) | 1.0 s: his fronds stiffen and rattle | A sweeping arc of drops across 90° toward where she last fired. The window follows. |
  | **The tangle** | 1.1 s: the kelp round him shivers toward him | Kelp in the arena closes into thorned walls of ink with gaps 2 wide. They slow her and last 6 s. |

- **Phases (2):** at 50% two other stands of kelp begin to drip as well (decoys). Only one holds him; her light shows
  which.
- **The arena:** a dense ring of kelp in a dim chamber, the shafts of light broken by its canopy.
- **Freed:** his thorns soften back into leaves, gold-green, and he drifts up like a loose leaf with the eggs safe on
  his tail.

### 6.3 In the bay

- **Home:** a quiet weed bed in the lagoon's far corner.
- **Opens:** the Brood Bed. She can leave shells with him, and those are never lost when she dims: *"I'll keep them the
  way I keep these."* The shells she carries are lost as today (her lost light).
- **Later:** once the next neighbour is freed, the eggs hatch. Dozens of tiny seadragons drift in the bay from then
  on: the bay filling up with life.
- **His chain, "The wreck has gone quiet":** dive down to Depth 4; seal a leak there; free a murkling prime there.
  Pays **Ink Cloud**: *"Be the weed. Nobody looks at weed."*

## 7. Ottilie: Many-Armed Ottilie

### 7.1 Who she is

- **A common octopus,** warm red-brown, her skin rippling through colours as she talks. She is a collector, as real
  octopuses are: shells, sea glass, brass glints from the wreck.
- **Her story.** The Leak's droplets glittered, so she collected those too. Her hoard rewrote her from inside.
- **Her voice:** grand, theatrical and a little vain, with eight arms that each have an opinion.
- **Ink:** she inks herself, and is fascinated that Clementine does too. *"Real ink is brown and honest. That stuff out
  there is ink pretending."*
- **The bloom:** in the wreck she found a glint in the family colour, a ring of glow caught on the timbers. Her chain
  gives it back (§7.3).

### 7.2 The boss


- **Body:** the octopus 5.5 m across her arms. True form 3.3 m.
- **Mutation, extra arms:** fourteen arms crowding over each other, ink sacs swelling between them. Ink stains spread
  from the arms' roots; the rest of her is ash. The drip rises from the sacs.
- **Her pearl:** she holds it. It is the best piece of her hoard, cupped in her arms.
- **Concept, the treasure (new, a Cephalopod plan's):** "get round to the pearl". Exposure: *moving window*. She passes
  the pearl from arm to arm, keeping her body between it and Clementine (she turns at most 1.2 rad/s, so flanking
  works). After each throw she lifts it up to admire it for 1.2 s, wherever it is.
- **Senses:** *water*.
- **Movement:** she turns in her den at the arena's back wall. From phase 2 she draws back into it between attacks.
- **Attacks:**

  | Attack | The tell | What it does |
  |---|---|---|
  | **Hoard throw** (signature) | 0.8 s: three arms curl back, their glints catching the light | Arc bombs: 3 glints of Leak ink, each landing in a ring of drops. The admiring pause follows. |
  | **The reach** | 0.7 s: suckers light up along a line on the floor | Two arms shoot along the lit lines across the arena; contact harm. |
  | **Ink bloom** (from phase 2) | 1.0 s: the sacs swell | A cloud of Leak ink fills a quarter of the arena for 5 s, dimming it. Her drops come out of it unseen until they leave it. |

- **Phases (2):** at 50% the ink bloom joins, and she retreats into the den between attacks, reaching out of it.
- **The arena:** the wreck's flooded hold, her den a hole in its back wall, her hoard heaped round it.
- **Freed:** fourteen arms melt back to eight, and her skin flashes through every colour she can make before it settles
  on red-brown. She jets off in a puff of her own brown ink.

### 7.3 In the bay

- **Home:** the market's cave, heaped with her hoard.
- **Opens:** the pearl collector. She sells pearls: the shell shop.
- **Her chain, "My best glint":** find her old hoard in a Depth 5 leak cave; bring her glint to a haven. Pays **Prism
  Pearl**: *"One light, three colours. Your family's, I think."*
- **A thread to the next:** *"The brightest fish I ever saw swam through my wreck going north-east. All the wrong
  colours, darling. Or all the right ones."*

## 8. Bubbles: The Pale Dragonet

### 8.1 Who she is

- **A mandarin dragonet,** small and slow, hopping on her fins. Her natural pattern is swirls of orange and electric
  blue, so vivid the bay half-suspects she is still corrupted. Natural brilliance next to the Leak's wrong brightness.
- **Her story.** She is one of the tank's escapees, who came out already changed, and she doesn't know it. To her it's
  half a dream: *"Before the sea I lived somewhere small. The sun never moved. Food fell like snow."*
- **Her name:** the reef doesn't know her, so it can only name her species. She names herself in the bay:
  *"Bubbles. That's the sound the sky made when it looked at me."* Played straight and sad, never as a joke (the
  Never-Wink test).
- **Her voice:** dreamy and gentle, happiest at dusk. Mandarins dance at dusk, rising in pairs.
- **The bloom:** *"There were jellies there too, before me. Pale ones. They never went anywhere."* Not the bloom, but
  exactly where the bloom is now.

### 8.2 The boss


- **Body:** the dragonet 4.5 m long, her fins spread to 5 m. True form 2.7 m.
- **Mutation, ink sacs where her colours were:** her whole pattern is drowned. Each swirl of it has swollen into a
  glossy ink sac; between them she is pale ash. The drip rises from the sacs.
- **Her pearl:** buried at her heart, under the sacs.
- **Concept, Boil Heart:** "burst the boils to reach the heart" (BOSS-GENERATOR §4). Exposure: *coax out*. Five sacs,
  each a small meter. **Each sac burst gives back one swirl of her true colour,** so the fight restores her pattern
  piece by piece. With all burst, the pearl surfaces.
- **Senses:** *blind to her*. She dances on her own cycle for the light above, so the fight is about where Clementine
  is when each move comes.
- **Movement:** she hops slowly round the arena's middle.
- **Attacks:**

  | Attack | The tell | What it does |
  |---|---|---|
  | **The dusk rise** (signature) | 1.0 s: she lifts off the plane, her shadow shrinking under her | She comes down where her shadow is: a landing ring of drops, gap 3 wide. No camera move. |
  | **Stage lights** | 0.8 s: the arena's neon flickers and one sac swells | That sac fires a ring; the sacs fire in turn round her. |
  | **Fin swirl** | 0.7 s: her fins fan out | A spiral of drops as she turns for 2 s. |

- **Phases (3):** at 33% and 66% the sacs grow back, one fewer each time (Boil Heart's rule), her restored swirls
  staying where they were.
- **The arena:** a round stage ringed by tall reef, its neon flickering like footlights with no sun behind them. As
  her battle begins, the first muffled glass thump is heard, far off (Bible §4.4).
- **Freed:** the last sac goes, her whole pattern floods back, and she hops away on her fins, slowly, looking up.
  Caption: "The dragonet has gone home to Lantern Bay". She gives her name only in the bay.

### 8.3 In the bay

- **Home:** the lagoon's brightest corner, where the evening light falls longest.
- **Opens:** the Dusk Dance. She teaches Clementine to move anew: all her skill points come back to lay again. *"You
  don't have to keep swimming the way you learned."*
- **Her chain, "Where the reef goes down":** dive on past Depth 5, where the reef goes down again. The first chain
  that points down toward the Crack. Pays **Conch Horn**: *"The sea answers. It always did, down here."*

---

## 9. Theme tests

| Test | How the five pass it |
|---|---|
| No-Villain | Each was a neighbour doing what it always did when the Leak got in. |
| Read-at-Range | Every boss carries all three tells: one dominant mutation, the ink look, the drip. Fennel's whole fight turns on the drip giving him away. |
| Freeing | Each melts back and goes home; Bubbles' fight is a freeing in five steps. |
| Warmth | None glows; Bubbles' colours are pigment, not light. |
| Never-Wink | The tank shows only in Bubbles' half-dreams and Tull's late line, both at Depth 4 or after, played straight. |
| Two-Breaks | Only the boss-awakening dip. The homecoming holds the camera still. |
| Sadness | The bay starts empty and fills one neighbour at a time; the folk get stranger and sadder the farther out. |

## 10. How it is built

- **The folk** (`PlaneFolk.cs`: `Folk`, `Neighbour`, `BayService`): all five are in the game, in depth order
  (`Folk.All`). A depth's boss reef holds its neighbour till she is freed (`Dives.FolkBossOf`, `TopDownMain.DiveBoss`),
  then Drippus Prime.
- **The arena** is the boss reef's, round the Crack: its walls rise behind her while the fight runs. **The boss is the
  reef's leak**: freeing her seals it, with all a seal brings (its experience, the ink and ash dissolving, its
  upwelling where she lay, the holes opening). Her pearl and her shells land at least 3 m from the upwelling, so
  taking them never carries her out (`AdventureTuning.ShellsClear`).
- **Her boss** (`FolkBosses`): authored, its meter `700 × (1 + 0.25 × (level − 1))`. Its acts are written out by phase
  (`BossSpec.Acts`); it holds still while it warns and acts (`BossSpec.ActsStill`). Tull: the flipper sweep (a fan of
  five big drops at where she last felt Clementine), then the slam (a ring of sixteen drops with a gap of three), then
  the sigh (her throat open for 2 s); from half her meter, a seam cracks and the slams come in pairs. Her body is
  `TurtleRig`.
- **Kip** (Depth 2, level 3): she keeps to her station in the arena's middle
  (`BossMove.Station`), flicks a ring of fourteen drops with a gap of three, then darts 14 m along her aim at 16 m/s
  (`BossAttack.Dart`) and waits side-on for 1.4 s, the pearl on her flank turned toward Clementine. Past half her
  meter her rests quicken, she darts twice, and six of her old clients come, blue tangs turned to lesser murklings
  (`BossSpec.Clients`). Her body is `FishRig`: it bobs at her station, tucks its fins on the dart's warning, and rolls
  onto its side in the wait.
- **Fennel** (Depth 3, level 6): he
  drifts between five stands of kelp round the arena (`BossMove.Hide`, `PlaneBoss.Stands`) and hides in one, half
  seen, his drip rising out of it. She finds him by swimming within 5 m or when her light reaches him (no harm to
  him); then he lashes nine drops across 90° at where he last felt her (`BossSpec.SweepCount`, `SweepArc`) and his
  chest is open for 1.6 s, before he drifts to another stand. Left unfound for 6 s, he strikes from hiding with the
  tangle, a wall of fourteen drops with a gap of two, and opens nothing after. From half his meter two other stands
  drip too (`PlaneBoss.Decoys`). His body is `FishRig`'s seadragon: lobed fronds that sway, stiffen and rattle on the
  lash's warning, his pink eggs under his tail in their own colour; the stands are `BossView.Stands`.
- **Ottilie** (Depth 4, level 9): her pearl is held
  behind her (`BossSpec.Shielded`): she turns at 0.5 rad/s to keep her body between it and Clementine, and a shot
  coming through her body to it is stopped, so circling round her finds it. She hurls three glints of ink at her (arc
  bombs), then two arms shoot 12 m out along lines lit on the floor through the second before (`BossAttack.Reach`,
  `PlaneBoss.ReachDirs`); after each act she lifts the pearl up to admire it for 1.2 s, when it can be struck from
  anywhere. From half her meter an ink bloom joins: a cloud 7 m across toward her for 5 s (`BossAttack.Bloom`,
  `PlaneBoss.Blooms`). Her body is `OctopusRig` (eight segmented arms that sway, curl back on the throw's warning and
  reach along the lines, six thinner inked ones crowding between, ink sacs at her back); the lit lines and the clouds
  are `BossView.Reach`.
- **Bubbles** (Depth 5, level 12):
  Boil Heart. Five ink sacs where her swirls were, each a small meter; her pearl is buried at her heart till all are
  burst. Blind to Clementine, she circles slowly; her dusk rise lifts her off the plane through its warning (not solid,
  her light passing under her) to come down where her shadow is, in a ring of eighteen drops (`BossAttack.Rise`,
  `PlaneBoss.RiseTo`), then her sacs fire their rings. A spiral joins at a third of her meter; at each third her sacs
  grow back, one fewer each time. Her body is `FishRig`'s dragonet: great fan fins, a sail, her sacs riding on her;
  each sac burst gives back a swirl of her true orange and blue, and it stays. The reef calls her "the dragonet"
  (`Neighbour.Called`) till she names herself in the bay.
- **The homecoming:** freed, she swims off, and the caption reads "Grandmother Tull has gone home to Lantern Bay"
  (`Jelly.Freed`).
- **At home** (`FolkView`, `FolkTalk`, `FolkPortrait`; `HandLayout.Homes`): her true body at her home by the Tide Fan,
  turning to watch Clementine; within 5 m, a speech bubble with her portrait, her name and her next line.
- **The bay's services** (`Folk.Opens`): the Tide Fan hangs bare, ties no ribbons and opens no drawer till Tull is
  home; silt lies over the mirror, which opens no drawer, till Kip is home (beside it). Fennel's weed bed, in the
  lagoon's quiet corner, is the Brood Bed: within 2.2 m of it its drawer opens (`FolkMenu`), where she leaves the
  shells she carries with him or takes them back (`BroodBed`, `Jelly.Banked`: never lost when she dims). The shell
  shop's cave is Ottilie's home: till she is in it the shop's stands are empty (`PlaneOptions.ShopShut`) and the
  Pearls tab sells nothing (`PauseMenu.Sells`), though her slots can still be changed in the bay. Bubbles's corner of
  the lagoon is the Dusk Dance: its drawer gives back every skill point spent, to lay again (`PlaneRun.Relearn`).
- **The key quest chains** (`PlaneChains.cs`: `Chains`, `Jelly.Chains`): each freed neighbour's begins once she is home,
  its steps in turn (as in §4.3–§8.3), shown as gold kelp ribbons on the HUD; the last pays her active pearl with her
  line for it. Steps name depths, never places (`Chains.DepthZone`, `CycleZone`).
- **Tull's late line** (`Neighbour.Late`): once Bubbles is home, the shore's lights that don't move like stars.
- **Verification:** `--dbg-dive --depth=N --level=5 --at=boss-near` starts by a depth's boss (code depths count the
  tutorial as 1: Tull's is `--depth=2`, Bubbles's `--depth=6`); `--dbg-haven --dbg-freed=tull,kip,fennel,ottilie,bubbles`
  starts in a haven with them home. Tests: `FolkTests`.
