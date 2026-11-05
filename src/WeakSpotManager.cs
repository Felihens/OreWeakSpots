using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OreWeakSpots
{
    /// Tracks the ore the local player is mining, places glowing weak spots on it
    /// and turns pickaxe hits that land on a spot into critical hits.
    public class WeakSpotManager : MonoBehaviour
    {
        private static WeakSpotManager _inst;

        private Component _target;
        private float _lastSeenTime;           // last time the ore was aimed at or hit
        private float _nextScan;
        private Vector3 _anchor;               // spots are placed around this point
        private readonly List<WeakSpot> _spots = new List<WeakSpot>();
        private int _critFrame = -1;

        private Texture2D _glowTex;
        private Mesh _quadMesh;
        private Mesh _sphereMesh;
        private Shader _shader;
        private AudioClip _critClip;

        private void Awake()
        {
            _inst = this;
            _glowTex = MakeGlowTexture(64);
            _quadMesh = MakeQuadMesh();
            // Borrow Unity's built-in sphere mesh. Safe to destroy the temp object here (not in a hit callback).
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            string[] shaderNames = { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Unlit/Transparent", "UI/Default" };
            for (int i = 0; i < shaderNames.Length && _shader == null; i++)
            {
                _shader = Shader.Find(shaderNames[i]);
                if (_shader != null) Plugin.Log.LogInfo("Shader usado: " + shaderNames[i]);
            }
            if (_shader == null) Plugin.Log.LogWarning("Nenhum shader transparente encontrado; só a luz do ponto será visível.");
            _critClip = MakeCritClip();
            StartCoroutine(LoadSoundFiles());
        }

        // ---------------------------------------------------------------- hits

        public static void OnPickaxeHit(Component rock, HitData hit)
        {
            if (_inst == null || hit == null || rock == null) return;
            if (!Plugin.Enabled.Value) return;
            Player player = Player.m_localPlayer;
            if (player == null) return;
            Character attacker = hit.GetAttacker();
            string why = null;
            if (attacker != player) why = "golpe não é do jogador local (" + (attacker == null ? "nulo" : attacker.name) + ")";
            else if (hit.m_skill != Skills.SkillType.Pickaxes) why = "ferramenta não é picareta (" + hit.m_skill + ")";
            else if (!IsOre(rock)) why = "não está na lista de minérios";
            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo("Golpe em " + OreId(rock) + " (" + rock.GetType().Name + "): " + (why == null ? "aceito" : "ignorado, " + why));
            if (why != null) return;
            _inst.HandleHit(rock, hit);
        }

        private static ZNetView NViewOf(Component rock)
        {
            ZNetView nview = null;
            System.Reflection.FieldInfo f = HarmonyLib.AccessTools.Field(rock.GetType(), "m_nview");
            if (f != null) nview = f.GetValue(rock) as ZNetView;
            if (nview == null) nview = rock.GetComponentInParent<ZNetView>();
            return nview;
        }

        private static Transform RootOf(Component rock)
        {
            ZNetView nview = NViewOf(rock);
            return nview != null ? nview.transform : rock.transform.root;
        }

        /// Text used to recognise the ore. MineRock5 renames its own object to
        /// "___MineRock5 m_meshFilter" on load, so the object name can't be trusted:
        /// use the registered prefab name (e.g. "rock4_copper_frac") plus the display name.
        private static string OreId(Component rock)
        {
            string id = rock.gameObject.name;
            ZNetView nview = NViewOf(rock);
            if (nview != null && ZNetScene.instance != null)
            {
                ZDO zdo = nview.GetZDO();
                if (zdo != null)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                    if (prefab != null) id = prefab.name;
                }
            }
            System.Reflection.FieldInfo nameField = HarmonyLib.AccessTools.Field(rock.GetType(), "m_name");
            if (nameField != null)
            {
                string display = nameField.GetValue(rock) as string;
                if (!string.IsNullOrEmpty(display)) id += " " + display;
            }
            return id;
        }

        private bool BelongsToTarget(Collider c)
        {
            Transform t = c.transform;
            return t.IsChildOf(RootOf(_target)) || t.IsChildOf(_target.transform);
        }

        private static bool NameMatches(string name, string list)
        {
            string[] parts = list.ToLowerInvariant().Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length > 0 && name.Contains(p)) return true;
            }
            return false;
        }

        private static bool IsOre(Component rock)
        {
            string name = OreId(rock).ToLowerInvariant();
            if (NameMatches(name, Plugin.Excluded.Value)) return false;
            if (!IsRock(rock)) return false;
            if (Plugin.AllRocks.Value) return true;
            if (NameMatches(name, Plugin.OreNames.Value)) return true;
            return DropsMoreThanStone(rock, 0);
        }

        /// MineRock/MineRock5 are always rocks. A Destructible counts only if it behaves like stone:
        /// the pickaxe damages it and the axe doesn't (rules out bushes, stumps, crates...).
        private static bool IsRock(Component rock)
        {
            Destructible d = rock as Destructible;
            if (d == null) return true;
            HitData.DamageModifier pick = d.m_damages.m_pickaxe;
            HitData.DamageModifier chop = d.m_damages.m_chop;
            bool pickWorks = pick != HitData.DamageModifier.Immune && pick != HitData.DamageModifier.Ignore;
            bool chopFails = chop == HitData.DamageModifier.Immune || chop == HitData.DamageModifier.Ignore;
            return pickWorks && chopFails;
        }

        /// Ore deposits drop something besides plain "Stone" (CopperOre, TinOre, IronScrap...).
        /// Same idea ImpactfulSkills uses; also works for ores added by other mods.
        private static bool DropsMoreThanStone(Component rock, int depth)
        {
            DropTable table = null;
            System.Reflection.FieldInfo f = HarmonyLib.AccessTools.Field(rock.GetType(), "m_dropItems");
            if (f != null) table = f.GetValue(rock) as DropTable;

            Destructible d = rock as Destructible;
            if (d != null)
            {
                DropOnDestroyed dod = d.GetComponent<DropOnDestroyed>();
                if (dod != null) table = dod.m_dropWhenDestroyed;
                // Intact copper/silver turns into a fractured rock: check what that one drops.
                if ((table == null || table.m_drops == null || table.m_drops.Count == 0) &&
                    d.m_spawnWhenDestroyed != null && depth < 2)
                {
                    Component next = d.m_spawnWhenDestroyed.GetComponent<MineRock5>();
                    if (next == null) next = d.m_spawnWhenDestroyed.GetComponent<MineRock>();
                    if (next != null) return DropsMoreThanStone(next, depth + 1);
                }
            }

            if (table == null || table.m_drops == null) return false;
            for (int i = 0; i < table.m_drops.Count; i++)
            {
                GameObject item = table.m_drops[i].m_item;
                if (item != null && item.name != "Stone") return true;
            }
            return false;
        }

        private void SetTarget(Component rock, Vector3 anchor)
        {
            ClearSpots();
            _target = rock;
            _anchor = anchor;
            _lastSeenTime = Time.time;
            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo("Marcando " + OreId(rock) + " (" + rock.GetType().Name + ") com " +
                                   Plugin.SpotCount.Value + " pontos fracos.");
            for (int i = 0; i < Plugin.SpotCount.Value; i++)
            {
                WeakSpot s = CreateSpot();
                s.Relocate(i * 0.3f);
                _spots.Add(s);
            }
        }

        private void HandleHit(Component rock, HitData hit)
        {
            _lastSeenTime = Time.time;

            // Same swing hitting several parts of the rock: all of them crit.
            if (_critFrame == Time.frameCount && _target == rock)
            {
                ApplyCrit(hit);
                return;
            }

            // Hit an ore that wasn't marked yet (e.g. never aimed at it): mark it now.
            if (_target != rock)
            {
                SetTarget(rock, hit.m_point);
                return;
            }

            WeakSpot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _spots.Count; i++)
            {
                WeakSpot s = _spots[i];
                if (!s.IsHittable) continue;
                float d = Vector3.Distance(s.Position, hit.m_point);
                if (d < bestDist) { bestDist = d; best = s; }
            }

            if (Plugin.DebugLog.Value)
                Plugin.Log.LogInfo("Golpe em " + rock.name + ": distância até o ponto mais próximo = " +
                                   (best == null ? "nenhum ponto" : bestDist.ToString("0.00") + " m"));

            if (best != null && bestDist <= Plugin.HitTolerance.Value)
            {
                _critFrame = Time.frameCount;
                ApplyCrit(hit);
                PlayCritEffects(best.Position, best.Color);
                best.Relocate(0.5f);
            }
        }

        private void ApplyCrit(HitData hit)
        {
            hit.m_damage.m_pickaxe *= Plugin.CritMultiplier.Value;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (_target == null && _spots.Count > 0) ClearSpots();   // rock destroyed or replaced

            Player player = Player.m_localPlayer;
            if (!Plugin.Enabled.Value || player == null || !HoldingPickaxe(player))
            {
                if (_target != null) ClearSpots();
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.1f;
                ScanAimedOre(player);
            }
            if (_target == null) return;

            bool tooFar = Vector3.Distance(player.transform.position, _anchor) > 10f;
            bool idle = Time.time - _lastSeenTime > Plugin.ActiveDuration.Value;
            if (tooFar || idle)
            {
                ClearSpots();
                return;
            }

            Camera cam = Camera.main;
            for (int i = 0; i < _spots.Count; i++) _spots[i].Tick(cam);
        }

        private static bool HoldingPickaxe(Player player)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            return weapon != null && weapon.m_shared != null &&
                   weapon.m_shared.m_skillType == Skills.SkillType.Pickaxes;
        }

        /// Uses the object under the crosshair (the one showing "Copper Deposit")
        /// to pick which ore gets weak spots.
        private void ScanAimedOre(Player player)
        {
            GameObject hover = player.GetHoverObject();
            if (hover == null) return;
            Component rock = hover.GetComponentInParent<MineRock5>();
            if (rock == null) rock = hover.GetComponentInParent<MineRock>();
            if (rock == null) rock = hover.GetComponentInParent<Destructible>();
            if (rock == null || !IsOre(rock)) return;

            Vector3 aimPoint;
            bool hasAim = AimPointOn(rock, out aimPoint);

            if (rock != _target)
            {
                SetTarget(rock, hasAim ? aimPoint : hover.transform.position);
                return;
            }
            _lastSeenTime = Time.time;
            if (hasAim) _anchor = aimPoint;   // new spots follow where you look
        }

        /// Point on the rock where the camera's center ray lands.
        private bool AimPointOn(Component rock, out Vector3 point)
        {
            point = Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 30f, ~0,
                                                   QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Component prev = _target;
            _target = rock;   // BelongsToTarget checks against _target
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance < best && BelongsToTarget(hits[i].collider))
                {
                    best = hits[i].distance;
                    point = hits[i].point;
                }
            }
            _target = prev;
            return best < float.MaxValue;
        }

        private void ClearSpots()
        {
            for (int i = 0; i < _spots.Count; i++) _spots[i].Destroy();
            _spots.Clear();
            _target = null;
        }

        // ---------------------------------------------------------------- placement

        /// Box around every collider of the ore (all its still-unbroken parts).
        private bool TargetBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            Transform[] roots = { RootOf(_target), _target.transform };
            for (int r = 0; r < roots.Length; r++)
            {
                if (r == 1 && roots[1] == roots[0]) break;
                Collider[] cols = roots[r].GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider c = cols[i];
                    if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy) continue;
                    if (!any) { bounds = c.bounds; any = true; }
                    else bounds.Encapsulate(c.bounds);
                }
            }
            return any;
        }

        /// Casts rays from the player's eyes toward random points inside the ore's bounds,
        /// so spots spread over the whole visible surface of the deposit.
        public bool FindSurfacePoint(WeakSpot self, out Vector3 pos, out Vector3 normal, out Collider col)
        {
            pos = Vector3.zero; normal = Vector3.up; col = null;
            Player player = Player.m_localPlayer;
            if (player == null || _target == null) return false;

            Bounds b;
            if (!TargetBounds(out b)) return false;

            Vector3 eye = player.GetEyePoint();
            float maxDist = Plugin.MaxDistance.Value;
            // Fixed minimum spacing: a small rock simply gets fewer spots instead of a cluster.
            float sep = Plugin.MinSpacing.Value;

            int noHit = 0, blocked = 0, far = 0, crowded = 0;
            string blocker = "";
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector3 aim = b.center + Vector3.Scale(Random.insideUnitSphere, b.extents);
                Vector3 dir = aim - eye;
                float len = dir.magnitude;
                if (len < 0.1f) continue;
                dir /= len;

                RaycastHit[] hits = Physics.RaycastAll(eye, dir, len + b.size.magnitude, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) { noHit++; continue; }
                System.Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));

                RaycastHit first = default(RaycastHit);
                bool found = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider.transform.IsChildOf(player.transform)) continue;
                    first = hits[h]; found = true; break;
                }
                if (!found) { noHit++; continue; }
                if (!BelongsToTarget(first.collider))                                   // something in the way
                {
                    blocked++;
                    blocker = first.collider.name + " (layer " + LayerMask.LayerToName(first.collider.gameObject.layer) + ")";
                    continue;
                }
                if (first.distance > maxDist) { far++; continue; }

                bool tooClose = false;
                for (int i = 0; i < _spots.Count; i++)
                {
                    WeakSpot o = _spots[i];
                    if (o == self || !o.IsPlaced) continue;
                    if (Vector3.Distance(o.Position, first.point) < sep) { tooClose = true; break; }
                }
                if (tooClose) { crowded++; continue; }

                pos = first.point; normal = first.normal; col = first.collider;
                if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("Ponto fraco colocado em " + first.collider.name + " a " + first.distance.ToString("0.0") + " m dos olhos.");
                return true;
            }
            if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("Não achei lugar para o ponto fraco: sem acerto=" + noHit + ", bloqueado=" + blocked +
                               (blocked > 0 ? " por " + blocker : "") + ", longe=" + far + ", perto de outro=" + crowded);
            return false;
        }

        // ---------------------------------------------------------------- visuals

        public Color CurrentColor()
        {
            return Plugin.SpotColor.Value == SpotColorOption.Azul
                ? new Color(0.35f, 0.75f, 1f)
                : new Color(1f, 0.78f, 0.25f);
        }

        private WeakSpot CreateSpot()
        {
            return new WeakSpot(this);
        }

        // Built by hand (not CreatePrimitive) so it never has a collider: spots are created
        // inside the attack animation event, where colliders can't be removed immediately.
        private static Mesh MakeQuadMesh()
        {
            Mesh m = new Mesh();
            m.name = "OreWeakSpots_Quad";
            m.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// Small glowing ball (no collider). Uses a plain white texture so the material color
        /// gives a solid, evenly lit orb.
        public GameObject MakeSphere(string name, Transform parent, float diameter)
        {
            GameObject s = new GameObject(name);
            s.transform.SetParent(parent, false);
            s.transform.localScale = Vector3.one * diameter;
            s.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
            MeshRenderer r = s.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (_shader != null && _sphereMesh != null)
            {
                Material m = new Material(_shader);
                m.mainTexture = Texture2D.whiteTexture;
                m.renderQueue = 3101;
                r.material = m;
            }
            else r.enabled = false;
            return s;
        }

        public GameObject MakeQuad(string name, Transform parent, float size)
        {
            GameObject q = new GameObject(name);
            q.transform.SetParent(parent, false);
            q.transform.localScale = Vector3.one * size;
            q.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
            MeshRenderer r = q.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (_shader != null)
            {
                Material m = new Material(_shader);
                m.mainTexture = _glowTex;
                m.renderQueue = 3100;
                r.material = m;
            }
            else r.enabled = false;
            return q;
        }

        private void PlayCritEffects(Vector3 pos, Color color)
        {
            if (DamageText.instance != null)
                DamageText.instance.ShowText(DamageText.TextType.Weak, pos + Vector3.up * 0.4f, "CRÍTICO!", true);

            GameObject burst = new GameObject("OreWeakSpots_Burst");
            burst.transform.position = pos;
            burst.AddComponent<CritBurst>().Init(this, color);

            PlayCritSound();
        }

        // ---------------------------------------------------------------- crit sound
        //
        // Done the way Better Archery plays its breathing sounds: an AudioSource attached to the
        // player, routed through the game's own audio mixer group (sound that bypasses Valheim's
        // mixer can end up silent), playing clips with PlayOneShot.

        private readonly List<AudioClip> _fileClips = new List<AudioClip>();
        private readonly List<AudioClip> _gameClips = new List<AudioClip>();
        private UnityEngine.Audio.AudioMixerGroup _mixerGroup;
        private bool _gameSoundSearched;
        private AudioSource _playerAudio;
        private Player _audioOwner;

        private void PlayCritSound()
        {
            float vol = Plugin.SoundVolume.Value;
            CritSoundOption mode = Plugin.CritSound.Value;
            if (vol <= 0f || mode == CritSoundOption.Nenhum) return;
            FindGameCritSound();

            List<AudioClip> pool = null;
            if (mode == CritSoundOption.Automatico)
                pool = _fileClips.Count > 0 ? _fileClips : (_gameClips.Count > 0 ? _gameClips : null);
            else if (mode == CritSoundOption.Arquivo) pool = _fileClips.Count > 0 ? _fileClips : null;
            else if (mode == CritSoundOption.Jogo) pool = _gameClips.Count > 0 ? _gameClips : null;

            AudioClip clip = pool != null ? pool[Random.Range(0, pool.Count)] : _critClip;
            AudioSource src = GetPlayerAudio();
            if (src == null || clip == null) return;
            src.pitch = Random.Range(0.95f, 1.05f);
            src.PlayOneShot(clip, vol);
            if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("Som de crítico: " + clip.name);
        }

        private AudioSource GetPlayerAudio()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            if (_playerAudio != null && _audioOwner == p) return _playerAudio;

            GameObject go = new GameObject("OreWeakSpots_SFX");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = Vector3.up * 1.5f;
            _playerAudio = go.AddComponent<AudioSource>();
            _playerAudio.playOnAwake = false;
            _playerAudio.loop = false;
            _playerAudio.spatialBlend = 0f;          // heard clearly, like a UI cue
            _playerAudio.outputAudioMixerGroup = _mixerGroup != null ? _mixerGroup : FindAnyMixerGroup(p);
            _audioOwner = p;
            return _playerAudio;
        }

        /// Any mixer group the game already uses on the player (footsteps, voice...).
        private static UnityEngine.Audio.AudioMixerGroup FindAnyMixerGroup(Player p)
        {
            AudioSource[] srcs = p.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < srcs.Length; i++)
                if (srcs[i].outputAudioMixerGroup != null) return srcs[i].outputAudioMixerGroup;
            return null;
        }

        /// Clips of Valheim's hit on a staggered (broken posture) enemy: Character.m_critHitEffects,
        /// prefab "fx_crit". Also takes its mixer group so our sound goes through the game's mixer.
        private void FindGameCritSound()
        {
            if (_gameSoundSearched || ZNetScene.instance == null) return;
            _gameSoundSearched = true;
            string[] creatures = { "Greydwarf", "Skeleton", "Draugr", "Troll", "Boar", "Deer" };
            for (int c = 0; c < creatures.Length && _gameClips.Count == 0; c++)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(creatures[c]);
                if (prefab == null) continue;
                Character ch = prefab.GetComponent<Character>();
                if (ch == null || ch.m_critHitEffects == null || ch.m_critHitEffects.m_effectPrefabs == null) continue;
                EffectList.EffectData[] list = ch.m_critHitEffects.m_effectPrefabs;
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].m_prefab != null) CollectClips(list[i].m_prefab);
                }
            }
            Plugin.Log.LogInfo("Som de postura quebrada do jogo: " + _gameClips.Count + " clipe(s)" +
                               (_mixerGroup != null ? ", canal " + _mixerGroup.name : ", sem canal de áudio"));
        }

        private void CollectClips(GameObject fx)
        {
            // ZSFX keeps its sounds in m_audioClips; read it by name so we don't depend on the class layout.
            Component[] comps = fx.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                Component comp = comps[i];
                if (comp == null) continue;
                System.Reflection.FieldInfo f = HarmonyLib.AccessTools.Field(comp.GetType(), "m_audioClips");
                if (f != null)
                {
                    AudioClip[] clips = f.GetValue(comp) as AudioClip[];
                    if (clips != null)
                        for (int k = 0; k < clips.Length; k++)
                            if (clips[k] != null && !_gameClips.Contains(clips[k])) _gameClips.Add(clips[k]);
                }
                AudioSource src = comp as AudioSource;
                if (src != null)
                {
                    if (src.clip != null && !_gameClips.Contains(src.clip)) _gameClips.Add(src.clip);
                    if (_mixerGroup == null && src.outputAudioMixerGroup != null) _mixerGroup = src.outputAudioMixerGroup;
                }
            }
        }

        /// Loads .wav/.ogg files from the mod's SFX folder (same idea as Better Archery's SFX folder).
        private System.Collections.IEnumerator LoadSoundFiles()
        {
            string dir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "SFX");
            if (!System.IO.Directory.Exists(dir))
            {
                try { System.IO.Directory.CreateDirectory(dir); } catch { }
                yield break;
            }
            string[] files = System.IO.Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = System.IO.Path.GetExtension(files[i]).ToLowerInvariant();
                AudioType type = ext == ".wav" ? AudioType.WAV : ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.UNKNOWN;
                if (type == AudioType.UNKNOWN) continue;
                using (UnityEngine.Networking.UnityWebRequest req =
                       UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file:///" + files[i].Replace('\\', '/'), type))
                {
                    yield return req.SendWebRequest();
                    AudioClip clip = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success
                        ? UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req) : null;
                    if (clip != null)
                    {
                        clip.name = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                        _fileClips.Add(clip);
                        Plugin.Log.LogInfo("Som carregado: " + System.IO.Path.GetFileName(files[i]));
                    }
                    else Plugin.Log.LogWarning("Não consegui carregar o som " + files[i] + ": " + req.error);
                }
            }
        }

        private static Texture2D MakeGlowTexture(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 center, 1 edge
                    float core = Mathf.Clamp01(1f - d * 3f);                          // hot center
                    float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);              // soft falloff
                    float a = Mathf.Clamp01(halo * 0.8f + core);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));   // tinted by the material color
                }
            t.Apply();
            return t;
        }

        /// Short "ting" made of two decaying sine tones, so no game assets are needed.
        private static AudioClip MakeCritClip()
        {
            int rate = 44100;
            int len = (int)(rate * 0.35f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 14f);
                float attack = Mathf.Clamp01(t * 400f);
                data[i] = attack * env * (0.55f * Mathf.Sin(2f * Mathf.PI * 1568f * t) +
                                          0.30f * Mathf.Sin(2f * Mathf.PI * 2349f * t) +
                                          0.15f * Mathf.Sin(2f * Mathf.PI * 3136f * t));
            }
            AudioClip clip = AudioClip.Create("OreWeakSpots_Crit", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// One weak spot: a small ball of light resting on the ore's surface, with a faint halo
    /// around it and a small point light. It fades in, pulses, fades out and reappears elsewhere.
    public class WeakSpot
    {
        private const float FadeIn = 0.5f;
        private const float FadeOut = 0.7f;

        private readonly WeakSpotManager _mgr;
        private readonly GameObject _root;    // sits exactly on the surface point
        private readonly Transform _orb;
        private readonly Transform _halo;
        private readonly Material _orbMat;
        private readonly Material _haloMat;
        private readonly Light _light;
        private readonly float _orbSize;
        private readonly float _haloSize;

        private Collider _surface;
        private Vector3 _normal = Vector3.up;
        private float _bornAt;        // when it becomes visible
        private float _lifetime;
        private bool _placed;
        private float _retryAt;

        public Color Color { get; private set; }
        public Vector3 Position { get { return _orb.position; } }
        public bool IsPlaced { get { return _placed; } }
        public bool IsHittable { get { return _placed && Time.time >= _bornAt; } }

        public WeakSpot(WeakSpotManager mgr)
        {
            _mgr = mgr;
            _root = new GameObject("OreWeakSpot");
            float size = Plugin.SpotSize.Value;
            _orbSize = 0.16f * size;
            _haloSize = 0.55f * size;

            GameObject orb = mgr.MakeSphere("Orb", _root.transform, _orbSize);
            GameObject halo = mgr.MakeQuad("Halo", _root.transform, _haloSize);
            _orb = orb.transform;
            _halo = halo.transform;
            _orbMat = orb.GetComponent<MeshRenderer>().material;
            _haloMat = halo.GetComponent<MeshRenderer>().material;

            _light = orb.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 1.3f;
            _light.shadows = LightShadows.None;

            _root.SetActive(false);
        }

        public void Relocate(float delay)
        {
            _placed = false;
            _root.SetActive(false);
            _retryAt = Time.time + delay;
        }

        private void TryPlace()
        {
            Vector3 pos, normal; Collider col;
            if (!_mgr.FindSurfacePoint(this, out pos, out normal, out col))
            {
                _retryAt = Time.time + 0.5f;
                return;
            }
            _root.transform.position = pos;
            _root.transform.rotation = Quaternion.identity;
            _normal = normal;
            _surface = col;
            Color = _mgr.CurrentColor();
            _lifetime = Plugin.SpotLifetime.Value * Random.Range(0.85f, 1.15f);
            _bornAt = Time.time;
            _placed = true;
            _root.SetActive(true);
        }

        public void Tick(Camera cam)
        {
            if (!_placed)
            {
                if (Time.time >= _retryAt) TryPlace();
                return;
            }

            // The part of the rock under the spot was broken off.
            if (_surface == null || !_surface.enabled || !_surface.gameObject.activeInHierarchy)
            {
                Relocate(0.2f);
                return;
            }

            float age = Time.time - _bornAt;
            if (age > _lifetime) { Relocate(0.4f); return; }

            float fadeIn = Mathf.SmoothStep(0f, 1f, age / FadeIn);
            float fadeOut = Mathf.SmoothStep(0f, 1f, (_lifetime - age) / FadeOut);
            float a = fadeIn * fadeOut;
            float pulse = 0.8f + 0.2f * Mathf.Sin(age * 6f);

            // Grows out of the rock while fading in, shrinks back while fading out.
            float grow = Mathf.Lerp(0.3f, 1f, a);
            float orb = _orbSize * grow * (0.95f + 0.05f * pulse);
            // Ball rests on the surface, a little sunk in so it looks attached to the rock.
            _orb.localPosition = _normal * (orb * 0.35f);
            _orb.localScale = Vector3.one * orb;
            _halo.localPosition = _orb.localPosition;
            _halo.localScale = Vector3.one * (_haloSize * grow * pulse);

            Color c = Color;
            _orbMat.color = new Color(Mathf.Lerp(c.r, 1f, 0.45f), Mathf.Lerp(c.g, 1f, 0.45f), Mathf.Lerp(c.b, 1f, 0.45f), a);
            _haloMat.color = new Color(c.r, c.g, c.b, 0.55f * a * pulse);
            _light.color = c;
            _light.intensity = 1.8f * a * pulse;

            if (cam != null) _halo.rotation = cam.transform.rotation;
        }

        public void Destroy()
        {
            Object.Destroy(_root);
            Object.Destroy(_orbMat);
            Object.Destroy(_haloMat);
        }
    }

    /// Flash shown on a critical hit: an expanding ring of light that fades out.
    public class CritBurst : MonoBehaviour
    {
        private const float Duration = 0.4f;
        private float _start;
        private Material _mat;
        private Light _light;
        private Color _color;
        private Transform _quad;

        public void Init(WeakSpotManager mgr, Color color)
        {
            _start = Time.time;
            _color = color;
            GameObject q = mgr.MakeQuad("Flash", transform, 0.4f);
            _quad = q.transform;
            _mat = q.GetComponent<MeshRenderer>().material;
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 3f;
            _light.color = color;
            _light.shadows = LightShadows.None;
        }

        private void Update()
        {
            float t = (Time.time - _start) / Duration;
            if (t >= 1f)
            {
                Object.Destroy(_mat);
                Object.Destroy(gameObject);
                return;
            }
            float a = 1f - t;
            _quad.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, t);
            _mat.color = new Color(Mathf.Lerp(_color.r, 1f, 0.4f), Mathf.Lerp(_color.g, 1f, 0.4f), Mathf.Lerp(_color.b, 1f, 0.4f), a);
            _light.intensity = 5f * a;
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
