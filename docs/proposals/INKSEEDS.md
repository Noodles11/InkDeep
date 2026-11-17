# Inkseeds: traps of ink on the cube reef

A proposal. Small seeds of ink lie scattered through the reef, unseen in the dark. Lit, one wakes and swells; unless
she holds her beam on it until it dies, it bursts in a ring of darkness that swallows the light round it and throws
everything back. §1–§7 the rules (the numbers marked *(tune)* are first guesses), §8 the open questions.

## 1. What it is

- A dot of ink the size of a lesser murkling (0.36 m across, `MurkSizeTuning`'s smallest), resting in the water at her
  plane. It is not a creature: it does not move, attack or drain anything by itself; it has no meter, no ring and no
  core of light.
- **Unseen in the dark:** drawn only where her light falls on it (her glow, her beam, a lit charge stone), as a murkling
  is, in the murkling's black-hole look (`murkling.gdshaderinc`'s `hole`), small and still.

## 2. Its heartbeat

- Within 14 m of her *(tune)* it pulses, lit or not: a double beat of the water's hot-air shimmer rings out from where
  it is (the game's `WaterSwells` rings), a heartbeat. The nearer she is, the quicker: one beat every 1.6 s at 14 m,
  every 0.35 s at 2 m *(tune)*, easing between.
- Armed (§3), the beat quickens further as it swells, to a flutter just before it bursts.
- This is how she finds them in the dark: it gives itself away, as the Unseen Test asks (THEME-BIBLE).

## 3. Lit, it wakes

- **Arming:** the first time any light of hers falls on it (her glow, her beam, a shot's light), it wakes, for good.
- **Swelling:** awake, it grows from its size to twice it over 3 s *(tune)*, always, whether lit or not, **except while
  her beam is on it**: then it holds its size.
- **Defusing:** her beam held on it for 2 s in all frees it. *(Default: the 2 s must be unbroken; the beam leaving it
  sets them back to naught.)* It then implodes as a freed murkling does (`BlightView.Implode`) and is gone, leaving no
  core, no shells and no ember.
- **Bursting:** at twice its size it always goes off, whatever lights it then (the beam on it at that moment no longer
  saves it).

## 4. The burst

- **The darkness:** a ring of black spreads from it to its reach, 4 m *(tune)*. Every light within it goes out for 3 s
  *(tune)*: her glow and her beam if she is inside it (as if she had turned her glow off), her floating bubbles, a lit
  charge stone's light (its charge emptied). Lights outside the ring are untouched.
- **The ripple:** one strong ring of hot-air shimmer, the strongest the water shows (`WaterSwells`, strength 2).
- **Knockback:** everything within its reach is thrown straight away from it: her, every creature, her shells, the
  reef's fish. 9 m/s at its middle *(tune)*, easing to nothing at its reach; rock stops it (`PlaneKnock`).
- **Damage:** only to her, if she is within its reach: a fixed 12 × (1 + Menace) *(tune)* (12 on a layer with no Menace,
  24 at full Menace), not through her shield; her dash's invulnerability does not save her. Creatures take none.

## 5. Where they lie

- Scattered as each cube is laid (`TopDownMain.Reef`'s `LaidHere`, as its charge stones are): 1 + 2 × Menace per cube
  *(tune)*, rounded, from the seed and the cube alone (the same cube always the same seeds), in its open water, at least
  6 m from its gates' mouths, its wells, its sockets' passages and each other, and 12 m from where she is when the cube
  is laid.
- **None on the warm-up layer, none in the cube she dives into, none in a boss's arena.**
- Kept with the cube's record: leaving a layer and coming back, its seeds are where they were, and the burst and
  defused ones gone.

## 6. Water carries them

- The reef's currents move them: the frontier drift and the wake current (`CubeCurrents`) push a seed as they would push
  her, at the same speed, rock stopping it. Any other moving water in a level (geysers, level currents) pushes them too.
- A burst's knockback does not move other seeds *(default)*.

## 7. Look and sound

- Asleep: unseen but for its heartbeat. Lit: its black-hole look, small; its core a dull ember.
- Armed: its core glows a deepening red-violet as it swells, flickering faster toward the end; its photon ring
  brightens.
- Defused: its implosion, a soft glassy chime.
- Burst: the ring of black spreads and fades, the strong ripple, a deep thump.

## 8. Open questions

1. **Lit vs. growing.** You said light starts the timer, but also that it can't grow while lit. My default reading (§3):
   any light of hers *arms* it, it grows whether lit or not, and only **her beam** on it stops the growing. Is that
   right? Or should *any* light stop it, so it only grows once she looks away?
2. **Defusing** (§3): must the 2 s of beam be unbroken (the default), or should they add up across several looks?
3. **Damage** (§4): 12 × (1 + Menace)? With "× Menace" taken literally, it would deal nothing on a layer with no Menace.
4. **The darkness** (§4): 4 m reach, every light inside out for 3 s, including her own glow and beam?
5. **How many** (§5): 1 + 2 × Menace a cube, none on the warm-up layer?
6. **Its size**: twice its size is still small (0.72 m). Should the burst's reach (4 m) be bigger or smaller than that
   suggests?
7. **The name**: "inkseed" for now. A name from the folklore you'd like instead?
