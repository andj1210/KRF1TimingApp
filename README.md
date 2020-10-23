# F1 Game Session Display

This repository contains a Software to Display the leaderboard and car status for the F1 2020 game.

Initially I made this software for F1-2018 for my own use and now ported it to the F1 2020 game and post it here since I hope it is useful for other players.
As well I´m hoping to find some help with the visualization, WPF/xaml or graphics contributions very welcome.

### Installation
I provide binaries in form of one DLL and one executable. They can be extracted to any directory.
On a Windows 10 machine usually just the executable must be started and everything should be working.
In case the program does not start, please install the Visual Studio C++ Redistributable (vx_redist.x86.exe) for Visual Studio 2015/2017/2019.

### Compilation
The .sln file should compile out of the box with Visual Studio 2019.

### Functions
The program does currently only contain two different views.

- The car status view
- The leader board

After the program has been started, the view can be changed with the space bar.
The keystroke are also captured in background, i.e. also when the window has no focus.

Keymapping:
F11   - change to fullscreen
space - Toggle view (Car status / Leaderboard)

[T.B.D.] explanation of each view.
