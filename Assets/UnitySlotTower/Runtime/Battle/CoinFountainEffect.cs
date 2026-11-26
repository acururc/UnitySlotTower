using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// コインの噴水の演出と、倒した敵のドロップが表示へ飛ぶ演出（spec 009 FR-001・FR-002・FR-005・FR-006・FR-009、plan 2章・7章）。
    /// 金貨は筐体（下）→戦闘モニター（上）→上端の帯をまたぐので、<see cref="SlotAlignEffect"/> と同じ UI Canvas いっぱいの透明な層に UI の Image で描く。
    /// 揃うと中段の真ん中のマスから金貨が噴き上がって戦闘カメラが弱く揺れ（ヒットストップなし）、金貨は回りながら降ってからコインの倍率の表示へ吸い込まれる。
    /// 最後の金貨は Core が倍率を上げる瞬間（<see cref="BattleController.CoinGained"/>、揃ってから coin_delay_seconds）に着き、そこで倍率の文字が弾む。
    /// 倍率が 1 より上の間は倍率の表示の周りがきらめく。敵を倒すと（<see cref="BattleController.EnemyDefeated"/>）、落とした所持金は緑のお札が所持金の数字へ、
    /// スロットコインは金色のコインが台のコインの数へ飛び、着いた瞬間にその分だけ数字が増えて弾む（飛んでいる間は表示に入れない）。
    /// 時間は戦闘の時間（Time.deltaTime）。部品はこの層の子に作って使い回す。絵は Setup（Build Coin Fountain Effect）が作る自作の PNG。
    /// </summary>
    public sealed class CoinFountainEffect : MonoBehaviour
    {
        private const int MaxCoins = 160;
        private const int MaxPuffs = 48;
        private const int MaxSparkles = 5;

        // 待ちが 0 のとき（倍率はもう上がっている）に噴水を見せる長さ（秒）。
        private const float FallbackSeconds = 1.2f;

        private const int KindFountain = 0;
        private const int KindBill = 1;
        private const int KindDropCoin = 2;

        private const int PunchRate = 0;
        private const int PunchMoney = 1;
        private const int PunchCredit = 2;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private OperationBarStatus status;
        [SerializeField] private Camera battleCamera;

        [Header("絵（Setup が入れる）")]
        [SerializeField] private Sprite coinSprite;
        [Tooltip("所持金のお札（MoneyIconArt と同じ緑）。")]
        [SerializeField] private Sprite billSprite;
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
        [Tooltip("1枚が倍率の表示まで飛ぶ時間（待ち時間に対する割合）。")]
        [SerializeField, Range(0.05f, 0.5f)] private float absorbFlight = 0.33f;
        [Tooltip("飛ぶ道の膨らみ（距離に対する割合）。")]
        [SerializeField, Range(0f, 0.5f)] private float arc = 0.15f;

        [Header("山場（倍率が上がる瞬間）")]
        [SerializeField, Min(0.05f)] private float punchSeconds = 0.3f;
        [SerializeField, Min(1f)] private float punchScale = 1.5f;
        [Tooltip("光の輪の大きさ（倍率の表示の高さ = 1、始め → 終わり）と長さ（秒）。")]
        [SerializeField] private Vector2 gainRingSize = new Vector2(1f, 5f);
        [SerializeField, Min(0.05f)] private float gainRingSeconds = 0.45f;

        [Header("倒したとき（お札・コイン）")]
        [Tooltip("同時に出ている、倒したときのお札とコインの合計の上限（重くしない、FR-005）。超えた分は飛ばさずすぐ表示に入れる。")]
        [SerializeField, Min(0)] private int maxKillCoins = 48;
        [Tooltip("倒したときのお札・コインの大きさ（噴水の金貨に対する割合）。")]
        [SerializeField, Min(0f)] private float killCoinScale = 0.6f;
        [Tooltip("飛び出してから表示へ向かうまで（秒）と、飛ぶ時間（秒）。")]
        [SerializeField, Min(0f)] private float killHopSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float killFlightSeconds = 0.45f;
        [Tooltip("着いたときに数字を弾ませる時間（秒）と倍率。")]
        [SerializeField, Min(0.05f)] private float arrivePunchSeconds = 0.15f;
        [SerializeField, Min(1f)] private float arrivePunchScale = 1.2f;
        [SerializeField] private Color billFlashColor = new Color(0.55f, 1f, 0.6f, 0.8f);

        [Header("きらめき")]
        [Tooltip("倍率の表示の周りのきらめきの大きさ（倍率の表示の高さ = 1）と点滅の速さ（毎秒）。続く効果なので控えめに。")]
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
            public int Kind;
            public int Amount;
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

        private struct Punch
        {
            public RectTransform Target;
            public Vector3 Base;
            public float Age;
            public float Seconds;
            public float Scale;
        }

        private readonly Coin[] _coins = new Coin[MaxCoins];
        private readonly Puff[] _puffs = new Puff[MaxPuffs];
        private readonly Punch[] _punches = new Punch[3];
        private readonly Image[] _sparkles = new Image[MaxSparkles];
        private readonly Vector2[] _targets = new Vector2[3];
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _layer;
        private BattleCameraShake _shake;
        private int _activeCoins;
        private int _activeFountainCoins;
        private int _activeKillCoins;
        private int _activePuffs;
        private int _sparkleCount;

        /// <summary>噴水を始めた回数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>倍率が上がって倍率の表示が弾んだ回数（テスト用）。</summary>
        public int GainCount { get; private set; }

        /// <summary>倒した敵から飛ばしたお札とコインの枚数の合計（テスト用）。</summary>
        public int KillCoinCount { get; private set; }

        /// <summary>倒した敵から飛ばしたお札の枚数の合計（テスト用）。</summary>
        public int BillCount { get; private set; }

        /// <summary>出ている金貨・お札の数（噴水と倒したときの両方）。</summary>
        public int ActiveCoinCount => _activeCoins;

        /// <summary>出ている噴水の金貨の数。</summary>
        public int ActiveFountainCoinCount => _activeFountainCoins;

        /// <summary>出ている、倒したときのお札とコインの数。</summary>
        public int ActiveKillCoinCount => _activeKillCoins;

        /// <summary>同時に出せる、倒したときのお札とコインの上限。</summary>
        public int MaxKillCoins => maxKillCoins;

        /// <summary>出ている倍率の表示の周りのきらめきの数（倍率が 1 より上の間だけ 0 より大きい）。</summary>
        public int SparkleCount => _sparkleCount;

        /// <summary>倍率の文字が弾んでいる。</summary>
        public bool IsPunching => _punches[PunchRate].Age >= 0f && _punches[PunchRate].Target != null;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>噴水の演出中か（噴水の金貨・倍率の弾み・揺れ）。倒したときのお札とコインは含めない。</summary>
        public bool IsFountainPlaying => _activeFountainCoins > 0 || IsPunching || IsShaking;

        /// <summary>演出中か（金貨・お札・閃光・弾み・揺れのどれか。続くきらめきは含めない）。</summary>
        public bool IsPlaying => _activeCoins > 0 || _activePuffs > 0 || AnyPunch() || IsShaking;

        private void Awake()
        {
            _layer = (RectTransform)transform;
            _shake = BattleCameraShake.Of(battleCamera);
            for (var i = 0; i < _punches.Length; i++)
            {
                _punches[i].Age = -1f;
            }
        }

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.CoinGained += OnGained;
            battle.EnemyDefeated += ShowDefeated;
            battle.NodeBegan += Clear;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.CoinGained -= OnGained;
            battle.EnemyDefeated -= ShowDefeated;
            battle.NodeBegan -= Clear;
            // シーンを離れるときは部品が先に消えていることがあるので、表示の持ち越しと数字の大きさだけ戻す。
            for (var i = 0; i < _punches.Length; i++)
            {
                EndPunch(i);
            }

            status.MoneyHeldBack = 0;
            cabinet.CreditHeldBack = 0;
        }

        // 前のステージの金貨・お札・閃光・きらめきを消し、表示を実際の値にそろえる（FR-009）。
        private void Clear()
        {
            for (var i = 0; i < MaxCoins; i++)
            {
                ReleaseCoin(i);
            }

            for (var i = 0; i < MaxPuffs; i++)
            {
                ReleasePuff(i);
            }

            for (var i = 0; i < _punches.Length; i++)
            {
                EndPunch(i);
            }

            SetSparkles(0);
            status.MoneyHeldBack = 0;
            cabinet.CreditHeldBack = 0;
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
                Spawn(KindFountain, 0, from, new Vector2((apex.x - from.x) / rise, gravity * rise), gravity, launch, homeAt - launch, flight,
                    size * Random.Range(0.8f, 1.2f));
            }

            AddPuff(reel.center, Vector2.one * reel.height * launchFlashSize * 0.5f, Vector2.one * reel.height * launchFlashSize, 0.35f, flashColor);
            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        // Core が倍率を上げた: 倍率の文字を弾ませ、光の輪を広げる。
        private void OnGained()
        {
            GainCount++;
            var rect = LocalRect(status.MoneyRateRect);
            AddPuff(rect.center, Vector2.one * rect.height * gainRingSize.x, Vector2.one * rect.height * gainRingSize.y, gainRingSeconds, flashColor);
            StartPunch(PunchRate, status.MoneyRateTextRect, punchSeconds, punchScale);
        }

        /// <summary>
        /// 倒した敵の場所から、落とした所持金はお札（1 枚＋倍率で増える）を所持金の数字へ、スロットコインは枚数分のコインを台のコインの数へ飛ばす。
        /// 飛んでいる分は表示に入れない（FR-009）。同時の上限を超えた分は飛ばさない。敵を倒した通知とテストから呼ぶ。
        /// </summary>
        public void ShowDefeated(BattleEnemy enemy, int money, int coins)
        {
            var effects = battle.Field?.Effects;
            if (fieldView.Map == null || (money <= 0 && coins <= 0))
            {
                return;
            }

            var from = ToLayer(fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) + Vector3.up * 0.3f);
            var size = LocalRect(cabinet.ReelCenterCell(1)).height * coinSize * killCoinScale;
            var gravity = size * 40f;
            var order = 0;
            if (money > 0)
            {
                var rate = effects?.MoneyRate ?? 1d;
                var bills = 1 + Mathf.Max(0, Mathf.RoundToInt((float)((rate - 1d) * 10d)));
                for (var i = 0; i < bills; i++)
                {
                    // 所持金を枚数で分けて持たせる（端数は最初のお札）。
                    var share = money / bills + (i == 0 ? money % bills : 0);
                    if (share > 0 && SpawnDrop(KindBill, share, from, size, gravity, order++))
                    {
                        BillCount++;
                        status.MoneyHeldBack += share;
                    }
                }
            }

            for (var i = 0; i < coins; i++)
            {
                if (SpawnDrop(KindDropCoin, 1, from, size, gravity, order++))
                {
                    cabinet.CreditHeldBack += 1;
                }
            }
        }

        private bool SpawnDrop(int kind, int amount, Vector2 from, float size, float gravity, int order)
        {
            if (_activeKillCoins >= maxKillCoins)
            {
                return false;
            }

            var hop = new Vector2(Random.Range(-1f, 1f) * size * 4f, Random.Range(6f, 9f) * size);
            if (!Spawn(kind, amount, from, hop, gravity, 0f, killHopSeconds + order * 0.05f, killFlightSeconds, size))
            {
                return false;
            }

            KillCoinCount++;
            return true;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_activeCoins > 0)
            {
                _targets[KindFountain] = LocalRect(status.MoneyRateRect).center;
                _targets[KindBill] = LocalRect(status.MoneyRect).center;
                _targets[KindDropCoin] = LocalRect(cabinet.CreditRect).center;
            }

            for (var i = 0; i < MaxCoins; i++)
            {
                if (_coins[i].Active)
                {
                    UpdateCoin(i, dt);
                }
            }

            for (var i = 0; i < MaxPuffs; i++)
            {
                if (_puffs[i].Active)
                {
                    UpdatePuff(i, dt);
                }
            }

            for (var i = 0; i < _punches.Length; i++)
            {
                UpdatePunch(i, dt);
            }
        }

        private void UpdateCoin(int i, float dt)
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
            var to = _targets[coin.Kind];
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
                    Arrive(i, to);
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
            // 金貨は横に回り、お札は縦に裏返るように見せる（真横でも少し残す）。
            var turn = Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(coin.SpinPhase + coin.SpinSpeed * t)));
            rect.sizeDelta = coin.Kind == KindBill
                ? new Vector2(coin.Size * 1.6f * scale, coin.Size * turn * scale)
                : new Vector2(coin.Size * turn * scale, coin.Size * scale);
        }

        // 行き先に着いた: 小さくきらめき、お札とコインは持っていた分を表示に入れて数字を弾ませる。
        private void Arrive(int i, Vector2 to)
        {
            ref var coin = ref _coins[i];
            var tiny = coin.Size * 0.8f;
            switch (coin.Kind)
            {
                case KindBill:
                    status.MoneyHeldBack = Mathf.Max(0, status.MoneyHeldBack - coin.Amount);
                    StartPunch(PunchMoney, status.MoneyRect, arrivePunchSeconds, arrivePunchScale);
                    AddPuff(to, Vector2.one * tiny, Vector2.one * tiny * 2.2f, 0.18f, billFlashColor);
                    break;
                case KindDropCoin:
                    cabinet.CreditHeldBack = Mathf.Max(0, cabinet.CreditHeldBack - coin.Amount);
                    StartPunch(PunchCredit, cabinet.CreditRect, arrivePunchSeconds, arrivePunchScale);
                    AddPuff(to, Vector2.one * tiny, Vector2.one * tiny * 2.2f, 0.18f, flashColor);
                    break;
                default:
                    AddPuff(to, Vector2.one * tiny, Vector2.one * tiny * 2.2f, 0.18f, flashColor);
                    break;
            }

            ReleaseCoin(i);
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

        // 数字を一瞬大きくして戻す。弾んでいる最中にもう一度来たら、元の大きさは覚えたまま最初から弾ませる。
        private void StartPunch(int slot, RectTransform target, float seconds, float scale)
        {
            ref var punch = ref _punches[slot];
            if (punch.Age < 0f || punch.Target != target)
            {
                EndPunch(slot);
                punch.Target = target;
                punch.Base = target.localScale;
            }

            punch.Age = 0f;
            punch.Seconds = seconds;
            punch.Scale = scale;
        }

        private void UpdatePunch(int slot, float dt)
        {
            ref var punch = ref _punches[slot];
            if (punch.Age < 0f)
            {
                return;
            }

            punch.Age += dt;
            var p = punch.Age / punch.Seconds;
            if (p >= 1f || punch.Target == null)
            {
                EndPunch(slot);
                return;
            }

            punch.Target.localScale = punch.Base * (1f + (punch.Scale - 1f) * Mathf.Sin(Mathf.PI * p));
        }

        private void EndPunch(int slot)
        {
            ref var punch = ref _punches[slot];
            if (punch.Age >= 0f && punch.Target != null)
            {
                punch.Target.localScale = punch.Base;
            }

            punch.Age = -1f;
        }

        private bool AnyPunch()
        {
            for (var i = 0; i < _punches.Length; i++)
            {
                if (_punches[i].Age >= 0f)
                {
                    return true;
                }
            }

            return false;
        }

        // 倍率が 1 より上の間、倍率の表示の周りにきらめきを置く（数は倍率に合わせて 1〜5）。
        private void LateUpdate()
        {
            var effects = battle.Field?.Effects;
            var tenths = effects == null ? 0 : Mathf.RoundToInt((float)((effects.MoneyRate - 1d) * 10d));
            SetSparkles(Mathf.Clamp(tenths, 0, MaxSparkles));
            if (_sparkleCount == 0)
            {
                return;
            }

            var rect = LocalRect(status.MoneyRateRect);
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

        private bool Spawn(int kind, int amount, Vector2 from, Vector2 velocity, float gravity, float launchDelay, float homeAt, float homeSeconds, float size)
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

                coin.Image.sprite = kind == KindBill ? billSprite : coinSprite;
                coin.Active = true;
                coin.Kind = kind;
                coin.Amount = amount;
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
                if (kind == KindFountain)
                {
                    _activeFountainCoins++;
                }
                else
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
            if (coin.Kind == KindFountain)
            {
                _activeFountainCoins--;
            }
            else
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
