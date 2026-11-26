using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// コインの噴水の演出（spec 009 FR-001・FR-002・FR-005・FR-006、plan 2章）。金貨は筐体（下）→戦闘モニター（上）→所持金の表示（上端の帯）を
    /// またぐので、<see cref="SlotAlignEffect"/> と同じ UI Canvas いっぱいの透明な層に UI の Image で描く。
    /// 揃うと中段の真ん中のマスから金貨が噴き上がって戦闘カメラが弱く揺れ（ヒットストップなし）、金貨は回りながら降ってから所持金の表示へ吸い込まれる。
    /// 最後の金貨は Core が倍率を上げる瞬間（<see cref="BattleController.CoinGained"/>、揃ってから coin_delay_seconds）に着き、そこで所持金の表示が弾む。
    /// 倍率が 1 より上の間は、倒した敵（<see cref="BattleController.EnemyDefeated"/>）から小さい金貨が所持金の表示へ飛び、表示の周りがきらめく。
    /// 時間は戦闘の時間（Time.deltaTime）。部品はこの層の子に作って使い回す。絵は Setup（Build Coin Fountain Effect）が作る自作の PNG。
    /// </summary>
    public sealed class CoinFountainEffect : MonoBehaviour
    {
        private const int MaxCoins = 128;
        private const int MaxPuffs = 32;
        private const int MaxSparkles = 5;

        // 待ちが 0 のとき（倍率はもう上がっている）に噴水を見せる長さ（秒）。
        private const float FallbackSeconds = 1.2f;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private OperationBarStatus status;
        [SerializeField] private Camera battleCamera;

        [Header("絵（Setup が入れる）")]
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite sparkleSprite;
        [Tooltip("閃光と光の輪の絵（丸い光）。")]
        [SerializeField] private Sprite glowSprite;

        [Header("噴き出し")]
        [SerializeField, Min(0)] private int fountainCoins = 36;
        [Tooltip("金貨を出し終えるまでの時間（秒）。噴水のように少しずつ出す。")]
        [SerializeField, Min(0f)] private float gushSeconds = 0.3f;
        [Tooltip("金貨の大きさ（中段のマスの高さに対する割合）。")]
        [SerializeField, Min(0f)] private float coinSize = 0.42f;
        [Tooltip("一番高い所に着くまでの時間（秒、最小〜最大）。")]
        [SerializeField] private Vector2 riseSeconds = new Vector2(0.5f, 0.65f);
        [Tooltip("一番高い所の高さ（戦闘モニターの下端 0 → 上端 1、最小〜最大）と、横の広がり（戦闘モニターの幅に対する割合）。")]
        [SerializeField] private Vector2 apexHeight = new Vector2(0.45f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float spread = 0.85f;
        [Tooltip("金貨が回る速さ（毎秒の回転、最小〜最大）。")]
        [SerializeField] private Vector2 spinTurns = new Vector2(1.5f, 3f);
        [Tooltip("噴き出しの閃光の大きさ（中段のマスの高さ = 1）。")]
        [SerializeField, Min(0f)] private float launchFlashSize = 3.5f;
        [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.45f, 0.9f);

        [Header("吸い込み")]
        [Tooltip("吸い込みが始まる時点（待ち時間に対する割合）。最後の金貨は待ち時間ちょうどに着く。")]
        [SerializeField, Range(0.1f, 0.9f)] private float absorbStart = 0.5f;
        [Tooltip("1枚が所持金の表示まで飛ぶ時間（待ち時間に対する割合）。")]
        [SerializeField, Range(0.05f, 0.5f)] private float absorbFlight = 0.33f;
        [Tooltip("飛ぶ道の膨らみ（距離に対する割合）。")]
        [SerializeField, Range(0f, 0.5f)] private float arc = 0.15f;

        [Header("山場（倍率が上がる瞬間）")]
        [SerializeField, Min(0.05f)] private float punchSeconds = 0.3f;
        [SerializeField, Min(1f)] private float punchScale = 1.4f;
        [Tooltip("光の輪の大きさ（所持金の数字の高さ = 1、始め → 終わり）と長さ（秒）。")]
        [SerializeField] private Vector2 gainRingSize = new Vector2(1f, 5f);
        [SerializeField, Min(0.05f)] private float gainRingSeconds = 0.45f;

        [Header("倒したとき")]
        [Tooltip("同時に出ている、倒したときの金貨の上限（重くしない、FR-005）。")]
        [SerializeField, Min(0)] private int maxKillCoins = 48;
        [Tooltip("倒したときの金貨の大きさ（噴水の金貨に対する割合）。")]
        [SerializeField, Min(0f)] private float killCoinScale = 0.6f;
        [Tooltip("飛び出してから所持金の表示へ向かうまで（秒）と、飛ぶ時間（秒）。")]
        [SerializeField, Min(0f)] private float killHopSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float killFlightSeconds = 0.45f;

        [Header("きらめき")]
        [Tooltip("所持金の表示の周りのきらめきの大きさ（所持金の数字の高さ = 1）と点滅の速さ（毎秒）。続く効果なので控えめに。")]
        [SerializeField, Min(0f)] private float sparkleSize = 0.6f;
        [SerializeField, Min(0f)] private float sparkleRate = 0.9f;
        [SerializeField] private Color sparkleColor = new Color(1f, 0.92f, 0.5f, 0.85f);

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.2f;
        [Tooltip("揺れの幅（ワールド）。黄7・バーと同じくらい（spec 009 D-04）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

        private struct Coin
        {
            public Image Image;
            public bool Active;
            public bool Kill;
            public bool Homing;
            public float Age;
            public float LaunchDelay;
            public Vector2 From;
            public Vector2 Velocity;
            public float Gravity;
            public float HomeAt;
            public float HomeSeconds;
            public Vector2 HomeFrom;
            public float Side;
            public float Size;
            public float SpinPhase;
            public float SpinSpeed;
        }

        private struct Puff
        {
            public Image Image;
            public bool Active;
            public float Age;
            public float Seconds;
            public Vector2 Position;
            public Vector2 Size;
            public Color Color;
        }

        private readonly Coin[] _coins = new Coin[MaxCoins];
        private readonly Puff[] _puffs = new Puff[MaxPuffs];
        private readonly Image[] _sparkles = new Image[MaxSparkles];
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _layer;
        private BattleCameraShake _shake;
        private int _activeCoins;
        private int _activeKillCoins;
        private int _activePuffs;
        private int _sparkleCount;
        private float _punchAge = -1f;
        private Vector3 _punchBase;
        private RectTransform _punchTarget;

        /// <summary>噴水を始めた回数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>倍率が上がって所持金の表示が弾んだ回数（テスト用）。</summary>
        public int GainCount { get; private set; }

        /// <summary>倒した敵から出した金貨の枚数の合計（テスト用）。</summary>
        public int KillCoinCount { get; private set; }

        /// <summary>出ている金貨の数（噴水と倒したときの両方）。</summary>
        public int ActiveCoinCount => _activeCoins;

        /// <summary>出ている、倒したときの金貨の数。</summary>
        public int ActiveKillCoinCount => _activeKillCoins;

        /// <summary>同時に出せる、倒したときの金貨の上限。</summary>
        public int MaxKillCoins => maxKillCoins;

        /// <summary>出ている所持金の表示の周りのきらめきの数（倍率が 1 より上の間だけ 0 より大きい）。</summary>
        public int SparkleCount => _sparkleCount;

        /// <summary>所持金の表示が弾んでいる。</summary>
        public bool IsPunching => _punchAge >= 0f;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>演出中か（金貨・閃光・弾み・揺れのどれか。続くきらめきは含めない）。</summary>
        public bool IsPlaying => _activeCoins > 0 || _activePuffs > 0 || IsPunching || IsShaking;

        private void Awake()
        {
            _layer = (RectTransform)transform;
            _shake = BattleCameraShake.Of(battleCamera);
        }

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.CoinGained += OnGained;
            battle.EnemyDefeated += ShowDefeated;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.CoinGained -= OnGained;
            battle.EnemyDefeated -= ShowDefeated;
            battle.NodeBegan -= OnNodeBegan;
            EndPunch();
        }

        // 前のステージの金貨・閃光・きらめきを消す。
        private void OnNodeBegan()
        {
            for (var i = 0; i < MaxCoins; i++)
            {
                ReleaseCoin(i);
            }

            for (var i = 0; i < MaxPuffs; i++)
            {
                ReleasePuff(i);
            }

            SetSparkles(0);
            EndPunch();
        }

        private void OnAligned(SlotSymbol symbol)
        {
            var effects = battle.Field?.Effects;
            if (symbol != SlotSymbol.Coin || effects == null)
            {
                return;
            }

            PlayCount++;
            var delay = (float)effects.CoinDelaySteps / SimulationClock.StepsPerSecond;
            if (delay <= 0f)
            {
                delay = FallbackSeconds;
            }

            var reel = LocalRect(cabinet.ReelCenterCell(1));
            var monitor = BattleRect();
            var size = reel.height * coinSize;
            var count = Mathf.Max(1, fountainCoins);
            var homeStart = delay * absorbStart;
            var flight = delay * absorbFlight;
            var lastStart = Mathf.Max(homeStart, delay - flight);
            for (var i = 0; i < count; i++)
            {
                var u = count > 1 ? i / (float)(count - 1) : 0f;
                var launch = u * gushSeconds;
                // 横の広がりは順番を混ぜて、左右へ交互に散らす。
                var lane = ((i * 7) % count + 0.5f) / count;
                var apex = new Vector2(
                    monitor.center.x + (lane - 0.5f) * monitor.width * spread,
                    monitor.yMin + monitor.height * Random.Range(apexHeight.x, apexHeight.y));
                var rise = Random.Range(riseSeconds.x, riseSeconds.y);
                var from = reel.center + new Vector2(Random.Range(-0.3f, 0.3f) * reel.width, 0f);
                var gravity = 2f * Mathf.Max(1f, apex.y - from.y) / (rise * rise);
                // 吸い込みの順は打ち上げの順と同じ。最後の1枚が待ち時間ちょうどに着く。
                var homeAt = Mathf.Lerp(homeStart, lastStart, u);
                Spawn(false, from, new Vector2((apex.x - from.x) / rise, gravity * rise), gravity, launch, homeAt - launch, flight,
                    size * Random.Range(0.8f, 1.2f));
            }

            AddPuff(reel.center, Vector2.one * reel.height * launchFlashSize * 0.5f, Vector2.one * reel.height * launchFlashSize, 0.35f, flashColor);
            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        // Core が倍率を上げた: 所持金の表示を弾ませ、光の輪を広げる。
        private void OnGained()
        {
            GainCount++;
            var target = status.MoneyRect;
            var rect = LocalRect(target);
            AddPuff(rect.center, Vector2.one * rect.height * gainRingSize.x, Vector2.one * rect.height * gainRingSize.y, gainRingSeconds, flashColor);
            if (_punchAge < 0f)
            {
                _punchTarget = target;
                _punchBase = target.localScale;
            }

            _punchAge = 0f;
        }

        /// <summary>倍率が 1 より上の間、倒した敵から小さい金貨を所持金の表示へ飛ばす（倍率が高いほど多い）。敵を倒した通知とテストから呼ぶ。</summary>
        public void ShowDefeated(BattleEnemy enemy)
        {
            var effects = battle.Field?.Effects;
            if (effects == null || fieldView.Map == null)
            {
                return;
            }

            var count = Mathf.RoundToInt((float)((effects.MoneyRate - 1d) * 10d));
            if (count <= 0)
            {
                return;
            }

            var from = ToLayer(fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) + Vector3.up * 0.3f);
            var size = LocalRect(cabinet.ReelCenterCell(1)).height * coinSize * killCoinScale;
            var gravity = size * 40f;
            for (var i = 0; i < count && _activeKillCoins < maxKillCoins; i++)
            {
                var hop = new Vector2(Random.Range(-1f, 1f) * size * 4f, Random.Range(6f, 9f) * size);
                if (Spawn(true, from, hop, gravity, 0f, killHopSeconds + i * 0.05f, killFlightSeconds, size))
                {
                    KillCoinCount++;
                }
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var to = _activeCoins > 0 ? LocalRect(status.MoneyRect).center : Vector2.zero;
            for (var i = 0; i < MaxCoins; i++)
            {
                if (_coins[i].Active)
                {
                    UpdateCoin(i, dt, to);
                }
            }

            for (var i = 0; i < MaxPuffs; i++)
            {
                if (_puffs[i].Active)
                {
                    UpdatePuff(i, dt);
                }
            }

            UpdatePunch(dt);
        }

        private void UpdateCoin(int i, float dt, Vector2 to)
        {
            ref var coin = ref _coins[i];
            coin.Age += dt;
            var t = coin.Age - coin.LaunchDelay;
            var image = coin.Image;
            if (t < 0f)
            {
                image.enabled = false;
                return;
            }

            image.enabled = true;
            var rect = image.rectTransform;
            Vector2 position;
            var scale = 1f;
            if (t < coin.HomeAt)
            {
                position = coin.From + coin.Velocity * t + new Vector2(0f, -0.5f * coin.Gravity * t * t);
            }
            else
            {
                if (!coin.Homing)
                {
                    coin.Homing = true;
                    coin.HomeFrom = rect.anchoredPosition;
                }

                var f = Mathf.Clamp01((t - coin.HomeAt) / coin.HomeSeconds);
                if (f >= 1f)
                {
                    // 着いた: 小さくきらめいて消える。
                    var tiny = coin.Size * 0.8f;
                    AddPuff(to, Vector2.one * tiny, Vector2.one * tiny * 2.2f, 0.18f, flashColor);
                    ReleaseCoin(i);
                    return;
                }

                // 始めはゆっくり、だんだん速く吸い込まれる。
                var e = f * f;
                var line = to - coin.HomeFrom;
                var side = new Vector2(-line.y, line.x) * (arc * coin.Side);
                position = Vector2.Lerp(coin.HomeFrom, to, e) + side * (4f * e * (1f - e));
                scale = Mathf.Lerp(1f, 0.55f, e);
            }

            rect.anchoredPosition = position;
            // 回っているように横幅を変える（真横でも少し残す）。
            var width = Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(coin.SpinPhase + coin.SpinSpeed * t)));
            rect.sizeDelta = new Vector2(coin.Size * width * scale, coin.Size * scale);
        }

        private void UpdatePuff(int i, float dt)
        {
            ref var puff = ref _puffs[i];
            puff.Age += dt;
            var p = puff.Age / puff.Seconds;
            if (p >= 1f)
            {
                ReleasePuff(i);
                return;
            }

            var rect = puff.Image.rectTransform;
            rect.anchoredPosition = puff.Position;
            rect.sizeDelta = Vector2.one * Mathf.Lerp(puff.Size.x, puff.Size.y, 1f - (1f - p) * (1f - p));
            var color = puff.Color;
            color.a *= (1f - p) * (1f - p);
            puff.Image.color = color;
        }

        private void UpdatePunch(float dt)
        {
            if (_punchAge < 0f)
            {
                return;
            }

            _punchAge += dt;
            var p = _punchAge / punchSeconds;
            if (p >= 1f || _punchTarget == null)
            {
                EndPunch();
                return;
            }

            _punchTarget.localScale = _punchBase * (1f + (punchScale - 1f) * Mathf.Sin(Mathf.PI * p));
        }

        private void EndPunch()
        {
            if (_punchAge >= 0f && _punchTarget != null)
            {
                _punchTarget.localScale = _punchBase;
            }

            _punchAge = -1f;
        }

        // 倍率が 1 より上の間、所持金の表示の周りにきらめきを置く（数は倍率に合わせて 1〜5）。
        private void LateUpdate()
        {
            var effects = battle.Field?.Effects;
            var tenths = effects == null ? 0 : Mathf.RoundToInt((float)((effects.MoneyRate - 1d) * 10d));
            SetSparkles(Mathf.Clamp(tenths, 0, MaxSparkles));
            if (_sparkleCount == 0)
            {
                return;
            }

            var rect = LocalRect(status.MoneyRect);
            var radius = new Vector2(rect.width * 0.6f + rect.height * 0.3f, rect.height * 0.8f);
            for (var i = 0; i < _sparkleCount; i++)
            {
                var phase = i * 2.39996f; // 黄金角でばらす。
                var angle = phase + Time.time * 0.35f;
                var twinkle = Mathf.Sin(Time.time * Mathf.PI * 2f * sparkleRate + phase * 3f);
                twinkle = twinkle > 0f ? twinkle * twinkle : 0f;
                var image = _sparkles[i];
                var r = image.rectTransform;
                r.anchoredPosition = rect.center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                r.sizeDelta = Vector2.one * rect.height * sparkleSize * (0.4f + 0.6f * twinkle);
                r.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f + i * 30f);
                var color = sparkleColor;
                color.a *= twinkle;
                image.color = color;
            }
        }

        private void SetSparkles(int count)
        {
            if (count == _sparkleCount)
            {
                return;
            }

            for (var i = 0; i < MaxSparkles; i++)
            {
                var show = i < count;
                if (show && _sparkles[i] == null)
                {
                    _sparkles[i] = NewImage("Coin Sparkle", sparkleSprite);
                }

                if (_sparkles[i] != null && _sparkles[i].gameObject.activeSelf != show)
                {
                    _sparkles[i].gameObject.SetActive(show);
                }
            }

            _sparkleCount = count;
        }

        private bool Spawn(bool kill, Vector2 from, Vector2 velocity, float gravity, float launchDelay, float homeAt, float homeSeconds, float size)
        {
            for (var i = 0; i < MaxCoins; i++)
            {
                if (_coins[i].Active)
                {
                    continue;
                }

                ref var coin = ref _coins[i];
                if (coin.Image == null)
                {
                    coin.Image = NewImage("Coin", coinSprite);
                }

                coin.Active = true;
                coin.Kill = kill;
                coin.Homing = false;
                coin.Age = 0f;
                coin.LaunchDelay = launchDelay;
                coin.From = from;
                coin.Velocity = velocity;
                coin.Gravity = gravity;
                coin.HomeAt = Mathf.Max(0f, homeAt);
                coin.HomeSeconds = Mathf.Max(0.05f, homeSeconds);
                coin.Side = Random.Range(-1f, 1f);
                coin.Size = size;
                coin.SpinPhase = Random.Range(0f, Mathf.PI);
                coin.SpinSpeed = Random.Range(spinTurns.x, spinTurns.y) * Mathf.PI * 2f;
                var rect = coin.Image.rectTransform;
                rect.anchoredPosition = from;
                rect.sizeDelta = Vector2.one * size;
                coin.Image.color = Color.white;
                coin.Image.enabled = launchDelay <= 0f;
                coin.Image.gameObject.SetActive(true);
                _activeCoins++;
                if (kill)
                {
                    _activeKillCoins++;
                }

                return true;
            }

            return false;
        }

        private void ReleaseCoin(int i)
        {
            ref var coin = ref _coins[i];
            if (!coin.Active)
            {
                return;
            }

            coin.Active = false;
            coin.Image.gameObject.SetActive(false);
            _activeCoins--;
            if (coin.Kill)
            {
                _activeKillCoins--;
            }
        }

        private void AddPuff(Vector2 position, Vector2 from, Vector2 to, float seconds, Color color)
        {
            for (var i = 0; i < MaxPuffs; i++)
            {
                if (_puffs[i].Active)
                {
                    continue;
                }

                ref var puff = ref _puffs[i];
                if (puff.Image == null)
                {
                    puff.Image = NewImage("Coin Flash", glowSprite);
                }

                puff.Active = true;
                puff.Age = 0f;
                puff.Seconds = seconds;
                puff.Position = position;
                puff.Size = new Vector2(from.x, to.x);
                puff.Color = color;
                var rect = puff.Image.rectTransform;
                rect.anchoredPosition = position;
                rect.sizeDelta = from;
                puff.Image.color = color;
                puff.Image.gameObject.SetActive(true);
                _activePuffs++;
                return;
            }
        }

        private void ReleasePuff(int i)
        {
            ref var puff = ref _puffs[i];
            if (!puff.Active)
            {
                return;
            }

            puff.Active = false;
            puff.Image.gameObject.SetActive(false);
            _activePuffs--;
        }

        private Image NewImage(string name, Sprite sprite)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(_layer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = false;
            rect.gameObject.SetActive(false);
            return image;
        }

        // 戦闘モニター（戦闘カメラの写す範囲）をこの層のローカル座標（中心が原点）にする。
        private Rect BattleRect()
        {
            var pixels = battleCamera.pixelRect;
            var min = ScreenToLayer(pixels.min);
            var max = ScreenToLayer(pixels.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ToLayer(Vector3 world) => ScreenToLayer(battleCamera.WorldToScreenPoint(world));

        private Vector2 ScreenToLayer(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
            return local + _layer.rect.size * (_layer.pivot - new Vector2(0.5f, 0.5f));
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
    }
}
