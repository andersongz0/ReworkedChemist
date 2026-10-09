# Reworked Chemist

[English](README.md) | [Português](README.pt-BR.md)

Autor: **ZeroDS** · [Código-fonte e releases](https://github.com/andersongz0/ReworkedChemist)

Reworked Chemist amplia a classe **Chemist**, organizando os medicamentos em menus de escolha e adicionando frascos de dano e essências de buff/debuff. Inclui verificação de estoque, arremesso e interceptação nativos, suporte à IA e venda nas lojas conforme a história avança.

## Funcionalidades

- Menu Potion: Potion, Hi-Potion, X-Potion e Elixir.
- Menu Ether: Ether e Hi-Ether.
- Menu Remedy: Antidote, Eye Drops, Echo Herbs, Maiden's Kiss, Gold Needle, Holy Water e Remedy.
- Bloqueio por estoque, escolha de alvo, arremesso e interceptação nativos.
- Onze frascos/essências; nomes/ícones, integração da IA e lojas por capítulo.
- Fire/Ice/Thunder: **5 de dano fixo + 15% do HP máximo**, arredondado para cima antes dos modificadores elementais.
- Lojas: Poison/Oil capítulo 1; elementais capítulo 2; essências de debuff capítulo 3; de buff capítulo 4.

## Requisitos

- Windows x64 e instalação legítima de **FINAL FANTASY TACTICS - The Ivalice Chronicles** pela Steam, modo Enhanced.
- [FFTModLoader 0.11.7-rc.2](https://github.com/andersongz0/FFTModLoader/releases/tag/v0.11.7-rc.2), instalado com o pacote completo.

ContentExpansion, JobExpansion, Utility Mod Loader e suas dependências de compatibilidade acompanham o FFTModLoader.

## Guia de Instalação do Reworked Chemist

1. Instale FFTModLoader seguindo [o guia dele](https://github.com/andersongz0/FFTModLoader/blob/main/README.pt-BR.md#guia-de-instalação-do-fftmodloader).
2. Feche o jogo. Feche também o loader, caso já esteja instalado e aberto.
3. Baixe **ReworkedChemist-0.2.44-rc.2.zip** em [Assets da release](https://github.com/andersongz0/ReworkedChemist/releases/tag/v0.2.44-rc.2), não **Source code**.
4. Extraia o ZIP em uma pasta separada, fora da instalação do jogo.
5. Dê dois cliques em **Install.cmd** e escolha o idioma.
6. Confira a pasta encontrada e digite **SIM** para confirmar. Se houver várias instalações, escolha uma; se nenhuma for encontrada, informe o caminho de **FFT_enhanced.exe** ou da pasta que o contém.
7. Autorize a solicitação de permissão do Windows, se aparecer, e aguarde a mensagem de conclusão.

Não é necessário digitar comandos. O instalador verifica o pacote e guarda arquivos substituídos em **FFTModLoader.Backup**, dentro da pasta do jogo. Saves e outros mods são preservados.
O mod é instalado em **Mods/Reworked Chemist**.

## Como usar

Abra **FFTModLoader.exe** na pasta do jogo. Aprenda as habilidades do Chemist e obtenha seus itens. Na batalha, escolha Potion, Ether ou Remedy, selecione um item em estoque e depois escolha o alvo. Os novos frascos e essências aparecem nas lojas conforme a história avança.

## Saves

Ao trocar de computador, faça backup dos saves nativos e de `FFTModLoader.Runtime/ExpandedSaves/ReworkedChemist`.

[Detalhes de instalações anteriores](MIGRATION.pt-BR.md)

## Atualização

Feche o jogo e o loader, se estiver aberto, e execute **Install.cmd** da nova release completa. Se faltarem arquivos de dependências, reinstale o pacote completo do FFTModLoader.

## Créditos

**Nenkai**: Utility Mod Loader, FF16Tools e FaithFramework.
Veja [créditos/ferramentas](CREDITS.pt-BR.md), [compilação](BUILD.pt-BR.md),
[avisos de terceiros](THIRD_PARTY_NOTICES.md).
