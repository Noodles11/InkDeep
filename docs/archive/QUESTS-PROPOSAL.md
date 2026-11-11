# Requests: repeatable side quests

Step 5 of docs/archive/RPG-PROPOSAL.md (§13), its first part: a few **repeatable side quests** for the open reef, so
Clementine has reasons to go back out into the caves and comes home with new pearls and more experience. The named
creatures, the go-home cutscene and the first key-quest chain are the rest of step 5 and are not covered here.

## 1. In one breath

The bay's creatures cannot write, and they cannot swim into the Leak. What they can do is ask. In the commune stands
the **Tide Fan**, a great old sea fan where the bay ties its troubles: each request is a ribbon of kelp, knotted round
a little token of what it offers. Clementine swims to the fan, takes a ribbon, goes out and does what it asks, and
comes home to untie it. Its token and its experience are hers.

## 2. Where she gets them: the Tide Fan

### 2.1 Why not a signboard

- **No human things yet.** A signboard is a made thing with writing on it. The Theme Bible keeps human objects out
  of the reef until Depth 4, where pipes and plastic start the foreshadowing (§4.4). A board in the bay would wink at
  the Tank five depths early (the Never-Wink Test).
- **Reef creatures don't write.** A field-guide voice would never have a clownfish pin up a notice.
- **It must move like water.** Everything in the ocean sways (the Sway Test). A board stands still.

### 2.2 The fan

- **What it is:** a gorgonian sea fan, taller than she is, rooted on a rock in the **commune** (where the freed come
  home; `shallows.json` `fan`, x 110, y 342, in the bay's corner of the reef). It is a lacy red-orange lattice with polyps, leaning into the bay's
  gentle current and swaying with it (`sea_fan.gdshader`). Sea fans feed facing the current, so the bay says the fan
  "hears what the water carries".
- **The ribbons:** each request is a kelp ribbon hanging from the lattice. Its knot holds the **token**, the thing it
  offers: the pearl itself (in its effect colour, PearlKey), or gold for shells. Its kind has a colour (§3).
- **How many:** three places for each region; the ribbons she has carried home done hang there too, glinting gold.
- **In the evening:** the bay is the Shallows at dusk (ReefLook.Bay), so the tokens glow softly and the fan reads from
  across the commune.

### 2.3 Using it

1. **Swim onto the fan's foot** (a clear ring round its rock). The **Tide Fan drawer** opens, in the same style as
   the pause menu and the mirror (`DrawerStyle`), with the sea held and the camera drawn in. It opens again only once
   she has swum off its foot and back.
2. **The drawer lists what she carries first, then the ribbons on the fan.** Each one shows:
   - who tied it, and a line in their own voice;
   - what it asks, in one plain caption with numbers (and, while carried, how far along it is);
   - its pearl (name, tagline, effects), if it offers one;
   - its shells and its experience;
   - Take, or Untie once it is done.
3. **She carries up to two requests** at once. A carried request stays on her until it is done or she lets it go.
   Letting one go (the pause menu's Requests tab) costs nothing: its ribbon goes back on the fan.
4. **Coming home with one done:** its ribbon glints gold on the fan, and the maps mark the fan. Untying it:
   - its pearl floats out to her bell and is absorbed with the usual ritual;
   - its shells spill out round her like a cache;
   - its experience is hers, shown and levelled as a leak's is.

   The empty place on the fan fills with a new ribbon the next time she comes home to the bay (a new trouble has
   drifted in).

## 3. The requests

Four kinds, all built from what the dive already holds (docs/DIVE.md §7 has the dive's form of each). Each has a
**level**, from the depth she is diving, which sets what it pays (§4).

### 3.1 Close the bleeding mouth (seal a leak)

- **Colour:** violet.
- **Asks:** seal the leak in the cave behind one named mouth of the region (in the Shallows: seagrass, north-west, spire, ridge, flats, east, south-east), never one more than two levels past her own.
- **Example:**
  - *Tied by a pufferfish mother:* "My little ones keep swimming toward the east mouth. It tastes warm, they say.
    Please shut it."
  - *Caption:* Seal the leak behind the east mouth (level 1).
- **Taking it:** if that mouth is shut (sealed recently, `Jelly.Shut`), the Leak seeps back into it at once and a
  fresh cave waits behind it. The bay asking calls the leak out. This is how the region's shut mouths are reopened
  early.
- **Done:** when she seals that cave's leak.

### 3.2 The swallowed school (free a murkling prime)

- **Colour:** silver, the colour of a chromis school.
- **Asks:** free a murkling prime in one of the region's caves.
- **Example:**
  - *Tied by an old blue tang:* "A great dark drop swallowed my whole school in the south caves. They're still in
    there. I can feel them."
  - *Caption:* Free a murkling prime in a Sunlit Shallows cave.
- **Done:** when she frees any prime in that region's caves (every cave has one waiting short of its leak).
- **The swallowed school:** every prime freed in a leak cave lets its school out, request or not. Eighteen healthy
  chromis burst from it and stream away toward the cave's mouth, going home (`ReefTuning.SwallowedSchool`,
  `PlaneWorld.ReleaseSchool`), with the caption "The swallowed school swims home".

### 3.3 Into the dark (a curse den)

- **Colour:** deep purple, the curse den's.
- **Asks:** reach the end of the curse den in the cave behind one named mouth, and swim back out of it.
- **Example:**
  - *Tied by the moray who never leaves her crack:* "There's a tunnel down there where the dark sits like ink in a
    shell. Something of ours fell in."
  - *Caption:* Reach the end of the curse den behind the west mouth, and swim back out.
- **Which mouth:** the request only names a mouth whose cave holds a curse den; the fan checks the cave's plan
  (`LevelPlan.ForCave`) when it ties the ribbon. If no mouth's cave has one, it ties a different kind.
- **Done:** when the den is cleared (`PlaneEventType.CurseCleared`: she reached its end and came back out of its
  dark). The den's own reward at its end is hers as well.

### 3.4 Wash the walls (clear ash)

- **Colour:** pale grey.
- **Asks:** clear so many patches of ash off the walls of the region's caves, over as many trips as she likes.
- **Example:**
  - *Tied by the cleaner shrimp:* "We'd scrub those walls ourselves if the Leak would let us near. Clear the grey off
    for us?"
  - *Caption:* Clear 150 patches of ash in Sunlit Shallows caves (40 / 150).
- **Counts:** every patch she clears with her light or her shots (`PlaneWorld.AshWashed`). Patches dissolved by a
  sealed leak's wave don't count; the work is hers.
- **How many:** 120 + 30 × level.
- **Done:** at the count. It is the slow, steady one, picked up alongside the others.

### 3.5 Who ties them

- **Unnamed bay folk**, species-true: the clownfish of the glade, an old blue tang, the moray in her crack, the
  cleaner shrimp, a pufferfish mother, the sea hare. Each kind has three posters, each with their own line
  (`Requests.Posters`).
- **The voice** is the Bible's (§10): warm, a little salty, never jokey about the dark. Never "kill", "enemy",
  "reward": things are freed, sealed, cleared and washed. The caption under the poster's line is the plain one, with
  numbers.
- **Later:** named NPCs, once she frees them (step 5's second half), tie their own requests to the same fan.

## 4. What they pay

### 4.1 Pearls and shells

- **Every request offers shells**, and three in four also offer a passive pearl. One without a pearl offers three
  times the shells instead.
- **The pearl** is drawn from the treasure pool, from those she could be offered and has not absorbed yet
  (`PlaneRun.CanOffer`), so a request is a way to find new pearls. Its quality follows the request's level:

  | Level | Pearl quality |
  |---|---|
  | 1 | 1–2 |
  | 2–3 | 2–3 |
  | 4 and up | 3–4 |

- **The pearl is on the ribbon before she takes it.** She chooses requests by what they offer, and no two ribbons on
  the fan offer the same pearl.
- **Shells:** 15 + 10 × level silver shells.
- **Absorbed in the bay:** a pearl is untied and absorbed at the fan, so it is safe until she next goes out. Like
  every passive, it goes into her lost light if she dims out there.

### 4.2 Experience

- **Every request pays experience on untying,** as much as sealing a leak of its level does
  (`ReefTuning.LeakXp(level)`, `RequestTuning.Xp`).
- **It comes on top of the leak's own.** Close the bleeding mouth pays twice over: the leak's experience when she
  seals it, and the request's when she unties it.
- **It is paid as a leak's is** (`PlaneWorld.GiveXp`, through the step's `XpGained` event), so it shows and levels
  her up the same way.

## 5. How the fan picks its requests

- **Seeded, like the caves:** each ribbon comes from her jelly's seed, the region, and a count of ribbons ever tied
  there (`Jelly.Tied`). The same jelly always gets the same run of requests, and a new jelly gets her own.
- **Variety:** the three ribbons are never two of the same kind if it can help it. Close the bleeding mouth is always
  one of them, as it is the main way to reopen mouths.
- **Their level** is the region's cave level when tied; it doesn't rise while the ribbon waits.
- **Fresh ribbons** fill empty places each time she comes home to the bay. A ribbon nobody has taken stays.

## 6. Keeping track

- **The HUD's kelp ribbons:** under the minimap, one kelp ribbon for each request she carries, its knot in its kind's
  colour holding its token, with a short line of what it asks and how far along it is ("Wash the walls · 40 / 150").
  A done one turns gold and glints: "Done · back to the Tide Fan". They show on the open reef only.
- **Toggled in the pause menu:** the Requests tab has a switch for the HUD ribbons (`ViewOptions.RequestRibbons`,
  saved with the view settings).
- **The pause menu's Requests tab** (beside Level up, Stats, Pearls and Achievements): the requests she carries, with
  their poster's line, what they ask, how far along they are, and Let go.
- **On the maps:** in its region, a carried request's named mouth is ringed in its kind's colour on the minimap and
  the Tab map; while a done one waits, the fan is ringed gold. A ringed place past the minimap's edge shows as an
  arrow just inside its rim (as the north mark), her own marker's shape in the ring's colour, pointing the way to it
  (`TopoMap.Pointers`); the folk's chain steps' places point the same way, in gold.
- **As she goes:** "Request done · back to the Tide Fan" when one is done, and "Request untied" at the fan.

## 7. Theme tests (Bible §12)

| Test | How requests pass it |
|---|---|
| Warmth | The fan's tokens glow softly; she is still the warmest light. |
| Bell | A pearl token is absorbed onto her bell; requests themselves show on the fan, the HUD's kelp and the map. |
| Freeing | Every request frees, seals or washes. The swallowed school is the freeing made visible: a school going home. |
| Sadness | The bay's troubles grow with the region's level; the deeper regions' posters will be fewer and quieter. |
| Sway | The fan and its ribbons sway in the bay's current. |
| No-Villain | Nobody is blamed: the Leak takes, the bay asks, she helps. |
| Never-Wink | No signs, no writing, no human things: kelp ribbons and tokens. |

## 8. Where it lives

- **Core:** `src/Core/Plane/PlaneRequests.cs`: `RequestTuning` (places, carry, shells, ash counts, quality bands,
  experience), `Requests` (posters, captions, `Fill`, `Take`, `LetGo`, `Note`, `Untie`). The school and the ash count
  are in `PlaneReef.cs`; the fan's point is `LevelMap.Fan` (from `HandLayout.Fan`).
- **Save:** `Jelly.Fan` (per region, the ribbons on it), `Jelly.Carried` (her requests and their progress) and
  `Jelly.Tied` (the count), in `SaveData.cs`, written with the rest of her.
- **Game:** `TopDownMain.Requests.cs` (the fan, taking, untying, progress from the sim's events, the HUD and map
  marks), `FanView` (the fan and its ribbons), `FanMenu` (its drawer), `RequestRibbons` (the HUD's kelp),
  `PauseMenu`'s Requests tab.
- **Tests:** `src/Core.Tests/RequestTests.cs`.
- **Verification flags:** `--reef=bay --at=fan` (at the fan, its drawer open), `--at=fan-near`, and `--dbg-requests`
  (ribbons tied, two carried, the first done).
