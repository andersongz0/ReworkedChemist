# Notas de migração do Reworked Chemist

[English](MIGRATION.md) | [Português](MIGRATION.pt-BR.md)

O nome público atual do mod é **Reworked Chemist**, instalado em **Mods/Reworked Chemist**.

Instalações antigas de teste usavam a pasta `Reworked Chemist - Venom Test`.
O instalador confere a identidade do mod e move somente essa pasta antiga identificada para um
backup recuperável em **FFTModLoader.Backup**, evitando duas cópias ativas do mesmo mod.
Não apague backups apenas para alterar um nome exibido.

O ModId atual é **`ffttic.jobs.reworkedchemist`** e a DLL do mod é
**FFTModLoader.ReworkedChemist.dll**. O FFTModLoader 0.11.7-rc.5 reconhece o antigo
ID de teste nos pacotes anteriores e o traduz na configuração de execução gerada.
O instalador preserva a DLL antiga em backup ao atualizar uma instalação existente.
O namespace dos saves expandidos continua igual; estoque e habilidades aprendidas não são reiniciados.

Faça backup dos saves nativos e de **FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist** juntos.
ContentExpansion e seu bridge nativo acompanham o pacote completo do FFTModLoader;
instale-o antes de atualizar este mod. As alterações de fontes e a aprovação anterior estão registradas em
[APPROVED_BUILD.json](APPROVED_BUILD.json).
