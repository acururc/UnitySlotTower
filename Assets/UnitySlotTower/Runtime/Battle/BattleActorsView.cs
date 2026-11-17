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

        private sealed class Actor
        {
            public Transform Root;
            public Transform Gauge;
            public Transform Fill;
            public Transform SkillMark;
            public float BodyBottom;
            public float HeadHeight;
            public bool SkillReady;
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
        }

        private void LateUpdate()
        {
            var field = battle.Field;
            if (field == null)
            {
                return;
            }

            var frame = Time.frameCount;
            SyncUnits(field.Units, frame);
            SyncEnemies(field.Enemies, frame);
        }

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
                UpdateSkillMark(actor, unit.Definition.Id);
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
                UpdateGauge(actor, enemy.Hp, enemy.Definition.MaxHp);
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

            var actor = CreateActor(root, 0f, unitGaugeColor, body, look.color);
            actor.HeadHeight = height;
            actor.SkillMark = CreatePrimitive(PrimitiveType.Quad, "Skill Ready", root, UiStyle.Ready);
            actor.SkillMark.localScale = new Vector3(0.22f, 0.22f, 1f);
            actor.SkillMark.gameObject.SetActive(false);
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
                return CreateActor(root, -size * 0.5f, enemyGaugeColor, body, color);
            }

            var height = BodyHeight(shape, scale.y);
            body.localPosition = Vector3.up * (height * 0.5f);
            if (definition.RangedDamageRate < 1)
            {
                // 遠距離に強い敵（重装、FR-075）は手前に大きな盾を持たせて見分けられるようにする。
                var shield = CreatePrimitive(PrimitiveType.Cube, "Shield", root, shieldColor);
                shield.localScale = new Vector3(size * 1.2f, height * 0.95f, size * 0.18f);
                shield.localPosition = new Vector3(0, height * 0.5f, -size * 0.6f);
            }
            return CreateActor(root, 0f, enemyGaugeColor, body, color);
        }

        /// <summary>体の下端のすぐ下に HP ゲージを付ける。空中の敵は浮いた体を基準にする。</summary>
        private Actor CreateActor(Transform root, float bottom, Color fillColor, Transform body, Color bodyColor)
        {
            var gauge = new GameObject("HP Gauge").transform;
            gauge.SetParent(root, false);
            gauge.localPosition = Vector3.up * (bottom - gaugeSize.y * 0.5f - 0.04f);


            var back = CreatePrimitive(PrimitiveType.Quad, "Back", gauge, gaugeBackColor);
            back.localScale = new Vector3(gaugeSize.x + 0.04f, gaugeSize.y + 0.04f, 1f);
            var fill = CreatePrimitive(PrimitiveType.Quad, "Fill", gauge, fillColor);
            fill.localScale = new Vector3(gaugeSize.x, gaugeSize.y, 1f);
            // Quad の表は -z 側。カメラの向きに揃えたとき背景より手前に出す。
            fill.localPosition = new Vector3(0f, 0f, -0.01f);
            return new Actor { Root = root, Gauge = gauge, Fill = fill, BodyBottom = bottom,
                Body = body.GetComponent<Renderer>(), BodyColor = bodyColor };
        }

        private void UpdateSkillMark(Actor actor, string unitId)
        {
            var ready = battle.Skills != null && battle.Run != null &&
                battle.Skills.TryGetColor(unitId, out var skillColor) && battle.Run.State.Gauges.IsFull(skillColor);
            if (actor.SkillReady != ready)
            {
                actor.SkillReady = ready;
                actor.SkillMark.gameObject.SetActive(ready);
            }
            if (!ready) return;

            actor.SkillMark.localPosition = Vector3.up *
                (actor.HeadHeight + 0.28f + 0.04f * Mathf.Sin(Time.unscaledTime * 2f));
            if (battleCamera != null)
            {
                actor.SkillMark.rotation = battleCamera.transform.rotation * Quaternion.Euler(0f, 0f, 45f);
            }
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
                // Screen-down from the feet, with a small camera-facing offset to clear the ground.
                actor.Gauge.position = actor.Root.TransformPoint(Vector3.up * actor.BodyBottom)
                    - cameraTransform.up * (gaugeSize.y * 0.5f + 0.04f)
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
            actor.Fill.localPosition = new Vector3((width - gaugeSize.x) * 0.5f, 0f, -0.01f);
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
