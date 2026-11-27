"""Build the updater archive and refresh the offline toolchain payload."""
import shutil
import sys
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parents[1]
work = Path(sys.argv[1])
dist = root / "dist"
files = ["Tekken5Launcher.exe", "Compilar-Tekken5.ps1", "preparar-ferramentas.py",
         "entradas-tekken5.txt", "Preparar-Projeto.ps1"]
patch = (root / "patches/tekken5-modes.patch").read_text(encoding="utf-8")
camera = (root / "CameraLauncher.py").read_text(encoding="utf-8").rstrip()
for name in files[1:]:
    data = (root / name).read_text(encoding="utf-8-sig")
    if name == "preparar-ferramentas.py":
        marker = "BUNDLED_MODES_PATCH = None"
        assert data.count(marker) == 1
        data = data.replace(marker, "BUNDLED_MODES_PATCH = " + repr(patch))
    elif name == "Compilar-Tekken5.ps1":
        marker = "    Run python @((Join-Path $PSScriptRoot 'CameraLauncher.py'), '--root', $PSScriptRoot, '--source-only')"
        assert data.count(marker) == 1
        data = data.replace(marker, "    @'\n" + camera + "\n'@ | python - --root $PSScriptRoot --source-only\n"
                            "    if ($LASTEXITCODE) { throw 'Falha ao preparar a camera.' }")
    (work / name).write_text(data, encoding="utf-8-sig" if name.endswith(".ps1") else "utf-8")
with ZipFile(dist / "Tekken5Vulkan-Windows-x64.zip", "w", ZIP_DEFLATED) as z:
    for name in files:
        z.write(work / name, name)
with ZipFile(work / "base.zip") as old, ZipFile(work / "payload.zip", "w", ZIP_DEFLATED) as new:
    for item in old.infolist():
        if item.filename not in files and item.filename != "CameraLauncher.py":
            with old.open(item) as src, new.open(item, "w") as dst:
                shutil.copyfileobj(src, dst, 1024 * 1024)
    for name in files:
        new.write(work / name, name)
    new.write(root / "CameraLauncher.py", "CameraLauncher.py")
