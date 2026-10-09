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

## Requisitos

- Windows x64 e instalação legítima da Steam de **FINAL FANTASY TACTICS - The Ivalice Chronicles**, modo Enhanced.
- [FFTModLoader 0.11.7-rc.1](https://github.com/andersongz0/FFTModLoader/releases/tag/v0.11.7-rc.1), instalado com o pacote completo.
- **ContentExpansion**, **JobExpansion**, Utility Mod Loader e SharedLib.Hooks, incluindo suas dependências de compatibilidade.

Todos os componentes necessários acompanham FFTModLoader; não é necessário baixar dependências separadamente.

## Guia de Instalação do Reworked Chemist

1. Instale FFTModLoader seguindo [o guia dele](https://github.com/andersongz0/FFTModLoader/blob/main/README.pt-BR.md#guia-de-instalação-do-fftmodloader).
2. Feche o jogo e o loader.
3. Baixe **ReworkedChemist-0.2.44-rc.1.zip** em [Assets da release](https://github.com/andersongz0/ReworkedChemist/releases/tag/v0.2.44-rc.1), não **Source code**.
4. Extraia fora da pasta do jogo. Abra o PowerShell na pasta extraída e execute `./install.ps1 -GameDirectory "pasta do jogo"`.
5. Confira se **Mods/Reworked Chemist/ModConfig.json** existe na instalação do jogo. O nome exibido é **Reworked Chemist**.
6. Abra **FFTModLoader.exe** no modo Enhanced. Aprenda as habilidades disponíveis do Chemist e obtenha seus itens; itens com estoque zerado não podem ser usados e as lojas seguem os capítulos listados acima.

O instalador confere hashes e guarda arquivos substituídos em **FFTModLoader.Backup**.
Para atualizar, feche jogo/loader e instale a nova release completa. Mantenha apenas uma cópia ativa de Reworked Chemist.
Se faltarem dependências, reinstale o pacote completo do FFTModLoader.

## Saves e instalações anteriores

Saves são preservados; extras continuam em `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.
Ao trocar de computador, faça backup dos saves nativos e dessa pasta.
A IA do jogador foi testada anteriormente; não afirmamos um teste separado exaustivo da IA inimiga.
Consulte [as notas de migração](MIGRATION.pt-BR.md) para detalhes de instalações antigas.

**Nenkai**: Utility Mod Loader, FF16Tools e FaithFramework.
Veja [créditos/ferramentas](CREDITS.pt-BR.md), [compilação](BUILD.pt-BR.md),
[registro da migração](APPROVED_BUILD.json) e [avisos de terceiros](THIRD_PARTY_NOTICES.md).
Novos mods só serão publicados quando explicitamente considerados finalizados pelo usuário.
