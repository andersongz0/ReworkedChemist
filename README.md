# Reworked Chemist

[English](README.md) | [Português](README.pt-BR.md)

Author: **ZeroDS** · [Source and releases](https://github.com/andersongz0/ReworkedChemist)

Reworked Chemist expands the **Chemist** job, grouping medicines into selection menus and adding damage flasks and buff/debuff essences. It includes inventory checks, native throws and interception, AI support and chapter-based shop availability.

## Features

- Potion menu: Potion, Hi-Potion, X-Potion, Elixir.
- Ether menu: Ether, Hi-Ether.
- Remedy menu: Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Holy Water, Remedy.
- Stock gating, target selection, native throws and interception.
- Eleven flasks/essences; native names/icons, AI integration and chapter-based shops.
- Fire/Ice/Thunder: **5 fixed damage + 15% target maximum HP**, rounded up before elemental modifiers.
- Shops: Poison/Oil chapter 1; elemental flasks chapter 2; debuff essences chapter 3; buff essences chapter 4.

## Requirements

- Windows x64 and a legal Steam installation of **FINAL FANTASY TACTICS - The Ivalice Chronicles**, Enhanced mode.
- The complete [FFTModLoader 0.11.7-rc.2 package](https://github.com/andersongz0/FFTModLoader/releases/tag/v0.11.7-rc.2).

ContentExpansion, JobExpansion, Utility Mod Loader and their compatibility dependencies are included with FFTModLoader.

## Reworked Chemist Installation Guide

1. Install FFTModLoader following [its guide](https://github.com/andersongz0/FFTModLoader#fftmodloader-installation-guide).
2. Close the game. Close the loader too if it is already installed and running.
3. Download **ReworkedChemist-0.2.44-rc.2.zip** from [release Assets](https://github.com/andersongz0/ReworkedChemist/releases/tag/v0.2.44-rc.2), not **Source code**.
4. Extract the ZIP to a separate folder outside the game installation.
5. Double-click **Install.cmd** and choose your language.
6. Check the detected folder and type **YES** to confirm. If several installations are found, choose one; if none is found, enter the path to **FFT_enhanced.exe** or its folder.
7. Approve the Windows permission prompt, if shown, and wait for completion.

No terminal commands are needed. The installer verifies the package and backs up replaced files under **FFTModLoader.Backup** in the game folder. Saves and unrelated mods are preserved.
The mod is installed in **Mods/Reworked Chemist**.

## Using Reworked Chemist

Start **FFTModLoader.exe** from the game folder. Learn the Chemist abilities and obtain their items. In battle, choose Potion, Ether or Remedy, select an item in stock, then choose the target. New flasks and essences appear in shops as the story progresses.

## Saves

When moving to another computer, back up your native saves and `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.

[Previous installation details](MIGRATION.md)

## Updating

Close the game and any running loader, then run **Install.cmd** from the new complete release. If dependency files are missing, reinstall the complete FFTModLoader package.

## Credits

**Nenkai**: Utility Mod Loader, FF16Tools and FaithFramework.
See [credits/tools](CREDITS.md), [building](BUILD.md), [third-party notices](THIRD_PARTY_NOTICES.md).
