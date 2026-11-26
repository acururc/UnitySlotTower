using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Core.Layout;
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
                // 難易度の選択をなくしたので、タイトルに文字あり版の配置はない（spec 002 E-18）。
                Debug.Log("[SlotTower Setup] Title には文字あり版の配置がない。");
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

        private static void BuildGame(SlotCabinet cabinet)
        {
            var root = (RectTransform)cabinet.transform.parent;
            BuildBattleHud(root);
            BuildCabinetLabels((RectTransform)cabinet.transform);
            BuildResult(root);
            BuildHelp(root);
        }

        // --- T359f 早見表 ---
        // ページごとの見出しと列名の高さを揃える（1440×1920 基準）。T614 で中身が 0.94 倍になり列名と重なったので見出しを 8 px 上げた。

        internal static readonly Vector2 HelpSlotHeadingY = new Vector2(1568f, 1638f);
        internal static readonly Vector2 HelpSlotColumnsY = new Vector2(1510f, 1560f);
        private const float HelpSlotRowHeight = 100f;
        internal static readonly Vector2 HelpUnitHeadingY = new Vector2(1568f, 1638f);
        internal static readonly Vector2 HelpUnitColumnsY = new Vector2(1510f, 1560f);
        private const float HelpUnitRowHeight = 330f;

        // 図柄の行（行の割合）: 図柄・確率・▶・効果（〜0.48）・説明（0.49〜）。
        internal const float HelpDescriptionLeft = 0.49f;

        // 味方の行（行の割合）: 左に本体の札と職名とスキルの印、範囲図、数値2列（各4段 50 px）、下の段にスキルの説明。
        internal const float HelpRangeLeft = 0.165f;
        internal const float HelpRangeRight = 0.335f;
        // 数値の列の左端（アイコン）と数字の左端。名前はその間。
        internal static readonly float[] HelpStatColumns = { 0.35f, 0.66f };
        private static readonly float[] HelpStatValueLefts = { 0.505f, 0.86f };
        private const float HelpStatIcon = 36f;

        /// <summary>数値の名前の幅（px）。アイコンの右から数字の左まで。</summary>
        internal static float HelpStatNameWidth(int column) =>
            (HelpStatValueLefts[column] - HelpStatColumns[column]) * 1440f - HelpStatIcon - 12f;

        /// <summary>
        /// 早見表: 表に見出しと列の名前を入れ、図柄の行は1行にして説明を右に、味方の行は数値に名前を付けて下にスキルの説明を入れる（plan 16.13）。
        /// </summary>
        private static void BuildHelp(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<HelpScreen>(true));
            // タブの絵は上半分（下に名前のラベル、TextModeSetup）。
            SetRect(Child(panel, "Page Tabs/Slot Tab/Icon"), new Vector2(0f, 0.52f), new Vector2(1f, 0.92f));
            SetRect(Child(panel, "Page Tabs/Unit Tab/Icon"), new Vector2(0f, 0.52f), new Vector2(1f, 0.92f));

            var slotRows = Child(Child(panel, "Slot Page"), HelpScreenSetup.SlotRowsName);
            for (var i = 0; i < slotRows.childCount; i++)
            {
                var top = HelpSlotColumnsY.x - i * HelpSlotRowHeight;
                var row = (RectTransform)slotRows.GetChild(i);
                SetRect(row, new Vector2(0f, (top - HelpSlotRowHeight) / 1920f), new Vector2(1f, top / 1920f));
                HelpSlotRow(row);
            }

            for (var i = 0; i < 4; i++)
            {
                var top = HelpUnitColumnsY.x - i * HelpUnitRowHeight;
                var row = Child(panel, $"Unit Page/Unit {i + 1}");
                SetRect(row, new Vector2(0f, (top - HelpUnitRowHeight) / 1920f), new Vector2(1f, top / 1920f));
                HelpUnitRow(row);
            }
        }

        // 図柄の行（100 px）の部品。名前は HelpScreenSetup.BuildSlot のもの。ない部品（ハズレの行など）は飛ばす。
        private static void HelpSlotRow(RectTransform row)
        {
            const float px = 1f / 1440f;
            var icon = row.Find("Icon") as RectTransform;
            if (icon != null)
            {
                // 札は行の高さの正方形（ハズレは少し小さい）。縦の割合はシーンの値のまま。
                var size = (icon.anchorMax.y - icon.anchorMin.y) * HelpSlotRowHeight * px;
                Set(icon.gameObject, TextModeLayout.Parts.Rect, s =>
                {
                    s.anchorMin.x = 0.047f - size / 2f;
                    s.anchorMax.x = 0.047f + size / 2f;
                    return s;
                });
            }

            SetXIfAny(row, "Chance", 0.08f, 0.175f);
            SetXIfAny(row, "Arrow", 0.185f, 0.2f);
            var effectIcon = 50f * px;
            SetXIfAny(row, HelpScreenSetup.EffectIconName, 0.215f, 0.215f + effectIcon);
            SetXIfAny(row, "Effect", 0.255f, 0.355f);
            SetXIfAny(row, HelpScreenSetup.CapIconName, 0.367f, 0.367f + effectIcon);
            SetXIfAny(row, "Cap", 0.407f, HelpDescriptionLeft - 0.005f);
        }

        // 味方の行（330 px）の部品。名前は HelpScreenSetup.BuildUnits のもの。
        private static void HelpUnitRow(RectTransform row)
        {
            const float h = HelpUnitRowHeight;
            // 本体の札 150 px の正方形（上）、その下に職名（TextModeSetup）。
            SetRect(Child(row, "Body"), new Vector2(0.035f, 159f / h), new Vector2(0.035f + 150f / 1440f, 309f / h));
            SetRect(Child(row, "Range"), new Vector2(HelpRangeLeft, 58f / h), new Vector2(HelpRangeRight, 309f / h));

            // 左の列は上から HP・攻撃・防御・攻撃間隔、右の列は ブロック・コスト・待ち時間（シーンと同じ並び）。段は下の 60 px より上に 65 px ずつ。
            string[][] columns = { new[] { "hp", "atk", "def", "interval" }, new[] { "block", "cost", "cooldown" } };
            for (var c = 0; c < columns.Length; c++)
            {
                for (var line = 0; line < columns[c].Length; line++)
                {
                    var stat = columns[c][line];
                    var bottom = 60f + (3 - line) * 65f;
                    var middle = bottom + 32.5f;
                    SetRect(Child(row, stat + " Icon"), new Vector2(HelpStatColumns[c], (middle - HelpStatIcon / 2f) / h),
                        new Vector2(HelpStatColumns[c] + HelpStatIcon / 1440f, (middle + HelpStatIcon / 2f) / h));
                    SetRect(Child(row, stat), new Vector2(HelpStatValueLefts[c], bottom / h),
                        new Vector2(HelpStatValueLefts[c] + 0.135f, (bottom + 65f) / h));
                }
            }
        }

        private static void SetXIfAny(RectTransform row, string name, float left, float right)
        {
            if (row.Find(name) is RectTransform target)
            {
                SetX(target, left, right);
            }
        }

        // --- T359e リザルト ---
        // Portrait Root いっぱい（1440×1920 px）。値はその割合。ラベルは TextModeSetup が文字あり版の位置で作る。

        /// <summary>リザルト: 数字の行を右に寄せて左に名前（48）を入れる。ボタンの三角は上に寄せて下に名前（40）。</summary>
        private static void BuildResult(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<ResultScreen>(true));
            foreach (var (icon, value) in new[] { ("Floor Icon", "Floor"), ("Money Icon", "Money"), ("Coin Icon", "Coins") })
            {
                SetX(Child(panel, icon), 0.42f, 0.52f);
                SetX(Child(panel, value), 0.55f, 0.88f);
            }

            SetRect(Child(Child(panel, "Retry Button"), "Arrow"), new Vector2(0.4f, 0.36f), new Vector2(0.6f, 0.92f));
            SetRect(Child(Child(panel, "Title Button"), "Arrow"), new Vector2(0.4f, 0.36f), new Vector2(0.6f, 0.92f));
        }

        // 横の位置だけ変える（縦はシーンの値のまま）。
        private static void SetX(RectTransform target, float left, float right) =>
            Set(target.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin.x = left;
                s.anchorMax.x = right;
                return s;
            });

        private static RectTransform PanelOf(Component screen) =>
            ((GameObject)new SerializedObject(screen).FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();

        // --- T359c 筐体の外の名前 ---

        private const float CabinetFont = 40f;

        /// <summary>
        /// レバー・コイン・回数固定の名前を、リール窓の上のボタン台に大きく出す。筐体の部品の配置と見た目は変えない（ラベルだけ動かす）。
        /// 筐体の中は 1440×720 px、リール窓は x 263〜1177・y 66〜516、窓の上のボタン台にレバーとコイン（GameSceneSetup、T614）。
        /// </summary>
        private static void BuildCabinetLabels(RectTransform cabinet)
        {
            // レバー（x 40〜230・y 604〜708）: レバーの右に2行（x 310〜640）。
            var lever = Child(cabinet, "Lever Button");
            SetLabel(Child(lever, "Text Label cabinet_lever"), new Vector2(1f, 0f), Vector2.one,
                new Vector2(10f, 0f), new Vector2(340f, 0f), CabinetFont);

            // コインの名前は出さない（2026-11-23 ユーザー: 「コイン（2枚で1回）」はいらない、T614）。前に作った札があれば消す。
            var oldCredit = cabinet.Find("Credit/Text Label cabinet_credit");
            if (oldCredit != null)
            {
                Object.DestroyImmediate(oldCredit.gameObject);
            }

            // 回数固定（絵 x 1046〜1130・y 616〜692、今は常に隠れている）: 絵の下。
            var fixedSpins = Child(cabinet, "Fixed Spins Icon");
            SetLabel(Child(fixedSpins, "Text Label cabinet_fixed"), Vector2.zero, new Vector2(1f, 0f),
                new Vector2(-46f, -62f), new Vector2(50f, -8f), CabinetFont);
        }

        // --- T359b 戦闘の HUD ---

        // 上端の帯（Top Band、T383）の文字あり版の高さ（px、縮める前の基準）。
        private static float BandHeight => BandRatio * 1920f;

        // 文字あり版の帯の高さの割合（中身の高さに対する割合。文字なし版の 1.4 倍、T614）。
        private const float TextBandScale = 1.4f;
        private static float BandRatio => (float)(PortraitLayout.BattleHeightRatio * PortraitLayout.TopBandRatio) * TextBandScale;

        // 帯の中の段（帯の割合）。上が名前、下が数字・ボタンの絵。
        // T384: 名前が画面の上端からはみ出さず、数字と重ならないように間を空け、数字の字を小さくする（BandValueFont）。
        // 名前の字は文字の箱が約52 px（36 の字）あるので、中心を帯の上端から約28 px 下に置く。
        private const float NameBottom = 0.46f;
        private const float NameTop = 0.96f;
        private const float ValueBottom = 0f;
        private const float ValueTop = 0.38f;
        private const float BandValueFont = 38f;

        private const float BandFont = 36f;
        private const float ButtonFont = 36f;
        private const float CardFont = 40f;

        /// <summary>
        /// 操作バーを味方4種の札にし、ライフ・所持金・ウェーブ・倍率・▶・撤退・一時停止を戦闘フィールドの上端の帯へ移す。
        /// </summary>
        private static void BuildBattleHud(RectTransform root)
        {
            var bar = Child(root, "Operation Bar");
            for (var i = 0; i < 4; i++)
            {
                BuildUnitCard(bar, i, BarDesignHeight);
            }

            // 上端の帯は文字あり版だけ高くする（名前と数字が詰まるため、T614）。戦闘カメラの範囲は変えず、帯が戦闘の上の余白に重なる。
            var band = Child(root, "Top Band");
            SetRect(band, new Vector2(0f, 1f - BandRatio), Vector2.one);
            BuildStatusBand(band);
            BuildRates(band);
        }

        // 操作バーの設計上の高さ（px）。札の枠は 10〜230 px（アンカー 10/240〜230/240）。外枠（T614）で実際の高さは縮むが、
        // アンカーの割合はこの高さで出す（実際の高さで割ると中身が縦に伸び、味方の絵が枠の上にはみ出す: 2026-11-26 ユーザー指摘）。
        private const float BarDesignHeight = 240f;

        // 札（T383）: 札の枠（Card Frame）は BattleHudRestyleSetup の位置のまま。文字あり版は味方アイコンを縮めて上に寄せ、
        // 下の段に職名、右に強化（「強化」の文字つき）とブロック数、右端にスキルゲージ。x は札の左からの px、y は操作バーの下からの px。
        private static void BuildUnitCard(RectTransform bar, int slot, float barHeight)
        {
            const float width = 1440f;
            var left = 10f + slot * 357.5f;
            const float iconBottom = 80f;
            const float iconTop = 218f;

            var icon = Child(bar, $"Unit Icon {slot + 1}");
            SetRect(icon, new Vector2((left + 12f) / width, iconBottom / barHeight), new Vector2((left + 150f) / width, iconTop / barHeight));
            var name = FindLabel(icon, "unit_");
            SetLabel(name, Vector2.zero, Vector2.zero, new Vector2(0f, 18f - iconBottom), new Vector2(292f, 74f - iconBottom), CardFont);

            var upgrade = Child(bar, $"Upgrade Icon {slot + 1}");
            SetRect(upgrade, new Vector2((left + 158f) / width, iconBottom / barHeight), new Vector2((left + 330f) / width, 152f / barHeight)); // T614 で右へ広げた（旧 304、中身が 0.88 倍）
            // T384: 左に「強化」とその下に段階の点、右の上に矢印、右の下に値段（重ならないように）。
            // 上の段に「強化」と値段を横に並べ、下の段に矢印と段階の点（上下に積むと重なる: T384）。
            // T614: 外枠で中身が縮んだので強化の板を右へ広げ、「強化」（字は 36 のまま）と値段を分け直す（狭いと2行に折り返す）。
            SetLabel(Child(upgrade, "Text Label bar_upgrade"), new Vector2(0f, 0.36f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero,
                ButtonFont);
            SetRect(Child(upgrade, "Cost"), new Vector2(0.61f, 0.36f), new Vector2(1f, 1f));
            SetRect(Child(upgrade, "Arrow"), new Vector2(0.04f, 0.02f), new Vector2(0.14f, 0.32f));
            for (var level = 0; level < 5; level++)
            {
                var x = 0.2f + level * 0.16f;
                SetRect(Child(upgrade, $"Level {level + 1}"), new Vector2(x, 0.06f), new Vector2(x + 0.12f, 0.26f));
            }

            var card = Child(bar, $"Card Frame {slot + 1}");
            // 札の枠は 220 px（操作バーの 10〜230 px）。
            SetRect(Child(card, "Block"), new Vector2(158f / 347f, (160f - 10f) / 220f), new Vector2(262f / 347f, (218f - 10f) / 220f));
        }

        // 帯（T383、Top Band の子）: ライフ・残りの敵・所持金（左）、倍率（中、BuildRates）、撤退・倍速・一時停止（右）。名前は数字の上。
        private static void BuildStatusBand(RectTransform band)
        {
            var status = Child(band, "Status");
            var bandHeight = BandHeight;

            NamedIcon(Child(status, "Life Icon"), Child(status, "Life"), "life", 0.012f, 0.13f, bandHeight);
            NamedIcon(Child(status, "Enemy Icon"), Child(status, "Remaining"), "bar_remaining", 0.142f, 0.265f, bandHeight);
            NamedIcon(Child(status, "Coin Icon"), Child(status, "Coins"), "money", 0.277f, 0.405f, bandHeight);

            // 撤退は T384 から選んだ味方の詳細の板（UnitDetailSetup、両方の版で同じ配置）。
            var speed = Child(status, "Speed Button");
            // 速さ: 名前を左、倍率を右に並べる（上下に積むと帯の高さに収まらない: T384）。T614 で名前の幅を広げた。
            SetRect(speed, new Vector2(0.75f, 0.04f), new Vector2(0.86f, 0.96f));
            SetLabel(Child(speed, "Text Label bar_speed"), Vector2.zero, new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero, ButtonFont);
            Set(Child(speed, "Speed").gameObject, TextModeLayout.Parts.Rect | TextModeLayout.Parts.Font, s =>
            {
                s.anchorMin = new Vector2(0.65f, 0f);
                s.anchorMax = Vector2.one;
                s.offsetMin = Vector2.zero;
                s.offsetMax = Vector2.zero;
                s.autoSize = true;
                s.fontSizeMax = 40f;
                s.fontSizeMin = 20f;
                s.fontSize = 40f;
                return s;
            });
            if (status.Find("Home Button") is RectTransform home)
            {
                var open = Child(status, "Open Button");
                BandButton(open, "help_open", 0.75f, 0.86f);
                // 名前（下から 46px まで）と重ならないよう上半分に置く（T423、T614 で中身が縮んだぶん少し上げた）。
                SetRect(Child(open, "Mark"), new Vector2(0.3f, 0.64f), new Vector2(0.7f, 1f));
                BandButton(home, "pause_home", 0.63f, 0.74f);
                SetRect(Child(home, "House"), new Vector2(0.34f, 0.52f), new Vector2(0.66f, 0.94f));
            }
            var pause = Child(status, "Pause Button");
            BandButton(pause, "bar_pause", 0.87f, 0.99f);
            SetRect(Child(pause, "Bar Left"), new Vector2(0.38f, 0.5f), new Vector2(0.46f, 0.94f));
            SetRect(Child(pause, "Bar Right"), new Vector2(0.54f, 0.5f), new Vector2(0.62f, 0.94f));
        }

        // 倍率は帯の中央に横並び（左が攻撃力、右が獲得金）。名前は数字の上。
        private static void BuildRates(RectTransform band)
        {
            var rates = Child(band, "Rates");
            SetRect(rates, new Vector2(0.415f, 0f), new Vector2(0.62f, 1f));
            var bandHeight = BandHeight;
            var attack = Child(rates, "Attack Rate");
            SetRect(attack, new Vector2(0f, ValueBottom), new Vector2(0.49f, ValueTop));
            ValueFont(Child(attack, "Value"));
            SetLabel(Child(attack, "Text Label bar_attack_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);
            var money = Child(rates, "Money Rate");
            SetRect(money, new Vector2(0.51f, ValueBottom), new Vector2(1f, ValueTop));
            ValueFont(Child(money, "Value"));
            SetLabel(Child(money, "Text Label bar_money_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);

            // 効果のアイコン（T611、SlotEffectBandSetup）: 倍率と速さのボタンの間。T615: 名前（停止・毒）を上の段に置き、アイコンは下の段。
            if (band.Find(SlotEffectBandSetup.EffectsName) is RectTransform effects)
            {
                const float effectsTop = 0.44f;
                SetRect(effects, new Vector2(0.628f, ValueBottom), new Vector2(0.744f, effectsTop));
                // 枠は約 70 px なので「停止」（2字）は右へ広げ、「毒」（1字）は右に寄せて重ならないようにする。
                foreach (var (cell, id, left, right) in new[] { ("Stop Effect", "bar_stop", -6f, 16f), ("Poison Effect", "bar_poison", 10f, -2f) })
                {
                    if (effects.Find($"{cell}/Text Label {id}") is RectTransform label)
                    {
                        SetLabel(label, new Vector2(0f, 1f), Vector2.one, new Vector2(left, (NameBottom - effectsTop) * bandHeight),
                            new Vector2(right, (NameTop - effectsTop) * bandHeight), BandFont);
                    }
                }
            }
        }

        // 帯の1項目: 左に絵、右に数字、上の段に名前（名前は絵の子なので絵の左上を基準に置く）。
        private static void NamedIcon(RectTransform icon, RectTransform value, string id, float left, float right, float bandHeight)
        {
            const float iconWidth = 0.034f;
            const float iconBottom = 0.10f;
            const float iconTop = 0.48f;
            SetRect(icon, new Vector2(left, iconBottom), new Vector2(left + iconWidth, iconTop));
            // 位置と字の大きさは1回の Set で置く（Set は部品の項目を置き換えるため、分けると後の方だけが残る）。
            var min = new Vector2(left + iconWidth + 0.002f, ValueBottom);
            var max = new Vector2(right, ValueTop);
            Set(value.gameObject, TextModeLayout.Parts.Rect | TextModeLayout.Parts.Font, s =>
            {
                s.anchorMin = min;
                s.anchorMax = max;
                s.offsetMin = Vector2.zero;
                s.offsetMax = Vector2.zero;
                return ValueFontState(s);
            });
            SetLabel(Child(icon, "Text Label " + id), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, (NameBottom - iconTop) * bandHeight), new Vector2((right - left) * 1440f, (NameTop - iconTop) * bandHeight),
                BandFont);
        }

        // 帯の数字の字の大きさ（文字あり版。文字なし版は BattleHudRestyleSetup の 54）。
        private static void ValueFont(RectTransform value) =>
            Set(value.gameObject, TextModeLayout.Parts.Font, ValueFontState);

        private static TextModeLayout.State ValueFontState(TextModeLayout.State s)
        {
            s.autoSize = true;
            s.fontSizeMax = BandValueFont;
            s.fontSizeMin = BandValueFont * 0.5f;
            s.fontSize = BandValueFont;
            return s;
        }

        // 帯のボタン: 帯の高さいっぱい、下に名前、絵は上に寄せる（絵は呼び出し側）。
        private static void BandButton(RectTransform button, string id, float left, float right)
        {
            SetRect(button, new Vector2(left, ValueBottom), new Vector2(right, NameTop));
            SetLabel(Child(button, "Text Label " + id), Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(0f, 46f),
                ButtonFont);
        }

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
