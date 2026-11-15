using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Tests.EditMode.Data;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.EditMode.Ui
{
    [Description("T352 (FR-006, plan 16.9): 文字あり版のフォントと、シーンに置いたラベルの決まり")]
    public sealed class TextModeAssetTests
    {
        private const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/TextMode SDF.asset";
        private const string TablePath = TablePaths.Root + "/" + UiTexts.FileName;
        private static readonly string[] Scenes = { "Assets/UnitySlotTower/Scenes/Title.unity", "Assets/UnitySlotTower/Scenes/Game.unity" };

        // 文言を実行中に入れるラベル（id が空でよい）。
        private static readonly string[] DynamicLabels = { "Text Label direction", "Text Label Caption", "Text Label Offer Template" };

        [Test]
        public void Font_Has_Every_Letter_Of_The_Table_In_One_Saved_Static_Atlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);
            var texts = UiTexts.Parse(File.ReadAllText(TablePath));

            var missing = new string(texts.Characters().Where(c => c != ' ' && !font.HasCharacter(c)).ToArray());

            Assert.That((missing, font.atlasPopulationMode, font.atlasTextures.Length, AssetDatabase.IsSubAsset(font.atlasTextures[0])),
                Is.EqualTo(("", AtlasPopulationMode.Static, 1, true)), "字が足りなければ Setup/Create Text Mode Font (T352) を再実行する");
        }

        [Test]
        public void Labels_In_Scenes_Use_Known_Ids_The_Font_And_Start_Hidden_Without_Catching_Clicks()
        {
            var texts = UiTexts.Parse(File.ReadAllText(TablePath));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var problems = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var path in Scenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                var wasLoaded = scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    var count = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var label in root.GetComponentsInChildren<TextModeLabel>(true))
                        {
                            count++;
                            Check(label, texts, font, problems);
                        }
                    }

                    counts[path] = count;
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That((counts[Scenes[0]] >= 3, counts[Scenes[1]] >= 40), Is.EqualTo((true, true)),
                $"ラベルが足りない（Setup/Add Text Mode Labels (T352) を各シーンで実行する）: {string.Join(", ", counts)}");
        }

        [Test]
        [Description("T359 (plan 16.13): 文字あり版の配置の部品は、文字なし版の値としてシーンの値そのものを持つ（文字なし版の見た目が変わらない）")]
        public void Layouts_In_Scenes_Keep_The_Scene_Values_As_The_No_Text_State()
        {
            var problems = new List<string>();
            foreach (var path in Scenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                var wasLoaded = scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var layout in root.GetComponentsInChildren<TextModeLayout>(true))
                        {
                            CheckLayout(layout, problems);
                        }
                    }
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static void CheckLayout(TextModeLayout layout, List<string> problems)
        {
            var name = layout.name;
            var scene = TextModeLayout.Read(layout.gameObject);
            var saved = layout.NoText;
            var parts = layout.Affected;
            if (parts == TextModeLayout.Parts.None)
            {
                problems.Add($"{name}: 差し替える部分がない");
            }

            if ((parts & TextModeLayout.Parts.Rect) != 0 && (scene.anchorMin != saved.anchorMin || scene.anchorMax != saved.anchorMax
                    || scene.offsetMin != saved.offsetMin || scene.offsetMax != saved.offsetMax || scene.pivot != saved.pivot))
            {
                problems.Add($"{name}: 配置がシーンの値と違う");
            }

            if ((parts & TextModeLayout.Parts.Font) != 0 && (layout.GetComponent<TMP_Text>() == null || !Mathf.Approximately(scene.fontSize, saved.fontSize)
                    || scene.autoSize != saved.autoSize || !Mathf.Approximately(scene.fontSizeMin, saved.fontSizeMin)
                    || !Mathf.Approximately(scene.fontSizeMax, saved.fontSizeMax)))
            {
                problems.Add($"{name}: 字の大きさがシーンの値と違うか、文字がない");
            }

            if ((parts & TextModeLayout.Parts.Color) != 0 && (layout.GetComponent<Graphic>() == null || scene.color != saved.color))
            {
                problems.Add($"{name}: 色がシーンの値と違うか、絵がない");
            }

            if ((parts & TextModeLayout.Parts.Visibility) != 0 && (layout.GetComponent<CanvasGroup>() == null || scene.hidden != saved.hidden))
            {
                problems.Add($"{name}: 表示の有無がシーンの値と違うか、CanvasGroup がない");
            }
        }

        private static void Check(TextModeLabel label, UiTexts texts, TMP_FontAsset font, List<string> problems)
        {
            var name = label.name;
            var serialized = new SerializedObject(label);
            if (serialized.FindProperty("table").objectReferenceValue == null)
            {
                problems.Add($"{name}: 表がない");
            }

            if (string.IsNullOrEmpty(label.Id))
            {
                if (!DynamicLabels.Contains(name))
                {
                    problems.Add($"{name}: id が空");
                }
            }
            else if (!texts.TryGet(label.Id, out _))
            {
                problems.Add($"{name}: id '{label.Id}' が表にない");
            }

            if (label.Text == null || label.Text.font != font)
            {
                problems.Add($"{name}: 文字がないか、フォントが文字あり版のものではない");
            }

            foreach (var graphic in label.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget || graphic.enabled)
                {
                    problems.Add($"{name}/{graphic.name}: クリックを受ける（{graphic.raycastTarget}）か、表示のまま保存されている（{graphic.enabled}）");
                }
            }
        }
    }
}
