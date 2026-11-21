using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T403・T351 / FR-004: 文字なし説明が操作の順に出て、示した操作をすると次へ進み、説明中は戦闘の時間が止まる")]
    public sealed class GuideTests
    {
        // 各戦闘マス2体だけの短い出現（最初の戦闘をすぐ終えてルートへ進むため）。
        private const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "2,0,melee,S1,battle_a_g1,0\n2,3,melee,S1,battle_a_g1,0\n" +
            "3,0,melee,S1,battle_b_g1,0\n3,3,melee,S1,battle_b_g1,0\n" +
            "5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n4,3,melee,S1,elite_a_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly System.Collections.Generic.List<InputDevice> _disabled = new System.Collections.Generic.List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private GuideOverlay _guide;
        private BattleFieldView _fieldView;
        private SlotCabinet _cabinet;
        private Camera _camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in InputSystem.devices)
            {
                if (device.enabled)
                {
                    _disabled.Add(device);
                }
            }

            foreach (var device in _disabled)
            {
                InputSystem.DisableDevice(device);
            }

            _keyboard = InputSystem.AddDevice<Keyboard>("Test Keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Test Mouse");

            GuideOverlay.DisabledForTests = false;
            // スキルのゲージや所持金が変わらないよう、バーを出す。
            SlotCabinet.RandomOverride = new BarPatternRandom();
            yield return LoadGame(false);
        }

        [TearDown]
        public void TearDown()
        {
            GuideOverlay.DisabledForTests = true;
            SlotCabinet.RandomOverride = null;
            RunController.SeedOverride = null;
            Time.timeScale = 1f;
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_mouse);
            foreach (var device in _disabled)
            {
                InputSystem.EnableDevice(device);
            }

            _disabled.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Mouse_Follows_The_Guide_From_Summon_To_The_First_Spin()
        {
            var overlay = _guide.transform.Find("Overlay");
            var dim = overlay.Find("Dim Top").gameObject;
            yield return null;
            Assert.That((_guide.Current, dim.activeSelf), Is.EqualTo((GuideHint.Summon, true)), "開始の演出の後は召喚");

            var icon = GameObject.Find("Unit Icon 1").transform;
            var target = FirstPlaceableCell();
            var cell = CellScreen(target);
            yield return MouseAt(ScreenOf(icon), false);
            yield return MouseAt(ScreenOf(icon), true);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Place), "アイコンを押すとマスの説明");
            yield return MouseAt(cell, true);
            yield return MouseAt(cell, false);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Facing), "マスを決めると向きの説明");

            var left = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(target) - _fieldView.transform.right * 1.5f);
            yield return MouseAt(left, false);
            yield return MouseAt(left, true);
            yield return MouseAt(left, false);
            Assert.That((_battle.Field.Units.Count, _guide.Current), Is.EqualTo((1, GuideHint.Lever)), "置くとスロットの説明");

            yield return Click(_cabinet.LeverRect);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Spinning), "レバーを引くとリールが止まるまでの説明");
            Assert.That(overlay.Find("Hand").gameObject.activeSelf, Is.False, "止まるのを待つあいだは手を出さない");

            yield return WaitForReels();
            Assert.That((_guide.Current, overlay.gameObject.activeSelf, _guide.Sequence.IsDone(GuideTopic.Slot)),
                Is.EqualTo((GuideHint.None, false, true)), "リールが止まると説明を閉じる");
        }

        [UnityTest]
        [Description("FR-004, D-91")]
        public IEnumerator Block_Hint_Points_At_The_Stopped_Enemy_And_Closes_On_A_Click()
        {
            yield return null;
            // 最初の敵の道の途中にタンクを置き、スロットの題目を済ませてから戦闘を進める。
            var limit = Time.realtimeSinceStartup + 10f;
            while (_battle.Field.Enemies.Count == 0 && Time.realtimeSinceStartup < limit)
            {
                _guide.enabled = false;
                yield return null;
            }

            _guide.enabled = true;
            Assert.That(_battle.Field.Enemies.Count, Is.GreaterThan(0), "敵が出ない");
            Assert.That(PlaceOnPath("tank", _battle.Field.Enemies[0]), Is.True, "敵の道にタンクを置けない");
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Lever));
            yield return Click(_cabinet.LeverRect);
            yield return WaitForReels();

            limit = Time.realtimeSinceStartup + 30f;
            while (_guide.Current != GuideHint.Block && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            var overlay = _guide.transform.Find("Overlay");
            Assert.That((_guide.Current, _battle.IsHeldByGuide, overlay.Find("Block Panel")?.gameObject.activeSelf),
                Is.EqualTo((GuideHint.Block, true, (bool?)true)), "止められたときに足止めの説明が出ず戦闘も止まらない");
            var panel = overlay.Find("Block Panel");
            Assert.That(panel.childCount, Is.EqualTo(3), "タンク・剣士・弓兵の3つ");
            Assert.That(panel.GetChild(0).Find("Block").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("3"), "タンクは3体");

            yield return new WaitForSecondsRealtime(0.7f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return MouseAt(center, true);
            yield return MouseAt(center, false);
            Assert.That((_guide.Current, _guide.Sequence.IsDone(GuideTopic.Block), _battle.IsHeldByGuide, panel.gameObject.activeSelf),
                Is.EqualTo((GuideHint.None, true, false, false)), "押すと閉じて戦闘が進む");
        }

        [UnityTest]
        public IEnumerator Battle_Time_Is_Held_While_A_Hint_Is_Shown()
        {
            yield return null;
            Assert.That((_battle.Run.Phase, _guide.Current), Is.EqualTo((WavePhase.Fighting, GuideHint.Summon)));

            // 開始の演出が終わったフレームの残りの分だけは進んでいてよい。
            var tick = _battle.Field.Tick;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.EqualTo(tick), "説明中は戦闘が進まない");

            _guide.enabled = false;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(0L), "説明を閉じれば進む");
        }

        [UnityTest]
        [Description("T426 / FR-083, AC-064: 示されていない札も暗幕に遮られずに押せて、その札を置いても召喚の題目が終わる")]
        public IEnumerator Another_Card_Through_The_Dim_Also_Ends_The_Deploy_Topic()
        {
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Summon));
            var units = _battle.Units.All;
            var other = -1;
            for (var slot = 1; slot < units.Count && other < 0; slot++)
            {
                if (_battle.Run.Summoner.Check(units[slot].Id) == SummonResult.Summoned)
                {
                    other = slot;
                }
            }

            Assert.That(other, Is.GreaterThan(0), "1番目以外に召喚できる札がない");
            yield return Click(GameObject.Find("Unit Icon " + (other + 1)).transform);
            Assert.That((_guide.Current, _battle.Deployment.Selected?.Id), Is.EqualTo((GuideHint.Place, units[other].Id)),
                "示されていない札を押せない");

            var deployment = _battle.Deployment;
            var map = _battle.Run.Map;
            var placed = false;
            for (var y = 0; y < map.Height && !placed; y++)
            {
                for (var x = 0; x < map.Width && !placed; x++)
                {
                    if (deployment.CanPlace(new GridPoint(x, y)))
                    {
                        deployment.SetCursor(new GridPoint(x, y));
                        deployment.ChooseCell();
                        deployment.SetFacing(Facing.Right);
                        placed = deployment.Confirm() == SummonResult.Summoned;
                    }
                }
            }

            Assert.That(placed, Is.True, "置けない");
            yield return null;
            Assert.That((_guide.Sequence.IsDone(GuideTopic.Deploy), _guide.Current), Is.EqualTo((true, GuideHint.Lever)),
                "ほかの札を置いても召喚の題目が終わらない");
        }

        [UnityTest]
        [Description("T426 / FR-083, AC-064: × で操作せずに次の題目へ進み、戦闘も止まったままにならない")]
        public IEnumerator Close_Button_Skips_The_Topic()
        {
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Summon));
            var close = _guide.transform.Find("Overlay/Close Button");
            Assert.That(close, Is.Not.Null, "閉じるボタンがない");
            yield return Click(close);
            Assert.That((_guide.Sequence.IsDone(GuideTopic.Deploy), _guide.Current, _battle.Field.Units.Count),
                Is.EqualTo((true, GuideHint.Lever, 0)), "× で召喚の題目を閉じるとスロットへ（置かずに）");

            yield return Click(close);
            Assert.That((_guide.Sequence.IsDone(GuideTopic.Slot), _guide.Current, _battle.IsHeldByGuide, _cabinet.SpinCount),
                Is.EqualTo((true, GuideHint.None, false, 0)), "× でスロットの題目も閉じ、戦闘が進む（レバーは引かない）");
        }

        [UnityTest]
        [Description("T426 / FR-083, AC-064: 向き選びの手は、道の上に置いたときその道の出口側（敵の来る方向）へ動く")]
        public IEnumerator Facing_Hand_Points_Toward_The_Exit_Side_Of_The_Path()
        {
            yield return null;
            var deployment = _battle.Deployment;
            Assert.That(deployment.Select("tank"), Is.EqualTo(SummonResult.Summoned));
            MapPath path = null;
            foreach (var candidate in _battle.Run.Map.Paths)
            {
                if (!candidate.IsAir)
                {
                    path = candidate;
                    break;
                }
            }

            Assert.That(path, Is.Not.Null, "地上の道がない");
            var index = -1;
            for (var i = 2; i < path.Cells.Count - 1 && index < 0; i++)
            {
                if (deployment.CanPlace(path.Cells[i]))
                {
                    index = i;
                }
            }

            Assert.That(index, Is.GreaterThan(0), "道の上にタンクを置けるマスがない");
            deployment.SetCursor(path.Cells[index]);
            deployment.ChooseCell();
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Facing));

            var from = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(path.Cells[index]));
            var exitSide = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(path.Cells[index - 1]));
            var expected = (exitSide - from).normalized;

            // 手の動き（押して動かして離す）のうち、マスの中心から一番離れた所の方向を見る。
            var hand = (RectTransform)_guide.transform.Find("Overlay/Hand");
            var farthest = from;
            var limit = Time.realtimeSinceStartup + 2.2f;
            while (Time.realtimeSinceStartup < limit)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(null, hand.position);
                if ((screen - from).sqrMagnitude > (farthest - from).sqrMagnitude)
                {
                    farthest = screen;
                }

                yield return null;
            }

            Assert.That(Vector2.Dot((farthest - from).normalized, expected), Is.GreaterThan(0.9f),
                $"手が敵の来る方向へ動かない（手 {farthest - from}、出口側 {expected}）");
            deployment.Cancel();
        }

        private IEnumerator LoadGame(bool shortWaves)
        {
            if (shortWaves)
            {
                SceneManager.sceneLoaded += ReplaceWaves;
            }

            try
            {
                yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            _battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && _battle.Deployment == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Deployment, Is.Not.Null, "Game シーンの戦闘が始まらない");
            var introLimit = Time.realtimeSinceStartup + 5f;
            while (_battle.IsInIntro && Time.realtimeSinceStartup < introLimit)
            {
                yield return null;
            }

            Assert.That(_battle.IsInIntro, Is.False, "開始の演出が終わらない");
            _guide = Object.FindAnyObjectByType<GuideOverlay>();
            Assert.That(_guide, Is.Not.Null, "Game シーンに文字なし説明がない");
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }

        // 自動停止は実時間（1.0 + 0.4×2 秒）で止まるので、フレーム数ではなく実時間で待つ。
        private IEnumerator WaitForReels()
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return null;
        }

        private bool Place(string unitId)
        {
            var deployment = _battle.Deployment;
            if (deployment.Select(unitId) != SummonResult.Summoned)
            {
                return false;
            }

            var map = _battle.Run.Map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (deployment.CanPlace(cell))
                    {
                        deployment.SetCursor(cell);
                        deployment.ChooseCell();
                        deployment.SetFacing(Facing.Right);
                        return deployment.Confirm() == SummonResult.Summoned;
                    }
                }
            }

            deployment.Cancel();
            return false;
        }

        // 敵の道のうち、敵より先でタンクを置けるマスに置く（敵が向かってきて止められる）。
        private bool PlaceOnPath(string unitId, BattleEnemy enemy)
        {
            var deployment = _battle.Deployment;
            if (deployment.Select(unitId) != SummonResult.Summoned)
            {
                return false;
            }

            var cells = enemy.Path.Cells;
            for (var i = (int)enemy.Distance + 2; i < cells.Count - 1; i++)
            {
                if (deployment.CanPlace(cells[i]))
                {
                    deployment.SetCursor(cells[i]);
                    deployment.ChooseCell();
                    deployment.SetFacing(Facing.Right);
                    return deployment.Confirm() == SummonResult.Summoned;
                }
            }

            deployment.Cancel();
            return false;
        }

        private static Vector2 ScreenOf(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);

        private IEnumerator Click(Transform target)
        {
            var screen = ScreenOf(target);
            yield return MouseAt(screen, false);
            yield return MouseAt(screen, true);
            yield return MouseAt(screen, false);
        }

        private GridPoint FirstPlaceableCell()
        {
            var map = _fieldView.Map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (UnitPlacement.CanPlace(map, new GridPoint(x, y), _battle.Units.Get("tank")))
                    {
                        return new GridPoint(x, y);
                    }
                }
            }

            Assert.Fail("置けるマスがない");
            return default;
        }

        private Vector2 CellScreen(GridPoint cell) => _camera.WorldToScreenPoint(_fieldView.CellTopToWorld(cell));

        private IEnumerator MouseAt(Vector2 position, bool pressed)
        {
            var state = new MouseState { position = position };
            if (pressed)
            {
                state = state.WithButton(MouseButton.Left);
            }

            InputSystem.QueueStateEvent(_mouse, state);
            yield return null;
            yield return null;
        }
    }
}
