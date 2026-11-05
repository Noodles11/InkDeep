# The dive

The game is a dive: generated levels, one after another, deeper and deeper. Each level's hole opens when its leak is
sealed, and swimming over it sinks her into the next level. There is no open map. Lantern Bay's folk are met in havens,
calm levels that come now and then between the others. This replaces the open reef of docs/REEF-WORLD.md and the region
maps of docs/RPG-PROPOSAL.md.

## 1. Jellies and dives

- **A jelly is a save slot.** The title's jellyfish cards (docs/TITLE-MENU-PROPOSAL.md) are the three slots. Choosing
  one continues her dive where it stands, or starts a new one.
- **A dive** (`Jelly.Dive`, a `SuspendedRun`) runs Depth 1 to Depth 6, five levels a depth with a boss on the fifth,
  then loops to the next cycle as runs always have (`LevelPlan.NextOf`). It is saved at the start of every level.
- **Its seed** comes from the jelly's own seed and how many dives she has ended (`Dives.SeedOf`), so every dive is a
  different reef.
- **The tutorial.** A new jelly's first dive starts in the tutorial (Depth 0); once she has dived out of it
  (`Jelly.Tutored`) her dives start in Depth 1.

## 2. What she keeps

- **Always hers:** her level, experience, skill points and skills, the folk she has freed, and what is banked.
- **Banked:** the pearls she owns between dives (`Jelly.Passives`, `Actives`, `Slots`) and her shells (`Jelly.Shells`).
  A new jelly starts with the run's starting pearls.
- **Carried:** everything found in a dive since the last haven, pearls and shells alike. A haven banks it all (§4).
- **Dimmed** (her meter full): the dive ends. What she carried is lost, what is banked and always hers stays, and the
  next dive starts at the top again.

## 3. Camera

The camera stands back far enough that one screen shows about 15% of a level (about 2.2 times as far as before).

## 4. Havens

A haven is a level of its own between two others: a small clearing in the bay's warm look, safe (no corruption, no
creatures), its hole open onward.

- **When:** never straight after a haven; after two levels without one, 35% of the time; after three, always. A haven
  never takes a boss level's place.
- **Who is there:** a home for every neighbour she has freed, each with their service (docs/NPC-BOSSES-PROPOSAL.md):
  Tull's Tide Fan, Kip's mirror, Fennel's brood bed, Ottilie's pearl collector, Bubbles's dusk dance.
- **Always:** a shop and a light spring, and reaching it banks what she carries.

## 5. Folk as the depths' bosses

Depth 1's boss is Grandmother Tull, Depth 2's Kip, Depth 3's Fennel, Depth 4's Ottilie, Depth 5's Bubbles; Depth 6 ends
with Drippus Prime. Freeing a neighbour unlocks her for every later haven; once she is freed, her depth's boss in later
dives is a generated one again.

## 6. Leak caves

The curse dens become leak caves: a cave mouth in a level's rock, leading down into a sub-level half a level's size.

- **Inside, it harms her** for as long as she stays, until its leak is sealed.
- **She can leave at any time,** back out of its mouth into the level.
- **Sealing its leak** stops the harm at once, heals her whole and leaves a pearl.
- **The level keeps its own leak:** sealing that opens the hole onward, so the caves are a choice, never the way down.

## 7. Requests and chains

- **The Tide Fan's requests** are dive goals: seal leaks, free a prime, clear a den, wash ash, in the levels to come.
  They are untied at the next haven. The minimap's pointers show the way to the level's leaks and its hole.
- **The folk's chains:** a "find" step is something lying in a random level of a named depth; a "reach a cave" step is
  reaching that depth.

## 8. Building it

Each step is playable on its own:

1. **Jellies dive** (built): the slots start and continue dives, and dimming ends one (§1, §2).
2. **The camera** stands back (§3).
3. **Havens** and their services (§4).
4. **Folk bosses** for the depths (§5).
5. **Leak caves** in place of the curse dens (§6).
6. **Requests and chains** as dive goals (§7).
7. **The open reef removed:** its map, zones, mouths, homeward current and charts.
