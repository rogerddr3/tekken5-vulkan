# Tekken 5 Vulkan

Launcher em português para uma recompilação de Tekken 5 no Windows x64. O jogo usa o backend Vulkan de wgpu e é compilado no seu computador a partir do seu disco americano de PS2 (SLUS‑21059, v1.00).

**[Baixar o launcher para Windows x64](https://github.com/rogerddr3/tekken5-vulkan/raw/refs/heads/main/dist/Tekken5Vulkan-Windows-x64.zip)** · [Lançamentos](https://github.com/rogerddr3/tekken5-vulkan/releases)

## Primeira execução

1. Instale Git para Windows, CMake 3.20 ou superior e Python 3.11 ou superior. Habilite o PATH dos três instaladores. Use uma placa com driver Vulkan.
2. Extraia o ZIP inteiro em uma pasta gravável e abra **Tekken5Launcher.exe**.
3. Em **Configurações**, escolha a ISO. Em **Início**, clique em **Preparar jogo**. A primeira preparação requer internet para baixar as ferramentas e os projetos nas revisões fixadas. O andamento detalhado fica em `compilacao.log`.
4. Nas próximas vezes, clique em **Jogar**. **F1** abre o menu de controles durante o jogo.

## Opções

- **Gráficos** permite alternar entre janela e tela cheia e escolher a resolução interna de 1× a 4×. Em escalas maiores o runtime usa um caminho de desenho diferente, com custo variável. A configuração inicial é 1×.
- **Mods** permite ativar um patch de dados por vez, no formato de texto do ps2-native-recomp (momento, endereço hexadecimal, largura em bytes, valor hexadecimal). Use patches feitos para a edição americana.
- **Lançamentos** verifica as versões do launcher no GitHub e instala o pacote escolhido. O atualizador mantém a compilação local, o perfil e o cartão de memória.

O perfil fica em `tekken5-perfil.xml`. O cartão de memória fica em `ps2-native-recomp/games/tekken5/tekken5.ps2`. A imagem testada tem SHA-1 `AEC7958A90D603DAEC0BF850F2F0EB19029CF11F`. Esta é uma versão experimental. O teste inicial chegou ao Story Battle em uma Radeon RX 6600.

## Desenvolvimento

`launcher/Compilar.ps1` gera o launcher WPF usando o compilador do .NET Framework do Windows. `launcher/Empacotar.ps1` gera o ZIP público a partir de uma lista explícita de cinco arquivos. Para publicar uma nova versão, atualize `VERSION` em `main`.

A recompilação e o renderer vêm de [ps2-native-recomp](https://github.com/exoteric33/ps2-native-recomp). A extração dos programas de Tekken 5 usa [tekken5-recomp](https://github.com/MrPrinceYL/tekken5-recomp). O launcher adapta o design e o fluxo de atualização do projeto [Downhill Revived](https://github.com/rogerddr3/downhill-revived). Projeto de fãs independente.
