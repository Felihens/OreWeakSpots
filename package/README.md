# Ore Weak Spots

**[English](#english) · [Português](#português)**

---

## English

A client-side Valheim mod that adds a timing-and-aim layer to mining. When you aim at a rock or ore deposit with a pickaxe, small balls of light appear on its surface. Hit one and the swing becomes a **critical hit** (2x damage by default), with a flash, a "CRÍTICO!" popup and a sound.

- **Client-side only:** the server and other players do **not** need the mod.
- Weak spots fade in, pulse, fade out and reappear somewhere else on the rock every few seconds.
- They spread over the whole visible surface of the rock and keep a minimum distance from each other.
- Works on every rock by default. Tin is excluded because the deposits are too small.

### Requirements

- BepInExPack Valheim.

### Installation

**r2modman / Thunderstore Mod Manager:** install from Thunderstore. To install a downloaded zip instead, go to Settings → *Import local mod*.

**Manual:**
1. Extract the zip.
2. Copy the `plugins/OreWeakSpots` folder into `Valheim/BepInEx/plugins/`.
   Result: `Valheim/BepInEx/plugins/OreWeakSpots/OreWeakSpots.dll`.
3. Launch the game.

To update, delete the old `OreWeakSpots` folder before copying the new one.

### Configuration

Press **F1** in game (requires Configuration Manager) or edit `BepInEx/config/felih.OreWeakSpots.cfg`. The option names are in Portuguese:

| Option | Default | What it does |
|---|---|---|
| Ativado | on | Enables/disables the mod. |
| MultiplicadorCritico | 2 | Damage multiplier when you hit a weak spot. |
| TodasAsPedras | on | Weak spots on every rock, not only ore. |
| NomesDeMinerios | copper, silver… | Ore names to match when TodasAsPedras is off. |
| Excluir | tin | Rocks that never get weak spots. |
| Quantidade | 4 | Weak spots at the same time (1–8). |
| DistanciaEntrePontos | 1.2 m | Minimum distance between weak spots. |
| ToleranciaDeAcerto | 0.6 m | How close the swing must land to a weak spot. |
| DuracaoDoPonto | 5 s | Time before a weak spot moves. |
| DistanciaMaxima | 8 m | Max distance from your eyes where spots can appear. |
| TempoSemMirar | 3 s | Time without aiming before the spots disappear. |
| Cor | Dourado | Gold (Dourado) or blue (Azul). |
| Tamanho | 1 | Size of the light balls. |
| VolumeDoSom | 1 | Crit sound volume (0 = off). |
| SomDoCritico | Automatico | Automatico, Arquivo (file), Jogo (game), Mod or Nenhum (none). |

### Custom sound

Drop `.wav` or `.ogg` files into `BepInEx/plugins/OreWeakSpots/SFX`. With `SomDoCritico` set to `Automatico` or `Arquivo`, one of them is picked at random on each crit. Without files, the mod plays the game's own sound for hitting a staggered enemy.

### Notes

- ImpactfulSkills has its own random mining crit. That one comes from ImpactfulSkills, not from this mod.
- On servers with rules or anti-cheat, let the owner know: this mod increases your pickaxe damage.

---

## Português

Mod client-side para Valheim: ao mirar numa pedra ou minério com a picareta, pequenas esferas de luz surgem na superfície. Acertar uma esfera dá **dano crítico** (2x por padrão), com clarão, texto "CRÍTICO!" e som.

- **Só no seu jogo:** o servidor e os outros jogadores **não** precisam ter o mod.
- As esferas surgem com fade, pulsam, somem e reaparecem em outro lugar da pedra a cada poucos segundos.
- Elas se espalham pela superfície visível inteira, com distância mínima entre si.
- Funciona em todas as pedras por padrão. O estanho fica de fora porque é pequeno demais.

### Requisitos

- BepInExPack Valheim.

### Instalação

**r2modman / Thunderstore Mod Manager:** instale pelo Thunderstore. Para instalar um zip baixado, use Configurações → *Import local mod*.

**À mão:**
1. Extraia o zip.
2. Copie a pasta `plugins/OreWeakSpots` para dentro de `Valheim/BepInEx/plugins/`.
   Resultado: `Valheim/BepInEx/plugins/OreWeakSpots/OreWeakSpots.dll`.
3. Abra o jogo.

Para atualizar, apague a pasta `OreWeakSpots` antiga antes de copiar a nova.

### Configuração

Aperte **F1** no jogo (precisa do Configuration Manager) ou edite `BepInEx/config/felih.OreWeakSpots.cfg`.

| Opção | Padrão | O que faz |
|---|---|---|
| Ativado | ligado | Liga/desliga o mod. |
| MultiplicadorCritico | 2 | Multiplicador do dano ao acertar uma esfera. |
| TodasAsPedras | ligado | Esferas em qualquer pedra, não só em minério. |
| NomesDeMinerios | copper, silver… | Usado quando TodasAsPedras está desligado. |
| Excluir | tin | Pedras que nunca recebem esferas. |
| Quantidade | 4 | Esferas ao mesmo tempo (1 a 8). |
| DistanciaEntrePontos | 1,2 m | Distância mínima entre esferas. |
| ToleranciaDeAcerto | 0,6 m | Quão perto o golpe precisa passar da esfera. |
| DuracaoDoPonto | 5 s | Tempo até a esfera mudar de lugar. |
| DistanciaMaxima | 8 m | Até que distância dos olhos as esferas surgem. |
| TempoSemMirar | 3 s | Tempo sem mirar até as esferas sumirem. |
| Cor | Dourado | Dourado ou Azul. |
| Tamanho | 1 | Tamanho das esferas. |
| VolumeDoSom | 1 | Volume do som de crítico (0 desliga). |
| SomDoCritico | Automatico | Automatico, Arquivo, Jogo, Mod ou Nenhum. |

### Som próprio

Coloque arquivos `.wav` ou `.ogg` na pasta `BepInEx/plugins/OreWeakSpots/SFX`. Com `SomDoCritico` em `Automatico` ou `Arquivo`, o mod sorteia um deles a cada crítico. Sem arquivos, ele usa o som do jogo de acertar inimigo de postura quebrada.

### Observações

- O ImpactfulSkills tem um crítico aleatório próprio na mineração. Esse crítico é dele, não deste mod.
- Em servidores com regras ou anti-cheat, avise o dono: o mod aumenta o dano dos seus golpes de picareta.
