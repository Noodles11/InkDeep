# Pearls

> Every pearl in the item catalogue (`data/items.json`): what it does, where it is found and what unlocks it. Built
> (✅) marks the pearls ported to the top-down game (`PlaneRun.PortedPearls`): they lie in clam rooms, are sold in
> shops and fall from Queen Clam. The rest are designed and in the catalogue (their numbers load and are tested) but
> not yet in play. The effect lines are the game's own captions (`ItemCaption`). Generated from the catalogue; when
> the catalogue changes, this page should be regenerated.

| | Passive | Active | All |
|---|---|---|---|
| Pearls | 59 | 13 | 72 |
| Built | 41 | 4 | 45 |

- **Passive** pearls work on their own from the moment she absorbs one: her stats, her bubbles, or something that
  happens when she is hit or frees a creature. They stack.
- **Active** pearls are used with **F** and then recharge over time. She carries one at a time: a new one takes the
  old one's place, and the old one is gone (`PlaneRun.Add`). Every run starts with **Tide Pulse** and the **Sea
  Chart Nautilus** (`PlaneRun.StartingPearls`; never offered, since she always has them). Its charge is the rim of her bell, a light sweeping round it (DESIGN-TOPDOWN §2.5).
- **Quality** is ★ to ★★★★ (rarer, stronger). **Found in** lists the item pools that can offer it.
- **Unique** pearls are absorbed once a run and then leave the pools. A non-unique one (**No**) can be absorbed again:
  it stays in the pools at a lower chance (×0.35 of its weight per copy held, `ItemPools.RepeatWeight`). Each extra
  copy adds the whole pearl again, or what the column notes in brackets (the catalog's `stack`: one more chain or
  bounce, more reach, +5% chance, +1 s, +1 HP).

## Reading a pearl

A pearl looks like what it does (`PearlKey`, applied as the catalogue loads; nothing in `items.json` paints it). Each
thing it does that helps her stands for a colour, weighted by how strong it is against a usual helping of it (+20 max
HP, +0.5 damage, ×1.3 and so on); the strongest is the pearl's middle colour (also its light), the next one or two blend
in beside it. A pearl of one effect is that colour from light to deep. A drawback colours nothing (Glass Squid's −20
max HP leaves it all red).

| Colour | What it does |
|---|---|
| Red | Damage: damage, bigger hits, crits, exploding or charged shots, lasers, chain lightning, shards |
| Orange | Fire rate: fire rate, Sardine Run's stream, Firefly Squid's surge |
| Blue | Shot speed, and shots that steer (homing, spiral, boomerang) |
| Cyan | Range, and shots that go on through (pierce, spectral, bounces) |
| Violet | More or bigger shots: multishot, split, wave, rear, grow, shot size, her bubbles |
| Green | Health: max HP, regeneration, healing |
| White | Mobility: swim speed, dash cooldown |
| Gold | Luck and shells |
| Tan | Finding the way: maps, landmarks |
| Pale lemon | Glow |
| Bronze | Knockback |
| Silver | Defence: foam, slower enemy shots, shields |
| Ice, ember, lime, pink, lilac | Freeze, burn, poison, charm, slow; her light's colours: frost ice, ember ember, bloom green (docs/LIGHT-AND-GRAPH.md §10) |
| Lavender | Active recharge |

The icon engraved on it is the kind of effect that weighs most (`PearlIcon`, `pearl_engraving.gdshaderinc`), cut dark
into the face turned to the camera, wherever the pearl is drawn:

| Icon | Kind of effect |
|---|---|
| Up-pointing arrow | Stat up |
| Three dots in a chevron pointing up | Change to her shots |
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

## Passive pearls

### Stats

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

| Unique | Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|:---:|---|---|---|---|---|---|
| No | ✅ | **Coral Crown** | *A reef princess's lost tiara* | +20 max HP; +0.3 damage; Heals 20 HP | ★★ | treasure, boss |  |
| No | ✅ | **Squid Ink Espresso** | *The Galleon's cook swore by it* | +0.3 speed; +0.4 beam tick rate | ★★ | treasure, shop |  |
| No | ✅ | **Whale Lung** | *Deep breath!* | +1 range; ×1.3 shot size | ★★ | treasure |  |
| No | ✅ | **Pufferfish Pout** | *Angry and proud of it* | ×1.35 damage; −0.2 beam carry | ★★★ | treasure, boss |  |
| No | ✅ | **Lucky Sea Glass** | *Found only on moonlit tides* | +2 luck | ★ | treasure, shop | untouchable |
| No | ✅ | **Plankton Swarm** | *Tiny friends* | +0.5 beam tick rate; Splits the beam into 3 rays, each 46% of the beam; ×0.6 shot size; ×0.85 shot damage | ★★ | treasure |  |
| No | ✅ | **Glass Squid** | *See-through, and sharp for it* | +1 damage; −20 max HP | ★★ | treasure, curse |  |
| No | ✅ | **Sea Cucumber** | *Slow, soft, and very hard to stop* | +30 max HP; −0.15 speed; Heals 30 HP | ★ | treasure, shop |  |
| No |  | **Barnacle Armor** | *Clingy but protective* | −0.1 speed; +30 foam HP | ★ | treasure, shop |  |

### Shot modifiers

| Unique | Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|:---:|---|---|---|---|---|---|
| No (+1 chain) |  | **Electric Eel Tail** | *Zzzap!* | Hits chain lightning to 2 foes (50% damage) | ★★★ | treasure |  |
| Yes |  | **Nautilus Spiral** | *Golden ratio, golden shots* | Shots spiral outward | ★★ | treasure |  |
| No (+ 1 bounce) | ✅ | **Mirror Scale** | *Shiny side out* | Her light bounces off rock (60% of it) | ★★ | treasure |  |
| Yes | ✅ | **Anglerfish Lure** | *Follow the light* | Shots home onto foes | ★★★ | treasure |  |
| Yes | ✅ | **Swordfish Bill** | *En garde* | Shots pierce foes | ★★ | treasure |  |
| Yes | ✅ | **Mitosis** | *One becomes two* | Where her light lands on rock it forks in two (70° apart, 30% each) | ★★ | treasure | bubble_bath |
| No | ✅ | **Frost Kelp** | *Grown in the cold seep* | Her light turns to frost: what it lights slows, and stands still after 2 s | ★★ | treasure, shop |  |
| No | ✅ | **Fire Coral** | *Don't touch* | Her light turns ember: what it lights keeps burning 2.5 s after it leaves | ★★ | treasure, shop |  |
| Yes |  | **Sea Nettle Sting** | *It lingers* | 30% chance to poison foes for 4s | ★★ | treasure, curse |  |
| Yes | ✅ | **Boomerang Shrimp** | *Always comes back* | Her light that lands on rock comes back along its way (50%): lit from both sides | ★★ | treasure |  |
| Yes | ✅ | **Ghost Jelly** | *Walls are a suggestion* | Her light passes through a wall up to 3 m thick, 50% as strong beyond | ★★ | treasure, secret |  |
| Yes | ✅ | **Pearl Diver** | *Patience makes pearls* | Hold to charge a pearl (up to ×3 damage) | ★★★ | treasure, boss |  |
| Yes |  | **Sunbeam** | *A shaft of the surface, bottled* | Hold to charge a laser beam | ★★★★ | treasure, boss, whale |  |
| Yes | ✅ | **Firefly Squid** | *Never blinks* | Fire pours out a continuous surge of light instead of shots: 90% of your shots' damage per second to all it touches, clearing a little corruption where it ends. Your other pearls shape it | ★★★★ | treasure, boss |  |
| Yes | ✅ | **Double Helix** | *Twice the twist* | Splits the beam into 2 rays, each 62% of the beam; The beam's rays weave across your aim in a double helix | ★★★ | treasure |  |
| Yes | ✅ | **Triple Tentacle** | *Why squirt once?* | Splits the beam into 3 rays, each 46% of the beam | ★★★ | treasure, boss |  |
| Yes | ✅ | **Starfish Arm** | *Grows back bigger* | Shots grow with distance | ★★ | treasure | big_bubble_energy |
| Yes | ✅ | **Starburst Gland** | *Bursts with borrowed sun* | Shots burst into light (cleanses ground, strikes the corrupted near) | ★★★ | treasure, curse |  |
| Yes | ✅ | **Sardine Run** | *A million small reasons* | Hold fire: she spins up to a stream of small shots, four times as fast; ×0.7 shot size; ×0.3 shot damage | ★★★ | treasure, boss |  |
| Yes | ✅ | **Pistol Shrimp Claw** | *Snap. Bang.* | Shots collapse with a bang where they hit or go out, hurting everything near | ★★★ | treasure, boss |  |
| Yes |  | **Siren Song** | *Come closer…* | 15% chance to charm foes for 3s | ★★ | treasure, siren |  |

### More passives (DESIGN-2D §28)

| Unique | Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|:---:|---|---|---|---|---|---|
| No | ✅ | **Moon Jelly Heart** | *Beats in slow pulses* | +20 max HP; Heals 20 HP | ★ | treasure, boss, whale |  |
| No | ✅ | **Shark Tooth** | *Still sharp* | +0.5 damage | ★★ | treasure, curse |  |
| No (+range) | ✅ | **Remora Sucker** | *Sticks to everything* | Pulls pickups toward you | ★ | shop, treasure |  |
| No |  | **Manta Shawl** | *Glide like a ray* | +0.2 speed; ×0.8 dash cooldown | ★★ | treasure, whale |  |
| No | ✅ | **Cuttlebone** | *Light as foam, hard as shell* | +0.3 beam carry; +1 range | ★ | treasure, shop |  |
| No (+5% chance)| ✅ | **Giant Squid Eye** | *It sees your weak spot* | 10% chance of a ×3 critical hit | ★★★ | treasure, boss | shucked_in_fifteen |
| No | ✅ | **Coral Polyp** | *Slowly, the reef rebuilds* | +0.25 HP regeneration per second | ★★ | treasure, whale |  |
| No |  | **Brain Coral** | *Thinks two moves ahead* | +1 luck; ×1.35 active item recharge speed | ★★ | treasure, shop |  |
| No | ✅ | **Pirate's Doubloon** | *Finders keepers* | +15 sand dollars; Shell caches hold 50% more | ★ | treasure, goldenClam | hermit_hoarder |
| No | ✅ | **Cannonball** | *Fire in the hold!* | +0.8 damage; −0.15 beam carry; ×1.4 shot size; ×2 light pressure | ★★ | treasure, boss |  |
| No | ✅ | **Captain's Hook** | *Get over here* | ×2.5 light pressure | ★ | treasure, shop |  |
| No (+1s) |  | **Stingray Barb** | *Numbs on contact* | 30% chance to slow foes for 2s | ★ | treasure |  |
| No (+1HP) |  | **Lamprey Mouth** | *Every bite counts* | Each creature freed heals 3 HP | ★★★ | treasure, curse | hollow_maw |
| Yes | ✅ | **Hammerhead** | *Wide-angle view* | Opens the beam into a 5-ray floodlight, each 32% of the beam | ★★★ | treasure, boss |  |
| Yes |  | **Rear Fin** | *Eyes in the back of your mantle* | Also fires backwards | ★ | treasure, shop |  |
| No | ✅ | **Abyssal Glow** | *Light from below the light* | +0.4 damage; ×1.3 glow radius | ★★ | treasure, curse |  |
| No (+x1.5 glow, +0.1HP regen) | ✅ | **Glowing Moss** | *Soft and luminous* | ×1.5 glow radius; +0.1 HP regeneration per second; her light turns to bloom: ink and ash clear ×2 as fast in it, and a murkling it frees leaves ×1.5 the light | ★ | treasure, shop |  |
| Yes |  | **Fire Urchin Spine** | *Pass it on* | Freed creatures burst into 6 spines | ★★★ | treasure, curse | jesters |
| No |  | **Oyster Pearl** | *A little grit, a little shine* | +1 luck; +10 max HP; +5 sand dollars | ★ | treasure, goldenClam |  |
| No | ✅ | **Dolphin Fin** | *Born to sprint* | +0.25 speed; ×0.85 dash cooldown | ★★ | treasure |  |
| Yes | ✅ | **Brass Compass** | *Points to what matters* | Every place shows on the maps before you reach it (secret rooms stay hidden) | ★ | shop, treasure |  |
| Yes | ✅ | **Sea Chart Nautilus** | *Remembers every current* | Shows the minimap: the reef round you, top right, and the whole level's map. She starts every run with it, for now | ★ | start |  |
| No | ✅ | **Lantern Pearl** | *Carry the light down* | ×1.8 glow radius | ★ | treasure, boss |  |

### Bubbles on the throwing tentacle

| Unique | Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|:---:|---|---|---|---|---|---|
| No |  | **Twin Siphon** | *Two at a time* | +1 shot per throw (a volley shares her light) | ★★★ | treasure, boss |  |
| Yes | ✅ | **Bubble Coral** | *Little ones grow up* | ×1.5 bubble hover time; Her light lingers at the end of its range; shots that touch merge into one, bigger and stronger | ★★ | treasure, shop |  |

## Active pearls (Right mouse click/RB, recharge over time)

| Unique | Built | Pearl | Tagline | Effect | Quality | Found in | Unlocked by |
|:---:|:---:|---|---|---|---|---|---|
| Yes | ✅ | **Tide Pulse** | *Room to breathe* | She starts every run with it. Three quick waves of water from her bell within 1 s, each telegraphed by her bell drawing in hard (deeper each time) and leaving as it snaps open, each wider and harder than the last (out to 4, 5.3 and 6.6 m; thrown on at 5, 7 and 10 m/s). Each wave's front sweeps everything loose out ahead of it, sliding along rock: mobs and corrupted creatures, the reef's fish, shells, hearts, loose pearls, pots, their shots and throws (turned away from her); the first shakes off clinging murklings. When it is done the water is clear 6.6 m round her bell (about six of her). Rooted creatures, Hearthguards on their beds, shop stands, the boss and her own light stay put. Seen as the currents' moving water: three rings of it shoving the picture outward (PlanePushWave.cs); recharges in 60 s | ★ | start |  |
| Yes | ✅ | **Prism Pearl** | *One light, three colours* | Splits her light into three smaller Clementines for 6 s, in a triangle round the point she steers (each corner 1.4 m from it, the triangle turning with her aim): she is the green one at its front corner toward her aim, a red at the back corner on her left and a blue on her right, keeping their corners (her own controls) round the rock. Each fires its own volleys, on its own beat, at where she aims, its light and shots in its colour. A hit on any of them is a hit on her, one body of health. Splitting and flowing back together each take 0.45 s, with a jolt, the picture's colours pulled apart for a moment and a burst of light (PlanePrism.cs); recharges in 45 s | ★★★ | treasure, boss |  |
| Yes | ✅ | **Conch Horn** | *The sea answers* | Every murkling within 8 m is shaken loose, knocked away and stunned for 2 s (a ring of moving water out to its reach); recharges in 30 s | ★★ | treasure, shop |  |
| Yes | ✅ | **Bubble Shield** | *Pop-proof, mostly* | An untouchable bubble round her for 3 s; recharges in 20 s | ★★ | treasure, shop |  |
| Yes |  | **Tidal Wave** | *Surf's up* | A wave that pushes and hurts everything ahead (10, 12 m); recharges in 40 s | ★★ | treasure |  |
| Yes |  | **Treasure Map** | *X marks a lot of spots* | Reveals the level, secrets included; recharges in 60 s | ★ | shop |  |
| Yes |  | **Mimic Clam** | *What's inside? Something else now* | Rerolls the pearls on offer in the room; recharges in 60 s | ★★★ | treasure, secret |  |
| Yes |  | **Glow Burst** | *Everybody shine* | Her bubbles ×2 for 5 s; a flash of light; recharges in 10 s | ★★ | treasure |  |
| Yes |  | **Sea Dice** | *Roll the tide* | Rerolls the pickups nearby; recharges in 30 s | ★ | shop |  |
| Yes |  | **Kraken Call** | *Something big owes you a favour* | A great tentacle strikes the foes about her (25); recharges in 60 s | ★★★ | treasure, boss | siphonophore |
| Yes | ✅ | **Ink Cloud** | *Now you don't* | An ink cloud (3 m) where she is, and for 4 s the Leak's creatures lose her: murklings stop chasing and throwing, and a boss aims where it last had her; recharges in 20 s | ★★ | treasure, secret |  |
| Yes | ✅ | **Whale Song** | *A low hum that mends* | Heals 35 HP; recharges in 40 s | ★★ | whale, treasure |  |
| Yes |  | **Anchor Drop** | *Straight down* | Opens a way straight down to the next level; recharges in 30 s | ★ | shop, treasure |  |

## Synergies and transformations

Not pearls themselves, but what pearls do together. Built (✅) synergies announce themselves when they form: the
caption reads "Synergy!" with its name, a chime plays and a ring of her light goes out.

| | Synergy | Needs | What happens |
|:---:|---|---|---|
| ✅ | **Cathedral** | Mirror Scale + Mitosis | Every bounce forks: a cave fills with a lattice of light |
| ✅ | **Hall of Mirrors** | Boomerang Shrimp + Mirror Scale | Light caught between two walls: everything between burns from all sides |
|  | **Pinball Storm** | Electric Eel Tail + Mirror Scale | Each wall bounce emits a lightning arc |
|  | **Lighthouse** | Sunbeam + Nautilus Spiral | The beam sweeps in a rotating spiral |
|  | **Cell Bloom** | Mitosis + Starfish Arm | Split shots also grow |
|  | **Guided Inkfish** | Ink Sac + Anglerfish Lure | Homing bombs; explosions leave slowing ink clouds |
| ✅ | **Shatter** | Frost Kelp + Fire Coral | Ember and frost mix to magenta: what frost has stilled, ember shatters in a burst of steam |
| ✅ | **Sunfire** | Fire Coral + Glowing Moss | Ember and bloom mix to gold: what it frees bursts in a ring of light |
| ✅ | **Lull** | Glowing Moss + Frost Kelp | Bloom and frost mix to cyan: what it lights forgets you |
| ✅ | **Daybreak** | all three | All three mix to white: every colour at once, and her light burns half again as hard |
|  | **Tuna Rang** | Boomerang Shrimp + Swordfish Bill | Piercing boomerang hits twice and grows on the way back |
| ✅ | **Pearl Necklace** | Pearl Diver + Mitosis | A full-charge pearl bursts into a ring of 8 pearls |
|  | **Prism Pearl** | Sunbeam + Pearl Diver | A full-charge pearl fires lasers in 4 directions when it pops |
|  | **Black Tide** | Tidal Wave + Ink Sac | The wave detonates ink on every foe it hits |
|  | **Big Mad Puff** | Pufferfish Pout + Starfish Arm | Shots burst into a spiky shotgun blast when they pop |
|  | **Will-o'-Wisp** | Ghost Jelly + Anglerfish Lure | Spectral wisps home twice as hard |
|  | **Toxic Bloom** | Sea Nettle Sting + Mitosis | Split shots carry a double dose of poison |
|  | **Feeding Frenzy** | Shark Tooth + Lamprey Mouth | Every creature freed sends you into a fast-firing frenzy |
| ✅ | **String of Pearls** | Sardine Run + Pearl Diver | No holding: the stream fires pearls, charged as far as it has spun up |
| ✅ | **Depth Charge** | Pistol Shrimp Claw + Pearl Diver | A full-charge pearl's blast is twice as wide and half again as strong |
|  | **Broadside** | Cannonball + Rear Fin | Shots out of the back explode |
| ✅ | **Supernova** | Firefly Squid + Starburst Gland | The surge bursts into light at its tip and wherever it strikes |
| ✅ | **Lance of Light** | Firefly Squid + Swordfish Bill | The surge runs through every creature in its path |
| ✅ | **Helix Lamp** | Firefly Squid + Double Helix | The surge twists into two strands spiralling round each other |
| ✅ | **Searchlight** | Firefly Squid + Hammerhead | The surge opens into a wide cone of light |

| Transformation | Pearls with the tag | What happens |
|---|---|---|
| **Kraken Form** | tentacle | Short beams shine all round her, 8 ways; ink trail |
| **Neon Rave** | glow | Shots cycle colours, +damage |
| **Shark Mode** | predator | Speed and damage up, freeing heals |
| **Coral Reef** | coral | The reef mends you: slowly regain health |
| **Pirate** | galleon | +1 damage, +2 luck, foes drop more coins |

## The crab's pearls

Found only in a Crab Deal (docs/ADVENTURE.md §7), at the price of a quarter of her max HP.

| Pearl | Tagline | Effect |
|---|---|---|
| **Severed Nerve** | *It never stopped stinging* | A severed strand circles her, stinging as her arm does |
| **Molten Carapace** | *Hurt, it runs hot* | Under half health her shots pierce and do 60% more |
| **Last Light** | *Not yet* | Once a level, a killing blow leaves her at 1 HP in a flood of light |
| **Claw Tithe** | *Paid in full* | Each cut the crab has taken adds 25% damage |
