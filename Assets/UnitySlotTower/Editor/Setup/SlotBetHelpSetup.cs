using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 011 FR-022: 早見表のスロットのページの下に、賭け枚数と有効ラインの説明（文字なし）を足す。
    /// 1〜3枚それぞれに「コインの絵 n 枚 ▶ 3段の窓で有効な段だけ明るい」を並べる。何度実行しても同じ結果（前の部品は消す）。位置は仮置き。
    /// </summary>
    internal static class SlotBetHelpSetup
    {
        internal const string BetRowsName = "Bet Rows";
        private static readonly Color CoinColor = new Color(1f, 0.82f, 0.2f);
        private static readonly Color LineOn = new Color(1f, 0.85f, 0.2f);
        private static readonly Color LineOff = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color WindowColor = new Color(0.15f, 0.14f, 0.2f);
        private static readonly Color ArrowColor = new Color(0.6f, 0.6f, 0.65f);

        [MenuItem("Tools/UnitySlotTower/Setup/Update Help Bet (spec 011)")]
        private static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            var portrait = GameObject.Find("Portrait Root");
            if (Application.isPlaying || scene.name != GameSession.GameScene || portrait == null || portrait.scene != scene)
            {
                Debug.LogError("文字なしの保存状態で Game シーンを開いてください。");
                return;
            }

            var help = portrait.GetComponentInChildren<HelpScreen>(true);
            var panel = help != null ? new SerializedObject(help).FindProperty("panel").objectReferenceValue as GameObject : null;
            var slotPage = panel != null ? panel.transform.Find("Slot Page") as RectTransform : null;
            if (slotPage == null)
            {
                Debug.LogError("早見表の Slot Page がありません。");
                return;
            }

            var old = slotPage.Find(BetRowsName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var area = Rect(BetRowsName, slotPage, new Vector2(0.03f, 0.07f), new Vector2(0.97f, 0.19f));
            for (var bet = 1; bet <= SlotMachine.MaxBet; bet++)
            {
                var left = (bet - 1) / 3f;
                var group = Rect($"Bet {bet}", area, new Vector2(left + 0.01f, 0f), new Vector2(left + 1f / 3f - 0.01f, 1f));

                // コインの絵 bet 枚（縦に積む）。
                var coins = Rect("Coins", group, new Vector2(0f, 0.05f), new Vector2(0.28f, 0.95f));
                for (var c = 0; c < bet; c++)
                {
                    var bottom = c / 3f;
                    Img("Coin", coins, new Vector2(0f, bottom), new Vector2(1f, bottom + 1f / 3f), CoinColor, knob, true);
                }

                var arrow = Rect("Arrow", group, new Vector2(0.32f, 0.35f), new Vector2(0.42f, 0.65f));
                arrow.gameObject.AddComponent<TriangleGraphic>().color = ArrowColor;

                // 3段の窓: 有効な段（1枚=中段、2枚=＋上段、3枚=＋下段）だけ明るい。
                var window = Img("Window", group, new Vector2(0.46f, 0.02f), new Vector2(0.98f, 0.98f), WindowColor, box, false);
                window.type = UnityEngine.UI.Image.Type.Sliced;
                for (var r = 0; r < SlotMachine.RowCount; r++)
                {
                    var row = r - 1;
                    var top = 1f - r / 3f;
                    Img($"Line {row}", window.rectTransform, new Vector2(0.08f, top - 1f / 3f + 0.08f), new Vector2(0.92f, top - 0.08f),
                        SlotMachine.IsLineActive(row, bet) ? LineOn : LineOff, box, false);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 早見表に賭け枚数とラインの説明を足した（spec 011）。");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite,
            bool preserveAspect)
        {
            var image = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }
    }
}
