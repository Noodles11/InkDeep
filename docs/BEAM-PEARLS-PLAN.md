# Beam pearls: a plan

Her beam is her one attack (docs/LIGHT-AND-GRAPH-PROPOSAL.md §5), so every pearl that only changed her shots does
nothing now. This plan carries each one over to the beam where light gives it a natural meaning, and drops the ones
where it does not. Not built yet: it is for review.

## What the beam already takes

These need nothing:

- **Damage** (Coral Crown, Glass Squid, Shark Tooth, Abyssal Glow, Pufferfish Pout, Cannonball, Claw Tithe; Neon Rave,
  Shark Mode, Pirate): the beam burns faster.
- **Range** (Whale Lung, Cuttlebone): the beam reaches farther.
- **Shot size** (Whale Lung, Cannonball): the beam's cone widens.
- **Chance pearls** (Frost Kelp, Fire Coral, Sea Nettle Sting, Stingray Barb, Siren Song, Giant Squid Eye): rolled once a
  second for each creature the beam keeps burning.
- **Electric Eel Tail**: arcs from what the beam burns.
- **Rear Fin**: a second beam behind her.
- **Sunbeam**: a long, narrow searchlight.
- **Lighthouse**: a fainter beam turning round her.
- **Swordfish Bill**: the beam goes through creatures (they no longer block it).

## The stats that change meaning

| Stat | On shots | On the beam |
|---|---|---|
| Fire rate (Squid Ink Espresso, Plankton Swarm) | throws faster | **Work rate**: the beam seals leaks, fills sealed walls' carvings, charges crystals and lights lamp-shells faster (× the stat). Creatures are not affected, so it does not double the damage pearls. |
| Shot speed (Cuttlebone; drawbacks: Pufferfish Pout, Cannonball) | bubbles fly faster | **Carry**: the beam keeps more of its strength far from her: the far end of its falloff (`LightTuning.FarLight`) rises with the stat, or drops with it. |
| Knockback (Captain's Hook, Cannonball) | hits push foes | **Light pressure**: creatures in the beam are pushed slowly away from her (× the stat), more near her. |

## Each shot pearl

| Pearl | On shots | On the beam | Keep? |
|---|---|---|---|
| **Triple Tentacle** | 3 shots, each 46% | The beam splits into **3 narrower beams** fanned across her aim (±12°), each worth 46%. | Keep |
| **Hammerhead** | a 5-pellet cone, each 32% | **5 beams** in a wide fan (±30°), each worth 32%: a floodlight. | Keep |
| **Double Helix** | 2 shots twisting round each other | **2 beams** weaving across her aim (±8°, out of step), each worth 62%. | Keep |
| **Plankton Swarm** | 3 small shots, + fire rate | **3 thin beams**, close together (±5°), each worth 46%; its fire rate becomes work rate. | Keep |
| **Anglerfish Lure** | shots home | **Homing light**: the beam leans toward the nearest corrupted creature within 20° of her aim (up to 10° off it). The shot decoy goes. | Keep |
| **Mirror Scale** | bounces off rock 2× | **Reflects**: where the beam meets rock it reflects off the rock face once (+1 per copy), each reflection at 60% strength and range. | Keep |
| **Ghost Jelly** | through walls | **Through rock**: the beam passes through one wall up to 3 m thick, at half strength beyond it (not yet built). | Keep |
| **Mitosis** | splits in two on a pop | **Forks**: where the beam stops (rock or a creature), two short beams fork off at ±35°, each worth 30%, half its range. | Keep |
| **Starfish Arm** | grows with distance | **Flares**: the cone widens along its length, half as wide again at its tip. | Keep |
| **Pearl Diver** | hold to charge a pearl, up to ×3 | **Focus**: held steady on one spot (aim moved under 4° over the last second), the beam narrows and its burn builds to ×3 over 1.5 s. Swinging it starts the focus over. | Keep |
| **Sardine Run** | hold to spin up a stream | **Spin-up**: the longer the beam is held without letting go, the stronger it burns, up to ×1.8 after 3 s, with a cone 30% narrower. Letting go starts it over. | Keep |
| **Starburst Gland** | shots burst into light | **Flare on freeing**: a creature the beam frees bursts in a ring of her light (3 m) that burns the corrupted near and clears the ink. | Keep |
| **Pistol Shrimp Claw** | collapse with a bang | **Snap**: held on a creature, every 0.8 s a snap of light bursts there, burning everything within 2.5 m and pushing it back. | Keep |
| **Molten Carapace** (crab) | under half health, shots pierce and do +60% | Under half health the beam goes through creatures and burns ×1.6. | Keep |
| **Glow Burst** (active) | bubbles ×2 for 5 s | The beam burns ×2 for 5 s; a flash of light. | Keep |
| **Nautilus Spiral** | shots spiral | No meaning for a beam (a swaying beam only makes aiming worse). | **Drop** |
| **Boomerang Shrimp** | shots return | Light does not come back. | **Drop** |
| **Firefly Squid** | Fire pours out a surge of light | The beam already is a continuous surge, and there is no Fire. | **Drop** |
| **Bubble Coral** | bubbles hover longer and merge | About her bubbles only. | **Drop** |
| **Bubble Gland**, **Anemone Pump** | the bubbles' reserve and refill | About her bubbles only. | **Drop** |
| **Twin Siphon** | +1 shot per throw | The same as Triple Tentacle's split beams, weaker. | **Drop** |

## Synergies and transformations

| Synergy | On the beam | Keep? |
|---|---|---|
| **Pinball Storm** (Eel Tail + Mirror Scale) | Each reflection point arcs lightning to the nearest corrupted creature. | Keep |
| **Cell Bloom** (Mitosis + Starfish Arm) | The forks flare too. | Keep |
| **Steam Vent** (Frost Kelp + Fire Coral) | A frozen creature the beam sets burning bursts into steam (already about statuses). | Keep |
| **Pearl Necklace** (Pearl Diver + Mitosis) | At full focus, the beam's end forks into a ring of 8 short rays. | Keep |
| **Depth Charge** (Pistol Shrimp + Pearl Diver) | At full focus, the snap is twice as wide and half again as strong. | Keep |
| **Will-o'-Wisp** (Ghost Jelly + Anglerfish Lure) | The homing beam leans twice as far, through rock. | Keep |
| **Toxic Bloom** (Sea Nettle + Mitosis) | The forks carry poison at twice the chance. | Keep |
| **Feeding Frenzy** (Shark Tooth + Lamprey Mouth) | Each creature freed makes the beam burn ×1.5 for 3 s. | Keep |
| **Lighthouse** | Built. It needs Sunbeam + Nautilus Spiral; with the Spiral dropped, it becomes **Sunbeam + Lantern Pearl**. | Keep, re-keyed |
| **Black Tide** (Tidal Wave + Starburst Gland) | Not about shots: unchanged. | Unchanged |
| **Tuna Rang** (Boomerang + Swordfish) | Its Boomerang is dropped. | **Drop** |
| **Guided Inkfish** (Starburst + Anglerfish) | Homing bombs: nothing left once both are beam pearls. | **Drop** |
| **Big Mad Puff** (Pufferfish Pout + Starfish Arm) | Shots bursting into spikes: no beam meaning. | **Drop** |
| **String of Pearls** (Sardine Run + Pearl Diver) | Focus and spin-up simply stack. | **Drop** |
| **Broadside** (Cannonball + Rear Fin) | Exploding rear shots: no beam meaning. | **Drop** |
| **Prism Pearl** synergy (Sunbeam + Pearl Diver) | Its name is the active Prism Pearl's, and Pearl Necklace already does it. | **Drop** |
| **Supernova**, **Lance of Light**, **Helix Lamp**, **Searchlight** | Built on Firefly Squid's surge; Starburst, Swordfish, Double Helix and Hammerhead now do each on the beam. | **Drop** |
| **Kraken Form** (tentacle) | 8 faint short rays all round her while the beam shines, each worth 15%. | Keep |
| **Neon Rave** (glow) | The beam cycles through colours, + damage (the damage already counts). | Keep |

## How it would be built

1. **Many beams** (`PlaneLight`): the beam becomes a list of rays with their own direction, range, cone and worth,
   which `HerBeams` and `BeamLight` already take. This covers the split pearls, Kraken Form, the forks and reflections.
   `BeamView` draws each ray as it draws the rear beam now.
2. **The stats' new meanings**: work rate (sealing, carvings, crystals, lamp-shells), carry (far light) and light
   pressure.
3. **Held beam**: focus (Pearl Diver), spin-up (Sardine Run), snap (Pistol Shrimp Claw), the synergies on them.
4. **Bending light**: homing (Anglerfish Lure), reflections (Mirror Scale, Pinball Storm), through rock (Ghost Jelly,
   Will-o'-Wisp).
5. **On freeing**: Starburst Gland's flare, Feeding Frenzy; Molten Carapace and Glow Burst.
6. **The catalog**:
   - the dropped pearls and synergies leave the pools and `PlaneRun.PortedPearls`;
   - the kept ones join it as they are built;
   - Lighthouse is re-keyed;
   - `ItemCaption` says what each does to the beam;
   - `PearlKey`'s colours follow;
   - docs/PEARLS.md is regenerated.
7. **Tests per pearl** (`BeamPearlTests`), and a capture of each new beam shape.

Each step is playable on its own; 1 and 2 matter most, as they bring back the most pearls already in the pools.
