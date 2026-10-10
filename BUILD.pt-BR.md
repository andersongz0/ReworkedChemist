# Compilar Reworked Chemist

[English](BUILD.md) | [Português](BUILD.pt-BR.md)

Windows x64 e SDK .NET compatível com net8/net9. Obtenha também as fontes de **FFTModLoader**.
A infraestrutura é compilada/distribuída pelo loader, sem duplicá-la neste repositório.

Compile a consumidora:
`dotnet build AlchemistRework/PlayableRuntime/PlayableRuntime.csproj -c Release -p:FFTModLoaderSourceDirectory="caminho/FFTModLoader"`

Execute testes do host com a mesma propriedade:
`dotnet run --project AlchemistRework/PlayableHostTests -c Release -p:FFTModLoaderSourceDirectory="caminho/FFTModLoader" -- "caminho/workspace/local/FFT"`
`HooksRuntimeDirectory` informa a pasta das bibliotecas de hooks da instalação local.
Os testes referenciam o **módulo real compilado do loader**, não outra cópia das fontes.
Fixtures históricos exigem entradas geradas localmente de uma instalação legítima; não são distribuídas.
Para o bridge, use `tools/build_native_bridge.ps1` do loader.

Geradores estão em `AlchemistRework/tools`; definição das habilidades em `AlchemistRework/Alchemist.definition.json`.
Use FF16Tools de Nenkai e recursos locais legítimos. Geradores históricos podem solicitar caminhos adicionais.
Não distribuímos executável do jogo, banco completo extraído, saves nem vídeos privados.

APPROVED_BUILD.json distingue conteúdo aprovado 0.2.43 da candidata de arquitetura 0.2.44.
Bridge nativo e campos de gameplay são preservados. `build_menu_descriptions.py --mod Mod --output "nova pasta de verificação" --ff16tools "caminho/FF16Tools.CLI.exe"`
altera somente três campos Description das ações de grupo em cada idioma do jogo e confere o round trip completo do NXD. Descrições dos itens individuais permanecem intactas.
MENU_DESCRIPTIONS.json registra os sete recursos verificados.
Testes isolados/ABI não comprovam renderização das cenas nem aprovação dentro do jogo.
