using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 味方と敵のグレーボックス表示（T209、plan 11章）。味方は種類ごとの色と形・向きの印（攻撃範囲は選んだ味方だけ UnitPlacementView が出す: D-82）、
    /// 敵は種類ごとの形（近距離は箱、遠距離は円柱、空中は浮いた球と地面の影。ブロック消費が大きいほど大きい）、
    /// どちらにもカメラを向く HP ゲージを付ける。M5 で素材に差し替える。
    /// </summary>
    public sealed class BattleActorsView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Serializable]
        private struct UnitLook
        {
            public string id;
            public PrimitiveType shape;
            public Vector3 size;
            public Color color;
        }

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("HP ゲージを向けるカメラ。")]
        [SerializeField] private Camera battleCamera;
        [Tooltip("味方・敵・ゲージの形に使う URP のマテリアル（Greybox.mat）。URP の既定マテリアルは Editor 専用で、未設定だとプレイヤーでマゼンタになる。")]
        [SerializeField] private Material primitiveMaterial;

        [Header("Units")]
        [Tooltip("units.csv の id ごとの形・大きさ・色（操作バーのアイコンと同じ色）。")]
        [SerializeField] private UnitLook[] unitLooks =
        {
            new UnitLook { id = "tank", shape = PrimitiveType.Cube, size = new Vector3(0.75f, 0.5f, 0.75f), color = new Color(0.45f, 0.55f, 0.75f) },
            new UnitLook { id = "sword", shape = PrimitiveType.Cube, size = new Vector3(0.4f, 0.75f, 0.4f), color = new Color(0.85f, 0.3f, 0.25f) },
            new UnitLook { id = "archer", shape = PrimitiveType.Cylinder, size = new Vector3(0.45f, 0.4f, 0.45f), color = new Color(0.35f, 0.75f, 0.35f) },
            new UnitLook { id = "buffer", shape = PrimitiveType.Sphere, size = new Vector3(0.6f, 0.6f, 0.6f), color = new Color(0.95f, 0.75f, 0.85f) },
        };
        [SerializeField] private Color unknownUnitColor = new Color(0.8f, 0.8f, 0.8f);
        [SerializeField] private Color noseColor = new Color(0.15f, 0.15f, 0.18f);

        [Header("Enemies")]
        [SerializeField] private Color meleeEnemyColor = new Color(0.55f, 0.12f, 0.15f);
        [SerializeField] private Color rangedEnemyColor = new Color(0.6f, 0.2f, 0.55f);
        [SerializeField] private Color airEnemyColor = new Color(0.35f, 0.2f, 0.6f);
        [SerializeField] private Color shadowColor = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private float enemySize = 0.5f;
        [Tooltip("遠距離に強い敵（ranged_damage_rate < 1）の盾の色。")]
        [SerializeField] private Color shieldColor = new Color(0.62f, 0.66f, 0.72f);

        [Header("HP Gauge")]
        [SerializeField] private Vector2 gaugeSize = new Vector2(0.72f, 0.07f);
        [SerializeField] private Color gaugeBackColor = new Color(0.05f, 0.05f, 0.06f);
        [SerializeField] private Color unitGaugeColor = UiStyle.Ally;
        [SerializeField] private Color enemyGaugeColor = UiStyle.Danger;
        [Tooltip("深度を見ずに最前面に描く材質（HpGauge.shader、spec 015 FR-042）。空なら体と同じ材質。")]
        [SerializeField] private Material gaugeMaterial;

        // ゲージと頭の間、ゲージの中身を背景より手前に出す距離（ワールド）。
        private const float GaugeLift = 0.06f;
        private const float FillForward = 0.05f;

        [Header("Shots")]
        [Tooltip("弓の矢と遠距離の敵の弾（FR-082、見た目だけ）。飛ぶ時間は攻撃の間隔の 1/3 とこの値の短い方（戦闘の時間で進む）。")]
        [SerializeField] private float maxShotSeconds = 0.2f;
        [SerializeField] private Vector3 arrowSize = new Vector3(0.05f, 0.05f, 0.38f);
        [SerializeField] private Color arrowColor = new Color(1f, 0.95f, 0.7f);
        [SerializeField] private float enemyShotSize = 0.15f;
        [SerializeField] private Color enemyShotColor = new Color(1f, 0.45f, 0.85f);

        [Header("Shot Trails (spec 008 FR-002)")]
        [Tooltip("弾の尾の材質（加算、Setup の Build Hit Impact Effect が入れる）。未設定なら尾を付けない。")]
        [SerializeField] private Material shotTrailMaterial;
        [Tooltip("尾が残る秒数（戦闘の時間）。")]
        [SerializeField, Min(0.01f)] private float trailSeconds = 0.12f;
        [Tooltip("尾の頭の太さ（ワールド）。尾の先は 0。")]
        [SerializeField, Min(0f)] private float arrowTrailWidth = 0.14f;
        [SerializeField, Min(0f)] private float enemyTrailWidth = 0.2f;
        [SerializeField] private Color arrowTrailColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        [SerializeField] private Color enemyTrailColor = new Color(1f, 0.25f, 0.3f, 0.9f);

        /// <summary>
        /// 攻撃が当たった（spec 008 FR-001、見た目の当たり）。位置と、味方の攻撃なら true。近接は攻撃の通知の中で、
        /// 遠距離は弾が見た目で届いた瞬間に呼ぶ（ダメージが入る時点は Core のまま）。
        /// </summary>
        public event Action<Vector3, bool> Impact;

        /// <summary>当たりを通知した数（テスト用）。</summary>
        public int ImpactCount { get; private set; }

        private sealed class Shot
        {
            public Transform Root;
            public TrailRenderer Trail;
            public bool IsArrow;
            public BattleUnit TargetUnit;
            public BattleEnemy TargetEnemy;
            public Vector3 From;
            public Vector3 To;
            public float Elapsed;
            public float Duration;
        }

        private readonly List<Shot> _shots = new List<Shot>();
        private readonly Stack<Shot> _freeArrows = new Stack<Shot>();
        private readonly Stack<Shot> _freeEnemyShots = new Stack<Shot>();
        private Transform _shotRoot;
        private Battlefield _observed;

        /// <summary>テスト用（AC-063）: 飛んでいる弾・撃った数・届いた数・作った弾の数（使い回すので撃った数より少ない）。</summary>
        public int ActiveShotCount => _shots.Count;
        public int ShotsFired { get; private set; }
        public int ArrowsFired { get; private set; }
        public int ArrowsLanded { get; private set; }
        public int ShotsLanded { get; private set; }
        public int CreatedShotCount { get; private set; }

        private sealed class Actor
        {
            public Transform Root;
            public float Center;
            public Transform Gauge;
            public Transform Fill;
            public float BodyTop;
            public int Hp = -1;
            public int Seen;
            public Renderer Body;
            public Color BodyColor;
            public float HitUntil;
            public bool IsHit;
        }

        private readonly Dictionary<BattleUnit, Actor> _units = new Dictionary<BattleUnit, Actor>();
        private readonly Dictionary<BattleEnemy, Actor> _enemies = new Dictionary<BattleEnemy, Actor>();
        private readonly List<BattleUnit> _goneUnits = new List<BattleUnit>();
        private readonly List<BattleEnemy> _goneEnemies = new List<BattleEnemy>();
        private MaterialPropertyBlock _block;
        private Transform _unitRoot;
        private Transform _enemyRoot;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _unitRoot = CreateGroup("Units");
            _enemyRoot = CreateGroup("Enemies");
            _shotRoot = CreateGroup("Shots");
        }

        private void OnDestroy() => Observe(null);

        private void LateUpdate()
        {
            var field = battle.Field;
            if (field != _observed)
            {
                Observe(field);
            }

            if (field == null)
            {
                return;
            }

            var frame = Time.frameCount;
            SyncUnits(field.Units, frame);
            SyncEnemies(field.Enemies, frame);
            UpdateShots();
        }

        /// <summary>戦闘が作り直されたら通知を付け替え、前の戦闘の弾は片付ける。</summary>
        private void Observe(Battlefield field)
        {
            if (_observed != null)
            {
                _observed.UnitAttacked -= OnUnitAttacked;
                _observed.EnemyAttacked -= OnEnemyAttacked;
            }

            _observed = field;
            for (var i = _shots.Count - 1; i >= 0; i--)
            {
                ReleaseShot(i);
            }

            if (field != null)
            {
                field.UnitAttacked += OnUnitAttacked;
                field.EnemyAttacked += OnEnemyAttacked;
            }
        }

        private void OnUnitAttacked(BattleUnit unit, BattleEnemy enemy)
        {
            if (unit.Definition.Role != UnitRole.Ranged)
            {
                RaiseImpact(AimPoint(enemy), true);
                return;
            }

            var shot = TakeShot(true);
            shot.TargetEnemy = enemy;
            Fire(shot, AimPoint(unit), AimPoint(enemy), unit.Definition.AttackInterval);
        }

        private void OnEnemyAttacked(BattleEnemy enemy, BattleUnit unit)
        {
            if (enemy.Definition.Range <= 0)
            {
                RaiseImpact(AimPoint(unit), false);
                return;
            }

            var shot = TakeShot(false);
            shot.TargetUnit = unit;
            Fire(shot, AimPoint(enemy), AimPoint(unit), enemy.Definition.AttackInterval);
        }

        private void Fire(Shot shot, Vector3 from, Vector3 to, double interval)
        {
            shot.From = from;
            shot.To = to;
            shot.Elapsed = 0f;
            shot.Duration = Mathf.Max(0.01f, Mathf.Min(maxShotSeconds, (float)interval / 3f));
            PlaceShot(shot, 0f);
            shot.Root.gameObject.SetActive(true);
            if (shot.Trail != null)
            {
                // 使い回した弾が前の位置から線を引かないよう、置いてから消す。
                shot.Trail.Clear();
                shot.Trail.emitting = true;
            }

            _shots.Add(shot);
            ShotsFired++;
            if (shot.IsArrow) ArrowsFired++;
        }

        /// <summary>相手が居る間は今の位置を追い、先に消えたら最後に見た位置まで飛んで消える。戦闘の時間（timeScale）で進む。</summary>
        private void UpdateShots()
        {
            var delta = Time.deltaTime;
            for (var i = _shots.Count - 1; i >= 0; i--)
            {
                var shot = _shots[i];
                if (shot.TargetEnemy != null && _enemies.TryGetValue(shot.TargetEnemy, out var enemyActor))
                {
                    shot.To = AimPoint(enemyActor);
                }
                else if (shot.TargetUnit != null && _units.TryGetValue(shot.TargetUnit, out var unitActor))
                {
                    shot.To = AimPoint(unitActor);
                }

                shot.Elapsed += delta;
                if (shot.Elapsed >= shot.Duration)
                {
                    ShotsLanded++;
                    if (shot.IsArrow) ArrowsLanded++;
                    RaiseImpact(shot.To, shot.IsArrow);
                    ReleaseShot(i);
                    continue;
                }

                PlaceShot(shot, shot.Elapsed / shot.Duration);
            }
        }

        private void PlaceShot(Shot shot, float t)
        {
            shot.Root.position = Vector3.Lerp(shot.From, shot.To, t);
            var direction = shot.To - shot.From;
            if (shot.IsArrow && direction.sqrMagnitude > 1e-6f)
            {
                shot.Root.rotation = Quaternion.LookRotation(direction);
            }
        }

        private Shot TakeShot(bool arrow)
        {
            var pool = arrow ? _freeArrows : _freeEnemyShots;
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            CreatedShotCount++;
            Transform root;
            if (arrow)
            {
                root = CreatePrimitive(PrimitiveType.Cube, "Arrow", _shotRoot, arrowColor);
                root.localScale = arrowSize;
            }
            else
            {
                root = CreatePrimitive(PrimitiveType.Sphere, "Enemy Shot", _shotRoot, enemyShotColor);
                root.localScale = Vector3.one * enemyShotSize;
            }

            return new Shot { Root = root, IsArrow = arrow, Trail = AddTrail(root, arrow) };
        }

        // 弾の尾（spec 008 FR-002）: 頭が太く尾が細い光の筋。材質がなければ付けない。
        private TrailRenderer AddTrail(Transform root, bool arrow)
        {
            if (shotTrailMaterial == null)
            {
                return null;
            }

            var trail = root.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = shotTrailMaterial;
            trail.time = trailSeconds;
            trail.minVertexDistance = 0.02f;
            trail.widthMultiplier = arrow ? arrowTrailWidth : enemyTrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var color = arrow ? arrowTrailColor : enemyTrailColor;
            var tail = color;
            tail.a = 0f;
            trail.startColor = color;
            trail.endColor = tail;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.emitting = false;
            return trail;
        }

        private void RaiseImpact(Vector3 at, bool byUnit)
        {
            ImpactCount++;
            Impact?.Invoke(at, byUnit);
        }

        private void ReleaseShot(int index)
        {
            var shot = _shots[index];
            _shots.RemoveAt(index);
            shot.TargetEnemy = null;
            shot.TargetUnit = null;
            if (shot.Trail != null)
            {
                shot.Trail.emitting = false;
                shot.Trail.Clear();
            }

            shot.Root.gameObject.SetActive(false);
            (shot.IsArrow ? _freeArrows : _freeEnemyShots).Push(shot);
        }

        private static Vector3 AimPoint(Actor actor) => actor.Root.position + Vector3.up * actor.Center;

        private Vector3 AimPoint(BattleUnit unit) =>
            _units.TryGetValue(unit, out var actor) ? AimPoint(actor)
                : fieldView.CellTopToWorld(unit.Cell) + Vector3.up * 0.3f;

        private Vector3 AimPoint(BattleEnemy enemy) =>
            _enemies.TryGetValue(enemy, out var actor) ? AimPoint(actor)
                : fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) +
                  (enemy.Definition.IsAir ? Vector3.zero : Vector3.up * enemySize * 0.5f);

        private void SyncUnits(IReadOnlyList<BattleUnit> units, int frame)
        {
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (!_units.TryGetValue(unit, out var actor))
                {
                    actor = CreateUnit(unit);
                    _units.Add(unit, actor);
                }

                actor.Seen = frame;
                UpdateGauge(actor, unit.Hp, unit.MaxHp);
            }

            _goneUnits.Clear();
            foreach (var pair in _units)
            {
                if (pair.Value.Seen != frame)
                {
                    _goneUnits.Add(pair.Key);
                }
            }

            foreach (var unit in _goneUnits)
            {
                Destroy(_units[unit].Root.gameObject);
                _units.Remove(unit);
            }
        }

        private void SyncEnemies(IReadOnlyList<BattleEnemy> enemies, int frame)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsAlive || enemy.ReachedGoal)
                {
                    continue;
                }

                if (!_enemies.TryGetValue(enemy, out var actor))
                {
                    actor = CreateEnemy(enemy);
                    _enemies.Add(enemy, actor);
                }

                actor.Seen = frame;
                actor.Root.position = fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir);
                UpdateGauge(actor, enemy.Hp, enemy.MaxHp);
            }

            _goneEnemies.Clear();
            foreach (var pair in _enemies)
            {
                if (pair.Value.Seen != frame)
                {
                    _goneEnemies.Add(pair.Key);
                }
            }

            foreach (var enemy in _goneEnemies)
            {
                Destroy(_enemies[enemy].Root.gameObject);
                _enemies.Remove(enemy);
            }
        }

        private Actor CreateUnit(BattleUnit unit)
        {
            var look = LookOf(unit.Definition.Id);
            var root = new GameObject($"Unit {unit.Definition.Id} {unit.Cell.X},{unit.Cell.Y}").transform;
            root.SetParent(_unitRoot, false);
            root.SetPositionAndRotation(fieldView.CellTopToWorld(unit.Cell), Quaternion.LookRotation(Direction(unit.Facing)));

            var height = BodyHeight(look.shape, look.size.y);
            var body = CreatePrimitive(look.shape, "Body", root, look.color);
            body.localScale = look.size;
            body.localPosition = Vector3.up * (height * 0.5f);

            // 向きの印: 体の前面から突き出した小さな箱。
            var nose = CreatePrimitive(PrimitiveType.Cube, "Nose", root, noseColor);
            nose.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            nose.localPosition = new Vector3(0f, height * 0.5f, look.size.z * 0.5f + 0.1f);

            var actor = CreateActor(root, height, unitGaugeColor, body, look.color);
            actor.Center = height * 0.5f;
            return actor;
        }

        private Actor CreateEnemy(BattleEnemy enemy)
        {
            var definition = enemy.Definition;
            var root = new GameObject($"Enemy {definition.Id}").transform;
            root.SetParent(_enemyRoot, false);

            // ボス（ブロック消費 2 以上）ほど大きくする。
            var size = enemySize * (1f + 0.35f * (Mathf.Max(1, definition.BlockCost) - 1));
            PrimitiveType shape;
            Color color;
            if (definition.IsAir)
            {
                shape = PrimitiveType.Sphere;
                color = airEnemyColor;
            }
            else if (definition.Range > 0)
            {
                shape = PrimitiveType.Cylinder;
                color = rangedEnemyColor;
            }
            else
            {
                shape = PrimitiveType.Cube;
                color = meleeEnemyColor;
            }

            var scale = shape == PrimitiveType.Cylinder ? new Vector3(size, size * 0.5f, size) : Vector3.one * size;
            var body = CreatePrimitive(shape, "Body", root, color);
            body.localScale = scale;
            if (definition.IsAir)
            {
                // 空中の敵は空中経路の高さに浮かべ、真下の地面に影を置く。
                var lift = fieldView.PositionToWorld(0, 0, true) - fieldView.PositionToWorld(0, 0, false);
                var shadow = CreatePrimitive(PrimitiveType.Cylinder, "Shadow", root, shadowColor);
                shadow.localScale = new Vector3(size * 0.9f, 0.005f, size * 0.9f);
                shadow.localPosition = Vector3.down * (lift.magnitude - 0.01f);
                return CreateActor(root, size * 0.5f, enemyGaugeColor, body, color);
            }

            var height = BodyHeight(shape, scale.y);
            body.localPosition = Vector3.up * (height * 0.5f);
            if (definition.RangedDamageRate < 1)
            {
                // 遠距離に強い敵（重装、FR-075）は体の横に盾を持たせて見分けられるようにする。
                // 体より低く細い板にし、HP ゲージや体を覆う大きな四角に見えないようにする（spec 015 FR-043）。
                var shield = CreatePrimitive(PrimitiveType.Cube, "Shield", root, shieldColor);
                shield.localScale = new Vector3(size * 0.16f, height * 0.7f, size * 0.85f);
                shield.localPosition = new Vector3(-size * 0.62f, height * 0.4f, 0f);
            }
            var groundActor = CreateActor(root, height, enemyGaugeColor, body, color);
            groundActor.Center = height * 0.5f;
            return groundActor;
        }

        /// <summary>体の上端のすぐ上に HP ゲージを付ける（spec 015 FR-042）。空中の敵は浮いた体を基準にする。深度を見ない材質で最前面に描く。</summary>
        private Actor CreateActor(Transform root, float top, Color fillColor, Transform body, Color bodyColor)
        {
            var gauge = new GameObject("HP Gauge").transform;
            gauge.SetParent(root, false);
            gauge.localPosition = Vector3.up * (top + gaugeSize.y * 0.5f + GaugeLift);

            var back = CreatePrimitive(PrimitiveType.Quad, "Back", gauge, gaugeBackColor);
            back.localScale = new Vector3(gaugeSize.x + 0.04f, gaugeSize.y + 0.04f, 1f);
            var fill = CreatePrimitive(PrimitiveType.Quad, "Fill", gauge, fillColor);
            fill.localScale = new Vector3(gaugeSize.x, gaugeSize.y, 1f);
            // Quad の表は -z 側。カメラの向きに揃えたとき背景より手前に出す（Overlay の列は遠い順に描く）。
            fill.localPosition = new Vector3(0f, 0f, -FillForward);
            UseGaugeMaterial(back);
            UseGaugeMaterial(fill);
            return new Actor { Root = root, Gauge = gauge, Fill = fill, BodyTop = top,
                Body = body.GetComponent<Renderer>(), BodyColor = bodyColor };
        }

        private void UpdateGauge(Actor actor, int hp, int maxHp)
        {
            // HP の減少を短い命中の光で返す。ゲージの値が変わらないフレームでも元の色へ戻す。
            if (actor.Hp >= 0 && hp < actor.Hp)
                actor.HitUntil = Time.unscaledTime + BattlePresentationTiming.HitFlash;
            var hit = Time.unscaledTime < actor.HitUntil;
            if (hit != actor.IsHit)
            {
                actor.IsHit = hit;
                SetColor(actor.Body, hit ? Color.white : actor.BodyColor);
            }

            // カメラと同じ向きにして、常に正面から見えるようにする。
            if (battleCamera != null)
            {
                var cameraTransform = battleCamera.transform;
                actor.Gauge.rotation = cameraTransform.rotation;
                // 頭の上から画面の上へ少し離し、カメラ寄りに置く（材質は深度を見ないので隠れない）。
                actor.Gauge.position = actor.Root.TransformPoint(Vector3.up * actor.BodyTop)
                    + cameraTransform.up * (gaugeSize.y * 0.5f + GaugeLift)
                    - cameraTransform.forward * 0.12f;
            }

            if (actor.Hp == hp)
            {
                return;
            }

            actor.Hp = hp;
            var ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            var width = gaugeSize.x * ratio;
            actor.Fill.localScale = new Vector3(width, gaugeSize.y, 1f);
            actor.Fill.localPosition = new Vector3((width - gaugeSize.x) * 0.5f, 0f, -FillForward);
        }

        private UnitLook LookOf(string id)
        {
            foreach (var look in unitLooks)
            {
                if (look.id == id)
                {
                    return look;
                }
            }

            return new UnitLook { id = id, shape = PrimitiveType.Cube, size = Vector3.one * 0.5f, color = unknownUnitColor };
        }

        /// <summary>プリミティブの見た目の高さ（円柱とカプセルは大きさ 1 で高さ 2）。</summary>
        private static float BodyHeight(PrimitiveType shape, float scaleY) =>
            shape == PrimitiveType.Cylinder || shape == PrimitiveType.Capsule ? scaleY * 2f : scaleY;

        /// <summary>向きのワールド方向（フィールドの +z が画面の奥＝マップの上）。</summary>
        private Vector3 Direction(Facing facing)
        {
            switch (facing)
            {
                case Facing.Down:
                    return -fieldView.transform.forward;
                case Facing.Left:
                    return -fieldView.transform.right;
                case Facing.Up:
                    return fieldView.transform.forward;
                default:
                    return fieldView.transform.right;
            }
        }

        private Transform CreateGroup(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(transform, false);
            return group;
        }

        private void UseGaugeMaterial(Transform part)
        {
            if (gaugeMaterial == null)
            {
                return;
            }

            var gaugeRenderer = part.GetComponent<Renderer>();
            gaugeRenderer.sharedMaterial = gaugeMaterial;
            gaugeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gaugeRenderer.receiveShadows = false;
        }

        private Transform CreatePrimitive(PrimitiveType shape, string name, Transform parent, Color color)
        {
            var primitive = GameObject.CreatePrimitive(shape);
            primitive.name = name;
            // 配置のレイキャストがマスの箱だけに当たるよう、当たり判定を持たせない。
            Destroy(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            var primitiveRenderer = primitive.GetComponent<Renderer>();
            if (primitiveMaterial != null)
            {
                primitiveRenderer.sharedMaterial = primitiveMaterial;
            }

            SetColor(primitiveRenderer, color);
            return primitive.transform;
        }

        private void SetColor(Renderer target, Color color)
        {
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }
    }
}
