using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T611（spec 002 FR-113、plan 4章の上端の帯）: 上端の帯の倍率と速さのボタンの間に、青7の停止と黄7の毒のアイコンを置き、
    /// 攻撃力の印のまわりにバーの残り時間の円を足す。円は白い Filled（Radial360）で、残り時間の割合だけ出る（<see cref="OperationBarStatus"/>）。
    /// 何度実行しても同じ結果になる。続けて Build Text Mode Layout (T359) を実行する（文字あり版の位置）。
    /// </summary>
    internal static class SlotEffectBandSetup
    {
        internal const string EffectsName = "Slot Effects";
        private const string RingName = "Ring";
        private const float Width = 1440f;

        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color RingColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color FaceColor = new Color(0.08f, 0.09f, 0.12f, 1f);

        private static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        [MenuItem("Tools/UnitySlotTower/Setup/Build Slot Effect Band (T611)")]
        private static void Build()
        {
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var band = status != null ? status.transform.parent as RectTransform : null;
            var attackIcon = band != null ? band.Find("Rates/Attack Rate/Icon") as RectTransform : null;
            if (band == null || band.name != "Top Band" || attackIcon == null)
            {
                Debug.LogError("[SlotTower Setup] Top Band と Rates/Attack Rate/Icon が見つからない（先に Restyle Battle HUD (T383)）。");
                return;
            }

            var old = band.Find(EffectsName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // 倍率（〜980px）と速さのボタン（1188px〜）の間。
            var effects = CreateRect(EffectsName, band, new Vector2(992f / Width, 0.08f), new Vector2(1176f / Width, 0.92f));
            var stop = BuildEffect(effects, "Stop Effect", SlotSymbol.Blue7, 0f, 0.48f, out var stopRing);
            var poison = BuildEffect(effects, "Poison Effect", SlotSymbol.Yellow7, 0.52f, 1f, out var poisonRing);

            var oldRing = attackIcon.Find(RingName);
            if (oldRing != null)
            {
                Object.DestroyImmediate(oldRing.gameObject);
            }

            var attackRing = BuildDial(attackIcon, false);
            attackRing.transform.SetAsFirstSibling();

            var serialized = new SerializedObject(status);
            serialized.FindProperty("stopEffectGroup").objectReferenceValue = stop;
            serialized.FindProperty("stopEffectRing").objectReferenceValue = stopRing;
            serialized.FindProperty("poisonEffectGroup").objectReferenceValue = poison;
            serialized.FindProperty("poisonEffectRing").objectReferenceValue = poisonRing;
            serialized.FindProperty("attackRateRing").objectReferenceValue = attackRing;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(band.gameObject.scene);
            EditorSceneManager.SaveScene(band.gameObject.scene);
            Debug.Log("[SlotTower Setup] 上端の帯に効果のアイコンと残り時間の円を置いた（続けて Build Text Mode Layout (T359)）。");
        }

        // 効果1つ: 暗い丸の地に図柄、そのまわりに残り時間の円。発動していないときは CanvasGroup で薄くする。
        private static CanvasGroup BuildEffect(RectTransform parent, string name, SlotSymbol symbol, float left, float right,
            out Image ring)
        {
            var cell = CreateRect(name, parent, new Vector2(left, 0f), new Vector2(right, 1f));
            var group = cell.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.3f;
            ring = BuildDial(cell, true);
            var face = CreateImage("Face", ring.transform.parent as RectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f),
                FaceColor);
            face.sprite = Knob;
            var art = CreateRect("Symbol", face.rectTransform, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f));
            SlotSymbolArt.Create(symbol, art);
            return group;
        }

        // 正方形の枠（親に収まる最大）の中に、薄い円の地と残り時間の円を作り、円を返す。
        // withTrack が false のときは枠を作らず親の中にそのまま円を置く（攻撃力の印は親がすでに正方形の絵を持つ）。
        private static Image BuildDial(RectTransform parent, bool withTrack)
        {
            var dial = parent;
            if (withTrack)
            {
                dial = CreateRect("Dial", parent, Vector2.zero, Vector2.one);
                Square(dial);
                CreateImage("Track", dial, Vector2.zero, Vector2.one, TrackColor).sprite = Knob;
            }

            var ring = CreateImage(RingName, dial, Vector2.zero, Vector2.one, RingColor);
            if (!withTrack)
            {
                Square(ring.rectTransform);
            }

            ring.sprite = Knob;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            ring.enabled = false;
            return ring;
        }

        private static void Square(RectTransform rect)
        {
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
