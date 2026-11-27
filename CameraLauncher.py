"""Mod de camera para Tekken 5 Recomp v0.1.4, SLUS-21059 v1.00."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument("--root", type=Path, default=Path(os.environ["LOCALAPPDATA"]) / "Tekken5Vulkan")
parser.add_argument("--check", action="store_true")
parser.add_argument("--remover", action="store_true")
parser.add_argument("--source-only", action="store_true")
args = parser.parse_args()
engine = args.root / "ps2-native-recomp"
unit = engine / "generated/tekken5/unit_008A.cpp"
build = engine / "build/native-tekken5"
source = unit.read_bytes()
start = source.index(b"\nL_0032B434:")
end = source.index(b"\nL_0032B4E0:", start)
body = source[start:end]
original = b"c.r[1].sd[0] = static_cast<std::int32_t>(0x3F800000u);"
# Both diagonal entries of the perspective matrix use this numerator.
modified = b"c.r[1].sd[0] = ld8(0x003E453Bu) == 1u ? 0x3F400000u : 0x3F800000u;"
legacy = modified
modified = b'{ static const bool enabled = []{ const char* value = std::getenv("TK5_WIDE_CAMERA"); return !value || value[0] != \'0\'; }(); c.r[1].sd[0] = enabled && ld8(0x003E453Bu) == 1u ? 0x3F400000u : 0x3F800000u; }'
body = body.replace(legacy, modified)
if body.count(original) + body.count(modified) != 1:
    raise SystemExit("Rotina de projecao incompativel.")
for register in (2, 5):
    if body.count(f"fp::ee::div(c.fcr31, c.f[1], c.f[{register}])".encode()) != 1:
        raise SystemExit("Matriz de projecao incompativel.")
if args.check:
    print("Rotina compativel. Mod " + ("ativo no fonte." if modified in body else "disponivel."))
    raise SystemExit()
body = body.replace(modified, original) if args.remover else body.replace(original, modified)
result = source[:start] + body + source[end:]
cmake = args.root / "ferramentas/cmake/bin/cmake.exe"
if not args.source_only and not (cmake.is_file() and (build / "build.ninja").is_file()):
    raise SystemExit("Prepare o jogo no launcher primeiro.")
if not args.remover and b"#include <cstdlib>" not in result:
    result = b"#include <cstdlib>\n" + result
if result != source:
    with tempfile.NamedTemporaryFile(dir=unit.parent, delete=False) as temp:
        temp.write(result)
    os.replace(temp.name, unit)
if args.source_only: raise SystemExit()
subprocess.run([str(cmake), "--build", str(build), "--target", "ps2run", "--parallel", "4"], check=True)
print("Concluido. Abra o launcher e escolha a proporcao em Graficos.")

