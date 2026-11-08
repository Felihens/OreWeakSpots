# Ore Weak Spots

A client-side BepInEx mod for Valheim: glowing weak spots appear on rocks when you aim with a pickaxe, and hitting one deals critical damage.

Mod para Valheim (BepInEx, client-side): esferas de luz surgem nas pedras ao mirar com a picareta; acertar uma dá dano crítico.

For players, see [package/README.md](package/README.md). That file is also the Thunderstore page.
Para jogadores, veja [package/README.md](package/README.md).

Download: [Thunderstore](https://thunderstore.io/c/valheim/p/DJAventuras/OreWeakSpots/) · [Nexus Mods](https://www.nexusmods.com/valheim/mods/4895) · [GitHub Releases](https://github.com/Felihens/OreWeakSpots/releases)

## Project layout

```
src/            C# source (Plugin.cs = config + Harmony patches, WeakSpotManager.cs = logic and visuals)
package/        Thunderstore package contents (manifest.json, icon.png, README.md, CHANGELOG.md)
build.sh        Compiles the DLL and copies it into the local game
pack.ps1        Builds dist/OreWeakSpots-<version>.zip from package/
```

## Building

No Visual Studio or .NET SDK is needed. The build uses the C# compiler that ships with Windows (.NET Framework 4.x), so the code is limited to C# 5.

1. Install Valheim with BepInExPack.
2. Edit `GAME=` in `build.sh` to point at your Valheim folder.
3. Run `sh build.sh` from Git Bash (or `sh build.sh nocopy` to compile without installing).
4. Run `powershell -File pack.ps1` to build the release zip in `dist/`.

When you release a new version, update `VERSION` in `src/Plugin.cs`, `version_number` in `package/manifest.json`, and `package/CHANGELOG.md`.

## How it works

- Harmony prefixes on `MineRock5.Damage`, `MineRock.Damage` and `Destructible.Damage`. The attacking client builds the `HitData`, so the mod multiplies `m_pickaxe` damage before the hit is sent over the network. Nothing is needed on the server.
- The ore under the crosshair (`Player.GetHoverObject`) gets weak spots. Rocks are identified by their ZDO prefab name and drop table, because `MineRock5` renames its own GameObject to `___MineRock5 m_meshFilter`.
- Spots are placed by raycasting from the player's eyes toward random points inside the rock's collider bounds.
- The crit sound plays through an `AudioSource` attached to the player and routed through the game's mixer group, taken from the `fx_crit` effect.

## License

MIT. See [LICENSE](LICENSE).
