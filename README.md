# KRF1 Timing App for F1 25

A second-monitor timing and telemetry display for the F1 25 game, driven by the game's UDP telemetry output.

## Installation

Extract the provided zip archive to any folder with write permissions (e.g. Documents). On Windows 10 the executable should start directly.

If the program does not start, install the Visual C++ Redistributable for Visual Studio:
- [vc_redist.x64.exe](https://aka.ms/vc14/vc_redist.x64.exe)

In the game, enable UDP telemetry output in mode **2025** on port **20777**.

---

## Views

Cycle through the available views with **Space** or the in-game UDP1 button (map it to a controller button in the game settings).

| View | Description |
|------|-------------|
| Leaderboard | Race/qualifying standings with delta times and tyre info |
| Combined | Leaderboard side by side with the track map |
| Car Status | Tyre and engine temperatures, wear, and damage for the player car |
| Track Map | Full-screen track map showing all car positions |

---

## Leaderboard

Shows all cars sorted by on-track position (race) or best lap time (qualifying / practice).

**Header row:** Track name, session type, and remaining time or laps.

**Columns per car:**

| Column | Description |
|--------|-------------|
| POS | Race/quali position. Colored by team if official F1 cars are used. |
| Leader | Gap to the leader in time or laps. In quali/practice: sector times S1/S2/S3 of the best lap. |
| Status | Race: "Retired", "Pitting", or the delta to the **player** car (positive = opponent is ahead). Quali/practice: pit/garage state, outlap indicator, or sector color bars and running delta. |
| Name | Driver name (see Driver Names below). |
| Tyre | Tyre compound history, rightmost = current. |
| Age | Age of the current tyre in laps (from last pit stop). |
| PT | Accumulated penalty time in seconds. |
| Pit Pen | Pending stop-go (SG) or drive-through (DT) penalties. A penalty in parentheses is estimated as served. |

**Qualifying / Practice detail:**
After each sector a colored bar indicates: green = personal best, yellow = slower, red = significantly off (usually an in-lap). The sector delta is shown in green (improving) or red (slower). A finished lap is highlighted in blue for 5 seconds. An invalid lap turns red.

**Race delta notes:**
- Delta is updated once per sector, not continuously.
- Time penalties are factored in, so a car slightly ahead on track may still show a negative delta.

---

## Track Map

Displays all car positions on a live track outline (when track data is available) or on a fallback circle.

- Car dots are colored by team.
- The player car blinks pink.
- The selected/highlighted car blinks with a thicker border.
- A white line marks the start/finish line.
- Cars in the pit lane or garage are pushed away from the track outline for easier visibility.
- In relay mode, car positions are smoothly interpolated between the slower relay update rate so movement appears fluid.

Track data is learned automatically during racing sessions (see Track Learning below).

---

## Driver Names

Because the F1 25 UDP protocol does not report human driver names (based on players telemetry settings) in online lobbies, the app defaults to team name + car number as the display name.

**Rename a driver:** Right-click any driver row to open the rename menu. Type a new name and press Enter or click OK. Previously used names for that driver are listed in a History sub-menu for quick re-use.

**Name mapping file:** Place a file named `namemappings.json` in the program directory and press **M** to cycle through the available mapping sets. This is useful for pre-defined league rosters.

---

## Key Bindings

| Key | Action |
|-----|--------|
| Space | Cycle views: Leaderboard -> Combined -> Car Status -> Track Map |
| F11 | Toggle fullscreen |
| S | Save a race report as a text file |
| D | Toggle delta display relative to the player car (race only) |
| L | Toggle delta-to-leader display for all cars |
| I | Toggle interval (gap to the car directly ahead) |
| M | Cycle through name mapping sets from `namemappings.json` |
| R | Start / stop UDP recording (see UDP Recording) *(dev builds only)* |
| T | Start / stop track learning mode (see Track Learning) *(dev builds only)* |
| Right-click | Open driver rename menu |
| UDP1 button | Same as Space (map in game settings) |

A sidebar is revealed by hovering the mouse over the left edge of the window. It shows a summary of key bindings and provides access to advanced features including the relay system.

---

## UDP Recording

> **Development builds only.** This feature is compiled in only when the corresponding preprocessor flag is set. It is not present in public release builds.

The app can record the raw UDP telemetry stream to files for later playback during development and testing.

- Press **R** to start recording. A status indicator appears in the sidebar.
- Press **R** again to stop.
- Each session is saved as a separate `.pkl` file in the `recordings` subfolder next to the executable. The file is named after the session UID from the game.
- Recordings can be played back to replay a session without the game running.

---

## Track Learning

> **Development builds only.** This feature is compiled in only when the corresponding preprocessor flag is set. It is not present in public release builds.

When track geometry data is not yet available for a circuit, the app can learn the track layout from live telemetry.

- Press **T** to enter learning mode. The sidebar shows the current state.
- Drive a complete lap. The track outline is recorded automatically and saved when the lap is completed.
- Track data is stored as JSON files in the `tracks` subfolder and loaded automatically on subsequent sessions.
- If the session changes (e.g. you return to the menu and start a new session), the learner resets and waits for a fresh lap.

---

## Car Status View

Displays the monitored car's current condition:

- **Driver name** shown prominently at the bottom of the view.
- **Current tyre compound** shown as a colour-coded tyre graphic.
- Tyre wear per corner (front-left, front-right, rear-left, rear-right).
- Tyre temperatures (surface and inner) per corner, colour-coded by operating range.
- Engine temperature.
- Front and rear wing damage.

When connected as a Race Engineer to two drivers simultaneously, two Car Status panels are displayed side by side.

### Status overlay

While any relay connection is active, a small overlay appears at the bottom right of the window showing the connection state for each active role. The overlay hides automatically when all connections are closed.

## Sidebar

Hover the mouse over the leftmost 100 pixels of the window to reveal the sidebar. It shows:
- All key bindings at a glance.
- **Advanced** section with controls for UDP Recording and Track Learning *(dev builds only).*
- **Relay** section with buttons for Driver Relay, Race Engineer, and Second Driver connections.

---

## Custom Car Images

The car silhouette shown in the Car Status view can be replaced with a team-specific bird's-eye image.

1. Create a subfolder named `cars` next to the executable.
2. Place PNG images named `car{TeamId}.png` inside it.

Team ID mapping:

| TeamId | Team |
|--------|------|
| 0 | MGP |
| 1 | SF |
| 2 | RBR |
| 3 | W |
| 4 | AMR |
| 5 | ALP |
| 6 | RB |
| 7 | H |
| 8 | MCL |
| 9 | KS |

If no image is found for the current team the built-in placeholder is used. Images are loaded on demand when the team changes and are not required for the app to function.

---

## Relay System

The relay system lets a Race Engineer monitor one or two remote drivers over the internet. It requires a separate relay server (see `KRF1Timing_relayserver`) running on a machine reachable by all participants.
***Function is disabled by default***, unless a "relay_config.json" file is placed into the program folder / Examplecontent:
{
    "server": "localhost",
    "port": 9877
}

### Roles

| Role | Description |
|------|-------------|
| Driver | Runs the app locally with the game, streams telemetry to the relay server. |
| Race Engineer | Connects to the relay server using the driver's password, receives the full telemetry stream. |

### Driver setup

1. Open the sidebar (hover over the left edge of the window).
2. Click **Remote -> Share** to connect to the relay server.
3. On successful connection, the server assigns a one-time password. Share this password with your Race Engineer.
4. The status overlay at the top of the window shows the connection state and the assigned password.

### Race Engineer setup

1. Obtain the driver's one-time password.
2. Open the sidebar and click **Remote -> Connect to 1st Driver**
3. On successful authentication, the full telemetry stream is received and all views update as if the game were running locally.

### Second driver (dual relay)

A Race Engineer can simultaneously monitor a second driver:

1. With the primary driver already connected, click **Remote -> Connect to 2nd Driver** in the sidebar.
2. Enter the second driver's password.
3. Both drivers' Car Status panels are shown side by side. CarDamage and tyre data for the secondary driver are sourced exclusively from their own stream.

### Relay server

The server is a standalone native binary (`krf1_relayserver`). Build it with CMake (see `KRF1Timing_relayserver/CMakeLists.txt`). Run it on any Linux or Windows machine that is reachable from both driver and engineer:

```
krf1_relayserver [port]   # default port: 9877
```

The server maintains per-driver sessions, buffers a history burst for late-joining engineers, and enforces a protocol version check — older clients are rejected.

### Privacy notice
Appart from the generic messages for connection esablishment with auto generated password, a portion of the UDP telemetry of the game is transmitted to the relay server. Currently it uses unencrypted TCP protocol.

Usage at your discretion.


---

## Limitations

- Human driver names are mostly unavailable in the UDP telemetry for online lobbies. Use the right-click rename or a name mapping file as a workaround.
- Delta times in the Status column are updated once per sector, not continuously. A pass on track is not reflected until the next sector boundary.
- The delta-to-leader value wraps at 65536 ms (~65 seconds), which can be misleading in large fields.
- If the app is started after the session begins, some data (driver count, initial deltas) may be incorrect until the next session starts.
- Flashback and fast-forward in single player can cause inconsistent data.
- Sector times in saved race reports may contain small rounding errors.
- Track learning requires a full, clean lap. Partial laps or laps with heavy cuts are not suitable.
- In relay mode, track map positions update at the rate the driver's app sends them (typically 2–5 Hz). Interpolation smooths the movement but cannot compensate for network latency or packet loss.

---

## Compilation

Open the `.sln` file in Visual Studio 2026. It should compile without any additional setup.

The relay server is a separate CMake project in `KRF1Timing_relayserver/` and compiles on both Windows and Linux.

---

## Disclaimer

F1 25 game and all associated trademarks are the property of Electronic Arts Inc. and
Codemasters.
This project is an independent community tool and is not affiliated with, endorsed by,
or in any way connected to EA, Codemasters, or Formula One Licensing B.V.
The UDP telemetry interface used by this application is publicly documented by
Codemasters for third-party use.