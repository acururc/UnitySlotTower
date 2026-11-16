using TMPro;
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
    /// T360（FR-007、plan 17.1）: 開いている Game シーンの所持金のアイコンを、金色の円からお札（<see cref="MoneyIconArt"/>）に替える。
    /// 操作バーは所持金の数字の隣の「Coin Icon」（名前は以前のまま）、ルート・ショップ・リザルトは画面内の「Money Icon」すべて。
    /// スロット台のコインのアイコンは触らない。何度実行しても同じ結果になる。
    /// </summary>
    internal static class YenMoneySetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Apply Yen Money Icons (T360)")]
        private static void Apply()
        {
            var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var count = 0;

            foreach (var status in Object.FindObjectsByType<OperationBarStatus>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var money = (TMP_Text)new SerializedObject(status).FindProperty("moneyText").objectReferenceValue;
                var icon = money != null ? money.transform.parent.Find("Coin Icon") : null;
                count += ApplyTo(icon, box, knob);
            }

            count += ApplyUnder<RouteScreen>(box, knob);
            count += ApplyUnder<ShopScreen>(box, knob);
            count += ApplyUnder<ResultScreen>(box, knob);

            if (count == 0)
            {
                Debug.LogError("[SlotTower Setup] 所持金のアイコンが見つからない。Game シーンを開いて実行する。");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 所持金のアイコン {count} 個をお札にした（T360）。");
        }

        private static int ApplyUnder<T>(Sprite box, Sprite knob) where T : Component
        {
            var count = 0;
            foreach (var screen in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var rect in screen.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "Money Icon")
                    {
                        count += ApplyTo(rect, box, knob);
                    }
                }
            }

            return count;
        }

        private static int ApplyTo(Transform icon, Sprite box, Sprite knob)
        {
            var image = icon != null ? icon.GetComponent<Image>() : null;
            if (image == null)
            {
                return 0;
            }

            MoneyIconArt.Apply(image, box, knob);
            EditorUtility.SetDirty(icon.gameObject);
            return 1;
        }
    }
}
