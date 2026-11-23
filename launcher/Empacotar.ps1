param([string]$Versao = (Get-Content (Join-Path $PSScriptRoot '../VERSION') -Raw).Trim())
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = [IO.Directory]::CreateDirectory((Join-Path $root 'dist')).FullName
& "$PSScriptRoot/Compilar.ps1" -Destino $root -Versao $Versao
$files = @('Tekken5Launcher.exe','Preparar-Projeto.ps1','Compilar-Tekken5.ps1','CameraLauncher.py','preparar-ferramentas.py','entradas-tekken5.txt') | ForEach-Object { Join-Path $root $_ }
Compress-Archive -LiteralPath $files -DestinationPath "$dist/Tekken5Vulkan-Windows-x64.zip" -Force
