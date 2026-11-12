# The light grammar (proposal)

Step 4 of the project review (MASTERPIECE-REVIEW.md §3.2, §9): the pearls re-founded on her light. Approved. **Steps 1
(colour), 2 (paths on rock) and 3 (rhythm) are built** (docs/LIGHT-AND-GRAPH.md §10–§12, where their rules now live);
the rest is to come. Built differently from this page: Sea Nettle Sting, Stingray Barb and Siren Song stay in the catalogue
until step 8; frost stills only the creatures a dazzle holds; the colour on a creature is drawn on murklings; a fork
opens round the way a mirror would send it (70° apart), not round the ray's own way; her shots keep their older paths;
String of Pearls was kept, renamed in meaning (a spun-up beam keeps its focus through a swing); the pulse beats twice
a second. It replaces BEAM-PEARLS-PLAN.md once approved, keeping everything that plan
already built (the split beams, tick rate, carry, light pressure).

## 1. In one breath

**Her light is a sentence, and every pearl is a word.** A pearl no longer "modifies shots". It gives her light one
word of one of five kinds: a **shape** (how the light leaves her), a **path** (what it does when it meets something),
a **colour** (what it does to what it touches), a **rhythm** (how it changes while she holds it), or an **echo**
(what the light she frees does next). Her **glow** speaks the same words in a second, quieter voice.

Words combine **by rule, not by table**. A split beam that bounces is a lattice because *split* and *bounce* each do
what they always do. Red light and blue light on one creature are magenta because that is how light mixes. Nobody has
to author "split + bounce + red + blue"; the player discovers it, and it looks like nothing they have seen before.

The pause menu reads her build back as the sentence it is:

> *A threefold, bouncing beam of ember and frost that lingers; freed light flares.*

## 2. The five kinds of word

| Kind | Asks | How copies stack | Seen as |
|---|---|---|---|
| **Shape** | How does the light leave her? | Shapes merge into one layout (as built: `RayLayout`) | The beam's silhouette |
| **Path** | What does a ray do when it meets rock or a body? | One more event per copy (a second bounce, a second fork) | New rays born where light lands |
| **Colour** | What does the light do to what it touches? | A deeper tint: its effect stronger | The beam's hue, and the hue of what it lights |
| **Rhythm** | What happens while she holds it? | Faster to its peak | The beam breathing, tightening, snapping |
| **Echo** | What does freed light do? | A bigger echo | Light bursting from every creature freed |

Plus **stat pearls** (reach, width, tick rate, carry, pressure, damage, health, speed…), unchanged: the plain adverbs.

**Light is conserved.** Every split shares the beam's worth among its rays; every path event passes on less than
arrived (a bounce 60%, a fork 30% each). A build gets *wider, stranger and cleverer*, never simply ×8. This is the one
budget that keeps the whole grammar fair.

## 3. Shape — how the light leaves her

| Word | Pearl | The beam | State |
|---|---|---|---|
| Threefold | Triple Tentacle | 3 beams fanned ±12°, each 46% | built |
| Floodlight | Hammerhead | 5 beams ±30°, each 32%, 85% as long | built |
| Twin weave | Double Helix | 2 beams crossing and recrossing, each 62% | built |
| Needles | Plankton Swarm | 3 thin beams ±9°, + tick rate | built |
| Lance | Sunbeam | One long narrow searchlight: reach ×1.6, width ×0.5, carry full | new |
| Wake | Rear Fin | A second beam straight behind her, 60% | new |
| Orbit | Nautilus Spiral | A faint second beam turns slowly round her (the old Lighthouse, now a word of its own) | rework |
| Crown | *Kraken Form* (3 tentacle pearls) | 7 short rays round her beside her beam | built |

## 4. Path — what a ray does when it lands

A path word acts **where a ray stops**: on rock, or on a body. Rays born from a path event are real rays: they burn,
reveal, carry her colours, and may take the *other* path words (never their own again), one generation deep.

| Word | Pearl | What happens | Mark |
|---|---|---|---|
| **Bounce** | Mirror Scale | Off rock the ray reflects (60% strength and reach); +1 bounce a copy | A bright glint on the rock, a new ray leaving it |
| **Fork** | Mitosis | Where it stops, two short rays fork off at ±35° (30% each) | A Y of light at every landing |
| **Pierce** | Swordfish Bill | Bodies no longer stop it (they still cast no shadow) | The beam runs on through creatures | 
| **Ghost** | Ghost Jelly | It passes through one wall up to 3 m thick, half as strong beyond | A pale stain where it enters and leaves the rock |
| **Lean** | Anglerfish Lure | The ray bends up to 10° toward the nearest corrupted creature | A visibly curved beam |
| **Arc** | Electric Eel Tail | From each body it burns, light arcs to 2 more within 4 m (50%) | Thin jagged threads between creatures |
| **Linger** | Bubble Coral *(rework)* | Where it lands, a pool of her light stays 2.5 s and goes on burning; swept, it paints a trail | Glowing strokes left on the reef: she writes with light |
| **Return** | Boomerang Shrimp *(rework)* | Light that lands on rock comes back along its own ray: what stands in it is lit from both sides (burn ×1.5 between her and the wall) | The beam visibly doubled, brighter toward the wall |
| **Flare** | Starfish Arm | The cone widens along its length, half as wide again at its tip | A trumpet-shaped beam |

**Rule of meeting.** Bounce, Fork and Return answer "rock"; Pierce and Arc answer "a body"; Ghost changes what counts
as rock; Lean and Flare shape the ray before it lands; Linger acts wherever any ray ends. So any two never contradict:
they apply in that order, each to what the other left.

## 5. Colour — what the light does

Three primary colours, mixed as light mixes. The sim already does this for prism stones (`LightHue`: red, green,
blue and their four mixes, a hue per ray), so colour is the cheapest spectacular thing in this proposal.

| Colour | From | What it does to the corrupted it lights |
|---|---|---|
| **Ember** (red) | Fire Coral | Keeps burning 3 s after the light has left it |
| **Bloom** (green) | Glowing Moss | Its light *mends*: ink and ash clear twice as fast, and what it frees leaves a core worth half again as much |
| **Frost** (blue) | Frost Kelp | Slows it, more the longer it is lit, to a standstill after 2 s |
| **Sunfire** (ember + bloom = yellow) | — | What it frees bursts in a ring of her light (3 m) |
| **Shatter** (ember + frost = magenta) | — | A frost-stilled creature takes the ember all at once: a burst of steam that burns everything near it |
| **Lull** (bloom + frost = cyan) | — | It forgets her: stops chasing, stops throwing, drifts |
| **Daybreak** (all three = white) | — | All of the above, burn ×1.5, and nothing in it stays hidden |

**How colours are dealt** (the rule that makes builds look different):

- **One ray:** her colours mix in it. Ember and Frost make one magenta beam.
- **Several rays:** each primary takes rays of its own, in turn across the fan. A Threefold beam with Ember and Frost is
  red, blue, red. **Where differently coloured rays overlap on a creature, they mix there.** The Twin weave's two beams
  cross and recross: a creature at a crossing is in magenta, a metre on it is in red alone.
- **Born rays keep their parent's colour.** A red ray's bounce is red.

**Colour meets the world** (all from rules already built):

- A coloured ray opens a **coloured seal** of its colour, with no prism stone. Pearls become keys; a seal she could
  not open on one dive is a reason to take Frost Kelp on the next.
- Through a **prism stone** a ray takes the stone's colour as well (red pearl through a green stone: yellow).
- **Charge stones** lit by coloured light glow in that colour and lend it to whatever passes through their light.
- Folded into colour and leaving the catalogue as separate status pearls: Sea Nettle Sting (ember), Stingray Barb
  (frost), Siren Song (lull). Five dice-roll statuses become seven colours with no dice.

## 6. Rhythm — what happens while she holds it

| Word | Pearl | While the beam is held | Mark |
|---|---|---|---|
| **Focus** | Pearl Diver | Held steady on one spot, it narrows and its burn builds to ×3 over 1.5 s; swinging it starts over | The cone tightening to a white-hot thread |
| **Spin-up** | Sardine Run | The longer without letting go, the stronger: ×1.8 after 3 s | The beam's haze thickening, its motes racing |
| **Snap** | Pistol Shrimp Claw | Held on a body, every 0.8 s a snap of light bursts there (2.5 m), pushing it back | A sharp flash and ring at the beam's tip |
| **Pulse** | Glow Burst *(active)* | For 5 s the beam beats: ×2 on each beat | The whole beam flashing in time |

## 7. Echo — what freed light does

The light a creature had drunk is hers again when it is freed. Echo words spend it on the spot. **An echo carries her
colours and one path word** (a flare's rays bounce if she bounces), one generation deep: this is where chain reactions
come from.

| Word | Pearl | When a creature is freed | Mark |
|---|---|---|---|
| **Flare** | Starburst Gland | A ring of her light (3 m) that burns and clears ink | A gold ring racing out |
| **Spines** | Fire Urchin Spine | 6 short rays out from where it was | A star of light where it burst |
| **Gather** | Lamprey Mouth | Its core flies to her at once and is worth +3 | A streak of light home to her bell |
| **Frenzy** | *Shark Mode* (3 predator pearls) | Her beam burns ×1.5 for 3 s | The beam flushing bright |

## 8. Her glow — the second voice

Her aura takes the same words, softly. It burns nothing by itself; these change that.

| Word | Pearl | Her glow |
|---|---|---|
| Reach | Lantern Pearl, Abyssal Glow | Reaches farther (built) |
| **Fireflies** | Firefly Squid *(rework: there is no surge any more)* | Four motes of her light orbit at her glow's edge. Each is a tiny lantern: it reveals, burns a little, and carries her colours. Dark, with her beam off, they are all the light she has |
| More fireflies | Bubble Gland *(rework)* | +3 fireflies |
| Quick fireflies | Anemone Pump *(rework)* | A firefly a murkling drinks is back in half the time |
| Colour | (her colour pearls) | Her glow takes her colours too: an ember glow leaves what brushes it burning |

## 9. Great lights — the transformations and the constellations

**Lantern forms** (three pearls of one tag; her whole light re-themed):

| Form | Tag | Her light becomes |
|---|---|---|
| Kraken Form | tentacle | The Crown: 7 rays round her (built) |
| Neon Rave | glow | It cycles through all seven colours, one every 0.8 s: every colour's effect in turn |
| Coral Reef | coral | Everything she Lingers on mends *her*: standing in her own afterlight heals |
| Shark Mode | predator | The Frenzy echo, and a narrower, hotter beam |
| Pirate | galleon | Gilded light: shells it touches fly to her; what it frees drops more |

**Constellations** (named, with the banner; each is only the rules above meeting, given a name so it is celebrated):

| Constellation | Words | What the rules make |
|---|---|---|
| **Cathedral** | Bounce + Fork | Every bounce forks: a cave fills with a lattice of light |
| **Pinball Storm** | Bounce + Arc | Each glint on the rock arcs to the nearest creature |
| **Daybreak** | Ember + Bloom + Frost | White light (§5) |
| **Aurora** | Linger + two colours | Her strokes on the reef hang as curtains of mixing colour |
| **Pearl Necklace** | Focus + Fork | At full focus the tip forks into a ring of 8 rays |
| **Depth Charge** | Focus + Snap | At full focus the snap is twice as wide |
| **Will-o'-Wisp** | Ghost + Lean | The beam bends twice as far, through rock |
| **Supernova** | Flare echo + Spines | The freed star's rays each end in a flare |
| **Lighthouse** | Lance + Orbit | The orbiting beam is a full Lance |
| **Hall of Mirrors** | Return + Bounce | Light caught between two walls: everything between burns from all sides |

**One dark word** (a Crab Deal pearl, the only place she carries the Leak): **Black Sun**. The shadow a creature
casts down her beam burns what stands in it, as hard as the light would have. It uses the real shadows her beam now
casts (`BeamShadow`), and turns a crowd into its own undoing.

## 10. The catalogue after it

| | Pearls |
|---|---|
| Shapes | Triple Tentacle, Hammerhead, Double Helix, Plankton Swarm, Sunbeam, Rear Fin, Nautilus Spiral |
| Paths | Mirror Scale, Mitosis, Swordfish Bill, Ghost Jelly, Anglerfish Lure, Electric Eel Tail, Bubble Coral, Boomerang Shrimp, Starfish Arm |
| Colours | Fire Coral, Glowing Moss, Frost Kelp |
| Rhythms | Pearl Diver, Sardine Run, Pistol Shrimp Claw (+ Glow Burst, active) |
| Echoes | Starburst Gland, Fire Urchin Spine, Lamprey Mouth |
| Glow | Lantern Pearl, Abyssal Glow, Firefly Squid, Bubble Gland, Anemone Pump |
| Stats and others | unchanged (Coral Crown, Shark Tooth, Cuttlebone, Giant Squid Eye, the maps, luck, the crab's four…) |
| **Leave** | Sea Nettle Sting, Stingray Barb, Siren Song (now colours), Twin Siphon (a weaker Threefold) |
| **New** | Black Sun (crab) |

Every word-pearl works on the plane: **about 60 pearls in play** (41 today), 10 constellations (8 today, most of them
on the dropped surge), 5 lantern forms (1 today). With 7 shapes, 9 paths, 7 colours, 3 rhythms and 3 echoes there are
several thousand distinct lights before stats, and each one *looks* different.

## 11. Seeing it (the Bible's Light Test)

- Every word has the visible mark in its table. No word is allowed without one.
- **Colour is drawn in the light itself**: the beam's haze, its spot on the reef, her glow, and the rim of what it
  lights. Her own body stays her own gold (the Warmth Test): the colour is in what she throws, not in her.
- **Pearl colours** (`PearlKey`) follow: a colour pearl is its colour; path pearls cyan, shape pearls violet, rhythm
  orange, echo gold.
- **The sentence** (`ItemCaption`): the pause menu's Stats tab and the absorbing card read her light back in words.
- Born rays (bounces, forks, echoes) are drawn as simple soft fans without the 3D shadow grid; only her own beam
  casts real shadows. A cap of 24 rays and one generation keeps the cost flat.

## 12. Building it — each step playable on its own

1. **Colour.** A hue per ray from her pearls, dealt across a split beam; colours mixing on a creature; the seven
   effects; coloured seals, prism stones and charge stones answering pearl colours; the beam, her glow and the lit
   rims drawn in colour. *(The plumbing exists; the biggest wow for the least work.)*
2. **Paths on rock:** Bounce, Fork, Return, Ghost; Cathedral, Hall of Mirrors.
3. **Rhythm:** Focus, Spin-up, Snap, Pulse; Pearl Necklace, Depth Charge.
4. **Paths in the open:** Linger (writing with light), Lean, Arc on born rays, Flare; Aurora, Will-o'-Wisp, Pinball Storm.
5. **Echo:** Flare, Spines, Gather; Supernova; echoes carrying colour and a path.
6. **Glow:** Fireflies and the reworked bubble pearls; coloured glow.
7. **Great lights:** the four unbuilt lantern forms, Lance and Orbit shapes, Lighthouse, Black Sun.
8. **The catalogue:** the leavers out, captions and the sentence, `PearlKey`, PEARLS.md regenerated, the Sea-pedia.

Every word gets a sim test (`BeamPearlTests`) and a capture; every step ends with the 667 tests green and a version.

## 13. Risks

- **Too much light.** Conservation (§2) and the ray cap are the guard; the first tuning pass after step 2 checks that
  a full lattice build clears a level no more than about twice as fast as a plain beam.
- **Colour against the art rules.** CLAUDE.md asks for no accent colours in the UI for damage or healing. This is not
  UI: it is her light, tinted, and corruption stays ink. Ember is a warm red-gold, Frost a pale blue-white, Bloom a
  soft sea-green: tints of her light, never neon.
- **Readability.** Coloured, forking light could undo the readability pass. Born rays are fainter than her own beam,
  and the unlit reef stays grey, so colour appears only where her light is.
- **Frost and Lull may trivialise fights.** Bosses take colours at a quarter strength and are never stilled or lulled.

## 14. To decide

1. The grammar as a whole.
2. The colour meanings (§5), above all Bloom as *mending* rather than poison.
3. The leavers and reworks (§10): three status pearls and Twin Siphon out; Bubble Coral, Boomerang Shrimp, Nautilus
   Spiral, Firefly Squid, Bubble Gland and Anemone Pump given new meanings.
4. Where to start (§12).
