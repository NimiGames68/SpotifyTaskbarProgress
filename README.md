# SpotifyTaskbarProgress

Shows the progress of Spotify music in the taskbar.

Uses Windows SMTC (System Media Transport Controls) to read the music position and show that in the taskbar.

---
## Why did i do this?

I wanted to keep track of the length of the song i was listening to, while i was working on something, and gave me the idea

---

## Requirements

- Windows 10 (1903+) and up
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Spotify desktop app
(nothing more i think..)

---

## Build

1. Clone this repo
2. Have [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Open powershell and change the directory to the one you just cloned
4. Run the following command: `dotnet publish -c Release -r win-x64 --self-contained false`

The exe will be located at:
```
bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SpotifyTaskbarProgress.exe
```

---

## Autostart with Windows

1. Open Explorer and go to `shell:startup` (paste in the win+r dialog box)
2. Create a shortcut to `SpotifyTaskbarProgress.exe` inside that folder

---

## How it works

Spotify reports the playback position to the Windows SMTC. The program reads that position every 500ms, calculates the real time elapsed since the last SMTC update, and uses `ITaskbarList3` to update the progress bar on the Spotify icon in the taskbar.
