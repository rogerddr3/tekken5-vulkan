param([Parameter(Mandatory=$true)][string]$Iso)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
foreach ($program in 'git','cmake','python') {
    if (!(Get-Command $program -ErrorAction SilentlyContinue)) { throw "Instale $program e abra o launcher novamente." }
}
& python -c 'import sys;sys.exit(0 if sys.version_info >= (3,11) else 1)'
if ($LASTEXITCODE) { throw 'Instale Python 3.11 ou mais recente com a opcao de adicionar ao PATH.' }
foreach ($dependency in @(
    @('ps2-native-recomp','exoteric33/ps2-native-recomp','74912bd8e57c327a02242cde4df36f12e986a2c0'),
    @('tekken5-recomp','MrPrinceYL/tekken5-recomp','308230a5d4cabaee121ec3fd1bc9aa149529c554')
)) {
    $folder,$repo,$commit = $dependency
    if (!(Test-Path -LiteralPath $folder)) {
        $stage = "$folder.partial"
        if (!(Test-Path -LiteralPath "$stage/.git")) {
            & git init -q $stage
            if ($LASTEXITCODE) { throw "Falha ao iniciar $stage." }
        }
        & git -C $stage fetch --depth 1 --no-tags "https://github.com/$repo.git" $commit
        if ($LASTEXITCODE) { throw "Falha ao baixar $repo." }
        & git -C $stage checkout -q --detach $commit
        if ($LASTEXITCODE) { throw "Falha ao preparar $repo." }
        Rename-Item -LiteralPath $stage -NewName $folder
    }
    $head = & git -C $folder rev-parse HEAD
    if ($LASTEXITCODE -or $head -ne $commit) { throw "A revisao local de $folder difere da versao usada pelo launcher." }
}
& "$PSScriptRoot/Compilar-Tekken5.ps1" -Iso $Iso
