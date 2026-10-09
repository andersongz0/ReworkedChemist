# Reworked Chemist migration notes

[English](MIGRATION.md) | [Português](MIGRATION.pt-BR.md)

The current public mod name is **Reworked Chemist**, installed under **Mods/Reworked Chemist**.

Older test installations used the folder `Reworked Chemist - Venom Test`.
The installer checks its mod identity and moves only that verified legacy folder into a recoverable
**FFTModLoader.Backup** location, preventing two active copies of the same mod.
Do not delete backup folders just to change a displayed name.

The historical ModId `ffttic.tests.reworkedchemist.venom`, managed assembly identity and save namespace
are compatibility identifiers, not the public mod name. Changing them casually can break mod resolution
or access to existing expanded saves. They are deliberately preserved.

Back up native saves and **FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist** together.
ContentExpansion and its native bridge are installed by the complete FFTModLoader package;
install that first before updating this mod. Source changes and prior approval are recorded in
[APPROVED_BUILD.json](APPROVED_BUILD.json).
