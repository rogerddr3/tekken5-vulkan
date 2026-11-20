param([string]$Destino = (Split-Path -Parent $PSScriptRoot),[version]$Versao=(Get-Content (Join-Path $PSScriptRoot '../VERSION') -Raw).Trim())
$ErrorActionPreference='Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$wpf = Join-Path $framework WPF
if ($Versao.Build -lt 0 -or $Versao.Revision -ge 0) { throw 'Use uma versao no formato X.Y.Z.' }
$target = [IO.Directory]::CreateDirectory($Destino).FullName
$meta = Join-Path $env:TEMP ("tekken-"+[guid]::NewGuid()+".cs")
try {
    [IO.File]::WriteAllText($meta,('[assembly:System.Reflection.AssemblyVersion("{0}.0")]' -f $Versao))
    $sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
    & "$framework/csc.exe" /nologo /target:winexe /optimize+ /platform:x64 /codepage:65001 "/out:$target/Tekken5Launcher.exe" "/resource:$PSScriptRoot/Janela.xaml,Janela.xaml" "/reference:$wpf/PresentationFramework.dll" "/reference:$wpf/PresentationCore.dll" "/reference:$wpf/WindowsBase.dll" /reference:System.Xaml.dll /reference:System.Xml.Linq.dll /reference:System.IO.Compression.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll $meta @sources
    if ($LASTEXITCODE) { throw 'Falha ao compilar o launcher.' }
} finally { [IO.File]::Delete($meta) }
