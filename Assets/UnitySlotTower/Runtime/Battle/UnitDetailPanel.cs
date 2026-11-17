using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 盤上で選んだ味方の詳細の板（T384、plan 18.4、FR-070）: 職の絵・強化段階・体力・攻撃力・ブロック数と撤退ボタン（戻る額つき）。
    /// 味方を選んでいる間だけ <see cref="content"/> を出す。撤退ボタンの押下は <see cref="OperationBarStatus"/> がつなぐ。
    /// 強化は札のまま（ユーザー回答 2026-11-18）。
    /// </summary>
    public sealed class UnitDetailPanel : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitPlacementInput placementInput;
        [Tooltip("選んでいるときだけ出す板の本体。")]
        [SerializeField] private GameObject content;
        [Tooltip("units.csv の並びの職の絵（選んだ味方の職のものだけ出す）。")]
        [SerializeField] private GameObject[] jobArts;
        [Tooltip("強化段階の点（左から段階 1, 2, ...）。")]
        [SerializeField] private Image[] levelPips;
        [SerializeField] private Color pipOnColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color pipOffColor = new Color(0.2f, 0.2f, 0.22f);
        [Tooltip("体力の残りの割合（Filled・Horizontal）。")]
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text blockText;
        [Tooltip("撤退で戻る額。")]
        [SerializeField] private TMP_Text refundText;
        [Tooltip("スキルの効果の絵を置く矩形（T385）。絵は職ごとに実行時に作って出し入れする。")]
        [SerializeField] private RectTransform skillArt;
        [Tooltip("色7の順（赤・青・緑・黄）のスキルの色。")]
        [SerializeField] private Color[] skillColors;
        [Tooltip("スキルの絵の丸。")]
        [SerializeField] private Sprite knob;

        private readonly System.Collections.Generic.Dictionary<string, GameObject> _skillArts =
            new System.Collections.Generic.Dictionary<string, GameObject>();

        private BattleUnit _shownUnit;
        private int _shownSlot = -2;
        private int _shownLevel = -1;
        private int _shownHp = -1;
        private int _shownMaxHp = -1;
        private int _shownAtk = -1;
        private int _shownBlock = -1;
        private int _shownRefund = -1;

        /// <summary>板を出しているか。</summary>
        public bool IsShown => content.activeSelf;

        private void Awake()
        {
            content.SetActive(false);
        }

        private void LateUpdate()
        {
            var unit = placementInput.SelectedUnit;
            var show = unit != null && unit.IsAlive && battle.Deployment != null;
            if (content.activeSelf != show)
            {
                content.SetActive(show);
            }

            if (!show)
            {
                _shownUnit = null;
                return;
            }

            if (unit != _shownUnit)
            {
                _shownUnit = unit;
                _shownLevel = _shownHp = _shownMaxHp = _shownAtk = _shownBlock = _shownRefund = -1;
                ShowJob(SlotOf(unit.Definition.Id));
                ShowSkill(unit.Definition.Id);
            }

            var level = battle.Upgrades?.Level(unit.Definition.Id) ?? 0;
            if (level != _shownLevel)
            {
                _shownLevel = level;
                for (var i = 0; i < levelPips.Length; i++)
                {
                    levelPips[i].color = i < level ? pipOnColor : pipOffColor;
                }
            }

            if (unit.Hp != _shownHp || unit.MaxHp != _shownMaxHp)
            {
                _shownHp = unit.Hp;
                _shownMaxHp = unit.MaxHp;
                hpFill.fillAmount = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
                hpText.SetText("{0}", unit.Hp);
            }

            SetNumber(atkText, unit.Atk, ref _shownAtk, "{0}");
            if (unit.Block != _shownBlock)
            {
                _shownBlock = unit.Block;
                BlockText.Show(blockText, unit.Block);
            }

            SetNumber(refundText, battle.Deployment.RetreatRefund(unit, battle.FullRefundRetreat), ref _shownRefund,
                NumericText.YenFormat);
        }

        private int SlotOf(string unitId)
        {
            var all = battle.Units.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].Id == unitId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ShowJob(int slot)
        {
            if (slot == _shownSlot)
            {
                return;
            }

            _shownSlot = slot;
            for (var i = 0; i < jobArts.Length; i++)
            {
                jobArts[i].SetActive(i == slot);
            }
        }

        // 職のスキルの絵だけ出す（初めて選んだ職の絵はそのとき作る）。
        private void ShowSkill(string unitId)
        {
            if (skillArt == null || battle.Skills == null)
            {
                return;
            }

            if (!_skillArts.ContainsKey(unitId) && battle.Skills.TryGetColor(unitId, out var color))
            {
                var tint = skillColors != null && (int)color < skillColors.Length ? skillColors[(int)color] : Color.white;
                var art = SkillPictures.Create(skillArt, color, tint, knob);
                art.name = "Skill Art " + unitId;
                _skillArts[unitId] = art.gameObject;
            }

            foreach (var pair in _skillArts)
            {
                pair.Value.SetActive(pair.Key == unitId);
            }
        }

        /// <summary>出しているスキルの絵（テスト用、なければ null）。</summary>
        public GameObject ShownSkillArt
        {
            get
            {
                foreach (var pair in _skillArts)
                {
                    if (pair.Value.activeSelf)
                    {
                        return pair.Value;
                    }
                }

                return null;
            }
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown, string format)
        {
            if (value != shown)
            {
                shown = value;
                text.SetText(format, value);
            }
        }
    }
}
