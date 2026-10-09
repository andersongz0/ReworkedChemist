# Reworked Chemist

[English](README.md) | [Português](README.pt-BR.md)

Autor: **ZeroDS** · [Código-fonte e releases](https://github.com/andersongz0/ReworkedChemist)

**0.2.44-rc.1** é a candidata com a infraestrutura transferida para o loader.
A implementação de gameplay e os recursos da versão aprovada **0.2.43** foram preservados.
A DLL foi recompilada como consumidora da API; essa mudança de arquitetura ainda precisa do teste no jogo.

- Menu Potion: Potion, Hi-Potion, X-Potion e Elixir.
- Menu Ether: Ether e Hi-Ether.
- Menu Remedy: Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Holy Water e Remedy.
- Bloqueio por estoque, escolha de alvo, arremesso e interceptação nativos.
- Onze frascos/essências; nomes/ícones, integração da IA e lojas por capítulo.
- Fire/Ice/Thunder: **5 de dano fixo + 15% do HP máximo**, arredondado para cima antes dos modificadores elementais.
- Lojas: Poison/Oil capítulo 1; elementais capítulo 2; essências de debuff capítulo 3; de buff capítulo 4.

## Instalação e dependências

Instale primeiro [FFTModLoader 0.11.7-rc.1](https://github.com/andersongz0/FFTModLoader/releases).
Extraia **ReworkedChemist-0.2.44-rc.1.zip** fora da pasta do jogo, feche jogo/loader e execute
`install.ps1 -GameDirectory "pasta do jogo"`.

A pasta instalada e o nome exibido agora são **Mods/Reworked Chemist** e **Reworked Chemist**.
O instalador move a pasta antiga identificada `Reworked Chemist - Venom Test` para um backup recuperável.
ModId/identidade da DLL históricos foram mantidos para compatibilidade, não como nome público.
Não mantenha duas pastas ativadas com o mesmo ModId.

Requer **ContentExpansion**, **JobExpansion**, Utility Mod Loader e SharedLib.Hooks.
A infraestrutura e as dependências de compatibilidade acompanham FFTModLoader.
**Não depende de Generic Knights.**
O pacote do mod não contém bridge nativo independente nem a implementação das expansões.

Saves são preservados; extras continuam em `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.
Ao trocar de computador, faça backup dos saves nativos e dessa pasta.
A IA do jogador foi testada anteriormente; não afirmamos um teste separado exaustivo da IA inimiga.

**Nenkai**: Utility Mod Loader, FF16Tools e FaithFramework.
Veja [créditos/ferramentas](CREDITS.pt-BR.md), [compilação](BUILD.pt-BR.md),
[registro da migração](APPROVED_BUILD.json) e [avisos de terceiros](THIRD_PARTY_NOTICES.md).
Novos mods só serão publicados quando explicitamente considerados finalizados pelo usuário.
