using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-002: タイトルから1戦闘（召喚・配置・向き・強化・スキル・レバー・一時停止・早見表・撤退）と次のステージまでを" +
                 "マウスだけで通す。リザルトもマウスで操作できる（FR-002, T353, spec 002 FR-101。キーボードだけの通しは D-55 で削除）")]
    public sealed class InputOnlyRunTests
    {
        // 最初の戦闘は近接の敵2体だけにして短く終わらせる。準備フェーズがない（D-79）ので、操作を確かめ終えるまで敵を出さない（30 秒後）。
        private const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,30,melee,S1,first_g1,0\n1,33,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n2,3,melee,S1,battle_a_g1,0\n3,3,melee,S1,battle_b_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        // ゲームオーバー用: 最初の戦闘に近接の敵 13 体（ライフ 10 より多い）。味方を置かないので全員ゴールに着く。
        private const string LosingWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,1,melee,S1,first_g1,0\n1,2,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "1,4,melee,S1,first_g1,0\n1,5,melee,S1,first_g1,0\n1,6,melee,S1,first_g1,0\n1,7,melee,S1,first_g1,0\n" +
            "1,8,melee,S1,first_g1,0\n1,9,melee,S1,first_g1,0\n1,10,melee,S1,first_g1,0\n1,11,melee,S1,first_g1,0\n" +
            "1,3,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n2,3,melee,S1,battle_a_g1,0\n3,3,melee,S1,battle_b_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        // テスト中は Game シーンを読むたびに差し替える（リトライ後も同じ表）。
        private static string _waves;

        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private RunController _run;
        private SlotCabinet _cabinet;
        private BattleFieldView _fieldView;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // 実機の入力（ゲームパッドを含む）が混ざらないよう全部止め、仮想のキーボードとマウスだけにする。
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
            // スロットの結果をバーに固定する。
            SlotCabinet.RandomOverride = new BarPatternRandom();
            SceneManager.sceneLoaded += ReplaceWaves;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= ReplaceWaves;
            _waves = null;
            GameSession.SetTextMode(false);
            RunController.SeedOverride = null;
            SlotCabinet.RandomOverride = null;
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
        public IEnumerator Mouse_Alone_Goes_From_Title_Through_A_Battle_To_The_Next_Stage([Values(false, true)] bool textMode)
        {
            // 文字あり版（T359、D-76）でも部品の配置が変わるだけで、同じ操作で同じことが起こる（AC-005）。
            GameSession.SetTextMode(textMode);
            _waves = ShortWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);

            // タイトル ▶ → すぐ Game のステージ1（難易度の選択はない: spec 002 E-18）。
            yield return Click(GameObject.Find("Start Button").transform);
            yield return WaitForGame();
            Assert.That(_battle.StageNumber, Is.EqualTo(1), "▶ のクリックでステージ1が始まらない");

            // リールのカスタムは 2026-11-14 に廃止（T341 で画面を削除）。

            // 召喚・配置（アイコンからマスへドラッグ）・向き（左をクリック）、強化アイコン。
            var bar = Object.FindAnyObjectByType<OperationBarStatus>().transform;
            var target = FirstPlaceableCell();
            var cell = CellScreen(target);
            var icon = ScreenOf(GameObject.Find("Unit Icon 1").transform);
            yield return MouseAt(icon, false);
            yield return MouseAt(icon, true);
            yield return MouseAt(cell, true);
            yield return MouseAt(cell, false);
            var left = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(target) - _fieldView.transform.right * 1.5f);
            yield return MouseAt(left, false);
            yield return MouseAt(left, true);
            yield return MouseAt(left, false);
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(1), "マウスで味方を置けない");
            AddUpgradeCoins();
            yield return Click(GameObject.Find("Upgrade Icon 1").transform);
            Assert.That(_battle.Upgrades.Level("tank"), Is.EqualTo(2), "強化アイコンで強化できない");

            // スキル: 満タンのゲージをクリック（ゲージを貯める部分は操作の確認の対象外なので直接満たす）。
            FillGaugeOf("tank");
            var activations = _battle.SkillActivations;
            yield return Click(GameObject.Find("Skill Gauge 1").transform);
            Assert.That(_battle.SkillActivations, Is.EqualTo(activations + 1), "ゲージのクリックでスキルが発動しない");

            // スロット: レバーのクリック。1回でコインが1枚減る。リールは自動で止まる。
            var coinsBeforeSpin = _cabinet.Wallet.Coins;
            yield return Click(_cabinet.LeverRect);
            Assert.That((_cabinet.IsSpinning, _cabinet.Wallet.Coins), Is.EqualTo((true, coinsBeforeSpin - 1)), "レバーのクリックで回らない");
            yield return WaitForReelsToStop();

            // 一時停止ボタン → 「?」で早見表 → ◀ で閉じる → 一時停止ボタンで再開。
            var help = Object.FindAnyObjectByType<HelpScreen>();
            var pause = bar.Find("Pause Button");
            yield return Click(pause);
            Assert.That(_battle.IsPaused, Is.True, "一時停止ボタンで止まらない");
            Assert.That(help.OpenButton.gameObject.activeInHierarchy, Is.True, "一時停止中に「?」が出ない");
            yield return Click(help.OpenButton.transform);
            Assert.That(help.IsOpen, Is.True, "「?」のクリックで早見表が開かない");
            yield return Click(help.CloseButton.transform);
            Assert.That((help.IsOpen, _battle.IsPaused), Is.EqualTo((false, true)), "◀ で早見表が閉じない");
            yield return Click(pause);
            Assert.That(_battle.IsPaused, Is.False, "一時停止ボタンで再開しない");

            // 撤退: 味方をクリックして撤退アイコン。
            yield return MouseAt(cell, false);
            yield return MouseAt(cell, true);
            yield return MouseAt(cell, false);
            Assert.That(Object.FindAnyObjectByType<UnitPlacementInput>().SelectedUnit, Is.Not.Null,
                "味方をクリックで選べない（当たった UI: " + HitsAt(cell) + "）");
            yield return Click(RetreatButton());
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(0),
                "撤退アイコンで撤退できない（当たった UI: " + HitsAt(ScreenOf(RetreatButton())) + "）");

            // ステージ1を終えると、勝利の帯の後でステージ2に進む（spec 002 FR-101）。
            yield return WaitForNextStage();
        }

        [UnityTest]
        public IEnumerator Mouse_Clicks_Retry_And_Title_On_The_Result([Values(false, true)] bool textMode)
        {
            // 文字あり版（T359e）では三角が上に寄り下に名前が入るが、同じクリックで同じことが起こる（AC-005）。
            GameSession.SetTextMode(textMode);
            _waves = LosingWaves;
            // 前のテストの Game シーンを拾わないよう、タイトルから始める。
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            GameSession.StartGame();
            yield return WaitForGame();
            yield return LoseTheGame();
            var result = Object.FindAnyObjectByType<ResultScreen>();
            Assert.That(result.IsOpen, Is.True, "ゲームオーバーのリザルトが開かない");

            var before = _battle;
            yield return Click(result.RetryButton.transform);
            for (var i = 0; i < 300 && Object.FindAnyObjectByType<BattleController>() == before; i++)
            {
                yield return null;
            }

            yield return WaitForGame();
            Assert.That((_battle != before, _battle.StageNumber), Is.EqualTo((true, 1)), "↻ のクリックでリトライしない");

            yield return LoseTheGame();
            result = Object.FindAnyObjectByType<ResultScreen>();
            yield return Click(result.TitleButton.transform);
            for (var i = 0; i < 300 && SceneManager.GetActiveScene().name != GameSession.TitleScene; i++)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene), "◀ のクリックでタイトルへ戻らない");
        }

        [UnityTest]
        public IEnumerator Title_Accepts_Input_After_Returning_From_The_Game([Values(false, true)] bool clickFirst)
        {
            // リザルトの ◀ と同じ経路（BackToTitle）で戻ったタイトルが、すぐにキーボードとマウスを受け付ける。
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            yield return WaitForGame();
            Object.FindAnyObjectByType<ResultScreen>().BackToTitle();
            for (var i = 0; i < 60 && !IsSelected("Start Button"); i++)
            {
                yield return null;
            }

            bool InGame() => SceneManager.GetActiveScene().name == GameSession.GameScene;
            if (clickFirst)
            {
                yield return Click(GameObject.Find("Start Button").transform);
                for (var i = 0; i < 60 && !InGame(); i++) yield return null;
                Assert.That(InGame(), Is.True, "Game から戻った後、▶ のクリックが効かない");
                yield break;
            }

            yield return Press(Key.Enter, InGame);
            Assert.That(InGame(), Is.True, "Game から戻った後、Enter が効かない");
        }

        // 初期コイン（30）では召喚（15）と強化（20）を両方は払えないので、強化の分だけ足す（操作の確認が目的）。
        private void AddUpgradeCoins() => _battle.Wallet.Add(_battle.Upgrades.NextCost("tank").Value);

        private void FillGaugeOf(string unitId)
        {
            Assert.That(_battle.Skills.TryGetColor(unitId, out var color), Is.True, unitId);
            var gauges = _battle.Run.State.Gauges;
            for (var i = 0; i < gauges.Max; i++)
            {
                gauges.Add(color);
            }
        }

        private IEnumerator WaitForNextStage()
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (_battle.StageNumber == 1 && !_battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That((_battle.StageNumber, _run.State.StageIndex), Is.EqualTo((2, 2)), "ステージ1を終えてもステージ2に進まない");
            yield return null;
        }

        private IEnumerator LoseTheGame()
        {
            yield return null;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (!_battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.GameOver), "時間内にゲームオーバーにならない");
            yield return null;
        }

        private IEnumerator WaitForReelsToStop()
        {
            // 自動停止は実時間（1.0 + 0.4×2 秒）で止まるので、フレーム数ではなく実時間で待つ。
            var until = Time.realtimeSinceStartup + 5f;
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(_cabinet.IsSpinning, Is.False, "リールが止まらない");
        }

        private IEnumerator WaitForGame()
        {
            _battle = null;
            for (var i = 0; i < 300; i++)
            {
                _battle = SceneManager.GetActiveScene().name == GameSession.GameScene
                    ? Object.FindAnyObjectByType<BattleController>()
                    : null;
                if (_battle != null && _battle.Run != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(_battle != null && _battle.Run != null, Is.True, "Game シーンの進行が作られない");
            _run = Object.FindAnyObjectByType<RunController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            _camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            yield return StageTestUtil.WaitForIntro(_battle);
            // 物理の当たり判定が揃うまで待つ。
            yield return new WaitForFixedUpdate();
            yield return null;
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

        private static bool IsSelected(string buttonName)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.name == buttonName;
        }

        private Vector2 CellScreen(GridPoint cell) => _camera.WorldToScreenPoint(_fieldView.CellTopToWorld(cell));

        private static Vector2 ScreenOf(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);

        // 失敗したときの手がかり: その点で UI の当たりに掛かる部品の名前（上から順）。
        private static string HitsAt(Vector2 screen)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            var names = new List<string>();
            foreach (var hit in hits)
            {
                names.Add(hit.gameObject.name);
            }

            return names.Count > 0 ? string.Join(", ", names) : "なし";
        }

        // 画面遷移のキーは、効果が出るまで押し続けて離す（TitleFlowTests と同じ）。
        private IEnumerator Press(Key key, System.Func<bool> done)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            for (var i = 0; i < 60; i++)
            {
                yield return null;
                if (done())
                {
                    break;
                }
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator Click(Transform target)
        {
            var screen = ScreenOf(target);
            yield return MouseAt(screen, false);
            yield return MouseAt(screen, true);
            yield return MouseAt(screen, false);
        }

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

        // sceneLoaded は Awake の後・Start の前に呼ばれるので、BattleController が表を読む前に差し替えられる。
        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            if (_waves == null || scene.name != GameSession.GameScene)
            {
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(_waves));
        }

        // 撤退ボタンは T384 から選んだ味方の詳細の板の中。
        private static Transform RetreatButton() =>
            Object.FindAnyObjectByType<UnitDetailPanel>().transform.Find("Content/Retreat Button");
    }
}
