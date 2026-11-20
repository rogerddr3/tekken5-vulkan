@echo off
setlocal
cd /d "%~dp0"
set "DISC=%~dp0..\SLUS-21059 (1.00).iso"
if not "%~1"=="" set "DISC=%~f1"
set "ENGINE=%~dp0ps2-native-recomp"
set "ELF=%~dp0dados-locais\files\SLUS_210.59"
set "RUNNER=%ENGINE%\build\native-tekken5\ps2run.exe"
set "GAME=%ENGINE%\games\tekken5"
if not exist "%DISC%" (
  echo Disco nao encontrado. Arraste a ISO de Tekken 5 USA sobre este arquivo.
  pause
  exit /b 1
)
if not exist "%ELF%" goto build
if not exist "%RUNNER%" goto build
set "PS2_GPU_BACKEND=vulkan"
set "PS2_GPU_HW=1"
set "PS2_WINDOW_TITLE=Tekken 5 - Vulkan"
set "PS2_VU_CXX=%ENGINE%\tools\toolchain\llvm-mingw-20260908-ucrt-x86_64\bin\clang++.exe"
for %%D in (vu0 vu1) do if not exist "%GAME%\%%D" mkdir "%GAME%\%%D"
"%RUNNER%" "%ELF%" --disc "%DISC%" --game-dir "%GAME%" --memcard "%GAME%\tekken5.ps2" --window
if errorlevel 1 (
  echo O jogo encerrou com erro. Consulte o registro na pasta do jogo.
  pause
  exit /b 1
)
exit /b 0
:build
echo Execute Compilar-Tekken5.ps1 antes de abrir o jogo.
pause
exit /b 1
