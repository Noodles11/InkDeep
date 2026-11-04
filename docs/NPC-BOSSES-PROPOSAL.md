# The bay's folk: five bosses who come home (proposal)

The second half of step 5 of docs/RPG-PROPOSAL.md (§13): named creatures, their homecoming and the key quests, built
on one rule. **Every NPC is a boss first.** Each great cave on the reef holds one of the bay's own folk, corrupted.
Freeing them brings them home to Lantern Bay, and each one who comes home opens something in the bay. Nothing here is
built. It follows the boss rules of docs/BOSS-GENERATOR.md §0 and the Theme Bible's tests (§12).

## 1. The rule

- **Bosses are the bay's folk.** On the reef, a boss waits only at the bottom of a great cave, and every one is a named
  creature of the bay. A minor cave still ends in a murkling prime (docs/REEF-WORLD.md §3). Every boss added later,
  on the second map below the shelf too, follows the same rule: a new boss is a new neighbour.
- **They are authored.** These bosses are written as Drippus Prime is (docs/CORRUPTION.md), not rolled: each has its
  own body, fight and cave, the same for every jelly.
- **Once each.** Freeing one seals its great leak for good (RPG-PROPOSAL §2.3). If she dims in the fight, the boss
  waits there, whole again, for her next try.
- **The homecoming.** The freeing plays as every boss's does (Bible §6.5): the pearl bursts white, the mutations melt,
  the drip turns gold, true colours flood the arena. Then the camera holds while the true body turns, looks at her
  and swims up and away, trailing gold. The caption reads "Grandmother Tull has gone home to Lantern Bay". Her
  cleansed pearl stays where it sat, as every boss's does.
- **In the bay.** Each has a home there and is always in it. She swims close to talk: a speech bubble with a
  portrait. Clementine never speaks; she bobs for yes and turns away for no (Bible §5.3).
- **Each one opens something.** The bay starts empty: the last clean water, with nobody left in it. The Tide Fan
  hangs bare, the mirror is clouded with silt, the shell shop's cave is dark. Each freed neighbour brings one of them
  back to life (§3). What the bay always has: the lantern post where she wakes, the homeward current, healing on
  entering, and changing which actives sit in her slots (her own body, not a service).
- **Each gives a key quest chain** that leads to the next one, a few small steps (chart a place, free a prime, seal a
  minor leak), ending at the next great cave's mouth. A chain's last step pays one active pearl (RPG-PROPOSAL §5.2).
  The chains suggest an order. The map does not enforce it: a great cave can be entered as soon as she finds its mouth.
- **Each knows a piece of what happened to the bloom.** The pieces come nearer the truth the farther out the
  neighbour was found, and none goes past the foreshadowing the Bible allows for its depth (§4.4).
- **Not fighters.** Freed folk never fight for her (RPG-PROPOSAL §11).

## 2. Where they lie

```
  0 m                        420 m                       840 m
  ┌────────────────────────────┬───────────────────────────┐
  │ KELP JUNGLE (4–6)          │ CORAL CARNIVAL (10–12)    │
  │ ③ The Still Bed            │        ⑤ The Empty Stage  │
  │   far north-west, Lv 6     │   far north-east, Lv 12   │
  │                            │                           │
  ├────────────────────────────┼───────────────────────────┤
  │ SUNLIT SHALLOWS (1–3)      │ SUNKEN GALLEON (7–9)      │
  │          ② The Cleaning    │   ④ The Captain's Hold    │
  │            Station, Lv 3 → │     in the wreck, Lv 9    │
  │ ① The Sleeping Ledge, Lv 2 │                           │
  │ ┌─────────┐                │                           │
  │ │LANTERN  │                │                           │
  │ │BAY      │                │                           │
  └─┴─────────┴────────────────┴───────────────────────────┘ 840 m
```

| # | Neighbour | Great cave | Where | Level | Floors |
|---|---|---|---|---|---|
| 1 | Grandmother Tull, green sea turtle | The Sleeping Ledge | Shallows, just north-east of the bay's gate: the first mouth she sees | 2 | 1 |
| 2 | Kip, cleaner wrasse | The Cleaning Station | Shallows' east, near the pass to the Galleon | 3 | 3 |
| 3 | Fennel, leafy seadragon | The Still Bed | Kelp Jungle's far north-west corner, deepest in the kelp | 6 | 3 |
| 4 | Ottilie, common octopus | The Captain's Hold | Sunken Galleon's middle, inside the great wreck | 9 | 3 |
| 5 | Bubbles, mandarin dragonet | The Empty Stage | Coral Carnival's far north-east corner, farthest from the bay | 12 | 3 |

- **Great caves are fixed:** a fixed mouth in `reef.json` (beside the 22 minor ones), a fixed layout and a fixed seed,
  so every jelly meets the same cave. Each floor is darker and tighter than the last, as a minor cave's is. The boss
  arena is the deepest floor's dead end, where its leak is.
- **Its mouth reads from far off:** a great mouth weeps more ink than any danger 2 mouth, and its label shows its name
  and level from 20 m.
- **The Shallows has two,** so the first boss is close and short (one floor, near the gate) and the region still has
  a proper great cave of three floors.

## 3. What each one opens

| Neighbour | Opens | What it does |
|---|---|---|
| Grandmother Tull | **The Tide Fan** | Requests (docs/QUESTS-PROPOSAL.md): she ties the bay's troubles to it. Before her the fan hangs bare. |
| Kip | **The mirror** | Changing her look (RPG-PROPOSAL §8). Kip cleans the silt off it and tends it. |
| Fennel | **The Brood Bed** | A shell keeper: shells left with him are never lost when she dims. New. |
| Ottilie | **The pearl collector** | The shell shop: buying and selling pearls (RPG-PROPOSAL §5.3). Her den is the market's cave. |
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

- **The cut-in:** CLEMENTINE VS SLEEPY OLD TULL. Warm and big, round-eyed: the Shallows' tone. She yawns.
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
- **Her chain, "Find Kip":** chart the Shallows' east; free two murkling primes there; open the Cleaning Station.
  Pays **Bubble Shield**: *"Every turtle knows: carry your house with you."*
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

- **The cut-in:** CLEMENTINE VS NINE-FIN KIP. She bobs up and down in her cleaning dance.
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
- **Her chain, "My kelp clients stopped coming":** reach the Kelp Jungle; untie one request there; free a prime in a
  Kelp cave; find the Still Bed's mouth. Pays **Whale Song**: *"Hold still. This is the good part."*
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

- **The cut-in:** CLEMENTINE VS PAPA FENNEL. He curls his tail tighter around his eggs.
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
- **Later:** after her next great cave is sealed, the eggs hatch. Dozens of tiny seadragons drift in the bay from then
  on: the bay filling up with life.
- **His chain, "The wreck has gone quiet":** reach the Galleon; seal a minor leak there; free a prime in the wreck's
  field; find the Captain's Hold. Pays **Ink Cloud**: *"Be the weed. Nobody looks at weed."*

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

- **The cut-in:** CLEMENTINE VS MANY-ARMED OTTILIE. Thinner and colder than the Shallows' cut-ins. She lifts the black
  pearl to admire it.
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
- **Opens:** the pearl collector. She buys and sells pearls: the shell shop (RPG-PROPOSAL §5.3).
- **Her chain, "My best glint":** go back to the Captain's Hold (sealed and safe now); find her old hoard on its middle
  floor; bring back the glint. She gives it to Clementine: *"I don't keep things that are someone's."* Pays **Prism
  Pearl**: *"One light, three colours. Your family's, I think."*
- **A thread to the next:** *"The brightest fish I ever saw swam through my wreck going north-east. All the wrong
  colours, darling. Or all the right ones."*

## 8. Bubbles: The Pale Dragonet

### 8.1 Who she is

- **A mandarin dragonet,** small and slow, hopping on her fins. Her natural pattern is swirls of orange and electric
  blue, so vivid the bay half-suspects she is still corrupted. Natural brilliance next to the Leak's wrong brightness.
- **Her story.** She is one of the tank's escapees, who came out already changed, and she doesn't know it. To her it's
  half a dream: *"Before the sea I lived somewhere small. The sun never moved. Food fell like snow."*
- **Her name:** the reef doesn't know her, so the cut-in can only name her species. She names herself in the bay:
  *"Bubbles. That's the sound the sky made when it looked at me."* Played straight and sad, never as a joke (the
  Never-Wink test).
- **Her voice:** dreamy and gentle, happiest at dusk. Mandarins dance at dusk, rising in pairs.
- **The bloom:** *"There were jellies there too, before me. Pale ones. They never went anywhere."* Not the bloom, but
  exactly where the bloom is now.

### 8.2 The boss

- **The cut-in:** CLEMENTINE VS THE PALE DRAGONET. The thin cold VS of the deep band. She doesn't look at Clementine at
  all: she looks up.
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
  the cut-in ends, the first muffled glass thump is heard, far off (Bible §4.4, Depth 4's first).
- **Freed:** the last sac goes, her whole pattern floods back, and she hops away on her fins, slowly, looking up.
  Caption: "The dragonet has gone home to Lantern Bay". She gives her name only in the bay.

### 8.3 In the bay

- **Home:** the lagoon's brightest corner, where the evening light falls longest.
- **Opens:** the Dusk Dance. She teaches Clementine to move anew: all her skill points come back to lay again. *"You
  don't have to keep swimming the way you learned."*
- **Her chain, "Where the reef goes down":** reach the shelf's edge beyond the Carnival; follow the water that pulls
  downward; find the way below the shelf (the second map, Twilight Trench and Abyss, when it is built). The first chain
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

## 10. What this changes elsewhere

- **The bay's built services become unlocks.** The Tide Fan, the mirror and the shell shop all exist today and work
  from the start. Under this proposal a jelly starts with all three closed; existing jellies start with nobody freed.
- **The boss roster.** On the reef, `BossRoster`'s Drippus Prime gives way to the five authored bosses. The seeded runs
  keep it.
- **New bodies.** None of the five fits a built body plan: they need a turtle, two Fish-plan rigs (wrasse, seadragon,
  dragonet) and a Cephalopod rig (BOSS-GENERATOR §3.2). Each is a new rig, the biggest cost here.
- **Bible §6.5 and §13** (freed creatures leave and are gone) already give way to RPG-PROPOSAL §7 for named creatures.

## 11. Pacing risk

**Shells have nothing to buy until the Galleon.** With the pearl collector fourth, the early reef pays in shells she
can only carry (and, after Fennel, keep). Early pearls come from requests and caves. If that feels flat in play, the
fallback is a small trade at Tull's fan: a pearl or two for shells, until Ottilie comes home.

## 12. What is built so far

- **The folk and their great caves** (`PlaneFolk.cs`: `Folk`, `Neighbour`, `BayService`): Grandmother Tull, Kip,
  Fennel and Ottilie are in the game. A great mouth names its neighbour (`HandPoint.Folk`) and comes after every minor mouth in `reef.json`
  (`OpenReef.MinorMouths`: the Tide Fan names only those). Its cave's level and floors are hers (`OpenReef.CaveLevel`,
  `CaveFloors`), its number is its own past every minor cave's (`OpenReef.GreatCave`), it is grown from the region's
  seed, and its deepest floor is a boss's arena round its leak (`LevelPlan.ForCave`'s `boss`).
- **The arena:** its walls stand open (its only leak is inside) and rise behind her only while its boss fights
  (`PlaneWorld.PlaceSeals`). No prime waits by the leak and no murkling buds in the cave while the fight runs. The
  boss lies over the leak: it cannot be sealed till she is freed. Sealed, the great cave is closed for good
  (`OpenReef.SealedGreat`, `Jelly.GreatLeaks`, `OpenReef.ShutMouths`).
- **Her boss** (`FolkBosses`): authored, its meter `700 × (1 + 0.25 × (level − 1))`. Its acts are written out by phase
  (`BossSpec.Acts`); it holds still while it warns and acts (`BossSpec.ActsStill`). Tull: the flipper sweep (a fan of
  five big drops at where she last felt Clementine), then the slam (a ring of sixteen drops with a gap of three), then
  the sigh (her throat open for 2 s); from half her meter, a seam cracks and the slams come in pairs. Her body is
  `TurtleRig`; her cut-in yawns (`BossIntroArt.Turtle`).
- **Kip** (The Cleaning Station, east of the spur, three floors, level 3): she keeps to her station in the arena's middle
  (`BossMove.Station`), flicks a ring of fourteen drops with a gap of three, then darts 14 m along her aim at 16 m/s
  (`BossAttack.Dart`) and waits side-on for 1.4 s, the pearl on her flank turned toward Clementine. Past half her
  meter her rests quicken, she darts twice, and six of her old clients come, blue tangs turned to lesser murklings
  (`BossSpec.Clients`). Her body is `FishRig`: it bobs at her station, tucks its fins on the dart's warning, and rolls
  onto its side in the wait. Her cut-in dances (`BossIntroArt.Wrasse`).
- **Fennel** (The Still Bed, past the moss bank in the Kelp Jungle's north-west corner, three floors, level 6): he
  drifts between five stands of kelp round the arena (`BossMove.Hide`, `PlaneBoss.Stands`) and hides in one, half
  seen, his drip rising out of it. She finds him by swimming within 5 m or when her light reaches him (no harm to
  him); then he lashes nine drops across 90° at where he last felt her (`BossSpec.SweepCount`, `SweepArc`) and his
  chest is open for 1.6 s, before he drifts to another stand. Left unfound for 6 s, he strikes from hiding with the
  tangle, a wall of fourteen drops with a gap of two, and opens nothing after. From half his meter two other stands
  drip too (`PlaneBoss.Decoys`). His body is `FishRig`'s seadragon: lobed fronds that sway, stiffen and rattle on the
  lash's warning, his pink eggs under his tail in their own colour; the stands are `BossView.Stands`. His cut-in curls
  his tail round his eggs (`BossIntroArt.Seadragon`).
- **Ottilie** (The Captain's Hold, east of the keel in the wreck's field, three floors, level 9): her pearl is held
  behind her (`BossSpec.Shielded`): she turns at 0.5 rad/s to keep her body between it and Clementine, and a shot
  coming through her body to it is stopped, so circling round her finds it. She hurls three glints of ink at her (arc
  bombs), then two arms shoot 12 m out along lines lit on the floor through the second before (`BossAttack.Reach`,
  `PlaneBoss.ReachDirs`); after each act she lifts the pearl up to admire it for 1.2 s, when it can be struck from
  anywhere. From half her meter an ink bloom joins: a cloud 7 m across toward her for 5 s (`BossAttack.Bloom`,
  `PlaneBoss.Blooms`). Her body is `OctopusRig` (eight segmented arms that sway, curl back on the throw's warning and
  reach along the lines, six thinner inked ones crowding between, ink sacs at her back); the lit lines and the clouds
  are `BossView.Reach`; her cut-in lifts her treasure (`BossIntroArt.Octopus`).
- **The homecoming:** freed, she swims off, and the caption reads "Grandmother Tull has gone home to Lantern Bay"
  (`Jelly.Freed`).
- **At home** (`FolkView`, `FolkTalk`, `FolkPortrait`; `HandLayout.Homes`): her true body at her home by the Tide Fan,
  turning to watch Clementine; within 5 m, a speech bubble with her portrait, her name and her next line.
- **The bay's services** (`Folk.Opens`): the Tide Fan hangs bare, ties no ribbons and opens no drawer till Tull is
  home; silt lies over the mirror, which opens no drawer, till Kip is home (beside it). Fennel's weed bed, in the
  lagoon's quiet corner, is the Brood Bed: within 2.2 m of it its drawer opens (`FolkMenu`), where she leaves the
  shells she carries with him or takes them back (`BroodBed`, `Jelly.Banked`: never lost when she dims). The shell
  shop's cave is Ottilie's home: till she is in it the shop's stands are empty (`PlaneOptions.ShopShut`) and the
  Pearls tab sells nothing (`PauseMenu.Sells`), though her slots can still be changed in the bay.
- **Verification:** `--reef=cave:22` starts in Tull's cave, and `--reef=cave:23`, `24` and `25` with `--floor=3` on
  Kip's, Fennel's and Ottilie's deepest floors (`--at=boss-near` near the boss); `--dbg-freed=tull,kip,fennel,ottilie`
  has them freed and home, and `--reef=bay --at=103,775`, `--at=57,777.5`, `--at=81,785.5` or `--at=28,782` sets
  Clementine down by Tull, Kip, Fennel or Ottilie (`--at=81,782` on Fennel's bed, its drawer open). Tests:
  `FolkTests`.
