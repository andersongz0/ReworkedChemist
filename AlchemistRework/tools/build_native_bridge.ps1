param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Join-Path $workspace 'FFTModLoader_Prototype_v0.10.30\src\ContentExpansionNative'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $workspace 'AlchemistRework\builds\native-bridge-001' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith((Join-Path $workspace 'AlchemistRework\builds\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Build output must stay inside AlchemistRework/builds.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$vs = & 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Existing Visual C++ x64 compiler unavailable.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
$command = "`"$vcvars`" && ml64 /nologo /c /Fo`"$output\bridge-asm.obj`" `"$source\bridge.asm`" && cl /nologo /O2 /W4 /WX /MT /LD /EHsc /Fo`"$output\\`" `"$source\bridge.cpp`" `"$source\first_fault.cpp`" `"$output\bridge-asm.obj`" /link /OUT:`"$output\FFTModLoader.ContentExpansion.Native.dll`" /IMPLIB:`"$output\bridge.lib`""
& $env:COMSPEC /d /s /c $command
if ($LASTEXITCODE -ne 0) { throw 'Native bridge build failed.' }
Get-FileHash -LiteralPath (Join-Path $output 'FFTModLoader.ContentExpansion.Native.dll') -Algorithm SHA256
