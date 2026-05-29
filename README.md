# SR2 Gyro Aim

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for **Slime Rancher 2** that adds gyroscope camera control without breaking controller button prompts or mixed input mode.

## How it works

Most Unity games treat mouse and gamepad as mutually exclusive input modes — if you feed gyro as mouse movement via Steam Input, the game switches to keyboard/mouse mode and shows the wrong button prompts.

SR2 Gyro Aim bypasses this entirely. It reads gyro data via the **Cemuhook UDP protocol** (port 26760) and injects rotation directly into the game's internal camera fields (`_planarDirection` and `_targetVerticalAngle` on `SRCameraController`). The game never sees any mouse input, so it stays in controller mode with correct button prompts at all times.

## Tested setup

- **Steam Deck** with [SteamDeckGyroDSU](https://github.com/kmicki/SteamDeckGyroDSU) running as a background service
- Steam Input left **enabled** (SteamDeckGyroDSU exposes gyro independently of Steam Input)

## Other likely compatible setups

Any device running a Cemuhook/DSU UDP server on `127.0.0.1:26760` should work, including:

- **DualSense / DualShock 4 on Windows** via [DS4Windows](https://github.com/Ryochan7/DS4Windows) with UDP server enabled
- **DualSense / DualShock 4 on Linux** via [DualSenseY](https://github.com/nowroz/DualSenseY) or similar
- **Nintendo Switch Pro Controller / Joy-Cons** via [BetterJoy](https://github.com/Davidobot/BetterJoy)
- Any other controller with a Cemuhook-compatible DSU server

These have not been personally tested — if you try one, please open an issue or PR to update this list.

## Requirements

- [MelonLoader 0.7.3+](https://github.com/LavaGang/MelonLoader/releases)
- A Cemuhook DSU server running on `127.0.0.1:26760` (see above)
- *(Optional)* [Starlight](https://www.nexusmods.com/slimerancher2/mods/60) for in-game settings UI

## Installation

1. Install **MelonLoader 0.7.3+** for Slime Rancher 2
2. Download `SR2GyroAim.dll` from the [latest release](../../releases/latest)
3. Place it in your `Slime Rancher 2/Mods/` folder
4. Start your DSU server before launching the game
5. Launch SR2

## Settings

Settings are stored in `UserData/MelonPreferences.cfg` and can be edited there or via the **Starlight mod menu** in-game.

| Setting | Default | Description |
|---|---|---|
| Pan Sensitivity | 1.5 | Left/right panning speed. Set to 0 to disable. |
| Tilt Sensitivity | 1.5 | Up/down tilt speed. Set to 0 to disable. |
| Twist Sensitivity | 0.0 | Left/right twist speed. Disabled by default — enable if you prefer twist aiming. |
| Invert Pan | false | Invert left/right panning direction. |
| Invert Tilt | false | Invert up/down tilt direction. |
| Invert Twist | false | Invert left/right twist direction. |
| Deadzone | 1.5 | Minimum movement (deg/s) before input registers. Increase if camera drifts when holding still. |

## Steam Deck setup

1. Install [SteamDeckGyroDSU](https://github.com/kmicki/SteamDeckGyroDSU) — it runs as a systemd service and starts automatically on boot
2. Install MelonLoader and this mod as above
3. Leave Steam Input **enabled** for SR2 — SteamDeckGyroDSU works independently of it
4. Launch SR2 normally from Game Mode

## Building from source

```bash
git clone https://github.com/FunkyByte1/SR2GyroAim
cd SR2GyroAim/SR2GyroAim

# Set path to your SR2 install
export SR2Path="$HOME/.local/share/Steam/steamapps/common/Slime Rancher 2"

dotnet build
```

The compiled DLL will be at `bin/Debug/netstandard2.1/SR2GyroAim.dll`.

## License

MIT












