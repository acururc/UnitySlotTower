using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの状態表示とボタン（T308・T349、plan 16.8・17.2）: ライフ・所持金、
    /// 撤退（味方を選んでいるときだけ出す）・一時停止（一時停止中は色を変える）、
    /// この戦闘のバーの攻撃力倍率とコインの所持金倍率（×1.2 など。倍率なしのときは薄く ×1.0）。
    /// </summary>
    public sealed class OperationBarStatus : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private TMP_Text lifeText;
        [FormerlySerializedAs("coinText")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private Button retreatButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Image pauseBackground;
        [SerializeField] private Color pausedColor = new Color(0.95f, 0.75f, 0.2f);
        [Tooltip("バーの攻撃力倍率（攻撃力の印の横）。")]
        [SerializeField] private CanvasGroup attackRateGroup;
        [SerializeField] private TMP_Text attackRateText;
        [Tooltip("コインの所持金倍率（所持金の印の横）。")]
        [SerializeField] private CanvasGroup moneyRateGroup;
        [SerializeField] private TMP_Text moneyRateText;
        [SerializeField, Range(0f, 1f)] private float inactiveRateAlpha = 0.3f;

        private Color _pauseNormalColor;
        private int _shownLife = -1;
        private int _shownMoney = -1;
        private bool _shownPaused;
        private int _shownAttackRate = -1;
        private int _shownMoneyRate = -1;

        private void Awake()
        {
            _pauseNormalColor = pauseBackground.color;
        }

        private void OnEnable()
        {
            retreatButton.onClick.AddListener(placementInput.RetreatSelected);
            pauseButton.onClick.AddListener(battle.TogglePause);
        }

        private void OnDisable()
        {
            retreatButton.onClick.RemoveListener(placementInput.RetreatSelected);
            pauseButton.onClick.RemoveListener(battle.TogglePause);
        }

        private void LateUpdate()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            SetNumber(lifeText, run.Field.Life, ref _shownLife);
            SetNumber(moneyText, run.Wallet.Coins, ref _shownMoney, NumericText.YenFormat);
            var boosts = run.Field.Boosts;
            SetRate(attackRateGroup, attackRateText, boosts?.AttackRate ?? 1d, ref _shownAttackRate);
            SetRate(moneyRateGroup, moneyRateText, boosts?.MoneyRate ?? 1d, ref _shownMoneyRate);

            SetActive(retreatButton, placementInput.SelectedUnit != null);
            if (battle.IsPaused != _shownPaused)
            {
                _shownPaused = battle.IsPaused;
                pauseBackground.color = _shownPaused ? pausedColor : _pauseNormalColor;
            }
        }

        // 倍率を小数1桁で出す（×1.2）。表示が変わるときだけ書き換える。
        private void SetRate(CanvasGroup group, TMP_Text text, double rate, ref int shownTenths)
        {
            var tenths = (int)System.Math.Round(rate * 10, System.MidpointRounding.AwayFromZero);
            if (tenths == shownTenths)
            {
                return;
            }

            shownTenths = tenths;
            group.alpha = tenths > 10 ? 1f : inactiveRateAlpha;
            text.SetText("×{0:1}", tenths / 10f);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button.gameObject.activeSelf != active)
            {
                button.gameObject.SetActive(active);
            }
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown, string format = "{0}")
        {
            if (value != shown)
            {
                shown = value;
                text.SetText(format, value);
            }
        }
    }
}
