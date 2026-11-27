using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 014: 音の取り込み設定、SoundCatalog の割り当て（下の表が正本）、Title と Game への配置。
    /// 何度実行しても同じ結果になる（前に置いたものは消して作り直す）。
    /// </summary>
    internal static class SoundSetup
    {
        private const string AudioFolder = "Assets/UnitySlotTower/Audio";
        private const string SeFolder = AudioFolder + "/Se";
        private const string BgmFolder = AudioFolder + "/Bgm";
        public const string CatalogPath = AudioFolder + "/SoundCatalog.asset";
        private const string TitleScenePath = "Assets/UnitySlotTower/Scenes/Title.unity";

        private const string AudioObject = "Game Audio";
        private const string ButtonSoundsObject = "UI Button Sounds";
        private const string BattleSoundObject = "Battle Sound";
        private const string VolumeObject = "Sound Volume";

        // 出来事 → ファイル（Se フォルダ）・音量・同時数の上限。名前だけで選んだので AC-003 で聞いて直す。
        private static readonly (SoundId id, string[] files, float volume, int maxVoices)[] Sounds =
        {
            (SoundId.Bet, new[] { "casino_chip-lay-1" }, 0.8f, 2),
            (SoundId.Lever, new[] { "rpg_metalLatch" }, 1f, 1),
            (SoundId.ReelSpin, new[] { "interface_scroll_002" }, 0.5f, 1),
            (SoundId.ReelStop, new[] { "impact_impactPlate_light_000" }, 0.8f, 3),
            (SoundId.LineHit, new[] { "interface_confirmation_002" }, 1f, 3),
            (SoundId.Miss, new[] { "interface_minimize_003" }, 0.6f, 1),
            (SoundId.Red7, new[] { "scifi_explosionCrunch_000" }, 1f, 2),
            (SoundId.Blue7, new[] { "impact_impactGlass_heavy_000" }, 1f, 2),
            (SoundId.Green7, new[] { "digital_powerUp3" }, 1f, 2),
            (SoundId.Yellow7, new[] { "scifi_slime_000" }, 1f, 2),
            (SoundId.Bar, new[] { "digital_phaserUp3" }, 1f, 2),
            (SoundId.CoinSymbol, new[] { "rpg_handleCoins" }, 1f, 2),
            (SoundId.Place, new[] { "impact_impactSoft_heavy_000" }, 1f, 2),
            (SoundId.SwordAttack, new[] { "rpg_knifeSlice", "rpg_knifeSlice2" }, 0.6f, 3),
            (SoundId.BowAttack, new[] { "interface_pluck_001", "interface_pluck_002" }, 0.6f, 3),
            (SoundId.Hit, new[] { "impact_impactPunch_medium_000", "impact_impactPunch_medium_001", "impact_impactPunch_medium_002" }, 0.5f, 4),
            (SoundId.EnemyDefeat, new[] { "impact_impactPunch_heavy_000", "impact_impactPunch_heavy_001", "impact_impactPunch_heavy_002" }, 0.7f, 3),
            (SoundId.BossDefeat, new[] { "scifi_lowFrequency_explosion_000" }, 1f, 1),
            (SoundId.BaseHit, new[] { "impact_impactMetal_heavy_000" }, 1f, 2),
            (SoundId.WaveStart, new[] { "jingles_jingles_HIT03" }, 0.8f, 1),
            (SoundId.BossAppear, new[] { "impact_impactBell_heavy_000" }, 1f, 1),
            (SoundId.Victory, new[] { "jingles_jingles_NES00" }, 1f, 1),
            (SoundId.Defeat, new[] { "jingles_jingles_NES13" }, 1f, 1),
            (SoundId.Button, new[] { "interface_click_002" }, 0.7f, 2),
            (SoundId.Reject, new[] { "interface_error_004" }, 0.8f, 1),
            (SoundId.CoinGet, new[] { "casino_chips-collide-1", "casino_chips-collide-2", "casino_chips-collide-3" }, 0.5f, 3),
        };

        private static readonly (BgmTrack track, string file, float volume)[] Bgms =
        {
            (BgmTrack.Title, "junkala_stage_select", 0.8f),
            (BgmTrack.Battle, "junkala_stage_1", 0.8f),
            (BgmTrack.Boss, "junkala_boss_fight", 0.8f),
        };

        [MenuItem("Tools/UnitySlotTower/Setup/Sound (spec 014)")]
        private static void Build()
        {
            ConfigureImporters();
            var catalog = BuildCatalog();
            if (catalog == null)
            {
                return;
            }

            if (!SetupTitle(catalog) || !SetupGame(catalog))
            {
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotTower Setup] 音: 効果音 {catalog.Sounds.Count} 件・BGM {catalog.Bgms.Count} 件を割り当て、Title と Game に配置した。");
        }

        private static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SeFolder, BgmFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    continue;
                }

                var bgm = path.StartsWith(BgmFolder + "/");
                var settings = importer.defaultSampleSettings;
                settings.loadType = bgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = bgm ? 0.6f : 0.8f;
                settings.preloadAudioData = !bgm;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !bgm;
                importer.loadInBackground = bgm;
                importer.SaveAndReimport();
            }
        }

        private static SoundCatalog BuildCatalog()
        {
            var missing = new List<string>();
            var sounds = new List<SoundCatalog.SoundEntry>();
            foreach (var (id, files, volume, maxVoices) in Sounds)
            {
                var clips = new List<AudioClip>();
                foreach (var file in files)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SeFolder}/{file}.ogg");
                    if (clip == null) missing.Add(file);
                    else clips.Add(clip);
                }

                sounds.Add(new SoundCatalog.SoundEntry { id = id, clips = clips.ToArray(), volume = volume, maxVoices = maxVoices });
            }

            var bgms = new List<SoundCatalog.BgmEntry>();
            foreach (var (track, file, volume) in Bgms)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{BgmFolder}/{file}.ogg");
                if (clip == null) missing.Add(file);
                bgms.Add(new SoundCatalog.BgmEntry { track = track, clip = clip, volume = volume });
            }

            if (missing.Count > 0)
            {
                Debug.LogError($"[SlotTower Setup] 音のファイルが見つからない: {string.Join(", ", missing)}");
                return null;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SoundCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(sounds, bgms);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static bool SetupTitle(SoundCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            var screen = Object.FindAnyObjectByType<TitleScreen>();
            if (screen == null || screen.StartButton == null)
            {
                Debug.LogError("[SlotTower Setup] Title シーンに TitleScreen がない。");
                return false;
            }

            PlaceAudio(catalog);
            var parent = screen.StartButton.transform.parent;
            RemoveChild(parent, VolumeObject);
            BuildVolumePanel(parent, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.22f), null, null, null);
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        private static bool SetupGame(SoundCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene(GameSceneSetup.ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("Portrait Root")?.transform as RectTransform;
            var battle = Object.FindAnyObjectByType<BattleController>();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            var actors = Object.FindAnyObjectByType<BattleActorsView>();
            var help = root != null ? root.GetComponentInChildren<HelpScreen>(true) : null;
            var quit = help != null ? help.GetComponent<QuitToTitle>() : null;
            if (root == null || battle == null || cabinet == null || actors == null || help == null || quit == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンに Portrait Root・BattleController・SlotCabinet・BattleActorsView・早見表・一時停止メニューが揃っていない。");
                return false;
            }

            PlaceAudio(catalog);
            DestroyRoot(BattleSoundObject);
            new GameObject(BattleSoundObject).AddComponent<BattleSound>().Bind(battle, cabinet, actors);

            RemoveChild(root, VolumeObject);
            var volume = BuildVolumePanel(root, new Vector2(0.15f, 0.66f), new Vector2(0.85f, 0.80f), battle, help, quit);
            // 確認の板・早見表より下に置く（それらが開いている間は消える）。
            volume.transform.SetSiblingIndex(help.transform.GetSiblingIndex());
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        private static void PlaceAudio(SoundCatalog catalog)
        {
            DestroyRoot(AudioObject);
            DestroyRoot(ButtonSoundsObject);
            new GameObject(AudioObject).AddComponent<GameAudio>().SetCatalog(catalog);
            new GameObject(ButtonSoundsObject).AddComponent<UiButtonSounds>();
        }

        // 板の上に、音符＋スライダー（BGM）とスピーカー＋スライダー（効果音）の2行。文字は使わない（D2）。
        private static SoundVolumePanel BuildVolumePanel(Transform parent, Vector2 min, Vector2 max,
            BattleController battle, HelpScreen help, QuitToTitle quit)
        {
            var holder = Rect(parent, VolumeObject, min, max);
            var board = Rect(holder, "Panel", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            board.color = UiStyle.ScreenFill;
            board.raycastTarget = true;
            UiSkin.Apply(board, UiStyle.ScreenFill, UiStyle.PanelBorder);

            var bgmRow = Rect(board.transform, "Bgm Row", new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.94f));
            NoteIcon(Rect(bgmRow, "Note Icon", new Vector2(0f, 0.1f), new Vector2(0.14f, 0.9f)));
            var bgm = CreateSlider(bgmRow);

            var seRow = Rect(board.transform, "Se Row", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.46f));
            SpeakerIcon(Rect(seRow, "Speaker Icon", new Vector2(0f, 0.1f), new Vector2(0.14f, 0.9f)));
            var se = CreateSlider(seRow);

            var panel = holder.gameObject.AddComponent<SoundVolumePanel>();
            var fields = new SerializedObject(panel);
            fields.FindProperty("panel").objectReferenceValue = board.gameObject;
            fields.FindProperty("bgmSlider").objectReferenceValue = bgm;
            fields.FindProperty("seSlider").objectReferenceValue = se;
            fields.FindProperty("battle").objectReferenceValue = battle;
            fields.FindProperty("help").objectReferenceValue = help;
            fields.FindProperty("quit").objectReferenceValue = quit;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (battle != null) board.gameObject.SetActive(false);
            return panel;
        }

        private static Slider CreateSlider(RectTransform row)
        {
            var area = Rect(row, "Slider", new Vector2(0.2f, 0.25f), new Vector2(1f, 0.75f));
            var background = area.gameObject.AddComponent<Image>();
            background.color = UiStyle.SectionFill;
            background.raycastTarget = true;

            var fillArea = Rect(area, "Fill Area", Vector2.zero, Vector2.one);
            var fill = Rect(fillArea, "Fill", Vector2.zero, new Vector2(0f, 1f)).gameObject.AddComponent<Image>();
            fill.color = UiStyle.Ready;
            fill.raycastTarget = false;

            var handleArea = Rect(area, "Handle Area", Vector2.zero, Vector2.one);
            var handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f));
            handle.offsetMin = new Vector2(-24f, -16f);
            handle.offsetMax = new Vector2(24f, 16f);
            var knob = handle.gameObject.AddComponent<Image>();
            knob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            knob.color = Color.white;

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static void NoteIcon(RectTransform icon)
        {
            var head = IconPart(icon, "Head", new Vector2(0.15f, 0f), new Vector2(0.6f, 0.4f));
            head.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            IconPart(icon, "Stem", new Vector2(0.5f, 0.2f), new Vector2(0.6f, 1f));
            IconPart(icon, "Flag", new Vector2(0.5f, 0.75f), new Vector2(0.85f, 1f));
        }

        private static void SpeakerIcon(RectTransform icon)
        {
            IconPart(icon, "Box", new Vector2(0.1f, 0.35f), new Vector2(0.35f, 0.65f));
            var cone = Rect(icon, "Cone", new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.9f)).gameObject.AddComponent<TriangleGraphic>();
            cone.Pointing = TriangleGraphic.Direction.Left;
            cone.raycastTarget = false;
            IconPart(icon, "Wave", new Vector2(0.78f, 0.3f), new Vector2(0.86f, 0.7f));
        }

        private static Image IconPart(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var image = Rect(parent, name, min, max).gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void DestroyRoot(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) Object.DestroyImmediate(go);
            }
        }
    }
}
