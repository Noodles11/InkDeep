# Ink Deep — the road to a masterpiece

A review of the whole project as it stands at version 0.26.0 (branch `m6-redefine`): the code, the documents, the
ideas, the lore, the look and the tooling, measured against one target — an underwater roguelite people play for
hundreds of hours. It lists what is strong, what is weak, what is missing, a story proposal rooted in real folklore,
and the order of work.

**How this was judged.** From the source (about 75,000 lines of C#, 84 shaders), the 29 design documents, the item and
level data, the 667 tests and the verification captures taken while building. Not from long play sessions: every
claim about *feel* below is an inference and should be checked with playtests (§9, step 1).

---

## 1. The verdict in one page

Ink Deep already has what most roguelites never find: **one idea that everything hangs from.** She is a small warm
light in the dark; light is her weapon, her health, her sight and her story. The sim is deterministic and well
tested, the reef generator is deep, the boss rules are original, and the Theme Bible is a better guideline than most
shipped games have.

What stands between it and the target is not quality of parts. It is three things:

1. **The game has changed its shape six times in six weeks** (2D → 3D → top-down → adventure → open-reef RPG → the
   dive → light-as-life). Each pivot left systems, documents and pearls half-converted. The next step is to *stop
   pivoting and close the loop*.
2. **The build engine is a third built.** 41 of 72 pearls work, 8 of 20 synergies, 0 of 5 transformations, no status
   effects. Hundreds of hours come from item combinations, and that is where the game is thinnest.
3. **The screen is beautiful and hard to read.** The captures show a rich, saturated, soft-focus reef in which a
   1 m jellyfish and her invisible foes are the smallest, quietest things. The brief asks for the opposite order.

Fix those three, add co-op and a tide-clock run structure (§6), and give the lore its folklore roots (§5), and this
is a game with its own face.

---

## 2. What is strong (keep, protect, build on)

| # | Strength | Why it matters |
|---|---|---|
| 1 | **Light is the one system.** Her aura and beam reveal the corrupted, free them, seal leaks, charge stones and are her HP; murklings drink it. | A single verb with many uses is what makes a game feel like itself. Nothing else in the genre plays like "your health bar is your flashlight". |
| 2 | **Freed, not killed.** Every foe is a neighbour turned; bosses hold one corrupted pearl that is their only weak spot; the freed folk come home and open services. | Combat with a moral shape, and bosses that are puzzles of *finding the pearl*, not sponges. Meta-progression that is people, not menus. |
| 3 | **The bell is the build sheet.** Pearls are absorbed for good, shown on her body; a pearl's colour and engraving are derived from what it does (`PearlKey`). | Readable builds, streamable runs, and a rule ("looks like what it does") that scales to hundreds of items without an artist. |
| 4 | **The Theme Bible and its ten tests.** | A written taste. It has already kept the game coherent through six pivots. |
| 5 | **A deterministic core, apart from the engine.** `src/Core` has no Godot in it; 667 tests in about 50 s; seeded sub-streams per generation stage; versioned saves and exact mid-level snapshots. | Seeded runs, daily dives, replays and **online co-op by lockstep or rollback** are all within reach because of this one decision (§6.4). |
| 6 | **The generator.** Paths first, caves, strata, mazes, level templates ("what a level is about"), a validator, a reef director that acts without her, a dive graph with choices at every hole. | Variety with intent. Levels have a subject, not just a layout. |
| 7 | **Authored folk bosses on top of a boss generator.** Five neighbours with distinct fights (Fennel hiding in kelp, Ottilie shielding her pearl), plus rolled bosses under hard fairness budgets. | Hand-made peaks and endless valleys: the right mix for long life. |
| 8 | **Juice already in place.** The absorbing ritual, synergy and achievement banners, the shell's flight, the freeing burst, haptics, the dive between levels. | The dopamine vocabulary exists; it needs more occasions, not invention. |
| 9 | **Verification culture.** 89 `--dbg-` flags, frame capture, design-first for creatures, documents that describe the present. | Changes can be proved, which is why the game is not full of regressions. |

---

## 3. What is weak, and how to fix it

### 3.1 Direction: too many games in one repository

- **Symptom.** Three stacked design documents inherit from one another (DESIGN-2D → 3D → TOPDOWN, 3,000 lines);
  fourteen files are still called "PROPOSAL" though most are built; the RPG proposal is half-replaced by DIVE.md.
- **Contradictions found** (each needs one decision):
  - The Bible tells of **seven depths and The Hand**; the dive has **five** depths and loops; `LevelPlan.Depths` is 6.
  - The Bible's HUD has "no health bar" and CLAUDE.md forbids red and green in the UI; creatures now wear
    **green, yellow and red bars** (0.24.0).
  - The Bible's boss intros have "round eyes, smiles"; CLAUDE.md forbids cartoon faces.
  - The Bible speaks of bubble ammo orbiting her bell; she now shines a beam.
  - Bible §13: freed creatures "never help" and there is no hub; the havens and the folk's services are a hub.
  - Experience: DESIGN-TOPDOWN pays for cleansing ground; the RPG proposal says "only from leaks".
- **Fix.** One week of no features:
  1. Write **`docs/GAME.md`**, the single current design (20 pages at most), from what is built.
  2. Move the three old design documents and the superseded proposals to `docs/archive/`; rename the built
     proposals to what they describe.
  3. Revise the Theme Bible to the game that exists (beam, havens, folk, five-or-seven depths decided).
  4. Freeze the shape: **no new pivot until the vertical slice in §9 has been played by strangers.**

### 3.2 The build engine is thin where it must be deep

- **Symptom.** 31 designed pearls are not in play; the built synergies all hang on two pearls (Firefly Squid, Pearl
  Diver); freeze, burn, poison, charm and slow do nothing yet; transformations are a table. Several stats were named
  for shots and now mean something else on a beam ("shot size", "shot speed" as "carry").
- **Why it matters most.** A 300-hour roguelite is a combinatorics machine. The player must meet, on run 80, a
  combination they have never seen.
- **Fix.**
  - **Re-found every pearl on light.** Give the catalogue a small grammar of light verbs — *split, bend, linger,
    bounce, tint, pulse, focus, store* — and make each pearl one verb or one stat. Rename the stats to what they are
    (tick rate, carry, width, pressure). Drop pearls that only made sense for bullets.
  - **Systemic synergies before named ones.** If verbs compose by rule (a split beam that bounces *is* a lattice),
    combinations appear without a table. Keep named synergies for the dozen that deserve a banner.
  - **Use the hue system already built** (prism stones, `PrismHue`): tinted light is the natural home for status
    effects — amber light slows, blue light chills, and so on — instead of five unbuilt status pearls.
  - **Target for the slice:** 60 working pearls, 25 synergies, 3 transformations, each one changing how her light
    *looks* at a glance.

### 3.3 Readability: the reef outshines the play

- **Symptom.** In captures the reef is saturated pink, gold and violet with depth blur; Clementine is about 1.5% of
  the screen's height (the camera stands 55 m back to show 15% of a level); the corrupted are invisible outside her
  light; pickups are small. The eye goes to the coral first.
- **Fix — a value hierarchy, enforced like a test:**
  1. **Her light and what it touches**: the only saturated warm colour and the brightest values on screen.
  2. **Threats and pickups**: high contrast silhouettes, a rim of light whenever they are in play.
  3. **Walls and ways**: clear dark/light separation of where she can and cannot swim.
  4. **The reef's beauty**: desaturated by a third, values compressed, no blur over the play plane; richness lives
     in the still moments (havens, a cleansed level, the idle drift).
  - Add a **Sixth-of-a-second test** to the Bible: in a random screenshot, can a stranger point at her, at the
    danger and at the way on within a sixth of a second?
  - Try the camera at 35–40 m for fights (pull back when the screen is calm). A bigger heroine is worth more than
    a wider map; the minimap already carries the overview.
  - Consider letting the level's colour *return* as it is cleansed: grey and quiet while corrupted, the full
    painting as the reward. The beauty becomes a dopamine kick instead of a distraction.

### 3.4 Fairness of the dark

- **Symptom.** Foes that cannot be seen drink her health through her own light. On paper this is the game's best
  idea and its biggest risk: damage from something unseen reads as cheating.
- **Fix.** Every unseen threat needs a *tell that is not sight*: the water bending round it (already in the leak
  shader), a positional sound, her rim flickering toward it, motes of her light drifting to the drinker. Rule for
  the Bible: **she is never harmed by something she had no way to sense a second earlier.**

### 3.5 Too few faces in the dark

- **Symptom.** The working cast is the murkling family, six corrupted fish kinds and Blightroot's garden. Twelve
  deeper fish are designed and unbuilt. Depths differ in look more than in what she must *do*.
- **Fix.** Each depth gets **one rule of light** (the folk bosses already have one each): kelp that casts moving
  shadows, a wreck with mirrors that carry her beam round corners, a trench where only her light exists, an abyss
  whose creatures flee light instead of drinking it. Then two new creatures per depth that are *about* that rule.

### 3.6 Sound is a placeholder

- **Symptom.** 27 sound files, mostly bubble pops; one music track not in the repository; the Bible's whole audio
  chapter is unbuilt. For a game about dark water, sound is half the picture (and the answer to §3.4).
- **Fix.** Treat audio as the next *system*, not polish: positional telegraphs, the depth-pitched world, silence as
  a resource, and **music that the reef plays** — each freed neighbour adds an instrument to the haven's theme, each
  cleansed level a layer to its bed. See §5.4.

### 3.7 Code: one file carries the game

- **Symptom.** `TopDownMain` is 4,300 lines with seven partial files beside it and all 89 debug flags inside;
  `LevelView` 1,750; `TopDownGenerator` 2,300. Strings are hard-coded (no translation calls at all). There is no
  export preset, no CI, no performance budget; the last session measured 13 FPS on the development machine without
  finding the cause.
- **Fix.**
  - Split `TopDownMain` into a small scene root plus services: `RunFlow` (dive, levels, saves), `DebugHarness`
    (every flag), `HudLayer`, `WorldViews`. No behaviour change, one service per commit.
  - **A frame budget as a test:** a headless capture run that fails over 12 ms CPU / 8 ms GPU on a named mid-range
    target; quality tiers for the raymarched beam (steps), shadows and blur.
  - Route every string through one table now; translation later is then free.
  - GitHub Actions: build, the Core tests, a smoke run of three seeds, an export.
  - Check floating-point determinism across machines before promising online play (§6.4).

### 3.8 The kicks are there, the rhythm is not designed

- **Symptom.** A level-up "never interrupts play" and waits in the pause menu; pearls come from rooms and shops;
  there is no stated cadence of reward.
- **Fix.** Design the **beat sheet** of a run and tune to it:
  - every 10–20 s a small kick (a freeing, shells flying, a patch of colour returning);
  - every 2–3 min a choice (a pearl, a hole, a deal);
  - every 8–10 min a peak (a leak sealed, a boss, a haven);
  - once a run, something never seen before (§6.3).
  - Make the level-up a moment in the world: her bell flares, time slows for a breath, three motes of light rise
    from her and she swims into one. No menu.

---

## 4. What is missing

| Missing | Note |
|---|---|
| **An ending** | Depths 6–7, the Tank and The Hand are written and unbuilt; the dive loops instead. A roguelite needs a summit before it needs endlessness. |
| **Run modes for 30 minutes and for an evening** | One dive is 25 reefs. See §6.1. |
| **Co-op** | The sim has one player. See §6.4. |
| **Depth of build** | §3.2. |
| **Difficulty that grows with the player** | No ascension ladder, no modifiers, no daily seed. See §6.2. |
| **A reason to look at the Sea-pedia** | It lists; it should *collect*: silhouettes until freed, lore lines earned, the folk's memories. |
| **Onboarding beyond the tutorial level** | The first three runs should each unlock one system (pearls → havens → deals), not show all at once. |
| **Audio, music, voice of the reef** | §3.6. |
| **Accessibility for a game of light** | A high-contrast mode, outlines for the corrupted, a "no darkness damage" assist, remappable everything (bindings exist), a photosensitivity pass. |
| **Translation, export, CI, performance tiers, controller glyph coverage, Steam Deck check** | §3.7. |
| **Telemetry of one's own runs** | Where runs end, which pearls are never taken. `PlaneProfileRecorder` is the seed of it. |

---

## 5. The story, rooted in folklore

The present lore — the Leak, the Crack, the home aquarium, "kept, not watched" — is strong and should stay. What it
lacks is *depth of time*: the reef has no beliefs of its own, so the twist has nothing to overturn. Japanese sea
folklore fits this game uncannily well, starting with the fact that its most famous sea tale has a jellyfish in it.

> These tales are retold from memory as starting points. Before anything ships, check each against sources and work
> with a cultural reader; borrow with credit in the Sea-pedia, and keep the reef's own names for things.

### 5.1 The folklore, and where it already lives in the game

| Tale or being | In brief | Its place in Ink Deep |
|---|---|---|
| **Kurage hone-nashi**, "how the jellyfish lost its bones" | The Dragon King sends the jellyfish to fetch a monkey's liver to heal his queen. The jellyfish lets the secret slip, fails, and is beaten until it has no bones. | **Clementine's line.** The bloom is the disgraced messenger's kin: boneless, drifting, trusted with nothing since. Her dive is the errand done right at last. |
| **Ryūgū-jō**, the Dragon Palace | A palace under the sea, lit from within, where a day is a hundred years outside. | **What the reef thinks the Tank is.** The warm light bleeding from the Crack is, to the folk, the Palace calling its own home. The truth is a child's aquarium. |
| **The tide jewels** (kanju and manju) | Two pearls of the Dragon King: one draws the tide out, one brings it in. | **The pearls.** The first two were the jewels; every pearl since is a splinter of them. The mechanic of §6.1. |
| **Urashima Tarō** and the box | A fisherman returns from the Palace with a box he must not open; he opens it and his lost years fall on him. | **The folk's lost time**, and the final choice (§5.3). |
| **Namazu** and the keystone | A giant catfish under the earth whose thrashing shakes the land, pinned by a god's stone. | **The Crack.** The pipe was driven through the keystone. The charge stones and seals are its shards. A boss for the Abyss. |
| **Shin**, the mirage clam | A great clam that breathes out mirages of towers and palaces. | **Queen Clam.** She already exists; now her arena shimmers with a palace that is not there. |
| **Umibōzu** | A vast black shape that rises from calm water. | **Drippus Prime.** The ink given a name the reef already feared. |
| **Funayūrei** | Drowned spirits who beg for a ladle and sink the boat with it; give them one with no bottom. | **Murklings.** They ask for light and drown her in the asking. A pearl, the Bottomless Ladle: what they drink runs through them. |
| **Heikegani** | Crabs that carry the faces of fallen warriors on their shells. | **The Crab Deals.** The crab is an old soldier who trades in what was lost. |
| **Amabie** | A glowing sea being who foretells sickness and says: show my picture to the people. | **The Sea-pedia.** Drawing a freed creature into it protects the next of its kind (a meta unlock with a reason). |
| **Bake-kujira** | The ghost of a whale, all white bones, with strange fish in its train. | **Whale Song** and the whale pool; a haven inside the ribs. |

### 5.2 The story

*Long ago the Dragon King's queen fell ill, and he sent his swiftest messenger for the cure: a jellyfish, who then
had bones and pride. She talked when she should have been silent, came home with nothing, and was broken for it. Her
children have drifted ever since, and the reef has a saying for anything lost: "sent with the jellyfish."*

*The Palace went dark in the same age. The tide jewels were scattered as pearls. The keystone held the old catfish
down, and the reef forgot all of it except as stories Grandmother Tull tells.*

*Then the Crack opened, and warm light came up out of it — too warm, too bright. The old ones said: the Palace is
lit again. One by one the reef's folk swam toward it to be let in, and came back wrong, dripping, their colours
loud. Clementine's whole bloom went in one current, singing. She was the smallest and was left behind.*

**Act I — The errand** (Shallows, Kelp). She dives because she believes what they believed: that the Palace has
taken her family in, and that a jellyfish who brings the jewels home might be forgiven. The folk she frees remember
the light and nothing after; each has lost time, like Urashima.

**Act II — The wrong palace** (Galleon, Carnival, Trench). The evidence stops fitting the tale. Straight pipes. A
castle of plastic. Flakes that fall like snow and taste of nothing. The freed begin to say the Palace was *small*,
that its sky was hard. Queen Clam's mirage shows towers; her freed pearl shows a room.

**Act III — Kept** (Abyss, the Tank). The keystone lies split around a pipe; the catfish, freed of its pin and
drowned in the Leak, is the last creature of the sea she must free. Above it, the Palace: gravel, a plastic diver, a
lamp for a sun, and the bloom drifting in it, well fed and dim. The Dragon King of the story is a child with a
bandaged finger.

### 5.3 The endings

- **The first ending.** She stings The Hand; the tank goes back to the sea; the bloom comes home. The Leak stops.
- **The box.** In the Tank's treasure chest lies the last thing from the old tale: a lacquered box. Leaving it shut
  ends the story as above. Opening it (on a later dive, with both tide jewels reunited from their splinters) gives
  the folk back their lost years — and with them the memory of *why* they swam to the light. This opens the true
  descent: under the keystone, to the real Palace, dark since the first errand failed, and the question the game
  has been asking — whether to light it again.
- **Why it holds for hundreds of hours.** Each folk member keeps a piece of the tale and tells it in fragments
  across many dives; fragments contradict; the Sea-pedia assembles them. The player is doing folklore: comparing
  versions of a story to find what happened.

### 5.4 The voice of the reef

Clementine never speaks, and the folk speak in a line or two. Give the reef one more voice: **song**. Each neighbour
has a short motif; freed, she hums it in the haven; the haven's theme is those motifs together and is thin and sad
with one voice and whole with five. The murklings' drinking is the same motifs, flattened. Nobody explains this.

---

## 6. Features that would make it its own

Each is built from something the game already has, so that it reads as part of the whole.

### 6.1 The tide clock — one mechanic for pacing, risk and run length

The tide jewels as a system. Every reef has a **tide** that turns on a clock she can see on her bell's rim:

- **Ebb**: the water is thin and clear, her light carries far, side caves and secrets stand open, the corrupted are
  slow. The time to explore.
- **Flood**: the Leak rises with it; currents run, ink spreads, primes wake, the light carries half as far, and
  what she frees pays double.
- Sealing the leak **turns the tide** at once. Pearls can bend the clock (hold the ebb, call the flood for greed).

This gives the director a heartbeat, gives the player a constant small decision ("one more cave before it turns?"),
and gives the game its run modes:

| Mode | Shape | Length |
|---|---|---|
| **A Tide** | 3 depths × 3 reefs and a boss, tide fast | 25–35 min |
| **The Descent** | all depths, saved at every haven | 2–4 h over several sittings |
| **The Long Night** | cycles after the ending, flood only gets higher | endless |

### 6.2 Reasons to dive again

- **The reef remembers.** Each depth's havens keep what she set right: freed neighbours, relit lamps, colour. A
  jelly's world visibly heals over fifty dives. (It already half exists as the folk's services.)
- **Undertow** (the ascension ladder): after the first ending, each win lets the player raise the flood by one mark,
  each mark one named rule ("the dark drinks faster", "pearls are engraved blank until absorbed").
- **The daily tide**: one seed for everybody, one try, a leaderboard of light freed. The seed system is ready.
- **Wakes**: where a jelly dimmed, a mote of her light stays in that seed's reef. Friends who dive the same seed
  find it and carry it down — the seed "as epitaph and invitation" that the Bible already promises.

### 6.3 Wow-moments by design

- **Nacre.** Every pearl has a hidden second layer that forms when it is used in its own way within a dive (the
  Mirror Scale after a hundred bounces; the Lantern Pearl after a level crossed without the beam). The pearl on her
  bell visibly thickens and changes. Hundreds of small discoveries, shared between players.
- **Uncleansed pearls.** A boss's pearl may be swallowed *before* it is cleansed: great power, a drawback, and ink
  veins on her bell — the one place she carries the Leak. The Crab's pearls are the pilot for this.
- **Rare weather of the deep**: a marine-snow fall that heals, a whale passing over that puts out every light, a
  red tide in which everything glows. One per few runs, never announced.
- **Once-per-run places** that break a rule: a reef with no dark at all; a reef that is one long current; the
  whale's ribs.

### 6.4 Co-op: the second lantern

- **Fiction.** The second player is a sibling of the bloom who was left behind too (a different bell, a cool light
  to her warm one). No explaining needed: two small lights in the dark is the theme doubled.
- **Mechanics that only exist for two:**
  - **Braided light.** Where the two beams cross, the light is white and does what neither does alone: it opens
    twin-sealed walls, frees primes at once, and combines the two players' light verbs (§3.2) into a third.
  - **Light is shared life.** One can pour light into the other by touching bells. A dimmed player is a mote the
    other carries to the next spring to relight. Nobody watches from a death screen.
  - **Shadows matter.** With the real shadows the beam now casts, one player can stand in the other's shade from a
    murkling prime. The newest feature becomes a co-op verb.
  - **Split absorption.** A pearl goes to one bell; the other gets its echo (a weaker, different facet). No
    fighting over loot, two builds that lean on each other.
- **How to build it.**
  1. `PlaneWorld.Player` becomes a list; views take a player index. This is the real work and is worth doing even
     for single-player cleanliness.
  2. **Couch first**: one camera that frames both, with a soft tether at the screen's edge.
  3. **Online second**: the core is deterministic, so send inputs, not state (lockstep with a short input delay
     fits a game of this pace; rollback later if needed). Before that, prove that two machines get the same
     result from the same inputs — floating-point maths differs between processors, and the fix (fixed-point for
     the sim's few critical paths, or pinned maths functions) is far cheaper before than after.
  4. Steam Remote Play Together gives online play of the couch mode for free in the meantime.

### 6.5 Small clever things

- **Dimming is a verb.** Let her douse her aura for a few seconds: she cannot be drunk or seen, and sees nothing.
  Stealth in a game of light costs one button.
- **The beam writes.** Lit long enough, sand holds a glow for a minute. Players will draw arrows for each other.
- **The Sea-pedia as Amabie's scroll** (§5.1): completing a creature's page changes that creature slightly for
  good.

---

## 7. Look and theme

- **Keep:** two forces and two looks (warm light, black ink), real animals, no faces, the bell as living UI, the
  felt-tip chart for maps, the pearl's "looks like what it does".
- **Change:** the value hierarchy of §3.3; colour as the *reward* of cleansing; one accent per depth, not a
  palette each.
- **Decide:** the health bars. They are useful and they break two written rules. Either write them into the Bible
  as the one exception, or redraw them in the game's own language (a ring of ink that thins to nothing round the
  creature, no green or red).
- **Add to the Bible:** the Sixth-of-a-second test (§3.3), the Never-unseen-harm rule (§3.4), and the folklore
  credits (§5).

---

## 8. Technology

| Area | State | Do |
|---|---|---|
| Engine and language | Godot 4.7, C# on .NET 8, sim apart from the engine | Keep. It is the right shape for co-op and ports. |
| Tests | 667 on the core, none on the views | Add screenshot comparisons for five fixed seeds; a frame-time test. |
| Structure | A few very large files | §3.7 split, service by service. |
| Performance | Unmeasured; a raymarched beam, many full-screen shaders, deep shadow range | Budgets, three quality tiers, a Steam Deck target of 60 FPS. |
| Data | Items, creatures, templates, achievements in JSON | Good. Add a pearl-grammar validator so that every pearl is one verb or one stat. |
| Saves | Versioned codec, exact snapshots | Keep; add a cloud-save-safe format note and a migration test for each version. |
| Delivery | No export, no CI | §3.7. |
| Text | Hard-coded | One string table. |
| Audio | A manager and a sound helper, few assets | A proper bus layout, positional sound, adaptive music layers (§5.4). |

---

## 9. The next steps, in order

1. **Play and watch.** Five strangers, thirty minutes each, no help. Note where they cannot see, where they are
   bored, where they smile. Everything below may change after this.
2. **Consolidate** (one week): `GAME.md`, the archive, the Bible revised, the contradictions of §3.1 decided.
3. **Readability pass** (§3.3, §3.4): value hierarchy, camera, tells for the unseen. Retest with strangers.
4. **The light grammar** (§3.2): re-found the pearls, reach 60 working pearls with systemic synergies, hue-based
   status effects, the level-up moment.
5. **The tide clock and the 30-minute mode** (§6.1), with the beat sheet of §3.8 tuned against real run timings.
6. **Sound as a system** (§3.6): telegraphs, the freeing chord, the haven's song.
7. **A vertical slice to be proud of:** Depths 1–3, three folk bosses, havens, sixty pearls, the folklore frame
   (§5) in the Sea-pedia and the folk's lines. Performance budget met. This is the demo.
8. **Refactor for two** (§6.4 step 1), then couch co-op with braided light.
9. **The ending:** Depths 4–7, Namazu, the Tank and The Hand; then the box and the true descent.
10. **Long life:** Undertow, the daily tide, wakes, nacre, the rare weather. Online co-op once determinism is
    proven.

**The one rule for all of it:** every new thing must be a new use of her light. That rule made the best parts of
this game, and it is the difference between a masterpiece and a list of features.
