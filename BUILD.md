# Building Reworked Chemist

[English](BUILD.md) | [Português](BUILD.pt-BR.md)

Windows x64 and .NET SDK supporting net8/net9. Clone **FFTModLoader** as well.
Expansion infrastructure is built and distributed by the loader; it is no longer duplicated in this repository.

Build the thin consumer:
`dotnet build AlchemistRework/PlayableRuntime/PlayableRuntime.csproj -c Release -p:FFTModLoaderSourceDirectory="path/to/FFTModLoader"`

Run the host tests with the same property:
`dotnet run --project AlchemistRework/PlayableHostTests -c Release -p:FFTModLoaderSourceDirectory="path/to/FFTModLoader" -- "path/to/local/FFT/workspace"`
`HooksRuntimeDirectory` selects the local actual hook library directory.
The host tests reference the **actual compiled loader module**, not another copy of its sources.
Historical native fixtures require locally generated input assets from a legal game; they are not shipped.
Use the loader's `tools/build_native_bridge.ps1` for bridge builds.

Content generators are in `AlchemistRework/tools`; the skill definition is `AlchemistRework/Alchemist.definition.json`.
Use Nenkai's FF16Tools and local legal game inputs. These historical generators can require additional local paths.
No game executable, full extracted table database, private save or recording is distributed.

APPROVED_BUILD.json distinguishes the approved 0.2.43 content from the 0.2.44 architecture candidate.
Native bridge bytes and gameplay fields are preserved. `build_menu_descriptions.py --mod Mod --output "new verification directory" --ff16tools "path/to/FF16Tools.CLI.exe"`
updates only the three group-action Description fields in each game locale, verifies a complete NXD round trip and leaves individual item descriptions intact.
MENU_DESCRIPTIONS.json records those seven verified resource changes.
Owned-memory/ABI tests do not establish live scene rendering or in-game approval.
