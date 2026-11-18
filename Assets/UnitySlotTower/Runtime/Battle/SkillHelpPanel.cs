using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 一時停止中にスキルゲージを押すと出る、そのスキルの効果の説明（T385、FR-072、AC-053、plan 18.5）。
    /// 文字を使わず、同じ場面を上の段（スキルなし、絵に斜線）と下の段（スキル中、絵が光る）の2本の道のアニメで比べる。
    /// 黄（タンク）: 同じ数の敵で、スキルなしは1体が通り抜け、スキル中は全部止まる / 赤（剣士）: 1回の攻撃で減る体力が増える / 青（弓）: 弾が速く出る / 緑（バッファー）: 隣の味方の攻撃力の倍率が上がる。
    /// どこを押しても、一時停止を解いても閉じる。中身は開くたびに実行時に作る。
    /// </summary>
    public sealed class SkillHelpPanel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private BattleController battle;
        [Tooltip("全画面の暗い下地（押すと閉じる）。開いている間だけ出す。")]
        [SerializeField] private GameObject content;
        [Tooltip("中央の板（中身は開くたびに作り直す）。")]
        [SerializeField] private RectTransform board;

        private const float BaseDamage = 0.12f; // スキルなしの1回の攻撃で減る体力の割合（絵の都合の仮の値）
        private const float BaseInterval = 0.6f;

        /// <summary>出しているか。</summary>
        public bool IsOpen => content.activeSelf;

        /// <summary>出しているスキル（出していなければ null）。</summary>
        public SlotSymbol? Shown { get; private set; }

        private void Awake()
        {
            content.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && !battle.IsPaused)
            {
                Close();
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Close();

        public void Close()
        {
            content.SetActive(false);
            Shown = null;
        }

        /// <summary>そのスキルの説明を出す。allyArt は職の絵（札の Job Art）、knob は丸の絵、color はゲージの色。</summary>
        public void Open(SlotSymbol skill, string unitId, RectTransform allyArt, Color allyColor, Color color, Sprite knob)
        {
            for (var i = board.childCount - 1; i >= 0; i--)
            {
                Destroy(board.GetChild(i).gameObject);
            }

            transform.SetAsLastSibling();
            content.SetActive(true);
            Shown = skill;

            var definition = battle.Skills.For(skill);
            var unit = battle.Units.Get(unitId);
            // 緑（バッファー）は、強くなる側の味方（近接で攻撃力のいちばん高い職）が殴る絵にする。
            var buffed = skill == SlotSymbol.Green7 ? StrongestMeleeIcon() : null;
            var font = FindFont();
            var captionSource = GetComponentInParent<Canvas>().GetComponentInChildren<TextModeLabel>(true);

            // 上: スキルの絵（大）と職の絵。
            var header = SkillPictures.Rect("Header", board, new Vector2(0.03f, 0.7f), new Vector2(0.97f, 0.96f));
            SkillPictures.Create(SkillPictures.Rect("Skill", header, new Vector2(0.02f, 0f), new Vector2(0.14f, 1f)), skill, color, knob);
            var job = SkillPictures.Rect("Job", header, new Vector2(0.16f, 0.05f), new Vector2(0.26f, 0.95f));
            if (allyArt != null)
            {
                Copy(allyArt, SkillPictures.Image("Back", job, Vector2.zero, Vector2.one, allyColor, null).rectTransform);
            }

            Caption(header, "Description", "skill_" + unitId, new Vector2(0.29f, 0f), Vector2.one, font, captionSource);

            for (var row = 0; row < 2; row++)
            {
                var on = row == 1;
                var y0 = on ? 0.05f : 0.38f;
                var area = SkillPictures.Rect(on ? "With Skill" : "Without Skill", board, new Vector2(0.03f, y0),
                    new Vector2(0.97f, y0 + 0.29f));
                var back = SkillPictures.Image("Back", area, Vector2.zero, Vector2.one,
                    on ? new Color(color.r, color.g, color.b, 0.14f) : new Color(1f, 1f, 1f, 0.05f), null);
                back.transform.SetAsFirstSibling();

                // 左端: スキルの絵（スキルなしは暗くして赤い斜線）。
                var mark = SkillPictures.Rect("Mark", area, new Vector2(0f, 0.1f), new Vector2(0.11f, 0.9f));
                var art = SkillPictures.Create(mark, skill, on ? color : new Color(0.4f, 0.4f, 0.42f), knob);
                if (!on)
                {
                    var slash = SkillPictures.Image("Slash", art, new Vector2(-0.05f, 0.44f), new Vector2(1.05f, 0.56f),
                        UiStyle.Danger, null);
                    slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, -40f);
                }

                Caption(area, "Caption", on ? "skill_help_on" : "skill_help_off",
                    new Vector2(0.5f, 0.66f), new Vector2(1f, 1f), font, captionSource);
                var laneArea = SkillPictures.Rect("Lane", area, new Vector2(0.13f, 0.05f), new Vector2(1f, 0.95f));
                // Leave a band above the road for the caption, clear of enemies and ally numbers.
                laneArea.anchorMax = new Vector2(1f, 0.6f);
                if (buffed != null)
                {
                    BuildBuffLane(laneArea, definition, unit, on, buffed, allyArt, allyColor, color, font);
                }
                else
                {
                    BuildLane(laneArea, skill, definition, unit, on, allyArt, allyColor, color, font);
                }
            }
        }

        private UnitBarIcon StrongestMeleeIcon()
        {
            UnitBarIcon best = null;
            var bestAtk = 0;
            foreach (var icon in FindObjectsByType<UnitBarIcon>(FindObjectsSortMode.None))
            {
                var all = battle.Units.All;
                if (icon.Slot >= all.Count || all[icon.Slot].Role != Core.Battle.UnitRole.Melee || all[icon.Slot].Atk <= bestAtk)
                {
                    continue;
                }

                best = icon;
                bestAtk = all[icon.Slot].Atk;
            }

            return best;
        }

        // 強くなる味方が殴り、その左下に小さくバッファー、頭上に上向きの矢印と攻撃力の倍率。
        private static void BuildBuffLane(RectTransform area, Core.Battle.SkillDefinition skillDef, Core.Battle.UnitDefinition buffer,
            bool on, UnitBarIcon buffed, RectTransform bufferArt, Color bufferColor, Color color, TMP_FontAsset font)
        {
            var rate = (float)(buffer.BuffRate + (on ? skillDef.BuffAdd : 0d));
            var lane = DemoLane.Hit(area, buffed.JobArt, buffed.JobColor, BaseInterval, BaseDamage * rate, false);
            var mini = SkillPictures.Rect("Buffer", lane.Ally, new Vector2(-0.45f, 0f), new Vector2(-0.05f, 0.45f));
            if (bufferArt != null)
            {
                Copy(bufferArt, SkillPictures.Image("Back", mini, Vector2.zero, Vector2.one, bufferColor, null).rectTransform);
            }

            var arrow = SkillPictures.Rect("Buff", lane.Ally, new Vector2(0.65f, 0.55f), new Vector2(1.1f, 0.95f));
            SkillPictures.Triangle("Up", arrow, Vector2.zero, Vector2.one, TriangleGraphic.Direction.Up,
                on ? color : new Color(0.6f, 0.6f, 0.62f));
            Number(lane.Ally, "×" + rate.ToString("0.0"), color, font, on);
        }

        private static void BuildLane(RectTransform area, SlotSymbol skill, Core.Battle.SkillDefinition skillDef,
            Core.Battle.UnitDefinition unit, bool on, RectTransform allyArt, Color allyColor, Color color, TMP_FontAsset font)
        {
            switch (skill)
            {
                case SlotSymbol.Yellow7:
                {
                    var block = unit.Block + (on ? skillDef.BlockAdd : 0);
                    var lane = DemoLane.Block(area, allyArt, allyColor, block, unit.Block + 1);
                    Number(lane.Ally, block.ToString(), color, font, on);
                    break;
                }
                case SlotSymbol.Red7:
                    DemoLane.Hit(area, allyArt, allyColor, BaseInterval, BaseDamage * (on ? (float)skillDef.DamageRate : 1f), false);
                    break;
                case SlotSymbol.Blue7:
                    DemoLane.Hit(area, allyArt, allyColor, BaseInterval * (on ? (float)skillDef.IntervalRate : 1f), BaseDamage, true);
                    break;
                default:
                {
                    // バッファーの隣の味方（剣士の形は持っていないので同じ職の絵）が殴る。倍率は units.csv と skills.csv から。
                    var rate = (float)(unit.BuffRate + (on ? skillDef.BuffAdd : 0d));
                    var lane = DemoLane.Hit(area, allyArt, allyColor, BaseInterval, BaseDamage * rate, false);
                    var arrow = SkillPictures.Rect("Buff", lane.Ally, new Vector2(0.55f, 0.7f), new Vector2(1.05f, 1.15f));
                    SkillPictures.Triangle("Up", arrow, Vector2.zero, Vector2.one, TriangleGraphic.Direction.Up,
                        on ? color : new Color(0.6f, 0.6f, 0.62f));
                    Number(lane.Ally, "×" + rate.ToString("0.0"), color, font, on);
                    break;
                }
            }
        }

        // 味方の上の数字（止められる数・倍率）。数字は文字なし版でも使う。
        private static void Number(RectTransform ally, string value, Color color, TMP_FontAsset font, bool on)
        {
            var rect = SkillPictures.Rect("Number", ally, new Vector2(-0.6f, 1f), new Vector2(1.6f, 1.45f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.fontSize = 44f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = on ? color : Color.white;
            text.raycastTarget = false;
        }

        private static void Copy(RectTransform source, RectTransform parent)
        {
            var art = (RectTransform)Instantiate(source, parent, false).transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = art.offsetMax = Vector2.zero;
            foreach (var graphic in art.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static void Caption(RectTransform parent, string name, string id, Vector2 min, Vector2 max,
            TMP_FontAsset font, TextModeLabel source)
        {
            var text = SkillPictures.Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 36f;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Left;
            text.color = UiStyle.TextColor;
            text.raycastTarget = false;
            var label = text.gameObject.AddComponent<TextModeLabel>();
            label.CopyTableFrom(source);
            label.SetIds(id);
        }

        private TMP_FontAsset FindFont()
        {
            var canvas = GetComponentInParent<Canvas>();
            var caption = canvas.GetComponentInChildren<TextModeLabel>(true);
            if (caption != null && caption.Text != null) return caption.Text.font;
            var text = canvas.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.font : null;
        }
    }
}
