# Playing on a phone: streaming from the PC

The game is C# on Godot 4 with the Forward+ renderer, so it cannot be exported to the web (Godot 4 cannot export C#
projects to the web, and web builds only have the Compatibility renderer). To play on a phone, the PC runs the game and
streams it. The phone and the PC must be on the same Wi-Fi; a 5 GHz network, or the PC on a cable to the router, keeps
the lag down.

## The launcher

`tools/play.bat` brings the C# build up to date and starts the game full screen at its title, from Godot 4.7.2 .NET
where winget installs it (set `INKDEEP_GODOT` to its executable to use another install). Any arguments go on to Godot.
It starts Godot's own executable, never winget's `godot` link on the PATH: run through the link, Godot looks for its
.NET assemblies beside the link and stops ("Assemblies not found").

## Steam Link (recommended)

On the PC:

1. Install Steam and sign in.
2. **Games → Add a Non-Steam Game to My Library… → Browse…**, set the file type to *All files*, choose
   `tools\play.bat` in the repository, and add it.
3. In the library, right-click it → **Properties…** and name it *Ink Deep* (the launcher finds the repository from its
   own place, whatever **Start In** says).
4. **Steam → Settings → Remote Play**: turn Remote Play on.

On the phone:

1. Install **Steam Link** from the store and open it; it finds the PC on the Wi-Fi. Pair it (it shows a PIN to type
   into Steam on the PC).
2. Start **Ink Deep** from the library.

The controller:

- **On the PC:** leave the Xbox controller connected to the PC; the game reads it directly.
- **On the phone:** pair it to the phone over Bluetooth; Steam Link sends its input to the PC. Steam may then present
  it to the game as its own virtual controller: if the buttons come out wrong, turn **Steam Input** off for Ink Deep
  (its Properties → Controller → *Disable Steam Input*).

The game has no touch controls: play it with a controller.

## Alternatives

Parsec, Chrome Remote Desktop, or Moonlight with Sunshine stream the PC's screen just as well; start the game on the
PC with `tools\play.bat` and connect from the phone.
