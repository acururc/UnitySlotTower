using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 揃ったときの盤面全体の演出（FR-113、spec 002 E-28、plan 50行目、T613）。筐体が揃いを通知したら、盤面（3Dのフィールド）の
    /// 画面上の範囲に出目ごとの演出を重ねる（赤7は spec 003 の <see cref="Red7BreathEffect"/>、青7は spec 004 の <see cref="Blue7FreezeEffect"/> に移した）:
    /// 緑7＝味方から緑の粒が上がる、黄7＝盤面に黄緑の霧と敵に毒の色、バー＝味方の頭上に上向きの矢印、コイン＝上からコインが降る。
    /// どの出目でも盤面をうっすら染める。見た目だけで戦闘とレバーは止めない。時間は戦闘の時間（Time.deltaTime）で進む。
    /// 絵は仮（丸と三角）で、本番の絵は M5 で差し替える。部品はこの層の子の「Board」（盤面の範囲で切り抜く）に作って使い回す。
    /// </summary>
    public sealed class SlotBoardEffect : MonoBehaviour
    {
        private const int MaxEnemies = 24;
        private const int MaxUnits = 16;
        private const int ParticlesPerUnit = 6;
        private const int SoftSize = 64;
        private const int FogCount = 6;
        private const int CoinCount = 14;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [Tooltip("丸の絵（氷・毒・粒・コイン）。爆発と霧は縁のないぼかした丸（実行時に作る）。")]
        [SerializeField] private Sprite circleSprite;
        [Tooltip("盤面の範囲に含める高い地形の上面の高さ（ワールド）。")]
        [SerializeField, Min(0f)] private float raisedHeight = 0.5f;
        [Tooltip("演出の長さ（秒）。")]
        [SerializeField, Min(0.2f)] private float seconds = 1.1f;
        [Tooltip("どの出目でも盤面を染める一番濃いときの不透明度。青7・黄7はこれより濃い。")]
        [SerializeField, Range(0f, 1f)] private float tintAlpha = 0.14f;
        [SerializeField, Range(0f, 1f)] private float strongTintAlpha = 0.32f;
        [SerializeField] private Color iceColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color greenColor = new Color(0.45f, 1f, 0.5f);
        [SerializeField] private Color poisonColor = new Color(0.7f, 0.95f, 0.25f);
        [SerializeField] private Color arrowColor = new Color(1f, 0.9f, 0.35f);
        [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.2f);

        private readonly List<Image> _circles = new List<Image>();
        private readonly List<TriangleGraphic> _triangles = new List<TriangleGraphic>();
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _layer;
        private RectTransform _board;
        private Texture2D _softTexture;
        private Sprite _softSprite;
        private int _playing;

        /// <summary>演出中か（同時に複数あってもよい）。</summary>
        public bool IsPlaying => _playing > 0;

        /// <summary>これまでに始めた演出の数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>直近の演出の出目。</summary>
        public SlotSymbol? LastSymbol { get; private set; }

        /// <summary>出ている部品の数（テスト用。演出が終われば 0）。</summary>
        public int ActivePieceCount
        {
            get
            {
                var count = 0;
                foreach (var image in _circles) if (image.gameObject.activeSelf) count++;
                foreach (var triangle in _triangles) if (triangle.gameObject.activeSelf) count++;
                return count;
            }
        }

        /// <summary>盤面の範囲（この層のローカル座標、中心が原点）。直近の演出の始めに測る。</summary>
        public Rect BoardRect { get; private set; }

        private void Awake()
        {
            _layer = (RectTransform)transform;
            _board = new GameObject("Board", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _board.gameObject.layer = gameObject.layer;
            _board.SetParent(_layer, false);
            _board.anchorMin = _board.anchorMax = _board.pivot = new Vector2(0.5f, 0.5f);

            // 縁のないぼかした丸（中心が不透明で外へ向かって消える）。
            _softTexture = new Texture2D(SoftSize, SoftSize, TextureFormat.RGBA32, false)
            {
                name = "Board Soft Circle",
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[SoftSize * SoftSize];
            var half = SoftSize * 0.5f;
            for (var y = 0; y < SoftSize; y++)
            {
                for (var x = 0; x < SoftSize; x++)
                {
                    var d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    var a = Mathf.Clamp01(1f - d);
                    pixels[y * SoftSize + x] = new Color32(255, 255, 255, (byte)(255f * a * a * (3f - 2f * a)));
                }
            }

            _softTexture.SetPixels32(pixels);
            _softTexture.Apply(false, true);
            _softSprite = Sprite.Create(_softTexture, new Rect(0f, 0f, SoftSize, SoftSize), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            Destroy(_softSprite);
            Destroy(_softTexture);
        }

        private void OnEnable() => cabinet.Aligned += Play;

        private void OnDisable() => cabinet.Aligned -= Play;

        /// <summary>出目の演出を始める（筐体の揃いの通知と撮影から呼ぶ）。</summary>
        public void Play(SlotSymbol symbol)
        {
            // 赤7は息吹の演出（Red7BreathEffect、spec 003）、青7は凍結の演出（Blue7FreezeEffect、spec 004）が受け持つ。
            var field = battle.Field;
            if (symbol == SlotSymbol.Red7 || symbol == SlotSymbol.Blue7 || field == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            LastSymbol = symbol;
            _ = Run(symbol, field);
        }

        private async Awaitable Run(SlotSymbol symbol, Battlefield field)
        {
            _playing++;
            var pieces = new List<Graphic>();
            var token = destroyCancellationToken;
            try
            {
                MeasureBoard(out var cell);
                var size = BoardRect.size;
                var enemies = Snapshot(field.Enemies, MaxEnemies);
                var units = Snapshot(field.Units, MaxUnits);
                var strong = symbol == SlotSymbol.Yellow7;
                var color = ColorOf(symbol);
                var tint = TakeCircle(pieces, color, Vector2.zero, size, null);
                tint.sprite = null;
                Image[] actorMarks = null;
                Image[] extras = null;
                TriangleGraphic[] arrows = null;
                switch (symbol)
                {
                    case SlotSymbol.Yellow7:
                        actorMarks = TakeMany(pieces, enemies.Count, poisonColor);
                        extras = TakeMany(pieces, FogCount, poisonColor, _softSprite);
                        break;
                    case SlotSymbol.Green7:
                        extras = TakeMany(pieces, units.Count * ParticlesPerUnit, greenColor);
                        break;
                    case SlotSymbol.Bar:
                        arrows = new TriangleGraphic[units.Count];
                        for (var i = 0; i < arrows.Length; i++) arrows[i] = TakeTriangle(pieces, arrowColor, cell * 0.6f);
                        break;
                    case SlotSymbol.Coin:
                        extras = TakeMany(pieces, CoinCount, coinColor);
                        break;
                }

                for (var t = 0f; t < seconds; t += Time.deltaTime)
                {
                    var p = t / seconds;
                    // 盤面の色: すぐ濃くなり、ゆっくり消える。
                    var fade = Mathf.Sin(Mathf.PI * Mathf.Sqrt(p));
                    SetAlpha(tint, color, (strong ? strongTintAlpha : tintAlpha) * fade);
                    switch (symbol)
                    {
                        case SlotSymbol.Yellow7:
                            AnimateEnemyColor(actorMarks, enemies, poisonColor, p, cell);
                            AnimateFog(extras, p, size);
                            break;
                        case SlotSymbol.Green7:
                            AnimateParticles(extras, units, p, cell);
                            break;
                        case SlotSymbol.Bar:
                            AnimateArrows(arrows, units, p, cell);
                            break;
                        case SlotSymbol.Coin:
                            AnimateCoins(extras, p, size, cell);
                            break;
                    }

                    await Awaitable.NextFrameAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // シーンを離れた: 層ごと消えるので片付けない。
                return;
            }
            finally
            {
                _playing--;
            }

            foreach (var piece in pieces)
            {
                piece.gameObject.SetActive(false);
            }
        }

        // 敵に色を重ねる（青7＝氷、黄7＝毒）。敵を追い、最後に消える。
        private void AnimateEnemyColor(Image[] marks, List<BattleEnemy> enemies, Color color, float p, float cell)
        {
            var alpha = 0.75f * Mathf.Clamp01(p / 0.15f) * Mathf.Clamp01((1f - p) / 0.3f);
            for (var i = 0; i < marks.Length; i++)
            {
                if (!enemies[i].IsAlive)
                {
                    SetAlpha(marks[i], color, 0f);
                    continue;
                }

                marks[i].rectTransform.anchoredPosition = EnemyPosition(enemies[i]);
                marks[i].rectTransform.sizeDelta = Vector2.one * cell * (1.05f + 0.1f * Mathf.Sin(p * 12f + i));
                SetAlpha(marks[i], color, alpha);
            }
        }

        // 黄緑の霧: 大きな丸が盤面の決まった位置からゆっくり流れる。
        private void AnimateFog(Image[] fogs, float p, Vector2 size)
        {
            var alpha = 0.55f * Mathf.Sin(Mathf.PI * p);
            for (var i = 0; i < fogs.Length; i++)
            {
                var u = (i + 0.5f) / fogs.Length;
                var start = new Vector2((u - 0.5f) * size.x, ((i % 3) / 2f - 0.5f) * size.y * 0.7f);
                var drift = new Vector2((i % 2 == 0 ? 1f : -1f) * size.x * 0.08f, size.y * 0.04f) * p;
                fogs[i].rectTransform.anchoredPosition = start + drift;
                fogs[i].rectTransform.sizeDelta = Vector2.one * Mathf.Max(size.x, size.y) * (0.45f + 0.1f * p);
                SetAlpha(fogs[i], poisonColor, alpha);
            }
        }

        // 緑の粒: 味方ごとにいくつかの粒が少しずつずれて上がって消える。
        private void AnimateParticles(Image[] particles, List<BattleUnit> units, float p, float cell)
        {
            for (var i = 0; i < particles.Length; i++)
            {
                var unit = units[i / ParticlesPerUnit];
                var n = i % ParticlesPerUnit;
                var k = Mathf.Clamp01((p - n * 0.07f) / 0.6f);
                if (!unit.IsAlive || k <= 0f || k >= 1f)
                {
                    SetAlpha(particles[i], greenColor, 0f);
                    continue;
                }

                var basePosition = ToBoard(fieldView.CellTopToWorld(unit.Cell));
                var side = (n - (ParticlesPerUnit - 1) * 0.5f) * cell * 0.16f;
                particles[i].rectTransform.anchoredPosition = basePosition + new Vector2(side, cell * (0.2f + 1.3f * k + 0.15f * (n % 2)));
                particles[i].rectTransform.sizeDelta = Vector2.one * cell * 0.26f;
                SetAlpha(particles[i], greenColor, Mathf.Sin(Mathf.PI * k));
            }
        }

        // 上向きの矢印: 味方の頭上に出て少し上がりながら消える。
        private void AnimateArrows(TriangleGraphic[] arrows, List<BattleUnit> units, float p, float cell)
        {
            for (var i = 0; i < arrows.Length; i++)
            {
                if (!units[i].IsAlive)
                {
                    SetAlpha(arrows[i], arrowColor, 0f);
                    continue;
                }

                var basePosition = ToBoard(fieldView.CellTopToWorld(units[i].Cell));
                arrows[i].rectTransform.anchoredPosition = basePosition + new Vector2(0f, cell * (0.9f + 0.5f * p));
                SetAlpha(arrows[i], arrowColor, Mathf.Clamp01(p / 0.15f) * Mathf.Clamp01((1f - p) / 0.35f));
            }
        }

        // コイン: 盤面の上から少しずつずれて降り、左右に揺れる。
        private void AnimateCoins(Image[] coins, float p, Vector2 size, float cell)
        {
            for (var i = 0; i < coins.Length; i++)
            {
                var u = (i + 0.5f) / coins.Length;
                var delay = (i * 5 % coins.Length) / (float)coins.Length * 0.4f;
                var k = Mathf.Clamp01((p - delay) / 0.6f);
                var x = (u - 0.5f) * size.x * 0.9f + Mathf.Sin(k * 9f + i) * cell * 0.15f;
                var y = Mathf.Lerp(size.y * 0.5f + cell, -size.y * 0.5f - cell, k * k);
                coins[i].rectTransform.anchoredPosition = new Vector2(x, y);
                // 回っているように横幅を変える。
                coins[i].rectTransform.sizeDelta = new Vector2(cell * 0.4f * Mathf.Abs(Mathf.Cos(k * 10f + i)), cell * 0.4f);
                SetAlpha(coins[i], coinColor, k > 0f && k < 1f ? 1f : 0f);
            }
        }

        private Vector2 EnemyPosition(BattleEnemy enemy) =>
            ToBoard(fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir));

        // 盤面の四隅を画面に写して、その外接矩形を「Board」の範囲にする。マス1つの画面上の幅も返す。
        private void MeasureBoard(out float cell)
        {
            var map = fieldView.Map;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            // 地面の四隅と、高い地形の上面の高さの四隅（上の段の箱が範囲からはみ出さないように）。
            for (var i = 0; i < 8; i++)
            {
                var x = (i & 1) == 0 ? -0.5 : map.Width - 0.5;
                var y = (i & 2) == 0 ? -0.5 : map.Height - 0.5;
                var world = fieldView.PositionToWorld(x, y, false) + ((i & 4) == 0 ? Vector3.zero : Vector3.up * raisedHeight);
                var point = ToLayer(world);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            // 横幅いっぱいに寄せたカメラ（D-87）では四隅が層の端からわずかにはみ出すので、層の中に収める（T614 の外枠に重ねない）。
            var layer = _layer.rect;
            min = Vector2.Max(min, layer.min);
            max = Vector2.Min(max, layer.max);
            BoardRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            _board.anchoredPosition = BoardRect.center;
            _board.sizeDelta = BoardRect.size;
            cell = BoardRect.width / Mathf.Max(1, map.Width);
        }

        private Vector2 ToLayer(Vector3 world)
        {
            var screen = battleCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
            var offset = _layer.rect.size * (_layer.pivot - new Vector2(0.5f, 0.5f));
            return local + offset;
        }

        private Vector2 ToBoard(Vector3 world) => ToLayer(world) - BoardRect.center;

        private Color ColorOf(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Blue7: return iceColor;
                case SlotSymbol.Green7: return greenColor;
                case SlotSymbol.Yellow7: return poisonColor;
                case SlotSymbol.Bar: return arrowColor;
                default: return coinColor;
            }
        }

        private static List<T> Snapshot<T>(IReadOnlyList<T> source, int max)
        {
            var list = new List<T>(Mathf.Min(source.Count, max));
            for (var i = 0; i < source.Count && list.Count < max; i++) list.Add(source[i]);
            return list;
        }

        private Image[] TakeMany(List<Graphic> pieces, int count, Color color, Sprite sprite = null)
        {
            var images = new Image[count];
            for (var i = 0; i < count; i++)
            {
                images[i] = TakeCircle(pieces, color, Vector2.zero, Vector2.zero, sprite);
                SetAlpha(images[i], color, 0f);
            }

            return images;
        }

        private Image TakeCircle(List<Graphic> pieces, Color color, Vector2 position, Vector2 size, Sprite sprite)
        {
            Image image = null;
            foreach (var candidate in _circles)
            {
                if (!candidate.gameObject.activeSelf)
                {
                    image = candidate;
                    break;
                }
            }

            if (image == null)
            {
                image = CreatePiece("Board Light").AddComponent<Image>();
                image.raycastTarget = false;
                _circles.Add(image);
            }

            image.sprite = sprite != null ? sprite : circleSprite;
            Place(image, color, position, size, pieces);
            return image;
        }

        private TriangleGraphic TakeTriangle(List<Graphic> pieces, Color color, float size)
        {
            TriangleGraphic triangle = null;
            foreach (var candidate in _triangles)
            {
                if (!candidate.gameObject.activeSelf)
                {
                    triangle = candidate;
                    break;
                }
            }

            if (triangle == null)
            {
                triangle = CreatePiece("Board Arrow").AddComponent<TriangleGraphic>();
                triangle.raycastTarget = false;
                triangle.Pointing = TriangleGraphic.Direction.Up;
                _triangles.Add(triangle);
            }

            Place(triangle, color, Vector2.zero, new Vector2(size, size), pieces);
            SetAlpha(triangle, color, 0f);
            return triangle;
        }

        private GameObject CreatePiece(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(_board, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect.gameObject;
        }

        private static void Place(Graphic graphic, Color color, Vector2 position, Vector2 size, List<Graphic> pieces)
        {
            graphic.color = color;
            graphic.rectTransform.anchoredPosition = position;
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.SetAsLastSibling();
            graphic.gameObject.SetActive(true);
            pieces.Add(graphic);
        }

        private static void SetAlpha(Graphic graphic, Color color, float alpha)
        {
            color.a *= alpha;
            graphic.color = color;
        }
    }
}
