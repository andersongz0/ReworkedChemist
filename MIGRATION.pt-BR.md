# Notas de migração do Reworked Chemist

[English](MIGRATION.md) | [Português](MIGRATION.pt-BR.md)

O nome público atual do mod é **Reworked Chemist**, instalado em **Mods/Reworked Chemist**.

Instalações antigas de teste usavam a pasta `Reworked Chemist - Venom Test`.
O instalador confere a identidade do mod e move somente essa pasta antiga identificada para um
backup recuperável em **FFTModLoader.Backup**, evitando duas cópias ativas do mesmo mod.
Não apague backups apenas para alterar um nome exibido.

O ModId histórico `ffttic.tests.reworkedchemist.venom`, a identidade do assembly e o namespace dos saves
são identificadores de compatibilidade, não o nome público do mod. Alterá-los sem uma migração completa
pode quebrar a resolução do mod ou o acesso aos saves expandidos existentes. Eles são preservados intencionalmente.

Faça backup dos saves nativos e de **FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist** juntos.
ContentExpansion e seu bridge nativo acompanham o pacote completo do FFTModLoader;
instale-o antes de atualizar este mod. As alterações de fontes e a aprovação anterior estão registradas em
[APPROVED_BUILD.json](APPROVED_BUILD.json).
