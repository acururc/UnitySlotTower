using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの味方アイコン（T308）。押すとその味方を選び、そのままフィールドへドラッグして離すとマスが決まる。
    /// コスト（ストックがあれば ×個数）、待ち時間ゲージ、召喚できないときの暗転、選択中の枠、その職の色のスキルゲージ（T349）を表示する。
    /// </summary>
    public sealed class UnitBarIcon : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private BattleController battle;
        [Tooltip("units.csv の並びで 0 始まり。")]
        [SerializeField] private int slot;

        [SerializeField] private Image body;
        [Tooltip("待ち時間の残りの割合だけ下から覆う（Filled・Vertical）。")]
        [SerializeField] private Image cooldownGauge;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private TMP_Text costText;
        [Tooltip("無料ストックの個数（×n）。ストックがないときは隠す。")]
        [SerializeField] private TMP_Text stockText;
        [Tooltip("召喚できないときに本体の色にかける。")]
        [SerializeField] private Color unavailableTint = new Color(0.45f, 0.45f, 0.45f);
        [Tooltip("召喚できないときに押したら本体をこの色で光らせる（T355）。")]
        [SerializeField] private Color rejectColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private float rejectSeconds = 0.35f;
        [Tooltip("待ち時間の残り秒数の字の大きさ（T355）。")]
        [SerializeField] private float cooldownFontSize = 44f;

        [Header("Skill gauge (T349)")]
        [Tooltip("この職の色のスキルゲージ。アイコン本体とは別のボタンで、満タンで光り、押すと発動する。")]
        [SerializeField] private Button skillButton;
        [SerializeField] private Image skillBackground;
        [Tooltip("下から順に1段ずつ点ける。")]
        [SerializeField] private Image[] gaugeDots;
        [Tooltip("色7の順（赤・青・緑・黄）の、点いた点の色。")]
        [SerializeField] private Color[] gaugeColors =
        {
            new Color(0.9f, 0.05f, 0.1f),
            new Color(0.15f, 0.35f, 0.95f),
            new Color(0.1f, 0.65f, 0.2f),
            new Color(0.95f, 0.75f, 0.05f),
        };
        [SerializeField] private Color emptyDotColor = new Color(1f, 1f, 1f, 0.15f);
        [Tooltip("満タンのとき背景をこの色との間で点滅させる。")]
        [SerializeField] private Color fullGlowColor = new Color(1f, 0.95f, 0.55f);
        [SerializeField] private float glowSpeed = 6f;

        private Color _bodyColor;
        private Color _skillBackgroundColor;
        private int _shownCost = -1;
        private int _shownStock = -1;
        private bool _shownAvailable = true;
        private int _shownLevel = -1;
        private bool _shownFull;
        private bool _hasSkill;
        private SlotSymbol _skillColor;
        private TMP_Text _cooldownText;
        private int _shownSeconds = -1;
        private float _rejectUntil;
        private bool _rejectMoney;
        private Color _costColor;

        /// <summary>スキルゲージのボタン（説明で指す）。なければ null。</summary>
        public RectTransform SkillButtonRect => skillButton != null ? (RectTransform)skillButton.transform : null;

        /// <summary>待ち時間の残り秒数（切り上げ）の文字。待ち時間がないときは隠す（T355）。</summary>
        public TMP_Text CooldownText => _cooldownText;

        /// <summary>召喚できないときに押した反応（赤く光る）を表示中か（T355）。</summary>
        public bool IsShowingReject => Time.unscaledTime < _rejectUntil;

        private void Awake()
        {
            _bodyColor = body.color;
            _costColor = costText.color;
            if (skillBackground != null)
            {
                _skillBackgroundColor = skillBackground.color;
            }

            _cooldownText = CreateCooldownText();
        }

        // 待ち時間ゲージの子でゲージいっぱいの中央に置く数字（文字あり版でゲージが動いても付いていく）。
        // シーンを作り直さずに足すので実行時に作る。
        private TMP_Text CreateCooldownText()
        {
            var rect = new GameObject("Cooldown Seconds", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(cooldownGauge.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = costText.font;
            text.fontSize = cooldownFontSize;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.raycastTarget = false;
            text.enabled = false;
            return text;
        }

        private void OnEnable()
        {
            if (skillButton != null)
            {
                skillButton.onClick.AddListener(ActivateSkill);
            }
        }

        private void OnDisable()
        {
            if (skillButton != null)
            {
                skillButton.onClick.RemoveListener(ActivateSkill);
            }
        }

        // 拒否（満タンでない・配置0人・一時停止中）なら何も起きない（AC-023・AC-011）。
        private void ActivateSkill()
        {
            if (_hasSkill)
            {
                battle.TryActivateSkill(_skillColor);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                placementInput.SelectSlot(slot);
                ShowRejectIfUnavailable();
            }
        }

        // 押しても選べなかった（待ち時間・配置の上限・所持金不足）ことを赤く光らせて返す。所持金不足ならコストも赤くする（T355）。
        private void ShowRejectIfUnavailable()
        {
            var summoner = battle.Summoner;
            if (summoner == null || battle.IsInputBlocked || slot >= battle.Units.All.Count)
            {
                return;
            }

            var result = summoner.Check(battle.Units.All[slot].Id);
            if (result != SummonResult.Summoned)
            {
                _rejectUntil = Time.unscaledTime + rejectSeconds;
                _rejectMoney = result == SummonResult.NotEnoughCoins;
            }
        }

        private void LateUpdate()
        {
            var summoner = battle.Summoner;
            if (summoner == null || slot >= battle.Units.All.Count)
            {
                return;
            }

            var unit = battle.Units.All[slot];
            var stock = battle.Run.Stock.Count(unit.Id);
            if (stock != _shownStock)
            {
                _shownStock = stock;
                stockText.gameObject.SetActive(stock > 0);
                stockText.SetText("×{0}", stock);
            }

            if (unit.Cost != _shownCost)
            {
                _shownCost = unit.Cost;
                costText.SetText("{0}", unit.Cost);
            }

            var remaining = summoner.IgnoresCooldown ? 0d : summoner.CooldownRemaining(unit.Id);
            cooldownGauge.fillAmount = unit.Cooldown > 0d ? (float)(remaining / unit.Cooldown) : 0f;
            var seconds = remaining > 0d ? (int)System.Math.Ceiling(remaining) : 0;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _cooldownText.enabled = seconds > 0;
                _cooldownText.SetText("{0}", seconds);
            }

            var available = summoner.Check(unit.Id) == SummonResult.Summoned;
            var rejecting = IsShowingReject;
            if (rejecting)
            {
                // 光りはじめを強く、だんだん元の色へ。
                var t = (_rejectUntil - Time.unscaledTime) / rejectSeconds;
                var normal = available ? _bodyColor : _bodyColor * unavailableTint;
                body.color = Color.Lerp(normal, rejectColor, t);
                costText.color = _rejectMoney ? Color.Lerp(_costColor, rejectColor, t) : _costColor;
                _shownAvailable = !available; // 光り終わりに色を戻させる。
            }
            else if (available != _shownAvailable)
            {
                _shownAvailable = available;
                body.color = available ? _bodyColor : _bodyColor * unavailableTint;
                costText.color = _costColor;
            }

            var selected = battle.Deployment.Selected;
            selectedFrame.enabled = selected != null && selected.Id == unit.Id;
            UpdateSkillGauge(unit.Id);
        }

        private void UpdateSkillGauge(string unitId)
        {
            if (skillButton == null || battle.Skills == null)
            {
                return;
            }

            if (!_hasSkill)
            {
                _hasSkill = battle.Skills.TryGetColor(unitId, out _skillColor);
                if (!_hasSkill)
                {
                    skillButton.gameObject.SetActive(false);
                    return;
                }
            }

            var gauges = battle.Run.State.Gauges;
            var level = gauges.Level(_skillColor);
            if (level != _shownLevel)
            {
                _shownLevel = level;
                var on = gaugeColors[(int)_skillColor];
                for (var i = 0; i < gaugeDots.Length; i++)
                {
                    gaugeDots[i].color = i < level ? on : emptyDotColor;
                }
            }

            var full = gauges.IsFull(_skillColor);
            if (full)
            {
                // 一時停止中も点滅は続ける（押しても発動しないが、満タンであることは見せる）。
                var t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowSpeed);
                skillBackground.color = Color.Lerp(_skillBackgroundColor, fullGlowColor, t);
            }
            else if (_shownFull)
            {
                skillBackground.color = _skillBackgroundColor;
            }

            _shownFull = full;
        }
    }
}
