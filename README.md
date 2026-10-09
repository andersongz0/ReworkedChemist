# Reworked Chemist

Autor: **ZeroDS** · [Código-fonte e releases](https://github.com/andersongz0/ReworkedChemist)

Versão **0.2.43-items-selected-back-test**, aprovada no teste final do jogador.
Esta publicação altera apenas autoria, link e documentação: as DLLs, os dados
e os recursos de gameplay do pacote final permanecem idênticos.

- Potion: submenu com Potion, Hi-Potion, X-Potion e Elixir.
- Ether: submenu com Ether e Hi-Ether.
- Remedy: Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Holy Water e Remedy.
- Bloqueio por estoque, escolha de alvo, arremesso e interceptação nativos.
- Novos frascos/essências, nomes e ícones próprios e integração com a IA.
- Fire/Ice/Thunder Flask: **5 de dano fixo + 15% do HP máximo do alvo**.
- Lojas: Poison/Oil no capítulo 1; Fire/Ice/Thunder no 2; essências de debuff no 3; de buff no 4.

## Dependências e instalação

Instale primeiro [FFTModLoader 0.11.6](https://github.com/andersongz0/FFTModLoader/releases).
Dependências diretas: **Utility Mod Loader** (`fftivc.utility.modloader`),
**JobExpansion** (`fftmodloader.jobexpansion`) e **SharedLib.Hooks**
(`reloaded.sharedlib.hooks`). SigScan e GenericJobs original também fazem parte
da cadeia de dependências da configuração atual, já incluídos no FFTModLoader.

**Não depende de Generic Knights.** A IA inimiga usa a mesma integração da IA
do jogador; o teste do jogador foi aprovado, mas não há afirmação de um teste
separado de todos os comportamentos inimigos.

Extraia `ReworkedChemist-0.2.43.zip` separadamente e, com jogo e loader fechados,
execute `install.ps1 -GameDirectory "caminho da pasta do jogo"`. O nome da pasta
`Reworked Chemist - Venom Test` e o ModId histórico são intencionais para manter
a compatibilidade com a instalação e os saves existentes. Não os renomeie.
O instalador faz backup do mod substituído e não modifica saves ou sidecars.

Veja [BUILD.md](BUILD.md), [APPROVED_BUILD.json](APPROVED_BUILD.json) e
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) no repositório.
