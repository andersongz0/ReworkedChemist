# Reworked Chemist

[English](README.md) | [Português](README.pt-BR.md)

Author: **ZeroDS** · [Source and releases](https://github.com/andersongz0/ReworkedChemist)

**0.2.44-rc.1** is the loader-infrastructure migration candidate.
Gameplay implementation and content are preserved from the approved **0.2.43**;
the DLL is rebuilt as a thin consumer, so this architecture update still needs an in-game test.

- Potion menu: Potion, Hi-Potion, X-Potion, Elixir.
- Ether menu: Ether, Hi-Ether.
- Remedy menu: Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Holy Water, Remedy.
- Stock gating, target selection, native throws and interception.
- Eleven flasks/essences; native names/icons, AI integration and chapter-based shops.
- Fire/Ice/Thunder: **5 fixed damage + 15% target maximum HP**, rounded up before elemental modifiers.
- Shops: Poison/Oil chapter 1; elemental flasks chapter 2; debuff essences chapter 3; buff essences chapter 4.

## Requirements

- Windows x64 and a legal Steam installation of **FINAL FANTASY TACTICS - The Ivalice Chronicles**, Enhanced mode.
- [FFTModLoader 0.11.7-rc.1](https://github.com/andersongz0/FFTModLoader/releases/tag/v0.11.7-rc.1), installed as a complete package.
- Loader-owned **ContentExpansion**, **JobExpansion**, Utility Mod Loader and SharedLib.Hooks, including their transitive compatibility dependencies.

All required runtime components are supplied by FFTModLoader; no separate dependency downloads are needed.

## Reworked Chemist Installation Guide

1. Install FFTModLoader following [its installation guide](https://github.com/andersongz0/FFTModLoader#fftmodloader-installation-guide).
2. Close the game and loader.
3. Download **ReworkedChemist-0.2.44-rc.1.zip** from [release Assets](https://github.com/andersongz0/ReworkedChemist/releases/tag/v0.2.44-rc.1), not **Source code**.
4. Extract outside the game directory. Open PowerShell in the extracted folder and run `./install.ps1 -GameDirectory "your game folder"`.
5. Confirm that **Mods/Reworked Chemist/ModConfig.json** exists in the game installation. The displayed mod name is **Reworked Chemist**.
6. Launch **FFTModLoader.exe** in Enhanced mode. Learn the available Chemist abilities and obtain their items; zero-stock entries cannot be used and shop availability follows the chapters listed above.

The installer verifies package hashes and backs up replaced files under **FFTModLoader.Backup**.
To update, close the game/loader and install the new complete release. Keep only one active copy of Reworked Chemist.
If dependency files are missing, reinstall the complete FFTModLoader package.

## Saves and previous installations

Saves remain unchanged; extras stay in `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.
Back up both native saves and this directory when migrating computers.
Player AI was tested previously; a separate exhaustive enemy-AI test is not claimed.
For legacy installation details, see [migration notes](MIGRATION.md).

**Nenkai**: Utility Mod Loader, FF16Tools and FaithFramework.
See [credits/tools](CREDITS.md), [building](BUILD.md), [migration record](APPROVED_BUILD.json)
and [third-party notices](THIRD_PARTY_NOTICES.md).
New mods will only be published once explicitly declared finalized by the user.
