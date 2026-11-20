"""Instala as dependências locais do build Windows a partir dos pacotes oficiais."""

import hashlib
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile


ENGINE = Path(__file__).resolve().parent / "ps2-native-recomp"
LLVM = "llvm-mingw-20260908-ucrt-x86_64"
PACKAGES = [
    (
        f"https://github.com/mstorsjo/llvm-mingw/releases/download/20260908/{LLVM}.zip",
        "1bcf74d06b724aeecaa6412ca85f5b26fb1da770e7cdcefa9263c9c5c3ad34b6",
        LLVM + "/", "tools/toolchain/" + LLVM, "bin/clang++.exe",
    ),
    (
        "https://github.com/ninja-build/ninja/releases/download/v1.13.2/ninja-win.zip",
        "07fc8261b42b20e71d1720b39068c2e14ffcee6396b76fb7a795fb460b78dc65",
        "", "tools/ninja", "ninja.exe",
    ),
    (
        "https://github.com/gfx-rs/wgpu-native/releases/download/v29.0.1.1/wgpu-windows-x86_64-gnu-release.zip",
        "d471e3614733c1d4ddd61bfd19868356477d0d37bf531bf8c6cb64a7f579bd2a",
        "", "third_party/wgpu-native", "lib/libwgpu_native.a",
    ),
    (
        "https://github.com/libsdl-org/SDL/releases/download/release-2.32.10/SDL2-devel-2.32.10-mingw.zip",
        "f15cff5fca62ec9381a016ef1d42a95c638cd72d2f226ba5781c76fe43dbd1ac",
        "SDL2-2.32.10/x86_64-w64-mingw32/", "third_party/sdl2", "lib/libSDL2.a",
    ),
]


def install(url, sha256, prefix, folder, probe):
    target = ENGINE / folder
    if (target / probe).is_file():
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=target.parent) as tmp:
        stage = Path(tmp) / "files"
        archive = ENGINE / "downloads" / url.rsplit("/", 1)[1]
        if not archive.is_file():
            archive = Path(tmp) / "package.zip"
            print(f"Baixando {url.rsplit('/', 1)[1]}", flush=True)
            with urllib.request.urlopen(url, timeout=90) as src, archive.open("wb") as out:
                shutil.copyfileobj(src, out)
        with archive.open("rb") as src:
            if hashlib.file_digest(src, "sha256").hexdigest() != sha256:
                raise ValueError(f"SHA-256 incorreto para {archive.name}")
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                if member.is_dir() or not member.filename.startswith(prefix):
                    continue
                path = (stage / member.filename[len(prefix):]).resolve()
                if not path.is_relative_to(stage):
                    raise ValueError(member.filename)
                path.parent.mkdir(parents=True, exist_ok=True)
                with package.open(member) as src, path.open("wb") as out:
                    shutil.copyfileobj(src, out)
        if not (stage / probe).is_file():
            raise ValueError(f"Arquivo ausente no pacote: {probe}")
        if target.exists():
            shutil.copytree(stage, target, dirs_exist_ok=True)
        else:
            stage.rename(target)


def prepare_vulkan():
    # The pinned renderer builds its mipmap pipeline in parallel with the base shaders.
    # Join that worker before handing the GPU to the game, while retaining parallel startup.
    source = ENGINE / "runtime/src/gs/gpu/gpu.cpp"
    original = source.read_bytes()
    newline = b"\r\n" if b"\r\n" in original else b"\n"
    anchor = newline.join([b"    release_module();", b"    ensure_capacity();"])
    patched = newline.join([
        b"#ifndef __EMSCRIPTEN__",
        b"    if (g_raster_mip_thread && g_raster_mip_thread->joinable()) {",
        b"        g_raster_mip_thread->join();",
        b"        g_raster_mip_ready.store(true, std::memory_order_release);",
        b'        std::fprintf(stderr, "[gpu] mipmap pipeline ready before gameplay\\n");',
        b"    }",
        b"#endif", anchor,
    ])
    if patched not in original:
        if original.count(anchor) != 1:
            raise ValueError("Revisão do renderer incompatível com o preparo dos shaders.")
        stage = source.with_suffix(".tekken.tmp")
        stage.write_bytes(original.replace(anchor, patched))
        stage.replace(source)

    # Portable render-state keys from Tekken 5. The driver builds its own pipelines.
    keys = """0000f802 00087835 00007002 0000f002 0040f059 00007035 00087a35
    0000fa02 0000f202 00187213 00087235 0001fa02 0003fa02 00097a35 00037a35
    00197a13 00137a13 00097c35 00037c35 00087035 00187013 00107013 00407002
    00407059 00187a13""".split()
    folder = ENGINE / "games/tekken5/hw"
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / "pipelines.txt"
    existing = path.read_text(encoding="ascii").splitlines() if path.exists() else []
    missing = [key for key in keys if key not in existing]
    if missing:
        stage = path.with_suffix(".tmp")
        stage.write_text("\n".join(existing + missing) + "\n", encoding="ascii")
        stage.replace(path)


if __name__ == "__main__":
    for package in PACKAGES:
        install(*package)
    prepare_vulkan()
