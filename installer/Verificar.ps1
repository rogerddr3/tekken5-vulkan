param([Parameter(Mandatory=$true)][string]$InstaladorBase)
$ErrorActionPreference = 'Stop'
$root = (Get-Item $PSScriptRoot).Parent.FullName
$dist = Join-Path $root 'dist'
$check = [IO.Directory]::CreateDirectory((Join-Path $dist ("verify-" + [Guid]::NewGuid().ToString('N')))).FullName
try {
    $setup = Join-Path $dist 'Tekken5Recomp-Setup.exe'
    $version = [Version]((Get-Content "$root/VERSION" -Raw).Trim() + '.0')
    if ([Reflection.AssemblyName]::GetAssemblyName($setup).Version -ne $version) { throw 'Versao incorreta no instalador.' }
    $install = Join-Path $check 'instalado'
    $process = Start-Process -FilePath $setup -ArgumentList ('--verify "' + $install + '"') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode) { throw "Falha na extracao: $(Get-Content -LiteralPath "$install.error.txt" -Raw)" }
    $patched = Join-Path $install 'preparar-ferramentas.py'
    foreach ($pass in 1..2) {
        & "$install/ferramentas/python/python.exe" $patched
        if ($LASTEXITCODE) { throw "Falha no preparo do runtime (passagem $pass)." }
    }
    if (!(Test-Path "$install/ps2-native-recomp/host/variant_units.cpp")) { throw 'Runtime incompleto.' }
    $old = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $InstaladorBase).Path)
    Add-Type -AssemblyName System.IO.Compression
    $payload = $old.GetManifestResourceStream('payload.zip')
    $archive = New-Object IO.Compression.ZipArchive($payload)
    try {
        $src = $archive.GetEntry('Tekken5Launcher.exe').Open()
        $mem = New-Object IO.MemoryStream
        try { $src.CopyTo($mem); $launcher = [Reflection.Assembly]::Load($mem.ToArray()) }
        finally { $mem.Dispose(); $src.Dispose() }
    } finally { $archive.Dispose(); $payload.Dispose() }
    $test = $launcher.GetType('Tekken5Launcher.InstallUpdate',$true).GetMethod('Prepare',[Reflection.BindingFlags]'Static,NonPublic')
    $test.Invoke($null,@($check,"$dist/Tekken5Vulkan-Windows-x64.zip",$version))
    if ((Get-FileHash "$check/new/Tekken5Launcher.exe").Hash -ne (Get-FileHash "$install/Tekken5Launcher.exe").Hash) { throw 'Os launchers divergem.' }
    Write-Output "OK: instalador $version, runtime preparado duas vezes e ZIP aceito pelo launcher anterior."
} finally {
    if ((Get-Item -LiteralPath $check).Parent.FullName -eq (Get-Item $dist).FullName) { Remove-Item -LiteralPath $check -Recurse -Force }
}
