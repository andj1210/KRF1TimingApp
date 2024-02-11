# F1-Game Session-Display

This repository contains a software to display the leaderboard and car status for the F1-2022 game on a second monitor by utilizing the game Telemetry output.

Initially I made this software for F1-2018 for my own use and now ported it to the F1-2020 then F1-2022 game and post it here since I hope it is useful for other players.
Newer Game Versions can be used, when the ingame UPD Telemetry setting is set to "2022". 
As well I´m hoping to find some help with the visualization, WPF/xaml or graphics contributions very welcome.

### Installation
The binaries are provided in form of one DLL and one executable. They can be extracted to any directory.
On a Windows 10 machine usually just the executable must be started and everything should be working.
In case the program does not start, please install the Visual Studio C++ Redistributable (vc_redist.x86.exe) for Visual Studio 2022:
- [vc_redist.x86.exe](https://aka.ms/vs/17/release/vc_redist.x86.exe)

Furthermore in the game the telemetry output must be enabled in mode "2022" to UDP port 20777.

### Functions
The program does currently only contain two different views and a combination of both.

- The leader board
- The car status view


After the program has been started, the view can be changed with the space bar.
Space bar is also captured when the window is not active.

Keymapping:
- F11           - toggle fullscreen
- s             - save a race report as text file
- d             - enable disable the other cars relative delta to the player (factoring in all penalties)
- l             - enable disable the delta to leader for all cars including player
- m             - toggle between namemappings from file "namemappings.json" placed into the program directory
- space         - Toggle view (Leaderboard -> Combined -> Car status), also captured when the window is not active (i.e. you are in game)
- UDP1 button   - same as above, assign UDP1 ingame to your controller/wheel button
- right mouse   - select driver name

**The window is updated automatically as soon as telemetry data from the game is received**

#### The leader board
The leader board displays the race leader board from perspective of the active player, meaning the deltas are relative to the player.

The leaderboard is useful for the race and practice/qualifying.
When a practive / qualifying session is detected, the view is focused arround the fastest lap of each car.

On top of the screen the event information is displayed:
- Track
- Session (qualifying, race...)
- Remaining time or laps

Below the personal leaderboard is displayed.
Each line represents one car, and the order is according to the position on tack (race) or best laptime (Q+P).  For each car the following columns are displayed:
- POS
The position of the car. The Number is colored after the team color if F1-2020 regular cars are used.
- Delta
The Circle is colored red, gray, or green.
 Red: The last sector of the opponent was 0.050 seconds or more faster than the player.
 Gray: The last sector of the opponent was within +-0.050 seconds of the player.
 Green: The last sector of the player was 0.050 seconds or more faster than the opponent.
Next to the circle, the time between the player and the oponent in seconds. A positive number meaning the opponent is ahead (number colored red), a negative number meaning the opponent is behind (number colored red).
The delta time column is also used for special status like PIT or DNF.
**Be aware, the delta is only updated sector by sector. Thus After passing or being passed the delta does not reflect this instantly!**
- Name
The driver name. Since the names are not reported by the game for online lobbies, the drivers are named by their team and their car number instead. This is a limitation by the Telemetry data. 
- Tyre
Display of the tyre history, the rightmost tyre beeing the currently fitted tyre.
- Age
The Age in laps of the current tyre. This only takes into account the age from pit stop. The telemetry does not indicate if a tyre was already used when fitted during the pit stop.
- PT
Penalty time. The time (in seconds) inevitably added to the car after the race (typically for corner cutting). After the race this also contains the added time for unserved pit penalties.
- Pit Penalty
Penalties which can still be served during the pit stop, there are two penalties:
"SG" = 5 sec Stop + Go 
"DT" = Drive through penalties
The pit penalties are seperated by ";". If a penalty is shown in parenthesis it is served (this can only be estimated since there is no specific telemetry output and thus the information *might* be inaccurate).
Additional "DNF" and "DSQ" are added to the column if a car retired / disqualified.

#### The Car status
Display the tyre and engine temperatures. Furthermore displays the tyre wear and wing damage. Behind the Rear wing the personal penalty time is shown.

### Limitations
- Human driver names are mostly not available in the telemetry (per default), therefore teamname + car number is shown as name if the actual player name is not available. Also a custom mapping file has can be used.
- The information during practice or qualifying is not particular useful, yet.
- When the start of the session is not captured, the raceboard will show incorrect data (i.e. number of drivers, deltas, etc.)
- The lap infos in racereport may contain rounding errors, so that sector 1-3 not always sum exactly the lap time
- Gaps / Delta times are only updated once per sector
- The data is focused on the driver participating in the race, no particular support for spectator mode

### Compilation
The .sln file should compile out of the box with Visual Studio 2022.