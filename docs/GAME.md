# Ink Deep — the game

The one current design of the whole game, as it is built, and the decided plan for what is not built yet. Start here.
Every other document goes into one part of it in detail. Where two documents disagree, the order of trust is: **this
page**, then the system documents (§0), then DESIGN-TOPDOWN.md, then anything in `archive/`.

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
  - her **aura**, always on round her: it shows the corrupted near her and seals a leak she stays close to;
  - her **beam**, a flashlight cone along her aim while the beam is held: it burns the corrupted, dazzles them, seals
    leaks, opens seals, clears ink and ash, charges stones and opens bosses, by how much of its light reaches. It is a
    real light: rock, the seabed and every creature's body stop it and cast shadows (VISUAL-SHOWCASE);
  - her **glow can be put out**: dark, she is hidden from the Leak's creatures and sees almost nothing.
- **Her life is her light.** What hurts her corrupts her; her corruption meter is the minimap's bezel and the gauge on
  the HUD. Murklings drink her light. It comes back from cores (the light freed creatures leave), light orbs, light
  springs, healing pearls, havens and a sealed leak cave. Full of corruption, she dims and the dive ends (DIVE §2).
- **Her active pearl** is used with its own button and recharges; she carries one at a time.
- **Controls:** move, aim, beam, glow, dash, dive, active pearl, review pearls, pause. Every one can be rebound, on the
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
- **A depth is a graph of reefs** (LIGHT-AND-GRAPH §6): four rows of two to four reefs, then the boss's reef. Each reef
  has two to four holes down, each ringed in the colour of the reef it leads to (a plain reef, a clam room, a curse den,
  a haven, the boss). The pause menu's chart shows what she has seen of the graph.
- **Havens** (DIVE §4): Lantern Bay's calm water, drawn by hand, between the rows. Arriving heals her whole and banks
  everything she carries. The freed folk live there; the shop is always open once Ottilie is home.
- **What she keeps** (DIVE §2): her level, skills and freed folk always; what is banked (pearls and shells) between
  dives; what she carries since the last haven is lost if she dims.

## 5. A reef

- **Its leak** (ADVENTURE §1): a brittle star of ink over the shut hole, seen only in light. Its ink spreads as dots
  through the water and turns the reef's fish one by one. Sealing it with her light opens every hole of the reef, sends
  a wave of light out that dissolves the ink and briefly shows the corrupted it touches, and pays her experience.
- **Its places** (LIGHT-AND-GRAPH §7): secrets (sealed walls with labyrinths behind), shops, shell caches, Crab Deals,
  clam rooms, leak caves (sub-levels with their own leak, harmful until sealed) and the holes. A template gives each reef
  its subject and cast (DESIGN-TOPDOWN §4.8).
- **Its light**: light springs where she heals, charge stones her beam lights (lures, lamps, and a trio that hides a
  cache), prism stones she pushes into line to colour her light for coloured seals.
- **Its life**: healthy reef fish (clownfish in anemones, blue tangs, schools of chromis, pufferlings) going about their
  day until the ink turns them; currents and geysers; the reef director that keeps the reef acting when she is not
  there.
- **Where she is shown**: the minimap (her surroundings, the unseen filled as rock), the Tab map (the whole level under
  fog), the chart (the dive).

## 6. The corrupted

The Leak's creatures give off no light: they are seen only where light falls on them. Each carries a corruption meter;
emptied, it is freed and swims off in its true colours, leaving a core of light. A thin ring of ink round it shows how
much corruption is left (CORRUPTION, "Corruption rings").

- **Murklings**, balls of ink that hunt light and drink it; throwers, hunters and primes (which break into murklings);
  lesser murklings (the fish they turned).
- **The war's creatures** (CORRUPTION): Blightroots, Blotters, Thumpsquirts, Inkwardens, Lashjaws, their dens.
- **Corrupted fish** (ADVENTURE §4): flounders and Hearthguards, pufferlings.
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
