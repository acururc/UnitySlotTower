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
            var root = (RectTransform)cabinet.transform.parent;
            BuildBattleHud(root);
            // T359c: 筐体の外の名前、T359d: ルート、T359e: ショップ・回復・リザルト、T359f: 早見表。
        }

        // --- T359b 戦闘の HUD ---

        // 操作バーの位置（Portrait Root の割合）。バーの子の割合はこれで換算する。
        private const float BarBottom = 0.32f;
        private const float BarTop = 0.40f;

        // 文字あり版の状態の帯（Portrait Root の割合）。戦闘フィールドの上の空き（Battle Camera の上端）に重ねる。
        private const float BandBottom = 1752f / 1920f;
        private const float BandTop = 1912f / 1920f;

        // 帯の中の段（帯の割合）。上が名前、下が数字・ボタンの絵。
        private const float NameBottom = 0.56f;
        private const float NameTop = 0.98f;
        private const float ValueBottom = 0.04f;
        private const float ValueTop = 0.54f;

        private const float BandFont = 40f;
        private const float ButtonFont = 36f;
        private const float CardFont = 40f;

        /// <summary>
        /// 操作バーを味方4種の札にし、ライフ・所持金・ウェーブ・倍率・▶・撤退・一時停止を戦闘フィールドの上端の帯へ移す。
        /// 説明の文を大きくする（plan 16.13）。
        /// </summary>
        private static void BuildBattleHud(RectTransform root)
        {
            var bar = Child(root, "Operation Bar");
            var barHeight = bar.rect.height;
            for (var i = 0; i < 4; i++)
            {
                BuildUnitCard(bar, i, barHeight);
            }

            BuildStatusBand(bar);
            BuildRates(bar);
            BuildGuideCaption(root);
        }

        // 札: 横 345px（基準解像度）。上の段の左に味方アイコン（コスト・ストックの数字は中のまま）、右に強化、下の段に職名、右端にスキルゲージ。
        // 職名を下にするのは、説明の文がアイコンのすぐ上に出るため（GuideOverlay.PlaceCaption）。
        private static void BuildUnitCard(RectTransform bar, int slot, float barHeight)
        {
            const float width = 1440f;
            var left = 8f + slot * 358f;
            const float nameBottom = 0.03f;
            const float nameTop = 0.35f;
            const float iconBottom = 0.38f;
            const float iconTop = 0.97f;

            var icon = Child(bar, $"Unit Icon {slot + 1}");
            SetRect(icon, new Vector2(left / width, iconBottom), new Vector2((left + 110f) / width, iconTop));
            // 職名はアイコンの子。アイコンの下の段に札の幅（ゲージの手前まで）で出す。
            var name = FindLabel(icon, "unit_");
            SetLabel(name, Vector2.zero, Vector2.zero,
                new Vector2(0f, (nameBottom - iconBottom) * barHeight), new Vector2(292f, (nameTop - iconBottom) * barHeight), CardFont);

            var upgrade = Child(bar, $"Upgrade Icon {slot + 1}");
            SetRect(upgrade, new Vector2((left + 116f) / width, iconBottom), new Vector2((left + 296f) / width, iconTop));
            SetLabel(Child(upgrade, "Text Label bar_upgrade"), Vector2.zero, new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero,
                ButtonFont);
            SetRect(Child(upgrade, "Arrow"), new Vector2(0.48f, 0.3f), new Vector2(0.62f, 0.7f));
            SetRect(Child(upgrade, "Cost"), new Vector2(0.64f, 0.4f), new Vector2(0.98f, 0.98f));
            for (var level = 0; level < 5; level++)
            {
                var x = 0.645f + level * 0.068f;
                SetRect(Child(upgrade, $"Level {level + 1}"), new Vector2(x, 0.1f), new Vector2(x + 0.054f, 0.3f));
            }

            SetRect(Child(bar, $"Skill Gauge {slot + 1}"), new Vector2((left + 302f) / width, nameBottom),
                new Vector2((left + 340f) / width, iconTop));
        }

        // 帯: ライフ・所持金・ウェーブ（左）、倍率（中、BuildRates）、▶・撤退・一時停止（右）。
        private static void BuildStatusBand(RectTransform bar)
        {
            var status = Child(bar, "Status");
            SetRect(status, new Vector2(0f, BarY(BandBottom)), new Vector2(1f, BarY(BandTop)));
            var bandHeight = (BandTop - BandBottom) * 1920f;

            NamedIcon(Child(status, "Life Icon"), Child(status, "Life"), "life", 0.012f, 0.13f, bandHeight);
            NamedIcon(Child(status, "Coin Icon"), Child(status, "Coins"), "money", 0.142f, 0.27f, bandHeight);

            var wave = Child(status, "Wave");
            SetRect(wave, new Vector2(0.28f, ValueBottom), new Vector2(0.40f, ValueTop));
            SetLabel(Child(wave, "Text Label bar_wave"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);

            BandButton(Child(status, "Start Button"), "bar_start", 0.63f, 0.74f);
            SetRect(Child(Child(status, "Start Button"), "Play"), new Vector2(0.38f, 0.46f), new Vector2(0.62f, 0.92f));
            var retreat = Child(status, "Retreat Button");
            BandButton(retreat, "bar_retreat", 0.75f, 0.86f);
            SetRect(Child(retreat, "Stroke 1"), new Vector2(0.45f, 0.44f), new Vector2(0.55f, 0.94f));
            SetRect(Child(retreat, "Stroke 2"), new Vector2(0.45f, 0.44f), new Vector2(0.55f, 0.94f));
            var pause = Child(status, "Pause Button");
            BandButton(pause, "bar_pause", 0.87f, 0.99f);
            SetRect(Child(pause, "Bar Left"), new Vector2(0.38f, 0.46f), new Vector2(0.46f, 0.92f));
            SetRect(Child(pause, "Bar Right"), new Vector2(0.54f, 0.46f), new Vector2(0.62f, 0.92f));
        }

        // 倍率は帯の中央に横並び（左が攻撃力、右が獲得金）。名前は数字の上。
        private static void BuildRates(RectTransform bar)
        {
            var rates = Child(bar, "Rates");
            SetRect(rates, new Vector2(0.41f, BarY(BandBottom)), new Vector2(0.62f, BarY(BandTop)));
            var bandHeight = (BandTop - BandBottom) * 1920f;
            var attack = Child(rates, "Attack Rate");
            SetRect(attack, new Vector2(0f, ValueBottom), new Vector2(0.49f, ValueTop));
            SetLabel(Child(attack, "Text Label bar_attack_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);
            var money = Child(rates, "Money Rate");
            SetRect(money, new Vector2(0.51f, ValueBottom), new Vector2(1f, ValueTop));
            SetLabel(Child(money, "Text Label bar_money_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);
        }

        // 説明の文（GuideOverlay が毎フレーム位置を決め、高さは sizeDelta.y を使う）。高さと字の大きさだけを変える。
        private static void BuildGuideCaption(RectTransform root)
        {
            var guide = root.GetComponentInChildren<GuideOverlay>(true);
            var caption = (TextModeLabel)new SerializedObject(guide).FindProperty("caption").objectReferenceValue;
            var rect = (RectTransform)caption.transform;
            Set(rect.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.offsetMax = s.offsetMin + new Vector2(1200f, 124f);
                return s;
            });
            Set(caption.Text.gameObject, TextModeLayout.Parts.Font, s =>
            {
                s.autoSize = true;
                s.fontSizeMin = MinFontSize;
                s.fontSizeMax = 48f;
                s.fontSize = 48f;
                return s;
            });
        }

        // 帯の1項目: 左に絵、右に数字、上の段に名前（名前は絵の子なので絵の左上を基準に置く）。
        private static void NamedIcon(RectTransform icon, RectTransform value, string id, float left, float right, float bandHeight)
        {
            const float iconWidth = 0.034f;
            const float iconBottom = 0.10f;
            const float iconTop = 0.48f;
            SetRect(icon, new Vector2(left, iconBottom), new Vector2(left + iconWidth, iconTop));
            SetRect(value, new Vector2(left + iconWidth + 0.002f, ValueBottom), new Vector2(right, ValueTop));
            SetLabel(Child(icon, "Text Label " + id), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, (NameBottom - iconTop) * bandHeight), new Vector2((right - left) * 1440f, (NameTop - iconTop) * bandHeight),
                BandFont);
        }

        // 帯のボタン: 帯の高さいっぱい、下に名前、絵は上に寄せる（絵は呼び出し側）。
        private static void BandButton(RectTransform button, string id, float left, float right)
        {
            SetRect(button, new Vector2(left, ValueBottom), new Vector2(right, NameTop));
            SetLabel(Child(button, "Text Label " + id), Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, 60f),
                ButtonFont);
        }

        // 操作バーの子に、Portrait Root の高さの割合を換算する。
        private static float BarY(float rootY) => (rootY - BarBottom) / (BarTop - BarBottom);

        private static void SetRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax) =>
            SetRect(target, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        private static void SetRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) =>
            Set(target.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin = anchorMin;
                s.anchorMax = anchorMax;
                s.offsetMin = offsetMin;
                s.offsetMax = offsetMax;
                return s;
            });

        /// <summary>ラベル（TextModeSetup が作る。下地と子の Text）の位置と字の大きさ。</summary>
        private static void SetLabel(RectTransform label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            float fontSize)
        {
            SetRect(label, anchorMin, anchorMax, offsetMin, offsetMax);
            Set(label.GetComponent<TextModeLabel>().Text.gameObject, TextModeLayout.Parts.Font, s =>
            {
                s.autoSize = false;
                s.fontSize = fontSize;
                return s;
            });
        }

        private static RectTransform FindLabel(RectTransform parent, string idPrefix)
        {
            foreach (Transform child in parent)
            {
                var label = child.GetComponent<TextModeLabel>();
                if (label != null && label.Id.StartsWith(idPrefix, StringComparison.Ordinal))
                {
                    return (RectTransform)child;
                }
            }

            throw new InvalidOperationException($"{parent.name} に {idPrefix} のラベルがない（先に Add Text Mode Labels (T352)）");
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                throw new InvalidOperationException($"{parent.name} に {name} がない");
            }

            return child;
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
