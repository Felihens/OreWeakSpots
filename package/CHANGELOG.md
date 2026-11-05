# Changelog

## 0.9.2
- Config options, values and log messages are now in English. **Your old settings reset to the defaults**; you can delete the old `felih.OreWeakSpots.cfg`. / Opções, valores e mensagens do log agora em inglês. **As configurações antigas voltam ao padrão**; pode apagar o `felih.OreWeakSpots.cfg` antigo.
- New README with a screenshot. / README novo, com print.

## 0.9.1
- Crit damage number is now yellow and twice as big (`CorDoNumeroCritico`, `TamanhoDoNumeroCritico`); the "CRÍTICO!" popup was removed. / Número de dano do crítico amarelo e com o dobro do tamanho; texto "CRÍTICO!" removido.
- Less noise in the BepInEx log. / Menos avisos no log do BepInEx.
- New icon. / Ícone novo.

## 0.9.0
- Crit sound now plays through the game's audio mixer (always audible). / Som de crítico tocado pelo canal de áudio do jogo.
- Default sound is the game's "hit a staggered enemy" sound. / Som padrão: acerto em inimigo de postura quebrada.
- Custom sounds: `.wav`/`.ogg` files in the `SFX` folder. / Sons próprios na pasta `SFX`.
- Weak spots on every rock by default; tin excluded (`Excluir` option). / Todas as pedras por padrão; estanho excluído.
- 4 weak spots by default with a minimum distance between them (`DistanciaEntrePontos`). / 4 pontos com distância mínima entre eles.
- Bushes and other non-rock objects are ignored. / Arbustos e objetos que não são pedra ficam de fora.

## 0.8.0
- Weak spots spread over the whole surface of the rock; new `DistanciaMaxima` option. / Pontos espalhados pela superfície inteira; nova opção `DistanciaMaxima`.

## 0.7.0
- Weak spots are now balls of light resting on the surface, with fade-in and fade-out. / Pontos viraram esferas de luz na superfície, com fade.

## 0.6.0
- Weak spots appear when you aim at the rock with a pickaxe; crits work from the first swing. / Pontos aparecem ao mirar; crítico vale desde o primeiro golpe.
- Ore detected by the game's prefab and drop table. / Minério reconhecido pelo tipo do objeto e pela tabela de drops.

## 0.5.0
- First version: glowing weak spots after the first hit; hitting them deals critical damage. / Primeira versão.
