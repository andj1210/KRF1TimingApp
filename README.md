# KRF1 Timing App

This repository contains a software to display the leaderboard and car status for the F1-24 game on a second monitor by utilizing the game Telemetry output.

### Installation
The binaries are provided in form of a zipped file containing DLLs and one executable. They can be extracted to any directory, but it should have write permissions (for race reports). It is recommended to extract to the personal document folder, or any other filder with write permissions.
On a Windows 10 machine usually just the executable must be started and everything should be working.
In case the program does not start, please install the Visual Studio C++ Redistributable (vc_redist.x86.exe) for Visual Studio 2022:
- [vc_redist.x86.exe](https://aka.ms/vs/17/release/vc_redist.x86.exe)

Furthermore in the game the telemetry output must be enabled in mode "2023" to UDP port 20777.

### Functions
The program contains two different views and a combination of both.

- The leader board
- The car status view


After the program has been started, the view can be changed with the space bar or by ingame UDP1 button (needs to be mapped for the input device).

Keymapping:
- F11           - toggle fullscreen
- s             - save a race report as text file
- d             - enable disable the status/delta of other cars relative delta to the player (factoring in all penalties)
- l             - enable disable the delta to leader for all cars including player
- i             - enable / disable interval (the time diff to the car ahead)
- m             - toggle between namemappings from file "namemappings.json" placed into the program directory
- space         - Toggle view (Leaderboard -> Combined -> Car status)
- UDP1 button   - same as above, assign UDP1 ingame to your controller/wheel button
- right mouse   - select driver name

**The window is updated automatically as soon as telemetry data from the game is received**

#### The leader board
The leader board displays the race leader board from perspective of the active player, meaning the deltas are relative to the player.

The leaderboard is useful for the race and practice/qualifying.
When a practice / qualifying session is detected, the view is focused arround the fastest lap of each car.

On top of the screen the event information is displayed:
- Track
- Session (qualifying, race...)
- Remaining time or laps
Below the leaderboard is displayed.

Each line represents one car, and the order is according to the position on tack (race) or best laptime (Q+P).  

For each car the following columns are displayed:
- POS
  - The position of the car. The Number is colored after the team color if F1 regular cars are used.
- Leader
  - The time or number of laps the car is behind the current leader. For Q/P sessions it is focussed arround the fastest lap. For Q/P also the sector times of the fastest lap for each car is shown as columns S1, S2, S3. 
- Status 
  - During Q/P Session
    - Shows the drivers status: Pit/Garage, Outlap
	- On Hotlap/inlap: 
	  - After each sector shows a filled rectangle for Personal Best = green, Worse = yellow, Way off (>1,25s) = red which usually indicates Inlap
	  - After each sector the delta up to that sector in seconds + milliseconds (green: improving, red: slower)
	  - After each set time the Time is marked in blue(ish) color to indicate a lap has finished for 5 seconds
	  - When a hotlap is/becomes invalid, the lap delta Text is colored in red background
  - During Race
    - showing special state: Retired, Pitting
	- or the delta time to the **Player car** in seconds and tenth of seconds, updated only after each sector
    - A positive number indicating the opponent is ahead (number colored red), a negative number indicating the opponent is behind (number colored red).
	- **time penalties are factored in**, thus the delta can be green even if the oponent is ahead on Track
	- **Be aware, the delta is only updated sector by sector. Thus After passing or being passed the delta does not reflect this instantly!**
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
- Human driver names are mostly not available in the telemetry (per default), therefore teamname + car number is shown as name if the actual player name is not available. A custom mapping file has can be used. Or the driver can be "right clicked" in order to change name in Textbox.
- When the start of the session is not captured, the raceboard will show incorrect data (i.e. number of drivers, deltas, etc.)
- The lap infos in racereport may contain rounding errors, so that sector 1-3 not always sum exactly the lap time
- Gaps / Delta times in Status are only updated once per sector
- During race Delta to leader can be between 0 and 65536 ms, above that value it wraps over and can therefore be misleading  
- The data is focused on the driver participating in the race, no particular support for spectator mode. Single player Flashback or Fast Forward can lead to inconsistent data.

### Compilation
The .sln file should compile out of the box with Visual Studio 2022.