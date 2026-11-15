using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Ui
{
    /// <summary>
    /// Title 以外のシーンとプレハブの文字を走査し、数字・記号以外を使っている部品を見つける（FR-003、plan 8章）。
    /// </summary>
    public static class TextAudit
    {
        public const string SceneFolder = "Assets/UnitySlotTower/Scenes";
        public const string TitleScene = SceneFolder + "/Title.unity";
        public const string PrefabFolder = "Assets/UnitySlotTower";
        public const string NumericFontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";

        /// <summary>タイトル画面のロゴ（文字を使ってよい唯一の部品、plan 4章）。Title シーンの難易度選択などは走査する。</summary>
        public const string TitleLogoName = "Logo";

        /// <summary>
        /// root 以下の文字部品を走査する。requiredFont を渡すと、TextMeshPro がそのフォント以外を使っていても違反にする
        /// （数字専用フォントなら文字は描けない、plan 8章）。exemptName と同じ名前の部品と、文字あり版のラベルは飛ばす。
        /// </summary>
        public static List<string> FindViolations(GameObject root, string context, TMP_FontAsset requiredFont = null,
            string exemptName = null)
        {
            var violations = new List<string>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if ((exemptName != null && text.name == exemptName) || IsTextModeLabel(text.transform))
                {
                    continue;
                }

                Check(text.text, text.transform, context, violations);
                if (requiredFont != null && text.font != requiredFont)
                {
                    violations.Add($"{context} {PathOf(text.transform)}: フォント {(text.font != null ? text.font.name : "なし")} は数字専用ではない");
                }
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if ((exemptName != null && text.name == exemptName) || IsTextModeLabel(text.transform))
                {
                    continue;
                }

                Check(text.text, text.transform, context, violations);
            }

            return violations;
        }

        /// <summary>
        /// プロジェクト全体（Title シーンはロゴ以外）を走査し、文字とフォントを確かめる。開いていなかったシーンは閉じ、
        /// 開いていたシーンはそのまま残す。
        /// </summary>
        public static List<string> AuditProject()
        {
            var violations = new List<string>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumericFontPath);
            if (font == null)
            {
                violations.Add($"数字専用フォントがない: {NumericFontPath}");
                return violations;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var exempt = path == TitleScene ? TitleLogoName : null;

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
                        violations.AddRange(FindViolations(root, path, font, exempt));
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

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    violations.AddRange(FindViolations(prefab, path, font));
                }
            }

            return violations;
        }

        /// <summary>文字あり版のラベル（<see cref="TextModeLabel"/> とその子）か。文字なし版では描かれないので走査しない（plan 16.9）。</summary>
        public static bool IsTextModeLabel(Transform transform) => transform.GetComponentInParent<TextModeLabel>(true) != null;

        private static void Check(string text, Transform transform, string context, List<string> violations)
        {
            var index = NumericText.FindInvalid(text);
            if (index >= 0)
            {
                violations.Add($"{context} {PathOf(transform)}: '{text}' の {index + 1} 文字目 '{text[index]}' は使えない");
            }
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
