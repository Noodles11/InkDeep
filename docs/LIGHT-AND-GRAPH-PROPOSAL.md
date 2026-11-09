# Light as life, and the dive as a graph (proposal)

The plan for Ink Deep's next shape, fitted to the game as it is (docs/DIVE.md: a dive of seeded levels, havens, leak
caves, folk bosses). Light comes first: it is what the murklings hunt, what she lives on and what frees the corrupted.
The dive turns from a line into a graph after that. Values marked *(tune)* are starting points.

Decided:

- The dive becomes a graph of reefs (§6).
- The murklings' extra behaviour (throws, fish hunting, primes) is kept, but only on variants; the basic murkling does
  none of it (§3). Requests and chains that count primes keep working.
- The corrupted are invisible outside her light, at every depth, the sunlit shallows included (§4).
- The beam strikes seals, clears ink and ash, and opens bosses, as her shots do. Leaks are sealed as they are now: by
  her own light, staying close to them (§2.3).

## 1. Light emitters

Everything that shines in the sim is a **light emitter**: her aura and her beam, her shots while they last, a lure
pearl's decoy, and later glowing plants and objects. One registry holds them all (`PlaneWorld.Lights`), and every
rule that asks "what is lit here, and how brightly" asks it, not the owners one by one.

An emitter has:

| Field | Meaning |
|---|---|
| Id, Owner | a stable number, and what it belongs to (her, a shot, a plant) |
| Shape | a point (shines all round) or a cone (Direction, HalfAngle) |
| Position, Direction | where it is and, for a cone, where it points |
| Intensity, Range | how bright at the source, and how far it reaches |
| Colour | warm gold for hers; others by owner |
| Active | an inactive emitter is in the registry but lights nothing |
| Drinkable | whether a murkling can feed on it |

What a murkling drinks from an emitter is taken from its **owner** (`ILightOwner.Absorb`): her aura and her beam
belong to her, so both drain her one pool of HP; a plant withers; a decoy goes out. When the owner is spent it says
so, and the drinker lets go.

**Brightness at a point.** For a point emitter: `Intensity × (1 − d / Range)^1.6` (0 past Range). A cone multiplies
that by its angle falloff: 1 within 70 % of HalfAngle, easing to 0 at its edge, 0 outside it. Rock between the
emitter and the point blocks it (the sim's `LineOfSight`).

Starting values *(tune)*: aura reach 5.5 m, intensity 1; beam reach 16 m, half-angle 22°, intensity 1.2; a plain
shot of hers 3.4 m (as now), intensity 0.4 to 1.3 by its bubbles (as now).

## 2. Clementine: aura, beam, HP

### 2.1 Light is her HP

Her HP (`Player.Hp`, `Run.MaxHp`) stays the number the game runs on; it is now *how much light she has*. Her bell
dims as it falls and goes out at 0, which is the end of the dive. Nothing refills it on its own: only cores (§4.2),
the Coral Polyp pearl (slow), havens and a sealed leak cave (full, as now).

Her aura and beam cost nothing to shine. They are what the murklings drink: a murkling feeding on either takes from
her one pool.

### 2.2 Aura and beam

- **Aura**: a point emitter on her, always on.
- **Beam**: a cone from her bell along her aim, on while the beam button is held (the left mouse button, the arrow
  keys, RT). It points where she aims, not where she swims. Turning it off is a choice: the beam is the brightest
  thing she has, and a murkling goes for the brightest light.
- While pearls move from shots to the beam (§5), shots are on the right mouse button (RB).

### 2.3 What the beam does

The beam does what her shots' light does now, continuously:

- **Burns** the corrupted it reaches, at `Burn` per second × her damage stat, freeing them when their meter is empty.
- **Dazzles** a creature once as it comes into the light, as shots do: it holds still for `Dazzle`, then is not
  dazzled again until it has been out of all her light for `Rearm`. A creature kept in the beam is not frozen for
  ever.
- **Strikes seals, clears ink dots and ash**: a sealed wall's carving its axis falls on gains `BeamShots` *(tune: 2)*
  plain shots' light a second; the ink dots along its axis and the ash it falls on clear.
- **Leaks** are not sealed by the beam: she seals one by staying close to it, her own light working on it, as now.
- **Opens a boss** where a shot would (its pearl, while open). Each boss gains one light rule of its own in stage 5,
  for example a hold time, the beam's angle, or a lit object in its arena.

The aura burns nothing (as her lamp does now). It reveals what is near her (§4).

## 3. Murklings

### 3.1 The basic murkling

A ball of ink that hunts light. It has four states:

| State | What it does | Leaves for |
|---|---|---|
| Searching | holds still; scans the emitters every 0.2 s | Approaching, when it finds one above the threshold |
| Approaching | swims toward the brightest emitter it found, rescanning every 0.2 s | Attached on contact; Searching when nothing is left |
| Attached | clings to the emitter's owner and drinks at its steady rate; does not scan | Released when shaken off (dash) or when the owner is spent |
| Released | tumbles where it was shaken off for `ShakeSeconds` | Searching (it scans at once) |

- The scan looks at emitters within 20 m *(tune)*. It measures each one's brightness at the murkling, drops those
  below 0.04 and those behind rock, and picks the brightest.
- It reaches its target by swimming (the sim's navigation), never through rock.
- It drinks 2.5 HP/s *(tune)* from any owner; there is no contact hit on top.
- It does not go back to her when released: it goes for whatever is brightest then.
- It acts on its own: no packs, no merging (merging is for a later variant).

### 3.2 Variants

The current murkling's extras move to variants, rolled by depth:

- **Thrower**: winds up and throws ink, as now.
- **Hunter**: hunts fish, as now (the corrupted-fish loop).
- **Prime**: as now: big, rams, breaks into murklings when freed.

Depth 1 has basic murklings only *(tune)*. Deeper, the share of variants grows. Requests and chains that count primes
keep their meaning.

## 4. Seeing them, and freeing them

### 4.1 Invisible in the dark

The corrupted give off no light of their own (their view lights go). Their bodies are drawn only where her light
reaches: the aura round her, the beam along it, a shot's glow. A murkling half in the beam shows its lit half. This
holds at every depth, the sunlit shallows included: there the ink reads as a shimmer where her light falls on it.

The view does this with the existing sear (`her_burn`): the body's visibility at each pixel is her light's brightness
there.

### 4.2 Freed light: the core

When its meter runs out, a murkling's ink shell splashes apart and its **core**, the light it had drunk, hangs where
it was:

- It fades over 1 s *(tune)*.
- Touching it gives her `CoreLight` HP *(tune: 4 for a murkling, 20 for a prime)*, never past her max.
- The Remora Sucker (magnet) widens how far she gathers cores from, and shells too.

This is freeing, not killing: the light was hers or the reef's, and it comes back.

## 5. Pearls

The catalog stays (`data/items.json`, `ItemCatalog`, `Loadout`). 43 pearls change her shots (by a stat or by the
shape of a shot), and 20 synergies are built on them.

**The shots stay**, as her second weapon: a throw of light on the right button, with its reserve. The beam is what she
fights with; a shot is what she sends where the beam cannot stay (round a corner, a bait left hanging, a burst). So most
shot pearls keep working on her shots, and a few carry over to the beam as well. None goes.

### 5.1 Each shot pearl (built)

| Pearl | What it does now | Proposed |
|---|---|---|
| Coral Crown, Glass Squid, Shark Tooth, Abyssal Glow, Pufferfish Pout, Cannonball; Neon Rave, Shark Mode, Pirate | + damage | **Beam too**: the beam burns faster by the same stat (it already does). |
| Whale Lung, Cuttlebone | + range | **Beam too**: the beam reaches farther (× √ of the range stat). |
| Whale Lung, Cannonball | + shot size | **Beam too**: the beam's cone widens (× √ of the size stat, at most 35°). |
| Sunbeam | her shot is a laser | **Beam**: the beam reaches half as far again and narrows to 15°: a searchlight. Its shot laser goes. |
| Lighthouse (Sunbeam + Nautilus Spiral) | the laser sweeps a spiral | **Beam**: a second, fainter beam turns slowly round her. |
| Electric Eel Tail | shots chain to 2 more foes | **Beam too**: what the beam burns arcs its burn to the 2 nearest corrupted within 4 m, at half the rate. |
| Rear Fin | a shot out of her back too | **Beam too**: a second beam behind her at half its strength (a murkling sees it too). |
| Frost Kelp, Fire Coral, Sea Nettle Sting, Stingray Barb, Siren Song, Giant Squid Eye | a chance per hit (freeze, burn, poison, slow, charm, crit) | **Beam too**: rolled once a second for each creature the beam keeps burning. |
| Anglerfish Lure | shots home | **Shot, and the decoy**: her shots still home, and a shot of hers left hanging shines 2.5 × as bright and can be drunk: murklings go for it instead of her. (Guided Inkfish and Will-o'-Wisp keep working.) |
| Squid Ink Espresso, Plankton Swarm, Triple Tentacle, Double Helix, Hammerhead, Kraken Form | fire rate, more shots, patterns | **Shot** |
| Nautilus Spiral, Mirror Scale, Swordfish Bill, Ghost Jelly, Mitosis, Boomerang Shrimp, Starfish Arm, Sardine Run | the shot's path or body | **Shot** |
| Pearl Diver, Starburst Gland | a charged pearl; a burst where it pops | **Shot** |
| Cannonball, Captain's Hook | knockback | **Shot** |
| Bubble Gland, Anemone Pump, Twin Siphon, Bubble Coral | the shots' reserve and hover | **Shot** |
| Diver's Watch | enemy throws slower | unchanged (not hers) |
| The other synergies (Pinball Storm, Cell Bloom, Guided Inkfish, Steam Vent, Tuna Rang, Pearl Necklace, Prism Pearl, Black Tide, Big Mad Puff, Will-o'-Wisp, Toxic Bloom, Feeding Frenzy, String of Pearls, Depth Charge, Broadside) | built on shot pearls | **Shot**, as their pearls |
| Supernova, Lance of Light, Helix Lamp, Searchlight | Firefly Squid's surge | unchanged (the surge is its own) |

The new effects go on existing pearls:

- The Anglerfish Lure is the **decoy** (above).
- The Remora Sucker is the **magnet** for cores (built).
- The Brass Compass and the Sea Chart Nautilus between them are the **cartographer**, showing the graph (§6.3).
- The Coral Polyp stays the slow mend of her light.

### 5.2 One light rule per folk boss (built)

Each folk boss keeps its fight (docs/NPC-BOSSES-PROPOSAL.md) and gains one rule that asks more of the beam than
holding it on the pearl:

| Boss | Its rule |
|---|---|
| Sleepy Old Tull (Depth 1) | **Hold it steady.** Her pearl burns only once the beam has stayed on it 0.5 s without leaving it: in her 2 s sigh, a sweep does nothing. She feels where the beam was last shone, as she feels shots and dashes. |
| Nine-Fin Kip (Depth 2) | **From the side.** Her pearl on her stripe burns only when the beam meets it within 45° of square-on to her flank, so the beam must come from beside her when she turns side-on. |
| Papa Fennel (Depth 3) | **Find him.** He is invisible among the kelp, as the corrupted are, until the beam falls on him; lit, he unfurls (his fight's opening), and stays lit for the round. |
| Many-Armed Ottilie (Depth 4) | **A brighter light.** Two lamp-shells in her hoard are light emitters. Lit by the beam, one glows brighter than Clementine for 3 s, and Ottilie turns to admire it, carrying the pearl round with her: her body no longer shields it. |
| The Pale Dragonet (Depth 5) | **The beam's heart.** Her ink sacs burst only under the middle of the beam (its inner half-angle), not its edge: the beam must be aimed, not swept. |

Drippus Prime and Queen Clam keep their rules (the whole body; the pearl while the shell is open).

## 6. The dive as a graph

### 6.1 Shape

A dive is a layered graph of reefs, generated from the dive's seed when it starts:

- **Depths**: the five depths, as now. Each depth has 4 rows of reefs *(tune)* and ends in its folk boss's reef.
- **Lanes**: each row has 2 to 4 reefs. Each reef has 2 to 4 exits down to reefs in the next row. Now and then an exit
  is a **shortcut** that skips a row.
- **Menace** comes from a reef's depth and row, never from how many reefs she has crossed: a shortcut lands her
  somewhere harder.
- **Rules the generator keeps** (the generator retries with the next sub-seed if any is broken):
  - every reef can be reached from the top;
  - every reef leads on to its depth's boss;
  - no two exits of a reef lead to the same reef;
  - every route meets a haven within 4 rows (§6.2).
- Past the fifth depth the loop starts again below the tutorial, as now.

### 6.2 Kinds of reef

| Kind | What it is |
|---|---|
| Reef | an ordinary seeded level, as now |
| Haven | as now: calm, heals and banks, all the freed folk, the Tide Fan, the shop. Its map stays hand-drawn. |
| Folk boss | the bottom of each depth, one reef wide, as now |
| Treasure, Den | the special reefs from stage 6's place rules (§7) |

### 6.3 Holes and choosing

- **Opening the holes**: a reef has a hole for each of its exits. Sealing the reef's leak opens them all at once (as
  the one hole opens now).
- **Showing where each leads**: each hole's rim shows the kind of reef it leads to: haven, boss, treasure or plain reef.
- **The chart**: the pause menu gains a **chart** of the graph, showing where she has been, the reefs she can see from
  here, and the rest unknown. The cartographer pearls show two rows further.

### 6.4 What changes in the code

- **LevelId**: gains a **Lane**; its Level becomes the row. The seed of each reef comes from (cycle, depth, row, lane).
- **The graph**: a new `DiveGraph` (in `src/Core/Run`) is built from the dive's seed. `LevelPlan.NextOf` and
  `Dives.NextOf` give way to the graph's exits. `Dives.HavenBefore` goes, since havens are reefs in the graph.
- **The save**: the dive's save keeps the reef she is on and the reefs she has visited. The graph itself is rebuilt
  from the seed. An older dive save is resumed by mapping its level onto lane 0 of its row.
- **Unchanged**: the Tide Fan's requests ("seal 3 leaks on the way down") and the chains ("dive to Depth N") count
  depths and kinds, so they keep working.

## 7. Places and sub-levels

- **Rules for each kind of place.** Each kind of place gets one definition of where it may go, how often and how far
  from others. These rules replace the conditions now spread through `LevelPlan` and `TopDownGenerator`. Places come
  in two sorts:
  - a spot on the reef (an ambush, a hidden tunnel, a secret);
  - a way into a sub-level (a leak cave, a secret cave, a treasure grotto).
- **Sub-levels are generated with their reef**, while the reef is being prepared, each from its own sub-seed. A
  sub-level has no sub-levels of its own.
- **Coming back**:
  - She returns to the exact point where she went in, facing the way she was swimming.
  - A sub-level keeps its state if she leaves it and comes back.
- **The ride in and out** is a short transition rather than the full blackout and loader, since the map is ready.

## 8. Order of work

| Stage | What | Plays differently? |
|---|---|---|
| 1 | The emitter registry, brightness at a point; her aura and her shots registered — built | no |
| 2 | The beam (held), the aura emitter, light as HP (her light dims with it) — built | yes |
| 3 | The basic murkling on the registry; variants by depth — built | yes |
| 4 | Invisible in the dark (built for murklings; the other corrupted kinds follow); cores and healing; the magnet — built | yes |
| 5 | Pearls sorted and moved; one light rule per boss — built | yes |
| 6 | The dive graph, the reef kinds, the chart; place rules | yes |
| 7 | Sub-levels generated with their reef; exact return; short transitions | yes |
| 8 | The save for the graph; the death fade and the dive's summary | yes |
| 9 | What nothing uses any more goes (shot reserve, the line of levels) | no |

Each stage ends with the build, the tests and a play check in the game, and is committed on its own.
