using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OreWeakSpots
{
    public enum SpotColorOption { Azul, Dourado }
    public enum CritSoundOption { Automatico, Arquivo, Jogo, Mod, Nenhum }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "felih.OreWeakSpots";
        public const string NAME = "Ore Weak Spots";
        public const string VERSION = "0.9.0";

        public static ManualLogSource Log;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> CritMultiplier;
        public static ConfigEntry<int> SpotCount;
        public static ConfigEntry<float> HitTolerance;
        public static ConfigEntry<float> SpotLifetime;
        public static ConfigEntry<float> MaxDistance;
        public static ConfigEntry<float> ActiveDuration;
        public static ConfigEntry<SpotColorOption> SpotColor;
        public static ConfigEntry<float> SpotSize;
        public static ConfigEntry<float> SoundVolume;
        public static ConfigEntry<CritSoundOption> CritSound;
        public static ConfigEntry<bool> AllRocks;
        public static ConfigEntry<string> OreNames;
        public static ConfigEntry<string> Excluded;
        public static ConfigEntry<float> MinSpacing;
        public static ConfigEntry<bool> DebugLog;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1 - Geral", "Ativado", true, "Liga ou desliga o mod.");
            CritMultiplier = Config.Bind("1 - Geral", "MultiplicadorCritico", 2f,
                new ConfigDescription("Multiplicador de dano da picareta ao acertar um ponto fraco.",
                    new AcceptableValueRange<float>(1f, 10f)));
            AllRocks = Config.Bind("1 - Geral", "TodasAsPedras", true,
                "Se ligado, pontos fracos aparecem em qualquer pedra, não só em minérios.");
            OreNames = Config.Bind("1 - Geral", "NomesDeMinerios",
                "copper,silver,obsidian,mudpile,meteorite,flametal,softtissue,giant,blackmarble,iron",
                "Partes do nome dos objetos que contam como minério (separados por vírgula). Usado quando TodasAsPedras está desligado.");
            Excluded = Config.Bind("1 - Geral", "Excluir", "tin",
                "Partes do nome de pedras que NUNCA recebem pontos fracos, mesmo com TodasAsPedras ligado (separados por vírgula). Padrão: estanho, que é pequeno demais.");

            SpotCount = Config.Bind("2 - Pontos fracos", "Quantidade", 4,
                new ConfigDescription("Quantos pontos fracos aparecem ao mesmo tempo.",
                    new AcceptableValueRange<int>(1, 8)));
            MinSpacing = Config.Bind("2 - Pontos fracos", "DistanciaEntrePontos", 1.2f,
                new ConfigDescription("Distância mínima (metros) entre dois pontos fracos. Se a pedra for pequena demais, aparecem menos pontos em vez de ficarem amontoados.",
                    new AcceptableValueRange<float>(0.3f, 5f)));
            HitTolerance = Config.Bind("2 - Pontos fracos", "ToleranciaDeAcerto", 0.6f,
                new ConfigDescription("Distância máxima (metros) entre o golpe e o ponto para contar como crítico.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            SpotLifetime = Config.Bind("2 - Pontos fracos", "DuracaoDoPonto", 5f,
                new ConfigDescription("Segundos que cada ponto fica visível antes de mudar de lugar.",
                    new AcceptableValueRange<float>(1f, 30f)));
            MaxDistance = Config.Bind("2 - Pontos fracos", "DistanciaMaxima", 8f,
                new ConfigDescription("Distância máxima (metros) dos seus olhos em que um ponto pode surgir. Os pontos se espalham pela superfície inteira do minério dentro dessa distância.",
                    new AcceptableValueRange<float>(2f, 20f)));
            ActiveDuration = Config.Bind("2 - Pontos fracos", "TempoSemMirar", 3f,
                new ConfigDescription("Segundos sem mirar nem golpear o minério até os pontos sumirem.",
                    new AcceptableValueRange<float>(0.5f, 60f)));

            SpotColor = Config.Bind("3 - Visual", "Cor", SpotColorOption.Dourado, "Cor dos pontos fracos.");
            SpotSize = Config.Bind("3 - Visual", "Tamanho", 1f,
                new ConfigDescription("Escala das esferas de luz (1 = cerca de 16 cm).", new AcceptableValueRange<float>(0.3f, 3f)));
            SoundVolume = Config.Bind("3 - Visual", "VolumeDoSom", 1f,
                new ConfigDescription("Volume do som de crítico (0 desliga). Também segue o volume de efeitos do jogo.", new AcceptableValueRange<float>(0f, 1f)));
            CritSound = Config.Bind("3 - Visual", "SomDoCritico", CritSoundOption.Automatico,
                "Automatico = arquivo da pasta SFX do mod se houver, senão o som do jogo. " +
                "Arquivo = só arquivos .wav/.ogg da pasta BepInEx/plugins/OreWeakSpots/SFX (sorteia um). " +
                "Jogo = som do Valheim ao acertar inimigo de postura quebrada. Mod = o \"tim\" do mod. Nenhum = sem som.");

            DebugLog = Config.Bind("9 - Debug", "LogDetalhado", false,
                "Escreve no log do BepInEx cada golpe, onde os pontos foram colocados e a distância do golpe até o ponto (ajuda a calibrar).");

            GameObject go = new GameObject("OreWeakSpots_Manager");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<WeakSpotManager>();

            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo(NAME + " " + VERSION + " carregado.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch]
    internal static class DamagePatches
    {
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        [HarmonyPrefix]
        private static void MineRock5Damage(MineRock5 __instance, HitData __0)
        {
            WeakSpotManager.OnPickaxeHit(__instance, __0);
        }

        [HarmonyPatch(typeof(MineRock), "Damage")]
        [HarmonyPrefix]
        private static void MineRockDamage(MineRock __instance, HitData __0)
        {
            WeakSpotManager.OnPickaxeHit(__instance, __0);
        }

        [HarmonyPatch(typeof(Destructible), "Damage")]
        [HarmonyPrefix]
        private static void DestructibleDamage(Destructible __instance, HitData __0)
        {
            WeakSpotManager.OnPickaxeHit(__instance, __0);
        }
    }
}
