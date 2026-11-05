using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OreWeakSpots
{
    public enum SpotColorOption { Blue, Gold }
    public enum CritSoundOption { Auto, File, Game, Mod, None }
    public enum CritNumberColorOption { Yellow, Red, Normal }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "felih.OreWeakSpots";
        public const string NAME = "Ore Weak Spots";
        public const string VERSION = "0.9.3";

        public static ManualLogSource Log;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> CritMultiplier;
        public static ConfigEntry<bool> CritUsesMidDamage;
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
        public static ConfigEntry<CritNumberColorOption> CritNumberColor;
        public static ConfigEntry<float> CritNumberScale;
        public static ConfigEntry<bool> DebugLog;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1 - General", "Enabled", true, "Turns the mod on or off.");
            CritMultiplier = Config.Bind("1 - General", "CritMultiplier", 2f,
                new ConfigDescription("Pickaxe damage multiplier when you hit a weak spot.",
                    new AcceptableValueRange<float>(1f, 10f)));
            CritUsesMidDamage = Config.Bind("1 - General", "CritUsesMidDamage", true,
                "Always use 2x mid damage: a crit ignores the game's random damage roll and uses the middle of your " +
                "pickaxe skill's damage range before multiplying, so crits are never weak. Off = the crit multiplies whatever the game rolled.");
            AllRocks =Config.Bind("1 - General", "AllRocks", true,
                "If on, weak spots appear on any rock, not only on ores.");
            OreNames = Config.Bind("1 - General", "OreNames",
                "copper,silver,obsidian,mudpile,meteorite,flametal,softtissue,giant,blackmarble,iron",
                "Parts of object names that count as ore (comma-separated). Used when AllRocks is off.");
            Excluded = Config.Bind("1 - General", "Exclude", "tin",
                "Parts of rock names that NEVER get weak spots, even with AllRocks on (comma-separated). Default: tin, which is too small.");

            SpotCount = Config.Bind("2 - Weak spots", "Count", 4,
                new ConfigDescription("How many weak spots appear at the same time.",
                    new AcceptableValueRange<int>(1, 8)));
            MinSpacing = Config.Bind("2 - Weak spots", "MinSpacing", 1.2f,
                new ConfigDescription("Minimum distance (meters) between two weak spots. On a rock that is too small, fewer spots appear instead of crowding together.",
                    new AcceptableValueRange<float>(0.3f, 5f)));
            HitTolerance = Config.Bind("2 - Weak spots", "HitTolerance", 0.6f,
                new ConfigDescription("Maximum distance (meters) between the hit and the spot for a crit.",
                    new AcceptableValueRange<float>(0.1f, 2f)));
            SpotLifetime = Config.Bind("2 - Weak spots", "SpotLifetime", 5f,
                new ConfigDescription("Seconds each spot stays visible before it moves.",
                    new AcceptableValueRange<float>(1f, 30f)));
            MaxDistance = Config.Bind("2 - Weak spots", "MaxDistance", 8f,
                new ConfigDescription("Maximum distance (meters) from your eyes where a spot can appear. Spots spread over the whole ore surface within this distance.",
                    new AcceptableValueRange<float>(2f, 20f)));
            ActiveDuration = Config.Bind("2 - Weak spots", "IdleTimeout", 3f,
                new ConfigDescription("Seconds without aiming at or hitting the ore before the spots disappear.",
                    new AcceptableValueRange<float>(0.5f, 60f)));

            SpotColor = Config.Bind("3 - Visuals", "Color", SpotColorOption.Gold, "Weak spot color.");
            SpotSize = Config.Bind("3 - Visuals", "Size", 1f,
                new ConfigDescription("Scale of the glowing orbs (1 = about 16 cm).", new AcceptableValueRange<float>(0.3f, 3f)));
            SoundVolume = Config.Bind("3 - Visuals", "SoundVolume", 1f,
                new ConfigDescription("Crit sound volume (0 = off). Also follows the game's effects volume.", new AcceptableValueRange<float>(0f, 1f)));
            CritSound = Config.Bind("3 - Visuals", "CritSound", CritSoundOption.Auto,
                "Auto = a file from the mod's SFX folder if there is one, otherwise the game sound. " +
                "File = only .wav/.ogg files from BepInEx/plugins/OreWeakSpots/SFX (one at random). " +
                "Game = Valheim's sound for hitting a staggered enemy. Mod = the mod's own chime. None = no sound.");
            CritNumberColor = Config.Bind("3 - Visuals", "CritNumberColor", CritNumberColorOption.Yellow,
                "Color of a crit's damage number. Normal = the game's default color.");
            CritNumberScale = Config.Bind("3 - Visuals", "CritNumberSize", 2f,
                new ConfigDescription("Size of a crit's damage number (2 = double, 1 = unchanged).",
                    new AcceptableValueRange<float>(1f, 4f)));

            DebugLog = Config.Bind("9 - Debug", "DetailedLog", false,
                "Writes each hit, where the spots were placed and the distance from the hit to the spot to the BepInEx log (helps tuning).");

            GameObject go = new GameObject("OreWeakSpots_Manager");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<WeakSpotManager>();

            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo(NAME + " " + VERSION + " loaded.");
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

    // Remembers the pickaxe damage roll so a crit can swap it for the middle of the range.
    [HarmonyPatch(typeof(Skills), "GetRandomSkillFactor")]
    internal static class SkillRollPatch
    {
        private static void Postfix(Skills __instance, Skills.SkillType __0, float __result)
        {
            WeakSpotManager.NoteSkillRoll(__instance, __0, __result);
        }
    }

    // Damage numbers arrive by RPC shortly after our hit (the rock's owner sends them).
    // Any number that pops up near a crit we just landed gets the crit color and size.
    [HarmonyPatch(typeof(DamageText), "AddInworldText")]
    internal static class CritNumberPatch
    {
        private static System.Reflection.FieldInfo _worldTexts;
        private static System.Reflection.FieldInfo _textField;

        private static void Postfix(DamageText __instance, Vector3 pos, string text)
        {
            try
            {
                bool crit = WeakSpotManager.IsRecentCrit(pos);
                if (Plugin.DebugLog.Value && (crit || WeakSpotManager.IsRecentHit(pos)))
                    Plugin.Log.LogInfo("On-screen number: " + text + (crit ? " (crit)" : ""));
                if (!crit) return;
                if (_worldTexts == null) _worldTexts = AccessTools.Field(typeof(DamageText), "m_worldTexts");
                System.Collections.IList list = _worldTexts.GetValue(__instance) as System.Collections.IList;
                if (list == null || list.Count == 0) return;
                object inst = list[list.Count - 1];
                if (_textField == null) _textField = AccessTools.Field(inst.GetType(), "m_textField");
                object tmp = _textField.GetValue(inst);
                if (tmp == null) return;

                System.Type t = tmp.GetType();
                if (Plugin.CritNumberColor.Value != CritNumberColorOption.Normal)
                {
                    Color c = Plugin.CritNumberColor.Value == CritNumberColorOption.Red
                        ? new Color(1f, 0.18f, 0.12f) : new Color(1f, 0.82f, 0.1f);
                    t.GetProperty("color").SetValue(tmp, c, null);
                }
                System.Reflection.PropertyInfo fs = t.GetProperty("fontSize");
                float size = (float)fs.GetValue(tmp, null);
                fs.SetValue(tmp, size * Plugin.CritNumberScale.Value, null);
            }
            catch (System.Exception e)
            {
                if (Plugin.DebugLog.Value) Plugin.Log.LogWarning("CritNumberPatch: " + e.Message);
            }
        }
    }
}
