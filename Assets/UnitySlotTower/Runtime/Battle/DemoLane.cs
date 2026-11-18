using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 文字なしの説明の短いループのアニメ（T385、FR-072、plan 18.5）。左に本拠地（青）、その右に味方、右から敵が歩いてくる1本の道。
    /// <see cref="Block"/>: 止められる数だけ敵が味方の前で止まり、残りの1体は通り抜けて本拠地が赤く光る（足止めの説明・タンクのスキル）。
    /// <see cref="Hit"/>: 1体の敵を一定の間隔で殴り（遠距離は弾を飛ばし）、体力のバーが減る（攻撃のスキル）。
    /// 一時停止中・説明中も動くよう unscaled の時間で進める。絵は仮（M5 で素材に差し替える）。
    /// </summary>
    public sealed class DemoLane : MonoBehaviour
    {
        public const float AllyLeft = 0.13f;
        public const float AllyRight = 0.29f;

        private static readonly Color HomeColor = new Color(0.25f, 0.6f, 0.95f);
        private static readonly Color HomeHitColor = new Color(0.95f, 0.25f, 0.2f);
        private static readonly Color EnemyColor = new Color(0.6f, 0.15f, 0.3f);
        private static readonly Color HpBackColor = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color HpColor = new Color(0.88f, 0.27f, 0.23f);
        private static readonly Color ShotColor = new Color(1f, 0.9f, 0.3f);

        private const float EnemyWidth = 0.08f;
        private const float WalkSpeed = 0.45f; // 道の幅 / 秒
        private const float SpawnGap = 0.4f;
        private const float HoldSeconds = 0.9f;
        private const float ShotSeconds = 0.18f;
        private const float DeadSeconds = 0.5f;

        private enum Mode { Block, Hit }

        private Mode _mode;
        private RectTransform _ally;
        private Image _home;
        private readonly List<RectTransform> _enemies = new List<RectTransform>();
        private int _stopCount;
        private float _cycle;
        private float _time;

        // Hit
        private float _interval;
        private float _damage;
        private Image _hpFill;
        private RectTransform _shot;
        private float _hp;
        private float _nextHit;
        private float _deadUntil = -1f;
        private float _stopX;

        /// <summary>止められる数を見せる道。敵は enemyCount 体（省略時は stopCount＋1。0 なら1体が素通り）。</summary>
        public static DemoLane Block(RectTransform parent, RectTransform allyArt, Color allyColor, int stopCount, int enemyCount = -1)
        {
            var lane = Create(parent, allyArt, allyColor, Mode.Block);
            lane._stopCount = Mathf.Clamp(stopCount, 0, 5);
            var count = enemyCount > 0 ? Mathf.Min(enemyCount, 6) : lane._stopCount + 1;
            for (var i = 0; i < count; i++)
            {
                lane._enemies.Add(Enemy(lane.transform as RectTransform, i));
            }

            lane._cycle = (count - 1) * SpawnGap + (1f - AllyLeft) / WalkSpeed + HoldSeconds;
            lane.Apply();
            return lane;
        }

        /// <summary>1体の敵を interval 秒ごとに殴り、1回で体力の damage（割合）を減らす道。ranged は弾を飛ばし、敵は離れて止まる。</summary>
        public static DemoLane Hit(RectTransform parent, RectTransform allyArt, Color allyColor, float interval, float damage,
            bool ranged)
        {
            var lane = Create(parent, allyArt, allyColor, Mode.Hit);
            var rect = (RectTransform)lane.transform;
            lane._interval = Mathf.Max(0.1f, interval);
            lane._damage = Mathf.Clamp(damage, 0.02f, 1f);
            lane._stopX = ranged ? 0.66f : AllyRight + 0.01f;
            var enemy = Enemy(rect, 0);
            lane._enemies.Add(enemy);
            var back = SkillPictures.Image("Hp Back", enemy, new Vector2(-0.1f, 1.08f), new Vector2(1.1f, 1.22f), HpBackColor, null);
            lane._hpFill = SkillPictures.Image("Hp", back.rectTransform, Vector2.zero, Vector2.one, HpColor, null);
            lane._hpFill.type = UnityEngine.UI.Image.Type.Filled;
            lane._hpFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            if (ranged)
            {
                lane._shot = SkillPictures.Image("Shot", rect, Vector2.zero, Vector2.zero, ShotColor, null).rectTransform;
            }

            lane.Restart();
            lane.Apply();
            return lane;
        }

        /// <summary>味方の絵の矩形（飾りを足すため）。</summary>
        public RectTransform Ally => _ally;

        private static DemoLane Create(RectTransform parent, RectTransform allyArt, Color allyColor, Mode mode)
        {
            var rect = SkillPictures.Rect("Demo Lane", parent, Vector2.zero, Vector2.one);
            SkillPictures.Image("Road", rect, new Vector2(0f, 0.12f), new Vector2(1f, 0.2f), new Color(1f, 1f, 1f, 0.12f), null);
            var lane = rect.gameObject.AddComponent<DemoLane>();
            lane._mode = mode;
            lane._home = SkillPictures.Image("Home", rect, new Vector2(0f, 0.12f), new Vector2(0.05f, 0.8f), HomeColor, null);
            lane._ally = SkillPictures.Rect("Ally", rect, new Vector2(AllyLeft, 0.12f), new Vector2(AllyRight, 0.95f));
            if (allyArt != null)
            {
                // 札と同じ: 職の色の下地の上に職の形（Job Art）。
                var back = SkillPictures.Image("Back", lane._ally, Vector2.zero, Vector2.one, allyColor, null);
                var art = (RectTransform)Instantiate(allyArt, back.rectTransform, false).transform;
                art.name = "Art";
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.offsetMin = art.offsetMax = Vector2.zero;
                foreach (var graphic in art.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }
            else
            {
                SkillPictures.Image("Art", lane._ally, new Vector2(0.15f, 0f), new Vector2(0.85f, 0.8f), HomeColor, null);
            }

            return lane;
        }

        // 赤紫の箱に白い目（説明の絵の敵と同じ形）。
        private static RectTransform Enemy(RectTransform lane, int index)
        {
            var body = SkillPictures.Image($"Enemy {index + 1}", lane, Vector2.zero, Vector2.zero, EnemyColor, null);
            SkillPictures.Image("Eye Left", body.rectTransform, new Vector2(0.18f, 0.5f), new Vector2(0.4f, 0.78f), Color.white, null);
            SkillPictures.Image("Eye Right", body.rectTransform, new Vector2(0.6f, 0.5f), new Vector2(0.82f, 0.78f), Color.white, null);
            return body.rectTransform;
        }

        private void OnEnable()
        {
            _time = 0f;
            if (_mode == Mode.Hit)
            {
                Restart();
            }
        }

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _time += dt;
            if (_mode == Mode.Hit)
            {
                StepHit();
            }

            Apply();
        }

        private void Apply()
        {
            if (_mode == Mode.Block)
            {
                ApplyBlock(Mathf.Repeat(_time, _cycle));
            }
            else
            {
                ApplyHit();
            }
        }

        private void ApplyBlock(float t)
        {
            var homeHit = false;
            for (var i = 0; i < _enemies.Count; i++)
            {
                var walked = t - i * SpawnGap;
                var x = 1f - WalkSpeed * Mathf.Max(0f, walked);
                var y = 0.2f;
                var visible = walked >= 0f;
                if (i < _stopCount)
                {
                    // 止まった敵は味方の前に少しずつずらして並ぶ。
                    var stop = AllyRight + 0.005f + i * 0.045f;
                    if (x <= stop)
                    {
                        x = stop;
                        y = 0.2f + (i % 2 == 0 ? 0f : 0.14f);
                    }
                }
                else if (x <= 0.05f)
                {
                    // 通り抜けた敵は本拠地に着いたら消え、本拠地が赤く光る。
                    visible = false;
                    homeHit = walked >= 0f;
                }

                Place(_enemies[i], x, y, visible);
            }

            _home.color = homeHit ? HomeHitColor : HomeColor;
        }

        private void Restart()
        {
            _hp = 1f;
            _time = 0f;
            _deadUntil = -1f;
            _nextHit = (1f - _stopX) / WalkSpeed;
        }

        private void StepHit()
        {
            if (_deadUntil >= 0f)
            {
                if (_time >= _deadUntil)
                {
                    Restart();
                }

                return;
            }

            if (_time >= _nextHit)
            {
                _hp -= _damage;
                _nextHit += _interval;
                if (_hp <= 0.001f)
                {
                    _hp = 0f;
                    _deadUntil = _time + DeadSeconds;
                }
            }
        }

        private void ApplyHit()
        {
            var enemy = _enemies[0];
            var x = Mathf.Max(_stopX, 1f - WalkSpeed * _time);
            Place(enemy, x, 0.2f, _deadUntil < 0f);
            _hpFill.fillAmount = _hp;

            // 殴った直後は味方が少し大きく、敵が白く光る。
            var sinceHit = _time - (_nextHit - _interval);
            var arrived = x <= _stopX + 0.0001f;
            var flash = arrived && sinceHit >= 0f && sinceHit < BattlePresentationTiming.HitFlash;
            _ally.localScale = Vector3.one * (flash ? 1.12f : 1f);
            enemy.GetComponent<Image>().color = flash ? Color.white : EnemyColor;

            if (_shot != null)
            {
                // 弾は次に当たる前の ShotSeconds で味方から敵へ飛ぶ。
                var untilHit = _nextHit - _time;
                var flying = arrived && _deadUntil < 0f && untilHit >= 0f && untilHit <= ShotSeconds;
                _shot.gameObject.SetActive(flying);
                if (flying)
                {
                    var p = 1f - untilHit / ShotSeconds;
                    var sx = Mathf.Lerp(AllyRight, _stopX, p);
                    _shot.anchorMin = new Vector2(sx - 0.03f, 0.5f);
                    _shot.anchorMax = new Vector2(sx + 0.03f, 0.58f);
                }
            }
        }

        private static void Place(RectTransform enemy, float x, float y, bool visible)
        {
            if (enemy.gameObject.activeSelf != visible)
            {
                enemy.gameObject.SetActive(visible);
            }

            enemy.anchorMin = new Vector2(x, y);
            enemy.anchorMax = new Vector2(x + EnemyWidth, y + 0.5f);
        }
    }
}
