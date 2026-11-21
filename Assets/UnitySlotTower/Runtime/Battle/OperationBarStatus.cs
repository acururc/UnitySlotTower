using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの状態表示とボタン（T308・T349・T365、plan 16.8・17.2・17.5）: ライフ・所持金・残りの敵の数、
    /// 撤退（T384 から選んだ味方の詳細の板 UnitDetailPanel の中。出し入れは板がする）・一時停止（一時停止中は色を変える）・倍速（×1・×2・×3）、
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
        [Tooltip("残りの敵の数（FR-009）。")]
        [SerializeField] private TMP_Text remainingText;
        [Tooltip("倍速のボタン（FR-011）と倍率の数字（×2）。")]
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedText;

        private Color _pauseNormalColor;
        private int _shownLife = -1;
        private int _shownMoney = -1;
        private bool _shownPaused;
        private int _shownAttackRate = -1;
        private int _shownMoneyRate = -1;
        private int _shownRemaining = -1;
        private int _shownSpeed = -1;
        private Graphic[] _lifeGraphics;
        private Color[] _lifeColors;
        private Vector3 _lifeScale;
        private bool _lifeFlashing;

        public bool IsLifeFlashing => _lifeFlashing;
        public float LifeNumberScale => lifeText.transform.localScale.x / _lifeScale.x;

        /// <summary>バーの攻撃力倍率の表示（揃った時の演出の行き先: T366）。</summary>
        public RectTransform AttackRateRect => (RectTransform)attackRateGroup.transform;

        /// <summary>コインの所持金倍率の表示（揃った時の演出の行き先: T366）。</summary>
        public RectTransform MoneyRateRect => (RectTransform)moneyRateGroup.transform;

        private void Awake()
        {
            _pauseNormalColor = pauseBackground.color;
            _lifeScale = lifeText.transform.localScale;
            var icon = lifeText.transform.parent.Find("Life Icon");
            var graphics = icon != null ? icon.GetComponentsInChildren<Graphic>(true) : System.Array.Empty<Graphic>();
            _lifeGraphics = new Graphic[graphics.Length + 1];
            System.Array.Copy(graphics, _lifeGraphics, graphics.Length);
            _lifeGraphics[graphics.Length] = lifeText;
            _lifeColors = new Color[_lifeGraphics.Length];
        }

        private void OnEnable()
        {
            retreatButton.onClick.AddListener(placementInput.RetreatSelected);
            pauseButton.onClick.AddListener(battle.TogglePause);
            speedButton.onClick.AddListener(battle.CycleBattleSpeed);
        }

        private void OnDisable()
        {
            RestoreLife();
            retreatButton.onClick.RemoveListener(placementInput.RetreatSelected);
            pauseButton.onClick.RemoveListener(battle.TogglePause);
            speedButton.onClick.RemoveListener(battle.CycleBattleSpeed);
        }

        private void LateUpdate()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            SetNumber(lifeText, run.Field.Life, ref _shownLife);
            UpdateLifeFlash();
            SetNumber(moneyText, run.Wallet.Coins, ref _shownMoney, NumericText.YenFormat);
            SetNumber(remainingText, run.RemainingEnemies, ref _shownRemaining);
            SetNumber(speedText, battle.BattleSpeed, ref _shownSpeed, "×{0}");
            var boosts = run.Field.Effects;
            SetRate(attackRateGroup, attackRateText, boosts?.AttackRate ?? 1d, ref _shownAttackRate);
            SetRate(moneyRateGroup, moneyRateText, boosts?.MoneyRate ?? 1d, ref _shownMoneyRate);

            if (battle.IsPaused != _shownPaused)
            {
                _shownPaused = battle.IsPaused;
                speedButton.gameObject.SetActive(!_shownPaused);
                pauseBackground.color = _shownPaused ? pausedColor : _pauseNormalColor;
            }
        }

        private void UpdateLifeFlash()
        {
            var left = battle.BaseFlashUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                RestoreLife();
                return;
            }
            if (!_lifeFlashing)
            {
                for (var i = 0; i < _lifeGraphics.Length; i++) _lifeColors[i] = _lifeGraphics[i].color;
                _lifeFlashing = true;
            }
            for (var i = 0; i < _lifeGraphics.Length; i++) _lifeGraphics[i].color = UnitySlotTower.Ui.UiStyle.Danger;
            var progress = 1f - Mathf.Clamp01(left / BattlePresentationTiming.BaseFlash);
            // 最初の半分で punch し、残りの赤い表示の間は通常の大きさに戻す。
            lifeText.transform.localScale = _lifeScale * (1f + 0.3f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress * 2f)));
        }

        private void RestoreLife()
        {
            if (!_lifeFlashing) return;
            _lifeFlashing = false;
            for (var i = 0; i < _lifeGraphics.Length; i++) _lifeGraphics[i].color = _lifeColors[i];
            lifeText.transform.localScale = _lifeScale;
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
