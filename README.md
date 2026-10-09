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

## Installation and dependencies

Install [FFTModLoader 0.11.7-rc.1](https://github.com/andersongz0/FFTModLoader/releases) first.
Extract **ReworkedChemist-0.2.44-rc.1.zip** outside the game directory, close game/loader and run
`install.ps1 -GameDirectory "your game folder"`.

The installed folder and displayed name are **Mods/Reworked Chemist** and **Reworked Chemist**.
The installer moves the verified legacy `Reworked Chemist - Venom Test` folder into a recoverable backup.
The historical ModId/assembly identity is retained for compatibility, not used as the public mod name.
Do not keep two folders with the same ModId enabled.

Requires loader-owned **ContentExpansion**, **JobExpansion**, Utility Mod Loader and SharedLib.Hooks.
Their transitive compatibility dependencies are bundled with FFTModLoader.
**Generic Knights is not required.**
There is no standalone native bridge or expansion implementation in this mod package.

Saves remain unchanged; extras stay in `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.
Back up both native saves and this directory when migrating computers.
Player AI was tested previously; a separate exhaustive enemy-AI test is not claimed.

**Nenkai**: Utility Mod Loader, FF16Tools and FaithFramework.
See [credits/tools](CREDITS.md), [building](BUILD.md), [migration record](APPROVED_BUILD.json)
and [third-party notices](THIRD_PARTY_NOTICES.md).
New mods will only be published once explicitly declared finalized by the user.
