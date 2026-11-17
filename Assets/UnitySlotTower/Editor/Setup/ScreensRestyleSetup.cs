using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 戦闘以外の画面を戦闘画面と同じ土台（UiStyle・角を切ったパネル）にそろえる（T387、FR-070、plan 18.4）。
    /// 位置と大きさは変えずに地・縁・色だけを替えるので、文字あり版の配置（TextModeLayout）はそのまま使える。
    /// 開いているシーン（Title か Game）の画面を替えて保存する。何度実行してもよい。
    /// </summary>
    internal static class ScreensRestyleSetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Restyle Screens (T387)")]
        private static void Restyle()
        {
            var count = 0;
            var title = Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            if (title != null)
            {
                count += RestyleTitle(title);
            }

            var root = GameObject.Find("Portrait Root")?.transform;
            if (root != null)
            {
                count += RestyleRoute(root.Find("Route Screen"));
                count += RestyleShop(root.Find("Shop Screen"));
                count += RestyleRest(root.Find("Rest Screen"));
                count += RestyleHelp(root.Find("Help"));
                count += RestyleResult(root.Find("Result"));
            }

            if (count == 0)
            {
                Debug.LogError("[SlotTower Setup] Title か Game シーンを開いてから実行する（替える画面が見つからない）。");
                return;
            }

            var scene = title != null ? title.gameObject.scene : root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] {scene.name} の画面の見た目を替えた（{count} 部品）。");
        }

        // --- 画面ごと ---

        private static int RestyleTitle(TitleScreen title)
        {
            var count = Background(GameObject.Find("Backdrop")?.transform);
            var serialized = new SerializedObject(title);
            count += Primary(Ref<Button>(serialized, "startButton"));
            count += Back(Ref<Button>(serialized, "backButton"));
            var plates = serialized.FindProperty("settingButtons");
            for (var i = 0; plates != null && i < plates.arraySize; i++)
            {
                // 設定の札は色で見分けるので、今の色のまま角を切って縁を付ける。
                var plate = plates.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                count += Skin(plate, null, UiStyle.PanelBorder);
            }

            return count;
        }

        private static int RestyleRoute(Transform screen)
        {
            if (screen == null)
            {
                return 0;
            }

            var panel = screen.Find("Panel");
            var count = Background(panel);
            count += Skin(panel?.Find("Status"), UiStyle.BandFill, UiStyle.PanelBorder);
            for (var i = 1; i <= 3; i++)
            {
                count += Skin(panel?.Find($"Direction {i}"), UiStyle.ButtonFill, UiStyle.PanelBorder);
            }

            return count;
        }

        private static int RestyleShop(Transform screen)
        {
            if (screen == null)
            {
                return 0;
            }

            var panel = screen.Find("Panel");
            var count = Background(panel);
            count += Skin(panel?.Find("Status"), UiStyle.BandFill, UiStyle.PanelBorder);
            var exchange = panel?.Find("Exchange");
            count += Skin(exchange, UiStyle.SectionFill, UiStyle.PanelBorder);
            count += Skin(exchange?.Find("Buy Coins"), UiStyle.ButtonFill, UiStyle.PanelBorder);
            count += Skin(exchange?.Find("Sell Coins"), UiStyle.ButtonFill, UiStyle.PanelBorder);
            count += Back(panel?.Find("Back"));
            return count;
        }

        private static int RestyleRest(Transform screen)
        {
            if (screen == null)
            {
                return 0;
            }

            var panel = screen.Find("Panel");
            return Background(panel) + Back(panel?.Find("Back"));
        }

        private static int RestyleHelp(Transform screen)
        {
            if (screen == null)
            {
                return 0;
            }

            var panel = screen.Find("Panel");
            var count = Background(panel);
            count += Back(panel?.Find("Close Button"));
            // 戦闘の帯の「?」は味方の色。
            count += Skin(screen.Find("Open Button"), UiStyle.Ally, UiStyle.PanelBorder);
            return count;
        }

        private static int RestyleResult(Transform screen)
        {
            if (screen == null)
            {
                return 0;
            }

            var panel = screen.Find("Panel");
            var count = Background(panel);
            count += Primary(panel?.Find("Retry Button"));
            count += Back(panel?.Find("Title Button"));
            return count;
        }

        // --- 部品の種類 ---

        private static int Background(Transform target)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null)
            {
                return 0;
            }

            image.color = UiStyle.ScreenFill;
            EditorUtility.SetDirty(image);
            return 1;
        }

        /// <summary>進む・始めるボタン: 黄色の地に暗い ▶。</summary>
        private static int Primary(Component target)
        {
            if (Skin(target, UiStyle.Ready, UiStyle.PanelBorder) == 0)
            {
                return 0;
            }

            foreach (var triangle in target.GetComponentsInChildren<TriangleGraphic>(true))
            {
                triangle.color = UiStyle.OnReady;
                EditorUtility.SetDirty(triangle);
            }

            return 1;
        }

        /// <summary>戻る・閉じるボタン: 暗い地に白い ◀（戦闘の帯の隅のボタンと同じ）。</summary>
        private static int Back(Component target) => Skin(target, UiStyle.ButtonFill, UiStyle.PanelBorder);

        /// <summary>fill が null なら今の色（2回目以降は Skin の色）のまま。</summary>
        private static int Skin(Component target, Color? fill, Color border)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null)
            {
                if (target != null)
                {
                    Debug.LogWarning($"[SlotTower Setup] {target.name} に Image がない。");
                }

                return 0;
            }

            var existing = image.transform.Find(UiSkin.SkinName)?.GetComponent<UiPanel>();
            var color = fill ?? (existing != null ? existing.color : image.color);
            var panel = UiSkin.Apply(image, color, border);
            if (panel.GetComponent<CanvasRenderer>() == null)
            {
                panel.gameObject.AddComponent<CanvasRenderer>();
            }

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(panel);
            if (image.TryGetComponent<Button>(out var button))
            {
                EditorUtility.SetDirty(button);
            }

            return 1;
        }

        private static T Ref<T>(SerializedObject serialized, string name) where T : Object =>
            serialized.FindProperty(name)?.objectReferenceValue as T;
    }
}
