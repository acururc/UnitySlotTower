using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 揃った時の演出（FR-044、plan 17.6、T366）。筐体が揃いを通知したら中段の3つの図柄を光らせ、そこから光の玉を効果の行き先
    /// （色7 → その職のスキルゲージ、バー → 攻撃力の倍率、コイン → 所持金の倍率）へ飛ばし、着いたら行き先を一瞬大きくする。
    /// ハズレでは出さない（筐体が通知しない）。見た目だけで、戦闘とレバーは止めない。時間は戦闘の時間（Time.deltaTime）で進むので、
    /// 一時停止中は止まり、倍速では速くなる。UI Canvas いっぱいの透明な層に置き、光と玉はこの層の子に作って使い回す。
    /// </summary>
    public sealed class SlotAlignEffect : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private OperationBarStatus status;
        [Tooltip("操作バーの味方アイコン（色7の行き先を探す）。")]
        [SerializeField] private UnitBarIcon[] icons;
        [Tooltip("光と玉の絵（丸）。")]
        [SerializeField] private Sprite glowSprite;
        [Tooltip("図柄の順（赤7・青7・緑7・黄7・バー・コイン）の光の色。")]
        [SerializeField] private Color[] symbolColors =
        {
            new Color(1f, 0.45f, 0.45f),
            new Color(0.5f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.55f),
            new Color(1f, 0.95f, 0.45f),
            new Color(1f, 1f, 1f),
            new Color(1f, 0.85f, 0.3f),
        };
        [Tooltip("図柄の光の長さ（秒）。")]
        [SerializeField, Min(0.05f)] private float glowSeconds = 0.45f;
        [Tooltip("図柄の光の一番明るいときの不透明度（図柄が透けて見えるように）と、図柄を大きくする倍率。")]
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.45f;
        [SerializeField, Min(1f)] private float symbolPulse = 1.25f;
        [Tooltip("玉が飛び始めるまで（秒）。")]
        [SerializeField, Min(0f)] private float flyDelay = 0.12f;
        [Tooltip("玉が行き先まで飛ぶ時間（秒）。")]
        [SerializeField, Min(0.05f)] private float flySeconds = 0.4f;
        [Tooltip("着いたときに行き先を大きくする時間（秒）と倍率。")]
        [SerializeField, Min(0.05f)] private float punchSeconds = 0.2f;
        [SerializeField, Min(1f)] private float punchScale = 1.3f;
        [Tooltip("玉の大きさ（中段のマスの高さに対する割合）と、飛ぶ道の膨らみ（距離に対する割合）。")]
        [SerializeField, Range(0.1f, 1f)] private float orbSize = 0.6f;
        [SerializeField, Range(0f, 0.5f)] private float arc = 0.18f;

        private readonly List<Image> _pool = new List<Image>();
        private readonly Dictionary<RectTransform, Vector3> _baseScales = new Dictionary<RectTransform, Vector3>();
        private readonly Dictionary<RectTransform, int> _punching = new Dictionary<RectTransform, int>();
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _layer;
        private int _playing;

        /// <summary>演出中か（同時に複数あってもよい）。</summary>
        public bool IsPlaying => _playing > 0;

        /// <summary>これまでに始めた演出の数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>直近の演出の図柄と行き先。行き先がなければ null。</summary>
        public SlotSymbol? LastSymbol { get; private set; }

        public RectTransform LastTarget { get; private set; }

        /// <summary>図柄の効果の行き先（上端の帯）。その場で終わる赤7・緑7は帯に出ないので null（盤面全体の演出は T613）。</summary>
        public RectTransform TargetOf(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Bar:
                    return status.AttackRateRect;
                case SlotSymbol.Coin:
                    return status.MoneyRateRect;
                case SlotSymbol.Blue7:
                    return status.StopEffectRect;
                case SlotSymbol.Yellow7:
                    return status.PoisonEffectRect;
            }

            return null;
        }

        private void Awake()
        {
            _layer = (RectTransform)transform;
        }

        private void OnEnable() => cabinet.Aligned += OnAligned;

        private void OnDisable() => cabinet.Aligned -= OnAligned;

        private void OnAligned(SlotSymbol symbol)
        {
            PlayCount++;
            LastSymbol = symbol;
            LastTarget = TargetOf(symbol);
            _ = Play(symbol, LastTarget);
        }

        private async Awaitable Play(SlotSymbol symbol, RectTransform target)
        {
            _playing++;
            var color = symbolColors[(int)symbol % symbolColors.Length];
            var glows = new Image[SlotMachine.ReelCount];
            var orbs = new Image[SlotMachine.ReelCount];
            var from = new Vector2[SlotMachine.ReelCount];
            var cells = new RectTransform[SlotMachine.ReelCount];
            var cellHeight = 0f;
            try
            {
                for (var i = 0; i < glows.Length; i++)
                {
                    cells[i] = cabinet.ReelCenterCell(i);
                    var cell = LocalRect(cells[i]);
                    from[i] = cell.center;
                    cellHeight = cell.height;
                    glows[i] = Take(color, cell.center, Vector2.one * Mathf.Max(cell.width, cell.height) * 1.2f);
                }

                var to = target != null ? LocalRect(target).center : Vector2.zero;
                var total = Mathf.Max(glowSeconds, target != null ? flyDelay + flySeconds : 0f);
                var orbsShown = false;
                var token = destroyCancellationToken;
                for (var t = 0f; t < total; t += Time.deltaTime)
                {
                    // 図柄の光: すぐ明るくなり、ふくらみながら消える。図柄そのものも一瞬大きくする。
                    var g = Mathf.Clamp01(t / glowSeconds);
                    var k = g < 1f ? Mathf.Sin(Mathf.PI * Mathf.Sqrt(g)) : 0f;
                    for (var i = 0; i < glows.Length; i++)
                    {
                        SetAlpha(glows[i], color, glowAlpha * k);
                        glows[i].rectTransform.localScale = Vector3.one * (0.9f + 0.3f * g);
                        cells[i].localScale = Vector3.one * (1f + (symbolPulse - 1f) * k);
                    }

                    if (target != null && t >= flyDelay)
                    {
                        if (!orbsShown)
                        {
                            orbsShown = true;
                            for (var i = 0; i < orbs.Length; i++)
                            {
                                orbs[i] = Take(Color.white, from[i], Vector2.one * cellHeight * orbSize);
                            }
                        }

                        var f = Mathf.Clamp01((t - flyDelay) / flySeconds);
                        var e = f * f * (3f - 2f * f);
                        for (var i = 0; i < orbs.Length; i++)
                        {
                            var line = to - from[i];
                            var side = new Vector2(-line.y, line.x) * (arc * (i - 1));
                            var position = Vector2.Lerp(from[i], to, e) + side * (4f * e * (1f - e));
                            orbs[i].rectTransform.anchoredPosition = position;
                            orbs[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
                            orbs[i].color = Color.Lerp(Color.white, color, e);
                        }
                    }

                    await Awaitable.NextFrameAsync(token);
                }

                Release(glows);
                Release(orbs);
                if (target != null)
                {
                    await Punch(target, token);
                }
            }
            catch (OperationCanceledException)
            {
                // シーンを離れた: 層ごと消えるので片付けない。
            }
            finally
            {
                foreach (var cell in cells)
                {
                    if (cell != null)
                    {
                        cell.localScale = Vector3.one;
                    }
                }

                _playing--;
            }
        }

        // 行き先を一瞬大きくして元の大きさに戻す。同じ行き先の演出が重なっても元の大きさを覚えておいて戻す。
        private async Awaitable Punch(RectTransform target, System.Threading.CancellationToken token)
        {
            if (!_punching.TryGetValue(target, out var count) || count == 0)
            {
                _baseScales[target] = target.localScale;
            }

            _punching[target] = count + 1;
            var baseScale = _baseScales[target];
            try
            {
                for (var t = 0f; t < punchSeconds; t += Time.deltaTime)
                {
                    var k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / punchSeconds));
                    target.localScale = baseScale * (1f + (punchScale - 1f) * k);
                    await Awaitable.NextFrameAsync(token);
                }
            }
            finally
            {
                _punching[target]--;
                if (_punching[target] == 0 && target != null)
                {
                    target.localScale = baseScale;
                }
            }
        }

        // 部品の矩形をこの層のローカル座標（中心が原点）にする。
        private Rect LocalRect(RectTransform target)
        {
            target.GetWorldCorners(_corners);
            Vector2 min = _layer.InverseTransformPoint(_corners[0]);
            Vector2 max = _layer.InverseTransformPoint(_corners[2]);
            var offset = _layer.rect.size * (_layer.pivot - new Vector2(0.5f, 0.5f));
            return Rect.MinMaxRect(min.x + offset.x, min.y + offset.y, max.x + offset.x, max.y + offset.y);
        }

        private Image Take(Color color, Vector2 position, Vector2 size)
        {
            Image image = null;
            for (var i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf)
                {
                    image = _pool[i];
                    break;
                }
            }

            if (image == null)
            {
                var rect = new GameObject("Align Light", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.gameObject.layer = gameObject.layer;
                rect.SetParent(_layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                image = rect.gameObject.AddComponent<Image>();
                image.sprite = glowSprite;
                image.raycastTarget = false;
                _pool.Add(image);
            }

            image.color = color;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.localScale = Vector3.one;
            image.gameObject.SetActive(true);
            return image;
        }

        private static void Release(Image[] images)
        {
            foreach (var image in images)
            {
                if (image != null)
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        private static void SetAlpha(Image image, Color color, float alpha)
        {
            color.a *= alpha;
            image.color = color;
        }
    }
}
