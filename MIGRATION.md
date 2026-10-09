# Reworked Chemist migration notes

[English](MIGRATION.md) | [Português](MIGRATION.pt-BR.md)

The current public mod name is **Reworked Chemist**, installed under **Mods/Reworked Chemist**.

Older test installations used the folder `Reworked Chemist - Venom Test`.
The installer checks its mod identity and moves only that verified legacy folder into a recoverable
**FFTModLoader.Backup** location, preventing two active copies of the same mod.
Do not delete backup folders just to change a displayed name.

The current ModId is **`ffttic.jobs.reworkedchemist`** and the consumer assembly is
**FFTModLoader.ReworkedChemist.dll**. FFTModLoader 0.11.7-rc.5 recognizes the former
test ID in older packages and translates it in the generated runtime configuration.
The mod installer backs up the old consumer assembly when updating an existing installation.
The expanded save namespace stays unchanged; no inventory or learned abilities are reset.

Back up native saves and **FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist** together.
ContentExpansion and its native bridge are installed by the complete FFTModLoader package;
install that first before updating this mod. Source changes and prior approval are recorded in
[APPROVED_BUILD.json](APPROVED_BUILD.json).
