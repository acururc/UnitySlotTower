using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Ui;

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
        [SerializeField] private TMP_Text blockText;
        [SerializeField] private Graphic stateFrame;
        [Tooltip("無料ストックの個数（×n）。ストックがないときは隠す。")]
        [SerializeField] private TMP_Text stockText;
        [Tooltip("無料ストックの数の下地（T384、数字の手前の兄弟）。数字と一緒に出し入れする。")]
        [SerializeField] private GameObject stockBadge;
        [Tooltip("旧シーン互換用。カードの状態色は UiStyle を使用する。")]
        [SerializeField] private Color unavailableTint = UiStyle.DisabledTint;
        [Tooltip("召喚できないときに押したら本体をこの色で光らせる（T355）。")]
        [SerializeField] private Color rejectColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private float rejectSeconds = 0.35f;
        [Tooltip("待ち時間の残り秒数の字の大きさ（T355）。")]
        [SerializeField] private float cooldownFontSize = 44f;

        private Color _bodyColor;
        private int _shownCost = -1;
        private int _shownStock = -1;
        private enum CardState { Unknown, Placeable, Money, Cooldown, Blocked }
        private CardState _shownState;
        private int _shownBlock = -1;
        private bool _shownFrameStock;
        private bool _wasRejecting;
        private TMP_Text _cooldownText;
        private int _shownSeconds = -1;
        private float _rejectUntil;
        private bool _rejectMoney;

        /// <summary>費用の字（ステージ1の無料の1回の間は「¥0」: T611）。</summary>
        public TMP_Text CostText => costText;

        /// <summary>職の絵（説明で写す: T369）。</summary>
        public Image Body => body;

        /// <summary>待ち時間の残り秒数（切り上げ）の文字。待ち時間がないときは隠す（T355）。</summary>
        public TMP_Text CooldownText => _cooldownText;

        /// <summary>召喚できないときに押した反応（赤く光る）を表示中か（T355）。</summary>
        public bool IsShowingReject => Time.unscaledTime < _rejectUntil;

        private void Awake()
        {
            _bodyColor = body.color;
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

        /// <summary>札の職の絵（T384 の Job Art）。なければ null。</summary>
        public RectTransform JobArt => body.transform.Find("Job Art") as RectTransform;

        /// <summary>units.csv の並びで 0 始まりの位置。</summary>
        public int Slot => slot;

        /// <summary>札の職の色（暗くする前の本体の色）。</summary>
        public Color JobColor => _bodyColor;

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
            // 無料ストックはない（spec 002 FR-106）。ステージ1の無料の1回が残っている間は全ての札が「¥0」（FR-108）。
            var free = summoner.FreeSummons > 0;
            if (_shownStock != 0)
            {
                _shownStock = 0;
                stockText.gameObject.SetActive(false);
                if (stockBadge != null)
                {
                    stockBadge.SetActive(false);
                }
            }

            var cost = free ? 0 : unit.Cost;
            if (cost != _shownCost)
            {
                _shownCost = cost;
                costText.SetText(NumericText.YenFormat, cost);
            }

            if (blockText != null && unit.Block != _shownBlock)
            {
                _shownBlock = unit.Block;
                BlockText.Show(blockText, unit.Block);
            }

            // 無料の1回があれば待ち時間中でも出せる（FR-108）。
            var remaining = free ? 0d : summoner.CooldownRemaining(unit.Id);
            cooldownGauge.fillAmount = unit.Cooldown > 0d ? (float)(remaining / unit.Cooldown) : 0f;
            var seconds = remaining > 0d ? (int)System.Math.Ceiling(remaining) : 0;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _cooldownText.enabled = seconds > 0;
                _cooldownText.SetText("{0}", seconds);
            }

            var result = summoner.Check(unit.Id);
            var state = battle.IsInputBlocked ? CardState.Blocked
                : remaining > 0d ? CardState.Cooldown
                : result == SummonResult.Summoned ? CardState.Placeable
                : result == SummonResult.NotEnoughCoins ? CardState.Money : CardState.Blocked;
            var normal = state == CardState.Placeable ? _bodyColor
                : _bodyColor * (state == CardState.Cooldown ? UiStyle.CooldownTint : UiStyle.DisabledTint);
            var costColor = state == CardState.Money ? UiStyle.Danger : free ? UiStyle.Ready : UiStyle.TextColor;
            var hasStock = free;
            var freeChanged = hasStock != _shownFrameStock;
            if (state != _shownState || freeChanged)
            {
                if (stateFrame != null)
                {
                    var frameColor = hasStock ? UiStyle.Ready
                        : state == CardState.Money || state == CardState.Cooldown ? UiStyle.Danger : UiStyle.PanelBorder;
                    if (!hasStock && state == CardState.Cooldown) frameColor.a = 0.6f;
                    stateFrame.color = frameColor;
                }
                _shownFrameStock = hasStock;
            }
            var rejecting = IsShowingReject;
            if (rejecting)
            {
                // 光りはじめを強く、だんだん元の色へ。
                var t = (_rejectUntil - Time.unscaledTime) / rejectSeconds;
                body.color = Color.Lerp(normal, rejectColor, t);
                costText.color = _rejectMoney ? Color.Lerp(costColor, rejectColor, t) : costColor;
            }
            else if (state != _shownState || _wasRejecting || freeChanged)
            {
                body.color = normal;
                costText.color = costColor;
            }
            _shownState = state;
            _wasRejecting = rejecting;

            var selected = battle.Deployment.Selected;
            selectedFrame.enabled = selected != null && selected.Id == unit.Id;
        }
    }
}
