# The lit world: light as physics, the deep as its instrument (proposal)

Half of Ink Deep is the fight against the dark: the Leak's creatures, its leaks, its bosses and their corrupted pearls.
The other half should be **the deep itself, met with her light**. Every biome is a different optical instrument: its
water filters and scatters light its own way, its walls drink it, throw it back, bend it or store it, and what lives
there opens, closes, grows, flees, follows, attacks or changes colour by what light falls on it. The player learns
each place the way a diver learns a reef: by shining into it and watching what it does.

This page is the design. **Phases 1 and 2 are built** on the `inkdeep-crossover` branch (§13): her light's physics, each
biome's water and rock, and her shots meeting rock. Everything else is the plan. Values marked *(tune)* are starting
points.

Contents:

1. In one breath
2. The physics of her light
3. The water
4. The walls
5. The rules of meeting
6. Living things that answer light (flora)
7. Living things that answer light (fauna, uncorrupted)
8. The dark and the physics (corrupted creatures, bosses)
9. The exploring half: what the player does with light
10. The pearls in physical terms
11. Each biome as a lesson
12. Readability and the art rules
13. Build plan
14. Tuning reference

---

## 1. In one breath

**Her light is a real light.** It has a **spectrum** (how much red, green and blue it carries), and so a dominant
**wavelength** and **frequency**. It has a **power**, a **coherence** (how tightly it holds together) and a **width**,
and so an **intensity**. These properties set what the player sees and feels:

- its **colour** comes from its spectrum;
- its **reach** is how far it gets through the water before the water has drunk it;
- its **damage** is the energy it still carries when it lands: power × photon energy × what survived the water;
- its **halo** comes from how much the water scatters it.

**The world is matter.** Each biome's water has its own absorption per colour, its own scattering and its own
refractive index. Each wall has a material: how much of each colour it reflects, how much is mirror and how much is
dull, whether light passes into it (and bends), and whether it stores light and glows afterwards. Plants and animals
have eyes and skins tuned to certain frequencies.

**Nothing is scripted by pair.** A blue shot crosses the Alien Wreckage's crystal wall and comes out the other side
because blue light refracts through crystal by Snell's law. Nobody wrote a rule for "blue shot + alien wall". The
player discovers it, and it is always true.

**Fifty-fifty.** In a level, about half of the rooms are about the dark (creatures, leaks, a boss) and about half are
about the place: a light room, a secret behind a wall that only one frequency opens, a garden that grows into a bridge
under her beam, a shoal that leads her somewhere if she keeps her light low. The two halves feed each other:

- what she learns about the water and the walls makes her a better fighter (bounce a shot round a corner, fight in the
  clear water and not the silt);
- the fight gives her the pearls that change her light, which open more of the place.

---

## 2. The physics of her light

### 2.1 The properties

| Property | What it is | Where it comes from | What it sets |
|---|---|---|---|
| **Spectrum** (R, G, B) | Her light's energy in three bands: red ≈ 640 nm, green ≈ 530 nm, blue ≈ 465 nm | Her warm gold base (1.0, 0.82, 0.45), shifted by her colour words: ember adds red, bloom green, frost blue | Colour, and with the water: reach and damage |
| **Wavelength, frequency** | The spectrum's energy-weighted wavelength λ (nm); f = c / λ (THz) | The spectrum | What a creature, plant or lock answers to (§6–§9) |
| **Photon energy** | E = 1239.84 / λ (eV) per band | The band | Damage: blue light hits harder per unit of light than red |
| **Power** | How much light leaves her | Her damage stat, her pearls; shared by a split beam's rays (light is conserved, LIGHT-GRAMMAR §2) | Damage and reach |
| **Coherence** (0..1) | How tightly the light holds together | 0.15 a plain beam; the Lance 0.9; her focus rhythm raises it while it holds | Scattering loss (a coherent beam loses less to the water), halo width |
| **Width** | The beam's half-angle, a shot's size | Her shot size, her shape | **Intensity** = power ÷ width: a narrow beam is more intense |
| **Pulse** | How its brightness changes over time | Her rhythm words | Some creatures answer only to a flicker (§7) |

**Light is conserved.** Recolouring does not add light: her colour words shift where her power goes in the spectrum
and keep its total. A pure ember light is redder, not brighter.

### 2.2 Colour

Her colour is her spectrum normalised. After it has travelled through water, its colour is the spectrum **as it
arrives**: in the Kelp Forest the red end of her beam dies first, so the far part of her beam is greener. The view
keeps her the warm light of the scene (§12): the shift is drawn only as a tint at the far end of her beam and on her
shots' halos.

### 2.3 Reach

The water drinks each band at its own rate (Beer–Lambert): what is left of a band after d metres is
`band × e^−(absorption + scatter loss) × d`. The scatter loss is the water's scattering × (1 − ½ coherence).

**Her reach in a water** is how far her light gets before the share left falls to a threshold (0.35 *(tune)*), against
how far her own plain light gets in the reference water (the Sunlit Reef's). That ratio scales her beam's length,
clamped to 0.55–1.5 *(tune)*.

- Blue light reaches far in clear water and short in the Ruins' yellow dust.
- Red light dies fast in the deep's clear water and in the Kelp Forest's green.

### 2.4 Damage

What a shot or the beam burns is its power × its **potency** × what survived the water to where it lands:

- **potency** is the spectrum's photon energy against her plain light's (blue ≈ 1.2 × her plain light's, red ≈ 0.85 ×);
- **what survived** is the transmitted share at the distance travelled.

In the plain Sunlit Reef this is ≈ 1 at her usual ranges, so the game's balance holds there. In the other biomes,
**where you fight is a choice**: a long shot through silt does less.

### 2.5 Halo

The scattering of the water sets how much of her light glows round its path rather than going straight on. In the
Shipwreck's silt her shots wear wide halos and her beam is a short, bright fog: she sees round her better and far less
far. In the Alien Wreckage's clear water her beam is a hard blade.

### 2.6 What the player reads

The pause screen's light card shows her light in physical terms: its colour swatch, its dominant wavelength (for
example "585 nm, 512 THz"), and three bars measured in this water: **reach**, **punch** (damage at mid range) and
**glow** (halo). Entering a biome compares them with the last one ("the water here drinks red").

---

## 3. The water

### 3.1 Each biome's water

Absorption and scattering are per metre of game space (game-tuned, not ocean values).

Her reach in each water, against her plain light's in the reef, for her plain light and for three copies of one colour
(the Lance's coherence in the last column):

| Biome | Absorbs R / G / B | Scatter | Index | Density | Plain / ember / bloom / frost reach | Lance | What it means in play |
|---|---|---|---|---|---|---|---|
| Sunlit Reef | 0.045 / 0.020 / 0.016 | 0.012 | 1.333 | 1.00 | 1.00 / 0.85 / 1.11 / 1.18 | 1.14 | The reference: clear, warm, every colour carries |
| Kelp Forest | 0.050 / 0.014 / 0.040 | 0.012 | 1.334 | 1.01 | 0.91 / 0.78 / 1.17 / 0.84 | 1.03 | Green carries, red and blue die: the bloom colour is strong here, the ember weak |
| Shipwreck Graveyard | 0.040 / 0.026 / 0.024 | 0.040 | 1.335 | 1.02 | 0.57 / 0.55 / 0.59 / 0.60 | 0.73 | Silt: short reach for all, halos 3 × the reef's; coherent light (the Lance) cuts through |
| Sunken Ruins | 0.022 / 0.020 / 0.075 | 0.016 | 1.334 | 1.02 | 0.92 / 0.99 / 1.01 / 0.66 | 1.09 | Yellow dust: blue dies, red and green carry; ember shots fly far |
| Alien Wreckage | 0.100 / 0.034 / 0.012 | 0.004 | 1.333 | 1.00 | 0.80 / 0.55 / 0.91 / 1.45 | 0.83 | Cold and clear: blue reaches farthest of anywhere, red dies quickly; the hardest, least haloed beam |
| Drowned Temple | 0.040 / 0.026 / 0.024 | 0.018 | 1.380 | 1.20 | about 0.82 for all | 0.95 | Dense brine: light bends at the brine's edges (§3.2); shots fly slower |
| Atlantis | 0.070 / 0.030 / 0.016 | 0.008 | 1.345 | 1.10 | 0.84 / 0.66 / 0.93 / 1.17 | 0.90 | The deep's clear: blue long, red short; everything learned before |

(Her reach is clamped to 0.55–1.5.)

Density also slows her shots a little (the Temple's brine × 0.9 *(tune)*) and sets how much her dash disturbs the
water (§3.2's silt).

### 3.2 Local water (phase 4)

Pockets that break a biome's rule. They are the exploring half's best tools.

| Pocket | Physics | Play |
|---|---|---|
| **Halocline** (Temple, Atlantis) | A layer of denser water with a higher index; light crossing it bends (Snell); a beam at a grazing angle reflects (total internal reflection) | A mirror lying in the water: her beam skims along it to light a far room. Seen as a faint shimmer line |
| **Brine pool** (Temple) | A pool of brine with a hard surface | Its surface is a mirror for her beam; a murkling can't see her through it |
| **Silt cloud** (Shipwreck, Ruins) | Scatter × 6 for a while, kicked up by her dash or a crab's dig | Blinds murklings to her (they hunt light, and the silt smears it); blinds her too |
| **Vent shimmer** (Atlantis, Alien) | Hot water: the index trembles | Her beam and shots wobble crossing it; a creature behind it is hard to hit |
| **Plankton bloom** (Reef, Kelp) | Light scattered by plankton that answer it: they glow back | Her beam lights the whole bloom; the bloom draws the murklings, a decoy she can use |
| **Clear lens** (Alien) | A bubble of very clear, very dense water: a lens | It focuses her beam to a point beyond it: a hard, hot spot that melts ink |
| **Oil slick** (Shipwreck) | Ink-black, light-drinking, floating | It drinks her light (corruption's look); lit long enough it burns away |

---

## 4. The walls

### 4.1 Each biome's rock

| Biome | Rock | Albedo R / G / B | Mirror | Into it | Index | Stores | Through | What it does |
|---|---|---|---|---|---|---|---|---|
| Sunlit Reef | Living coral limestone | 0.55 / 0.50 / 0.45 | 0.15 | 0 | — | 0.15 | — | Mostly drinks a shot; a little bounces; it holds a faint glow where lit (coral fluoresces) |
| Kelp Forest | Holdfast rock, kelp-grown | 0.25 / 0.55 / 0.20 | 0.08 | 0 | — | 0.30 | — | Drinks most light and glows green after: the kelp feeds on it |
| Shipwreck | Iron hull, brass and glass | 0.60 / 0.50 / 0.40 | 0.65 | 0 | — | 0 | — | Rings shots off it: her gun ricochets |
| Sunken Ruins | Sandstone, gilt inlay | 0.70 / 0.60 / 0.35 | 0.20 | 0 | — | 0.55 | — | Stores light: walls she hits glow for seconds after, warm |
| Alien Wreckage | Crystal lattice | 0.30 / 0.55 / 0.85 | 0.15 | 0.70 | 1.9 | 0.10 | 3 m | Light passes in, bends and comes out the other side of a thin wall |
| Drowned Temple | Black glass (obsidian) and marble | 0.35 / 0.35 / 0.40 | 0.50 | 0.10 | 1.5 | 0.20 | 1 m | Half mirror; light that does not bounce is stored as a cold glow |
| Atlantis | Orichalcum and quartz | 0.85 / 0.65 / 0.45 | 0.80 | 0.25 | 1.6 | 0.25 | 2 m | Bright mirror metal, quartz windows: the hardest optics |

- **Albedo** is how much of each band the surface gives back at all.
- **Mirror** is how much of what it gives back is a clean bounce (the rest goes back in all directions: a glow on the
  wall).
- **Into it** is how much light that is not reflected enters the material.
- **Stores** is how much of the drunk light it keeps and gives out as a glow.
- **Through** is the thickest wall light crosses.

### 4.2 Special walls (phase 2–5)

Placed by the cave templates (CaveTemplates), as features of a cell.

| Wall | Physics | Play |
|---|---|---|
| **Mirror plate** (Shipwreck, Temple, Atlantis) | Mirror 0.95 | Her beam bounces off it (the Bounce path word for free); turnable plates make light puzzles |
| **Lightglass** (Alien, Atlantis) | Transparent to one band only (a filter) | A wall that a blue light crosses and a red light stops: a colour lock that needs no seal |
| **Sun-stone** (Ruins, Temple) | Exists only while lit: the light hardens it | A bridge or wall that appears in her light and fades after it; light it to cross a current, light it to shut a door on a murkling |
| **Shadow-stone** (Ruins, Temple, Atlantis) | Exists only in the dark | A wall that her light makes vanish: a secret way, or a door that opens only while she keeps light on it |
| **Phosphor seam** (Ruins, Kelp) | Stores 0.9 for a long time | Charge it, and it lights a room behind her for half a minute; murklings come to it, not her |
| **Ink-rock** (all, more with depth) | Absorbs all; drinks light and grows ink | Corruption's rock: it spreads while lit weakly, and burns back only to a strong, coherent light |
| **Prism column** (Alien, Atlantis) | Index varies with wavelength (dispersion) | Splits her white light into its three colours along three ways (the prism stones, made physical) |
| **Fluorescent coral** (Reef, Kelp) | Takes in high frequency, gives back lower | Blue in, green out: a frost shot wakes a green glow that opens a bloom lock |
| **Thin membrane** (Kelp, Temple) | Into it 0.9, thickness 0.3 m | A curtain her light passes but she cannot; she can see and shoot through, not swim |

---

## 5. The rules of meeting

When her light meets matter, its energy is shared in four ways. They always add up to 1, per band:

1. **reflected as a mirror** = albedo × mirror; at grazing angles more is mirrored (Fresnel, Schlick's form);
2. **scattered back** = albedo × (1 − mirror): it lights the wall where it lands and nothing more;
3. **into the material** = (1 − albedo) × into-it: it bends (Snell, n₁ sin θ₁ = n₂ sin θ₂) and travels inside;
   - if the wall is thinner than its *through* depth, it comes out the far side bent back;
   - if not, or if it would leave at too grazing an angle (total internal reflection), it is drunk inside;
4. **drunk** = the rest; of what is drunk, the *stores* share comes back as a glow over seconds.

**A shot is one packet:** it takes one of these outcomes, rolled by the shares (seeded, so a replay plays out the same):

- bounces on with its power × the reflected share;
- passes through bent;
- or goes out there, leaving the wall glowing by what it stored.

**The beam is continuous** (phase 2): it divides itself among all four at once.

- The reflected share becomes a ray born at the wall (the Bounce path word's machinery, PlanePaths).
- The refracted share becomes a ray inside the wall.
- The stored share charges the wall's glow.

**Fluorescence** (phase 3): some materials turn what they drink into a lower frequency. Blue in, green out.

**Light on light:** her light and a lamp, a plant or a stone's glow add up as light does (LightQuery.Total). Colours mix
as light mixes (PlaneColour).

---

## 6. Living things that answer light (flora)

Every plant has a **sense** (which bands it answers to, and above what intensity) and **verbs** (what it does when lit,
when lit too long, when dark). They make the place readable: a player who knows the kelp grows towards green light can
plan a route.

| Plant | Biome | Sense | Lit | Dark | In play |
|---|---|---|---|---|---|
| **Sun anemone** | Reef | Any, ≥ 0.3 | Opens its crown, shows what it hid (a shell, a key pearl) | Closes over a minute | The tutorial plant: light reveals |
| **Shy anemone** | Reef, Kelp | Any, ≥ 0.6 | Snaps shut; stays shut while lit | Opens slowly | Something lives inside it: dim your light, wait, take it |
| **Fan coral** | Reef | Blue | Turns to face the light, slowly | Rests | A dial: turned to face a lamp, it points a way |
| **Sea grass** | Reef, Kelp | Any | Leans away from her light (shade-seeking) | Stands up | Reveals the way a current flows; hides small things |
| **Giant kelp** | Kelp | Green, held | Grows a stalk towards the light, 1 m/s; a held light grows a climbable bridge | Withers back over 20 s | Bridges across currents; a curtain she grows shut behind her to block a murkling's path |
| **Bladder wrack** | Kelp | Red | Its air bladders fill and it rises, lifting what lies on it | Sinks | Lifts a rock off a cache, or a vault |
| **Bloom moss** | Kelp, Ruins | Green | Spreads over ink, clearing it (PlaneColour's bloom, made a plant) | Stops | Clears ink-rock for her if she feeds it green |
| **Sunflower sponge** | Ruins | Any, held 3 s | Stores the light and glows for 30 s | Glows down | A lamp she can make; murklings drawn to it, not her |
| **Glass sponge** | Alien, Atlantis | Blue | Carries light down its silica body (a fibre) and out at its root | — | Light she shines in one end comes out somewhere else: round a corner, behind a wall |
| **Night lily** | Temple | Darkness | Closes in light | Opens and glows faint violet in the dark | Only seen with her beam off: a quiet-path game |
| **Crown of thorns** (living, uncorrupted) | Reef | Red | Bristles; its spines block the way | Lies flat | A gate that red light closes |
| **Seed-pod vine** | Kelp, Temple | Pulsed light | Bursts its pods: spores that float and glow | — | Answers only a pulse rhythm: her rhythm words open it |
| **Ink-blight bloom** (corrupted, see §8) | All | Weak light | Drinks the light and grows | Sleeps | Feed it weakly and it grows; burn it with a strong, coherent light |

---

## 7. Living things that answer light (fauna, uncorrupted)

The reef's own animals, not the Leak's. Each has the same sense-and-verbs shape. **Fleeing, following and attacking
are all driven by light**, so the player steers them with it.

| Animal | Biome | Sense | Lit | Dark | In play |
|---|---|---|---|---|---|
| **Glass shrimp** | Reef, Kelp | Any | Flee from the light's centre | Gather | Herd them off a pressure plate, or onto one |
| **Lantern fish shoal** | Kelp, Alien | Blue, pulse | Follow a slow pulse; scatter from a fast one | Wander | A living light: a shoal following her lights the room as she goes |
| **Cuttlefish** | Reef, Ruins | Any colour | Takes the colour of the light on it, and its skin glows it back | Grey, hidden | Lit red, it looks red to everything: lures a hunter that hunts red; a living colour key |
| **Mantis shrimp** | Reef, Temple | Polarised and UV (frost) | Strikes the light's source if it comes close | Hides | Dangerous to light carelessly; strikes and breaks a glass wall when it sees frost light through it |
| **Moray** (uncorrupted) | Shipwreck, Ruins | Any ≥ 0.5 | Withdraws into its hole | Comes out | Light it to pass its hole |
| **Hermit crab** | Reef, Ruins | Any | Hides in its shell (a stepping stone) | Walks off | Light it to keep it still where you need it |
| **Firefly squid** | Kelp, Temple | Darkness | — | Glows blue in the dark, flashes in patterns | Repeat its flash pattern with her pulse and it leads her to a secret |
| **Comb jelly** | Alien, Atlantis | Any | Its combs diffract the light: rainbow bands (a living prism) | Clear, invisible | Splits her light into colours when she lights it |
| **Whale-fall worms** | Shipwreck | Red | Retract | Wave | The floor clears when lit red |
| **Sea butterfly** | Temple, Atlantis | Green | Swarm to it | Sink | A swarm she draws over a pressure plate or into a murkling's way (they blind it) |
| **Deep angler** (uncorrupted) | Atlantis | Any | Hunts the light, bites what shines | Lies still | It hunts the murklings' prey: her shots. A danger, and an ally if she makes it chase a murkling's light |

---

## 8. The dark and the physics (corrupted creatures, bosses)

The Leak's creatures are made of ink, and **ink drinks light** (its look: black, glossy, light-drinking). In physical
terms ink has albedo ≈ 0 and absorbs every band. That is why her light burns it: what it drinks, it cannot hold.

The corrupted take on the same physics, which makes them fights with a reason:

| Corrupted trait | Physics | Fight |
|---|---|---|
| **Ink (all)** | Absorbs all bands; burned by the energy it drinks | Damage is energy delivered: potency × what survived the water (§2.4) |
| **Silt-born** (Shipwreck) | Hides in scatter | Hunts her by her halo; fight it in clear water |
| **Chitin-mirror** (a carapace corrupted to glass) | Mirror 0.7 on its back | Shots off its back ricochet; hit its soft front, or bounce a shot off a wall into it |
| **Refractor** (an ink jelly in crystal) | Index 1.8 body | Bends her beam away: shine at its edge, not its centre |
| **Band-ward** (Alien) | Ink that one band passes through | Hurt only by its other colours: its colour reads from what light it shrugs off |
| **Light-eater** (ink-blight bloom, the Leak's ivy) | Grows on weak light (under 0.3 intensity), burns in strong | Don't sweep it with a wide, weak beam: focus it |
| **Murkling (as built)** | Hunts the brightest light it can see (LightQuery) | Every light in a room is a lure: wall glows (§5), sunflower sponges, lantern shoals |
| **Bosses** | Each pearl is lit only by light that reaches it (CLAUDE.md's boss rule) | Each biome's boss uses its biome's optics: Queen Clam's open shell; Kip's shipwreck hull mirrors her shots back; Bubbles' crystal must be lit through; Fennel's temple halocline hides her pearl unless the beam skims it |

**The boss's projectiles stay ink.** Her light cannot clear them (CLAUDE.md), and physically they are the darkest
matter in the room.

---

## 9. The exploring half: what the player does with light

### 9.1 The verbs

The player has six verbs. Each of them is a light verb:

| Verb | How | What it does here |
|---|---|---|
| **Look** | Beam on, swept | See: ink creatures, walls, plants, secrets. The afterimage remembers |
| **Hide** | Beam off, no shots | The murklings lose her; night lilies, firefly squid, shy anemones show themselves |
| **Feed** | Hold light on a thing | Grow kelp, fill bladders, charge phosphor, harden sun-stone |
| **Bounce** | Shoot at mirror walls | Reach round corners, into vaults, behind a mantis |
| **Pass through** | Shoot through crystal and membranes | Hit what she can't reach, open locks from the far side |
| **Colour** | Her colour words, prism stones, fluorescent coral | Open colour locks, pass lightglass, steer cuttlefish and lantern shoals |

### 9.2 The light room

A cell type of its own in the crossover's templates (CaveTemplates). There are one or two a level, and none are
needed to finish it. Each is a small, readable puzzle built from the parts above, with a pearl, a heart, shells or
lore at its heart. Examples by biome:

- **Reef: the sleeping garden.** Sun anemones in a ring, one with a pearl. Light opens them one at a time, but each
  closes after 5 s. Find the order the fan corals point.
- **Kelp: the green ladder.** A current too strong to swim. Hold green light on the kelp to grow a stalk bridge across;
  the bridge withers once she's over.
- **Shipwreck: the ricochet hold.** A vault in a sealed hold, the only way in a porthole. Bounce a shot off three hull
  plates. The silt cloud she raises if she dashes hides the angles.
- **Ruins: the sun door.** A doorway of shadow-stone, and a phosphor seam beside it. Charge the seam, and its glow keeps
  the shadow-stone away long enough to slip through.
- **Alien: the crystal lens.** A pearl in a crystal block. Blue light passes into the crystal and focuses on the pearl;
  red is drunk at its face. Use frost, or the prism column's blue way.
- **Temple: the brine mirror.** A pool of brine between her and an altar. Her beam, laid low across the pool, reflects
  off its surface onto the altar's sun-stone.
- **Atlantis: the orrery.** Turnable orichalcum mirrors, quartz windows, a prism. Every trick together.

### 9.3 Secrets by light

- **Lightglass walls** hide secret cells (the crossover's secret rooms), crossed only by the right band.
- **Shadow-stone** hides ways that open while lit.
- **Phosphor writing** on Ruins and Temple walls (the Folk's lore): it appears only after she has lit a wall and turned
  her light away. It is the afterglow that reads.
- **Firefly squid** flash patterns lead to caches.

### 9.4 The spectral survey (the codex)

Every plant and animal of §6–§7 has an entry that fills in as she lights it:

- by each colour (ember, bloom, frost, white);
- in the dark;
- with a pulse.

Each completed entry gives a small permanent reward: a shell's worth, a hint pearl, a line of lore. Completing a
biome's survey opens its keystone (CROSSOVER.md's keystones). This gives the exploring half its own long goal,
alongside the dive.

### 9.5 The rhythm of a level

| Beat | Half | What |
|---|---|---|
| Arrive | Explore | A new water: the light card says what it does to her light |
| First rooms | Both | Creatures in the open, plants that answer, a first light trick |
| Danger climbs | Fight | The clock (PlaneDanger): murklings come to her light: hide, or light a decoy |
| A light room | Explore | Optional, rewarding, no creatures inside |
| A secret | Explore | Behind a wall only one light opens |
| The boss | Fight | Its pearl, lit by this biome's optics |

---

## 10. The pearls in physical terms

The light grammar's words (LIGHT-GRAMMAR.md, PEARLS.md) keep their meaning. Each also reads as physics:

| Word kind | Physics | Examples |
|---|---|---|
| **Colour** (ember, bloom, frost) | Shifts power along the spectrum (conserved) | Ember: redder, longer in the Ruins, weaker per hit, burns on |
| **Shape** | Splits power across rays; width sets intensity | Hammerhead: wide and weak, great for sun-stone, bad for ink-rock |
| **Path** (Bounce, Fork…) | Bounce and fork read the wall's material | Bounce on iron is sure; on coral it may be drunk |
| **Rhythm** | Pulse and coherence over time | Focus raises coherence: cuts through silt |
| **Great lights** (the Lance) | Coherence 0.9 | A laser: least scatter, longest reach in silt, burns ink-rock |

New pearls the physics invites (phase 5):

- **Polariser lens**: sees through the brine's glare; mantis shrimp stop seeing her.
- **Abyssal blue**: shifts her light past frost into near-UV. It wakes fluorescent coral everywhere and hits hardest,
  but the Ruins' dust drinks it.
- **Ember coal**: near-infrared. Murklings can't see it (stealth light), but it is short and weak.
- **Lens of the deep**: her shots focus as they fly, more intense at range.
- **Siltwalker**: her halo gets smaller in scattering water.

---

## 11. Each biome as a lesson

| Biome | Teaches | Fight twist | Explore twist |
|---|---|---|---|
| Sunlit Reef | Light reveals; hide in the dark | Murklings to the brightest light | Sun anemones, shy anemones |
| Kelp Forest | Light feeds and grows | Green carries: bloom builds shine | Kelp bridges, bladder lifts |
| Shipwreck | Light bounces; silt scatters | Ricochet off iron; fight in clear water | Ricochet holds, porthole vaults |
| Sunken Ruins | Light is stored | Wall glows as decoys | Phosphor seams, sun-stone, shadow-stone, glowing lore |
| Alien Wreckage | Light bends and filters | Through-crystal shots; band-wards | Lightglass, lenses, prism columns |
| Drowned Temple | Light reflects in water | Haloclines hide and reveal | Brine mirrors, night lilies |
| Atlantis | Everything | All the tricks against the deep's hunters | The orrery rooms |

---

## 12. Readability and the art rules

The standing rules (CLAUDE.md) hold:

- **Clementine stays the warm light.** Her light is drawn warm gold to white at its source. The water's filtering shows
  as a tint along it, never a recolouring of her. A wall glow, a phosphor seam or a sponge never out-glows her.
- **The corruption is ink.** Ink-rock, oil slicks and the light-eaters are black, glossy and light-drinking: the
  physics agrees with the look.
- **Other colours are free.** Fluorescent coral, comb-jelly rainbows and lightglass tints are accents.
- **Bending must be seen.** A shot through crystal is drawn going in, bent, and coming out. A bounce leaves a bright
  spark on the wall. A stored glow fades visibly.
- **Creatures look real.** A cuttlefish's colour change is its skin, not a face. A mantis shrimp's strike is its
  clubs.

---

## 13. Build plan

**Phase 1: built (0.72.0).**

- The physics: `src/Core/Plane/PlaneOptics.cs`:
  - `Spectrum` (bands, wavelength, frequency, photon energy);
  - `WaterMedium` and `RockMaterial`, each biome's in `BiomeOptics`;
  - `Optics` (transmission, reach, potency, halo, reflect, refract, meeting shares).
- Her light's properties each step (`PlaneWorld.HerLightPhysics`): spectrum from her colour words, coherence from the
  Lance and her focus, and from these her reach and punch in this level's water. Her beam's length and burn take them.
- Her shots take the water: their power falls with what the water drinks over the distance they flew (`PlaneShot.Lit`).
- Her gun's shots meet the biome's rock by the rules of meeting: bounce, pass through a thin crystal wall bent, or be
  drunk and leave the wall glowing (`WallGlows`). Murklings see wall glows as light, and the view lights the water
  round them.
- `PlaneOptions.Optics`: on in the crossover. Without it, everything is as before (the water neutral, rock as it was).

**Phase 2: built (0.73.0)**, as LIGHT-AND-GRAPH.md §18 says.

- The beam divided at walls: mirror rays off metal, refracted rays through crystal (PlanePaths).
- Mirror plates and lightglass in the templates.
- The light card on the pause screen.

**Phase 3.**

- Flora with senses and verbs (`PlaneFlora`: sun anemones, shy anemones, giant kelp, sunflower sponges, night lilies).
- The light room cell type.
- Fluorescence.

**Phase 4.**

- Local water: haloclines, brine pools, silt clouds, vent shimmer, plankton blooms.
- Sun-stone and shadow-stone.

**Phase 5.**

- Fauna that answer light (glass shrimp, lantern shoals, cuttlefish, mantis shrimp, firefly squid).
- Corrupted traits (chitin-mirror, refractor, band-ward, light-eater).
- The new pearls.

**Phase 6.** The spectral survey, the keystones, the orrery rooms.

---

## 14. Tuning reference (phase 1)

| Name | Value | Meaning |
|---|---|---|
| Bands | 640 / 530 / 465 nm | Red, green and blue's wavelengths |
| Her base | 1.0 / 0.82 / 0.45 | Her warm gold spectrum |
| Colour shift | 0.6 a copy | How much of her power one colour word moves into its band |
| ReachThreshold | 0.35 | The share left where her reach ends |
| ReachClamp | 0.55–1.5 | Her reach in a water, against the reference |
| ScatterLoss | 1 − ½ coherence | How much of the scattering leaves the beam |
| Coherence | 0.15 plain, 0.9 Lance, + 0.4 × focus | |
| GlowSeconds | 3 | How long a stored wall glow lasts at full charge |
| GlowLight | 0.9 | A wall glow's brightness × its stored share |
| ShotFloor | 0.25 | The least share of its power a shot keeps after the water |
| BounceFloor | 0.35 | The least power a bounced shot keeps |
