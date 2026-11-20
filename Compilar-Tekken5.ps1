param([string]$Iso = (Join-Path $PSScriptRoot '..\SLUS-21059 (1.00).iso'), [ValidateRange(1,32)][int]$Jobs = 7, [switch]$Otimizar)
$ErrorActionPreference = 'Stop'
$isoPath = (Resolve-Path -LiteralPath $Iso).Path
$engine = Join-Path $PSScriptRoot 'ps2-native-recomp'
$Otimizar = $Otimizar -and (Test-Path -LiteralPath (Join-Path $engine 'generated/tekken5/table.cpp'))
$extractors = Join-Path $PSScriptRoot 'tekken5-recomp\tools'
$data = Join-Path $PSScriptRoot 'dados-locais'
function Run([string]$Program, [string[]]$Options) {
    & $Program @Options
    if ($LASTEXITCODE -ne 0) { throw "$Program encerrou com o codigo $LASTEXITCODE." }
}
if (!$Otimizar -and (Get-FileHash -LiteralPath $isoPath -Algorithm SHA1).Hash -ne 'AEC7958A90D603DAEC0BF850F2F0EB19029CF11F') {
    throw 'Esta compilacao usa o disco americano SLUS-21059 v1.00 testado neste projeto.'
}
Run python @((Join-Path $PSScriptRoot 'preparar-ferramentas.py'))
$compiler = (Resolve-Path (Join-Path $engine 'tools\toolchain\llvm-mingw-20260908-ucrt-x86_64\bin\clang++.exe')).Path
$ninja = (Resolve-Path (Join-Path $engine 'tools\ninja\ninja.exe')).Path
$originalPath = $env:Path
Push-Location -LiteralPath $engine
try {
    $env:Path = "$(Split-Path $compiler);$originalPath"
    foreach ($dep in 'third_party/wgpu-native/lib/libwgpu_native.a','third_party/sdl2/lib/libSDL2.a') {
        if (!(Test-Path -LiteralPath $dep)) { throw "Dependencia ausente: $dep" }
    }
    if (!$Otimizar) {
        foreach ($script in 'ps2_extract.py','tk5data.py') { Run python @((Join-Path $extractors $script), $isoPath, $data) }
        Run python @((Join-Path $extractors 'make_elfs.py'), $data)
    }
    $gen = (New-Item -ItemType Directory -Force 'generated/tekken5').FullName
    $game = (New-Item -ItemType Directory -Force 'games/tekken5').FullName
    $build = 'build/native-tekken5'
    $entries = Join-Path $PSScriptRoot 'entradas-tekken5.txt'
    $elf = Join-Path $data 'elf/main.elf'
    $config = @('-G','Ninja','-S','.',"-DCMAKE_MAKE_PROGRAM=$ninja","-DCMAKE_CXX_COMPILER=$compiler",'-DCMAKE_BUILD_TYPE=Release','-DPS2_WIN_STATIC=ON')
    Run cmake ($config + @('-B','build/tradutores'))
    Run cmake @('--build','build/tradutores','--target','eerecomp','vurecomp','--parallel',"$Jobs")
    if (!$Otimizar) { Run 'build/tradutores/eerecomp.exe' @($elf,'--entries',$entries,'--out',$gen) }
    $recordings = @(foreach ($unit in 'vu0','vu1') {
        $dir = [IO.Directory]::CreateDirectory((Join-Path $game $unit))
        foreach ($file in Get-ChildItem -LiteralPath $dir.FullName -Filter '*.bin' -File) {
            "$unit/$($file.Name) $((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
        }
    })
    Run 'build/tradutores/vurecomp.exe' @("$game/vu1",$gen,'--vu0-dir',"$game/vu0")
    $nativeMacros = '' | & $compiler -march=native -dM -E -x c++ -
    $gameFlags = '-ffp-contract=off'
    if ($LASTEXITCODE -eq 0 -and $nativeMacros -contains '#define __AVX2__ 1') {
        $gameFlags = '-mavx2 -ffp-contract=off'
    }
    Run cmake ($config + @('-B',$build,"-DPS2_GENERATED=$gen","-DCMAKE_CXX_FLAGS=$gameFlags"))
    Run cmake @('--build',$build,'--target','ps2run','--parallel',"$Jobs")
    [IO.File]::WriteAllLines((Join-Path (Resolve-Path $build) 'preparo-v2.ok'), [string[]]$recordings)
} finally {
    Pop-Location
    $env:Path = $originalPath
}
