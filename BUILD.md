# Compilação e testes

Windows x64, SDK .NET 9, Visual Studio 2022 com C++ e assembler x64, Python 3
com Pillow. O projeto conserva os caminhos relativos entre `AlchemistRework`
e `FFTModLoader_Prototype_v0.10.30` para manter os links de compilação originais.

Compilação gerenciada:

```powershell
dotnet build AlchemistRework/PlayableRuntime/PlayableRuntime.csproj -c Release
dotnet run --project FFTModLoader_Prototype_v0.10.30/tests/ContentExpansionCoreTests -c Release -- AlchemistRework/Alchemist.definition.json
```

Bridge nativo:

```powershell
./AlchemistRework/tools/build_native_bridge.ps1
```

Os testes de host usam as bibliotecas de hooks da instalação local. Informe o
caminho com `-p:HooksRuntimeDirectory="caminho das bibliotecas de hooks"`.
Testes de ABI, vídeo, saves e recursos nativos não são equivalentes a uma
aprovação visual em jogo. Não são distribuídos saves, vídeos privados, dumps,
executáveis do jogo ou bancos completos de tabelas extraídas.

Os geradores de conteúdo e arte estão em `AlchemistRework/tools`. Sua
reprodução exige as entradas originais da instalação legal do usuário e os
conversores externos apropriados. A definição de habilidades/balanceamento
fica em `AlchemistRework/Alchemist.definition.json`.

Os dois binários da release foram preservados da versão final aprovada;
`APPROVED_BUILD.json` registra seus hashes e a alteração exclusiva de metadados.
Compilar novamente produz um novo binário de desenvolvimento, não uma
certificação automática de que ele foi testado dentro do jogo.
