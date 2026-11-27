param([Parameter(Mandatory=$true)][string]$InstaladorBase, [string]$Versao)
$ErrorActionPreference = 'Stop'
$root = (Get-Item $PSScriptRoot).Parent.FullName
if (!$Versao) { $Versao = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim() }
$dist = [IO.Directory]::CreateDirectory((Join-Path $root 'dist')).FullName
$work = [IO.Directory]::CreateDirectory((Join-Path $dist ("build-" + [Guid]::NewGuid().ToString('N')))).FullName
Add-Type -AssemblyName System.IO.Compression
function Copy-Resource($assembly, $name, $dest) {
    $stream = $assembly.GetManifestResourceStream($name)
    if (!$stream) { throw "Recurso ausente: $name" }
    try {
        $file = [IO.File]::Create($dest)
        try { $stream.CopyTo($file) } finally { $file.Dispose() }
    } finally { $stream.Dispose() }
}
try {
    $old = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $InstaladorBase).Path)
    Copy-Resource $old 'payload.zip' "$work/base.zip"
    $file = [IO.File]::OpenRead("$work/base.zip")
    try {
        $zip = New-Object IO.Compression.ZipArchive($file)
        try {
            $stream = $zip.GetEntry('Tekken5Launcher.exe').Open()
            $buffer = New-Object IO.MemoryStream
            try { $stream.CopyTo($buffer); $launcher = [Reflection.Assembly]::Load($buffer.ToArray()) }
            finally { $stream.Dispose(); $buffer.Dispose() }
        } finally { $zip.Dispose() }
    } finally { $file.Dispose() }
    foreach ($name in 'Tekken5.ico','Capa.jpg') { Copy-Resource $launcher $name "$work/$name" }
    & "$PSScriptRoot/Compilar.ps1" -Destino $work -Recursos $work -Versao $Versao
    & python "$root/installer/empacotar.py" $work
    if ($LASTEXITCODE) { throw 'Falha ao empacotar a atualização.' }
    $meta = Join-Path $work 'version.cs'
    [IO.File]::WriteAllText($meta,('[assembly:System.Reflection.AssemblyVersion("{0}.0")]' -f $Versao))
    $csc = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
    & $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32icon:$work/Tekken5.ico" "/resource:$work/payload.zip,payload.zip" "/out:$dist/Tekken5Recomp-Setup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $root 'installer\Setup.cs') (Join-Path $PSScriptRoot 'SaveStorage.cs') $meta
    if ($LASTEXITCODE) { throw 'Falha ao compilar o instalador.' }
} finally {
    if ((Get-Item -LiteralPath $work).Parent.FullName -eq $dist) { Remove-Item -LiteralPath $work -Recurse -Force }
}
