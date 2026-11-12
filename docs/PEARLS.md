# Pearls

> Every pearl in the item catalogue (`data/items.json`), by the kind of word it is in her light's grammar
> (docs/proposals/LIGHT-GRAMMAR.md; `PearlWords`): what it does, where it is found and what unlocks it. "In play" are
> the pearls offered on the reef (`PlaneRun.PortedPearls`, and the crab's): they lie in clam rooms, are sold in shops
> and fall from bosses. The effect lines are the game's own captions (`ItemCaption`), as the absorbing card, the pause
> menu and the Sea-pedia read them. The tables below are generated from the catalogue (`PearlsPageTests`, which fails
> when this page falls behind); the prose is this page's own.

## The kinds of pearl

Her light is a sentence and her pearls are its words (docs/LIGHT-AND-GRAPH.md §10–§16):

- **Shapes** say how her light leaves her (split rays, a lance, a beam turning round her).
- **Paths** say what a ray does where it lands (bounce, fork, return, ghost, pierce, lean, flare, linger, arc).
- **Colours** say what it does to what it lights (ember, bloom, frost, and their mixes).
- **Rhythms** say how it changes while held (focus, spin-up, snap; Glow Burst's pulse).
- **Echoes** say what freed light does (flare, spines, gather).
- **Glow** is her aura's own voice (its reach, the fireflies).
- **Stats** are the plain adverbs. **Actives** are used with their button and recharge.
- **Lantern forms** (three pearls of one tag) re-theme her whole light; **constellations** are words meeting, named.
- **The crab's pearls** are found only in a Crab Deal.

The pause menu's Stats tab reads her light back in these words (`PlaneStatSheet.Words`), above her numbers.

- **Passive** pearls work from the moment she absorbs one. **Active** pearls are used with their button and then
  recharge; every run starts with **Tide Pulse** and the **Sea Chart Nautilus** (`PlaneRun.StartingPearls`; never
  offered, since she always has them).
- **Quality** is ★ to ★★★★ (rarer, stronger). **Found in** lists where it can be offered.
- **Stacks**: a pearl taken **Once** leaves the pools for the run. One that stacks can be absorbed again: it stays in
  the pools at a lower chance (×0.35 of its weight per copy held, `ItemPools.RepeatWeight`), and each copy adds the
  whole pearl again, or what the column notes.

Every stat counts on the plane: damage, max HP, fire rate, shot speed, range, shot size, swim speed, dash cooldown,
HP regeneration (her corruption ebbing), luck, glow and knockback. On her beam (docs/proposals/BEAM-PEARLS-PLAN.md) her fire rate
is its **tick rate** (it burns, seals and charges faster), her shot speed its **carry** (the light it keeps far out) and
her knockback its **light pressure** (what is in it is pushed away from her). Stat pearls are never unique, so each copy stacks
(including the drawbacks: Glass Squid, Sea Cucumber, Pufferfish Pout and Cannonball all trade something away).

**Luck**, per point (each capped; `LuckTuning`): every freed foe leaves one more shell; a level without a clam room gets
one with a 10% chance (at most 60%); each shell she picks
up counts twice with an 8% chance (at most 60%; a second shell flies to her counter); a light spring is 10% likelier (at
most +60%) — on an adventure level, a second one — and holds 15% more healing (at most
twice as much). A level is planned with her luck as she
comes to it (the level below is laid out while she plays the one above); a run without luck gets the very same levels.

## Reading a pearl

A pearl looks like what it does (`PearlKey`, applied as the catalogue loads; nothing in `items.json` paints it). Each
thing it does that helps her stands for a colour, weighted by how strong it is against a usual helping of it (+20 max
HP, +0.5 damage, ×1.3 and so on); the strongest is the pearl's middle colour (also its light), the next one or two blend
in beside it. A pearl of one effect is that colour from light to deep. A drawback colours nothing (Glass Squid's −20
max HP leaves it all red).

| Colour | What it does |
|---|---|
| Violet | Her beam's shape: split rays, a lance, a beam behind or turning round her; and its width |
| Cyan | Her light's paths: bounce, fork, return, ghost, pierce, lean, flare, linger, arc; and its reach |
| Orange | Her beam's rhythms and tick rate: focus, spin-up, snap |
| Amber | Her light's echoes: flares, spines, gather |
| Ember, green, ice | Her light's colours: ember, bloom, frost (docs/LIGHT-AND-GRAPH.md §10) |
| Pale lemon | Glow: its reach, her fireflies |
| Red | Damage: damage, crits, the crab's burning pearls |
| Blue | Her beam's carry |
| Green | Health: max HP, mending, healing |
| White | Mobility: swim speed, dash cooldown |
| Gold | Luck and shells |
| Tan | Finding the way: maps, landmarks |
| Bronze | Light pressure (knockback) |
| Silver | Defence: foam, slower enemy shots, shields |
| Lavender | Active recharge |

The icon engraved on it is the kind of effect that weighs most (`PearlIcon`, `pearl_engraving.gdshaderinc`), cut dark
into the face turned to the camera, wherever the pearl is drawn:

| Icon | Kind of effect |
|---|---|
| Up-pointing arrow | Stat up |
| Three dots in a chevron pointing up | A word of her light (shape, path, rhythm, echo) |
| Shield | Defence |
| Nautilus shell | Luck, shells and maps |
| Feather | Mobility |
| Swirl | Status effect |
| Five-armed star | Active pearl (its colours by what it does: Tide Pulse bronze and silver, Whale Song green, Prism Pearl red, green and blue) |

## Absorbing a pearl: the ritual

Taking a pearl is a moment of its own. It plays round Clementine while she swims on, so it never interrupts play
(`PearlRitual`, `BellView.SyncRitual`, `CombatView`).

- **Seen before it is taken:** the clam's mouth faces the room's entrance as far as it can with the pearl in sight: never
  past side-on to the camera, where its upper valve would stand in front of it, so the pearl's icon always shows. Within 6
  m of any pearl (in a clam room, on a shop's or the crab's stand, or loose on the reef), the absorbing banner's card,
  72% of its size, floats up over the nearest with what the pearl does (`PearlHint`), and fades as she leaves or begins
  to absorb it.
- **Floating and glowing:** every pearl floats slowly up and down (0.3 m, about every 7 s; a clam room's half as far,
  just over its valve, `CombatView.PearlBob`) and sheds its own light round it (an omni light, 8 m), with a haze of its
  colour on the water (4.5 m, `TopDownMain.SyncPearlGlows`). Round it the reef keeps its colour as in her light, so a
  pearl shows in the dark of a secret room's maze.
- **The pull:** whenever a pearl is within 6 m, her tentacles and oral arms are drawn toward it like a magnet. The
  nearer it is, the harder the pull.
- **Touch (0 s):** the ritual begins. One plays at a time. A bought pearl begins its ritual by her too. The pearl
  lifts and circles her bell, and her strands reach for it. A card rises at the bottom centre of the HUD
  (`PearlBanner`): "Absorbing" over the pearl as the pause menu lists it — the pearl in its colours, its name, its
  tagline and its effects (standing above an achievement card when one shows).
- **Mimic (0–1.2 s):** like a chameleon, she takes on the pearl's look: its colours with its icon engraved, and
  its glow, spreading over her bell and strands. The pearl pales to a milky, empty vessel as its light leaves it. Her
  light swells.
- **Hold (1.2–1.65 s):** she wears the new look. The emptied pearl trembles.
- **Shatter (1.65 s):** the pearl breaks into pieces that tumble out and dissolve, with a few last glints. Her
  strands let go and fall back to their natural place.
- **Burst (1.95 s):** her light bursts out in a strong pulse round her, like an explosion, with a bloom on the water.
  She flashes white and returns to her own colours. Only now is the pearl hers: it is absorbed, and the card reads
  "Absorbed"; it holds 3.5 s more to be read, then sinks away.
  A dive cut through a ritual completes it, so a pearl is never lost.
- **Haptics:** a hum rising from touch to a trembling peak while she holds the look, a sharp tap on the shatter, and
  the climax, a full-strength pulse, on the burst.

## The pearls

<!-- generated: PearlsPageTests (OCTO_WRITE_DOCS=1 dotnet test regenerates it) -->

| Kind | In play | Designed, not yet in play |
|---|---|---|
| Shapes | 7 | 0 |
| Paths | 9 | 0 |
| Colours | 3 | 0 |
| Rhythms | 3 | 0 |
| Echoes | 3 | 0 |
| Glow | 5 | 0 |
| Stats and others | 19 | 6 |
| Active pearls | 7 | 6 |
| The crab's | 5 | |
| **All** | **61** | **12** |

### Shapes

How her light leaves her.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Plankton Swarm** | *Tiny friends* | +19% beam tick rate; Splits the beam into 3 rays, each 46% of the beam; −40% beam width; −15% burn of each ray | Yes | ★★ | clam rooms |  |
| **Nautilus Spiral** | *Golden ratio, golden shots* | A fainter beam turns slowly round her, always (50% as bright, 70% as long) | Once | ★★ | clam rooms |  |
| **Sunbeam** | *A shaft of the surface, bottled* | Her beam becomes a lance: 160% as long, 50% as wide, carrying its light to its end | Once | ★★★★ | clam rooms, bosses, the whale |  |
| **Double Helix** | *Twice the twist* | Splits the beam into 2 rays, each 62% of the beam; The beam's rays weave across her aim in a double helix | Once | ★★★ | clam rooms |  |
| **Triple Tentacle** | *Why squirt once?* | Splits the beam into 3 rays, each 46% of the beam | Once | ★★★ | clam rooms, bosses |  |
| **Hammerhead** | *Wide-angle view* | Opens the beam into a 5-ray floodlight, each 32% of the beam | Once | ★★★ | clam rooms, bosses |  |
| **Rear Fin** | *Eyes in the back of your mantle* | A fainter beam shines out of her back too | Once | ★ | clam rooms, shops |  |

### Paths

What a ray of hers does where it lands, on rock or on a body, or along the way.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Electric Eel Tail** | *Zzzap!* | What her beam burns arcs its burn to 2 more within 4 m, at half the rate | Yes (each copy: What her beam burns arcs its burn to 1 more within 4 m, at half the rate) | ★★★ | clam rooms |  |
| **Mirror Scale** | *Shiny side out* | Her light bounces off rock (60% of it) | Yes (each copy: Her light bounces off rock (60% of it)) | ★★ | clam rooms |  |
| **Anglerfish Lure** | *Follow the light* | Her beam leans toward the nearest creature near it, up to 10° off her aim | Once | ★★★ | clam rooms |  |
| **Swordfish Bill** | *En garde* | Her light runs on through creatures: their bodies stop none of it and cast no shadow | Once | ★★ | clam rooms |  |
| **Ghost Jelly** | *Walls are a suggestion* | Her light passes through a wall up to 3 m thick, 50% as strong beyond | Once | ★★ | clam rooms, secret rooms |  |
| **Mitosis** | *One becomes two* | Where her light lands on rock it forks in two (70° apart, 30% of it each) | Once | ★★ | clam rooms | bubble_bath |
| **Boomerang Shrimp** | *Always comes back* | Her light that lands on rock comes back along its way (50% of it): what stands in it is lit from both sides | Once | ★★ | clam rooms |  |
| **Starfish Arm** | *Grows back bigger* | Her beam flares, widening along its length: 50% wider again at its tip | Once | ★★ | clam rooms | big_bubble_energy |
| **Bubble Coral** | *Little ones grow up* | Where her beam ends, a pool of her light stays 2.5 s and goes on burning: swept, it paints a trail | Once | ★★ | clam rooms, shops |  |

### Colours

What her light does to what it lights; two or three mix (docs/LIGHT-AND-GRAPH.md §10).

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Frost Kelp** | *Grown in the cold seep* | Her light turns to frost: what it lights slows, and stands still after 2 s | Yes | ★★ | clam rooms, shops |  |
| **Fire Coral** | *Don't touch* | Her light turns ember: what it lights keeps burning 2.5 s after it leaves | Yes | ★★ | clam rooms, shops |  |
| **Glowing Moss** | *Soft and luminous* | +22% reach of her glow and her beam; +0.1 HP mended per second; Her light turns to bloom: ink and ash clear ×2 as fast in it, and a murkling it frees leaves ×1.5 the light | Yes | ★ | clam rooms, shops |  |

### Rhythms

How her beam changes the longer it is held.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Pearl Diver** | *Patience makes pearls* | Held steady, her beam focuses: over 1.5 s it narrows and burns up to ×3; swinging it loses the focus | Once | ★★★ | clam rooms, bosses |  |
| **Sardine Run** | *A million small reasons* | Held without a break, her beam spins up: ×1.8 burn after 3 s | Once | ★★★ | clam rooms, bosses |  |
| **Pistol Shrimp Claw** | *Snap. Bang.* | Held on a creature, her beam snaps every 0.8 s: a burst of light 2.5 m wide that pushes it back | Once | ★★★ | clam rooms, bosses |  |

### Echoes

What the light a creature had drunk does when her light frees it.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Starburst Gland** | *Bursts with borrowed sun* | What her light frees flares: a ring of her light 3 m wide that burns and clears ink, in her colours | Once | ★★★ | clam rooms, curse rooms |  |
| **Lamprey Mouth** | *Every bite counts* | What her light frees sends its core of light flying to her, worth 3 more | Yes (each copy: What her light frees sends its core of light flying to her, worth 1 more) | ★★★ | clam rooms, curse rooms |  |
| **Fire Urchin Spine** | *Pass it on* | What her light frees shines out 6 spines of her light (3.2 m), in her colours | Once | ★★★ | clam rooms, curse rooms |  |

### Glow

Her glow's own voice: its reach, and the fireflies round her.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Abyssal Glow** | *Light from below the light* | +11% damage; +14% reach of her glow and her beam | Yes | ★★ | clam rooms, curse rooms |  |
| **Firefly Squid** | *Never blinks* | +4 fireflies of her light round her (each reveals, burns a little and carries her colours) | Once | ★★★★ | clam rooms, bosses |  |
| **Lantern Pearl** | *Carry the light down* | +34% reach of her glow and her beam | Yes | ★ | clam rooms, bosses |  |
| **Bubble Gland** | *Always one more in reserve* | +3 fireflies of her light round her (each reveals, burns a little and carries her colours) | Yes | ★★ | clam rooms, shops |  |
| **Anemone Pump** | *Squeeze, refill, repeat* | +1 firefly of her light round her (it reveals, burns a little and carries her colours); −50% time a firefly a murkling drank takes to come back | Yes | ★★ | clam rooms, shops |  |

### Stats and others

The plain adverbs: her numbers, her pickups, her maps.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Coral Crown** | *A reef princess's lost tiara* | +20 max HP; +8.6% damage; Heals 20 HP | Yes | ★★ | clam rooms, bosses |  |
| **Squid Ink Espresso** | *The Galleon's cook swore by it* | +30% swim speed; +15% beam tick rate | Yes | ★★ | clam rooms, shops |  |
| **Whale Lung** | *Deep breath!* | +7.4% beam reach; +14% beam width | Yes | ★★ | clam rooms |  |
| **Pufferfish Pout** | *Angry and proud of it* | +35% damage; −20% beam carry | Yes | ★★★ | clam rooms, bosses |  |
| **Lucky Sea Glass** | *Found only on moonlit tides* | +2 luck | Yes | ★ | clam rooms, shops | untouchable |
| **Glass Squid** | *See-through, and sharp for it* | +29% damage; −20 max HP | Yes | ★★ | clam rooms, curse rooms |  |
| **Sea Cucumber** | *Slow, soft, and very hard to stop* | +30 max HP; −15% swim speed; Heals 30 HP | Yes | ★ | clam rooms, shops |  |
| **Moon Jelly Heart** | *Beats in slow pulses* | +20 max HP; Heals 20 HP | Yes | ★ | clam rooms, bosses, the whale |  |
| **Shark Tooth** | *Still sharp* | +14% damage | Yes | ★★ | clam rooms, curse rooms |  |
| **Remora Sucker** | *Sticks to everything* | Pulls pickups and freed murklings' light toward her from 7.5 m (not 2.5 m) | Yes (each copy: Pulls pickups and freed murklings' light toward her from 7.5 m (not 2.5 m)) | ★ | shops, clam rooms |  |
| **Cuttlebone** | *Light as foam, hard as shell* | +30% beam carry; +7.4% beam reach | Yes | ★ | clam rooms, shops |  |
| **Giant Squid Eye** | *It sees your weak spot* | 10% chance of a ×3 critical hit | Yes (each copy: +5% chance of a critical hit) | ★★★ | clam rooms, bosses | shucked_in_fifteen |
| **Coral Polyp** | *Slowly, the reef rebuilds* | +0.25 HP mended per second | Yes | ★★ | clam rooms, the whale |  |
| **Pirate's Doubloon** | *Finders keepers* | +15 sand dollars; Shell caches hold 50% more | Yes | ★ | clam rooms, golden clams | hermit_hoarder |
| **Cannonball** | *Fire in the hold!* | +23% damage; −15% beam carry; +18% beam width; +100% light pressure | Yes | ★★ | clam rooms, bosses |  |
| **Captain's Hook** | *Get over here* | +150% light pressure | Yes | ★ | clam rooms, shops |  |
| **Dolphin Fin** | *Born to sprint* | +25% swim speed; −15% dash cooldown | Yes | ★★ | clam rooms |  |
| **Brass Compass** | *Points to what matters* | Every place shows on the maps before she reaches it (secret rooms stay hidden) | Once | ★ | shops, clam rooms |  |
| **Sea Chart Nautilus** | *Remembers every current* | Shows the minimap: the reef round her, top right | Once | ★ | every run's start |  |

### Active pearls

Used with their button, then they recharge. She carries three.

| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |
|---|---|---|---|---|---|---|
| **Tide Pulse** | *Room to breathe* | Three waves push everything loose 6.6 m away from her; recharges in 60s | Once | ★ | every run's start |  |
| **Prism Pearl** | *One light, three colours* | Splits her into three smaller Clementines, red, green and blue, for 6 s, each shining in its colour; recharges in 45s | Once | ★★★ | clam rooms, bosses |  |
| **Conch Horn** | *The sea answers* | Every murkling within 8 m is shaken loose, knocked away and stunned for 2 s; recharges in 30s | Once | ★★ | clam rooms, shops |  |
| **Bubble Shield** | *Pop-proof, mostly* | An untouchable bubble round her for 3 s; recharges in 20s | Once | ★★ | clam rooms, shops |  |
| **Glow Burst** | *Everybody shine* | For 5 s her beam beats, burning ×2 on each beat; recharges in 10s | Once | ★★ | clam rooms |  |
| **Ink Cloud** | *Now you don't* | An ink cloud (3 m) where she is, and for 4 s the Leak's creatures lose her; recharges in 20s | Once | ★★ | clam rooms, secret rooms |  |
| **Whale Song** | *A low hum that mends* | Heals 35 HP; recharges in 40s | Once | ★★ | the whale, clam rooms |  |

### The crab's pearls

Found only in a Crab Deal (docs/ADVENTURE.md §7), at the price of a quarter of her max HP.

| Pearl | Tagline | What it does |
|---|---|---|
| **Severed Nerve** | *It never stopped stinging* | A severed strand circles her, stinging as her arm does |
| **Molten Carapace** | *Hurt, it runs hot* | Under half health her light burns +60% and runs on through creatures |
| **Last Light** | *Not yet* | Once a level, a killing blow leaves her at 1 HP in a flood of light |
| **Claw Tithe** | *Paid in full* | Each cut the crab has taken adds 25% damage |
| **Black Sun** | *A little of the Leak, kept* | The shadow a creature casts down her beam burns what stands in it, as hard as her light would have |

### Constellations

What pearls make together, named (each announces itself: "Synergy!" with its name, a chime and a ring of her light).

| Constellation | Needs | What happens |
|---|---|---|
| **Pinball Storm** | Electric Eel Tail + Mirror Scale | Each glint where her light bounces arcs to the nearest creature |
| **Lighthouse** | Sunbeam + Nautilus Spiral | The beam turning round her is a full Lance |
| **Shatter** | Frost Kelp + Fire Coral | Ember and frost mix to magenta: what frost has stilled, ember shatters in a burst of steam |
| **Sunfire** | Fire Coral + Glowing Moss | Ember and bloom mix to gold: what it frees bursts in a ring of light |
| **Lull** | Glowing Moss + Frost Kelp | Bloom and frost mix to cyan: what it lights forgets her |
| **Daybreak** | Fire Coral + Glowing Moss + Frost Kelp | All three mix to white: every colour at once, and her light burns half again as hard |
| **Cathedral** | Mirror Scale + Mitosis | Every bounce forks: a cave fills with a lattice of light |
| **Hall of Mirrors** | Boomerang Shrimp + Mirror Scale | Light caught between two walls: everything between burns from all sides |
| **Pearl Necklace** | Pearl Diver + Mitosis | At full focus the beam's tip opens into a ring of 8 rays |
| **Will-o'-Wisp** | Ghost Jelly + Anglerfish Lure | Her light leans twice as far, toward creatures behind rock too |
| **Aurora** | Bubble Coral + any 2 of Fire Coral, Glowing Moss, Frost Kelp | Her pools of light hang twice as long, curtains of mixing colour |
| **String of Pearls** | Sardine Run + Pearl Diver | Spun right up, her beam keeps its focus through a swing |
| **Depth Charge** | Pistol Shrimp Claw + Pearl Diver | At full focus the snap is twice as wide and half again as strong |
| **Supernova** | Starburst Gland + Fire Urchin Spine | Each spine of a freed creature's light ends in a flare |

### Lantern forms

Three pearls of one tag re-theme her whole light (docs/LIGHT-AND-GRAPH.md §16).

| Form | Pearls with the tag | What happens | Its numbers |
|---|---|---|---|
| **Kraken Form** | 3 of: Triple Tentacle, Starfish Arm, Starburst Gland, Cuttlebone, Giant Squid Eye, Ink Cloud | The Crown: seven short rays shine all round her beside her beam |  |
| **Neon Rave** | 3 of: Anglerfish Lure, Ghost Jelly, Sunbeam, Moon Jelly Heart, Abyssal Glow, Glowing Moss, Firefly Squid, Lantern Pearl, Glow Burst | Her light cycles through all seven colours, every colour's effect in turn | +14% reach of her glow and her beam |
| **Shark Mode** | 3 of: Pufferfish Pout, Electric Eel Tail, Swordfish Bill, Shark Tooth, Lamprey Mouth, Hammerhead, Fire Urchin Spine | A narrower, hotter beam; what it frees sends her into a frenzy | +20% swim speed |
| **Coral Reef** | 3 of: Coral Crown, Fire Coral, Coral Polyp, Anemone Pump, Bubble Coral | Her light lingers where it lands, and standing in her own afterlight mends her | Where her beam ends, a pool of her light stays 2.5 s and goes on burning: swept, it paints a trail |
| **Pirate** | 3 of: Squid Ink Espresso, Pirate's Doubloon, Cannonball, Captain's Hook, Brass Compass, Sea Chart Nautilus | Gilded light: shells it touches fly to her, and what it frees drops more | +2 luck |

### Designed, not yet in play

In the catalogue (their numbers load and are tested), but not yet offered on the reef.

| Pearl | Kind | Tagline | What it does | Quality |
|---|---|---|---|---|
| **Barnacle Armor** | Stat | *Clingy but protective* | −10% swim speed; +30 foam HP | ★ |
| **Manta Shawl** | Stat | *Glide like a ray* | +20% swim speed; −20% dash cooldown | ★★ |
| **Brain Coral** | Stat | *Thinks two moves ahead* | +1 luck; +35% active item recharge speed | ★★ |
| **Oyster Pearl** | Stat | *A little grit, a little shine* | +1 luck; +10 max HP; +5 sand dollars | ★ |
| **Sea Sponge** | Stat | *Soaks it up* | What her light frees gives 3 foam HP | ★ |
| **Diver's Watch** | Stat | *Time moves slower down here* | −20% enemy shot speed | ★★ |
| **Tidal Wave** | Active | *Surf's up* | A wave that pushes and hurts everything ahead (10, 12 m); recharges in 40s | ★★ |
| **Treasure Map** | Active | *X marks a lot of spots* | Reveals the level, secrets included; recharges in 60s | ★ |
| **Mimic Clam** | Active | *What's inside? Something else now* | Rerolls the pearls on offer in the room; recharges in 60s | ★★★ |
| **Sea Dice** | Active | *Roll the tide* | Rerolls the pickups nearby; recharges in 30s | ★ |
| **Kraken Call** | Active | *Something big owes you a favour* | A great tentacle strikes the creatures about her (25); recharges in 60s | ★★★ |
| **Anchor Drop** | Active | *Straight down* | Opens a way straight down to the next level; recharges in 30s | ★ |

<!-- end of generated -->
