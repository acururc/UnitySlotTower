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
        // 各ウェーブ1体だけの短い出現（最初の戦闘をすぐ終えてルートへ進むため）。
        private const string ShortWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\n" +
            "battle_a,1,0,melee,S1,battle_a_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\n" +
            "battle_b,1,0,melee,S1,battle_b_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\n" +
            "battle_c,1,0,melee,S1,battle_c_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

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
            // 当たりでスキルのゲージや所持金が変わると、テストの想定より先に別の題目が出てしまうので必ずハズレにする。
            SlotCabinet.RandomOverride = new AlwaysMissRandom();
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
            Assert.That((_guide.Current, dim.activeSelf), Is.EqualTo((GuideHint.Summon, true)), "開始時は召喚");

            var bar = Object.FindAnyObjectByType<OperationBarStatus>();
            yield return Click(bar.transform.Find("Start Button"));
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.Preparing), "暗幕の上の ▶ は押せない");

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
        public IEnumerator Skill_Hint_Points_At_The_Full_Gauge_And_A_Click_Activates_It()
        {
            yield return null;
            Assert.That(Place("tank"), Is.True, "タンクを置けない");
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Lever));

            // スロットの題目のあいだにタンクの色（黄）のゲージを満タンにしておくと、リールが止まった後にスキルの題目が出る。
            Assert.That(_battle.Skills.TryGetColor("tank", out var color), Is.True);
            var gauges = _battle.Run.State.Gauges;
            while (!gauges.IsFull(color))
            {
                gauges.Add(color);
            }

            yield return Click(_cabinet.LeverRect);
            yield return WaitForReels();
            Assert.That((_guide.Current, _guide.Sequence.TargetSlot, _battle.IsHeldByGuide),
                Is.EqualTo((GuideHint.Skill, 0, true)), "満タンのゲージ（タンク = 操作バーの1番目）を指して戦闘を止める");

            var gauge = GameObject.Find("Unit Icon 1").GetComponent<UnitBarIcon>().SkillButtonRect;
            yield return Click(gauge);
            Assert.That((_battle.SkillActivations, gauges.Level(color), _guide.Current, _guide.Sequence.IsDone(GuideTopic.Skill)),
                Is.EqualTo((1, 0, GuideHint.None, true)), "ゲージを押すと発動して説明を閉じる");
        }

        [UnityTest]
        public IEnumerator Route_Hint_Leads_From_Lever_To_A_Destination_And_Hides_On_The_Shop()
        {
            // 2階にショップがあるシードで読み直す（RunController と同じ生成で探す）。
            var rules = Object.FindAnyObjectByType<RunController>().Rules;
            ulong? seed = null;
            for (ulong s = 1; s < 5000 && seed == null; s++)
            {
                foreach (var next in RouteGenerator.Generate(rules, new SeededRandom(s)).Start.Next)
                {
                    if (next.Kind == RouteNodeKinds.Shop)
                    {
                        seed = s;
                        break;
                    }
                }
            }

            Assert.That(seed, Is.Not.Null, "2階にショップのあるシードが見つからない");
            RunController.SeedOverride = seed;
            yield return LoadGame(true);
            var run = Object.FindAnyObjectByType<RunController>();

            // 戦闘の題目は対象外なので説明を止めて最初の戦闘を早送りし、ルート選びで戻す。
            _guide.enabled = false;
            var first = _battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (!run.IsChoosingRoute && first.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                _battle.StartWave();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            _guide.enabled = true;
            yield return null;
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.RouteLever), "初めてのルートでレバーを指す");

            yield return Click(_cabinet.LeverRect);
            Assert.That((_cabinet.IsRouteSpinning, _guide.Current), Is.EqualTo((true, GuideHint.RoutePick)),
                "レバーを引くと行き先のリールを指す");

            var reel = -1;
            for (var r = 0; r < 3 && reel < 0; r++)
            {
                if (run.State.Current.NextOnReel(r)?.Kind == RouteNodeKinds.Shop)
                {
                    reel = r;
                }
            }

            // 説明の手が指す先とは限らない行き先（ショップ）も、暗幕に遮られずに押せる。
            var route = Object.FindAnyObjectByType<RouteScreen>();
            yield return Click(route.ArrowButtons[reel].transform);
            limit = Time.realtimeSinceStartup + 5f;
            while (run.OpenScreen == null && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            yield return null;
            Assert.That((run.OpenScreen is ShopScreen, _guide.Current, _guide.transform.Find("Overlay").gameObject.activeSelf,
                    _guide.Sequence.IsDone(GuideTopic.Route)),
                Is.EqualTo((true, GuideHint.None, false, true)), "ショップの画面中は説明を出さず、ルートの題目は済み");
        }

        [UnityTest]
        public IEnumerator Battle_Time_Is_Held_While_A_Hint_Is_Shown()
        {
            yield return null;
            _battle.StartWave();
            Assert.That((_battle.Run.Phase, _guide.Current), Is.EqualTo((WavePhase.Fighting, GuideHint.Summon)));

            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.EqualTo(0L), "説明中は戦闘が進まない");

            _guide.enabled = false;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(0L), "説明を閉じれば進む");
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
