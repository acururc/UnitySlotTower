using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;
using Object = UnityEngine.Object;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 文字あり版の作り直し（T359、FR-006、D-76、plan 16.13）。既存の部品に <see cref="TextModeLayout"/> を付け、文字あり版の配置と見た目を置く。
    /// シーンの値（文字なし版）は書き換えない。再実行しても同じ結果になる（文字なし版の値は、部品が既にあればその値、なければシーンの値）。
    /// 画面ごとの中身は T359b〜T359f で足す。
    /// </summary>
    internal static class TextModeLayoutSetup
    {
        /// <summary>文字あり版の文字の最小の大きさ（基準解像度 1440×1920、plan 16.13）。</summary>
        public const float MinFontSize = 36f;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Text Mode Layout (T359)")]
        private static void Build()
        {
            var title = Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            if (title != null)
            {
                BuildTitle(title);
                Save(title.gameObject);
            }
            else if (cabinet != null)
            {
                BuildGame(cabinet);
                Save(cabinet.gameObject);
            }
            else
            {
                Debug.LogError("[SlotTower Setup] Title か Game シーンを開いてから実行する。");
            }
        }

        private static void BuildTitle(TitleScreen title)
        {
            // T359e: 難易度選択の札。
        }

        private static void BuildGame(SlotCabinet cabinet)
        {
            // T359b: 戦闘の HUD、T359c: 筐体の外の名前、T359d: ルート、T359e: ショップ・回復・リザルト、T359f: 早見表。
        }

        /// <summary>
        /// 部品に文字あり版の値を置く。<paramref name="edit"/> は文字なし版の値の写しを受け取り、文字あり版に変える部分だけを書き換える。
        /// </summary>
        internal static TextModeLayout Set(GameObject target, TextModeLayout.Parts parts, Func<TextModeLayout.State, TextModeLayout.State> edit)
        {
            var layout = target.GetComponent<TextModeLayout>();
            var noText = layout != null ? layout.NoText : TextModeLayout.Read(target);
            if (layout == null)
            {
                layout = Undo.AddComponent<TextModeLayout>(target);
            }

            if ((parts & TextModeLayout.Parts.Visibility) != 0 && target.GetComponent<CanvasGroup>() == null)
            {
                Undo.AddComponent<CanvasGroup>(target);
            }

            Undo.RecordObject(layout, "Text Mode Layout");
            layout.Configure(parts, noText, edit(noText));
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static void Save(GameObject inScene)
        {
            var scene = inScene.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] {scene.name} に文字あり版の配置を置いた。");
        }
    }
}
