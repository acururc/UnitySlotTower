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
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Tests.PlayMode.Battle;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-002: タイトルからリールのカスタムと1ウェーブまでを、キーボードだけ・マウスだけで通す。リザルトもマウスで操作できる（FR-002, T405）")]
    public sealed class InputOnlyRunTests
    {
        // S1-W1 は近接の敵1体だけにして、1ウェーブを短く終わらせる。
        private const string ShortWaves =
            "stage,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,1,0,melee,S1,s1_g1,0\n1,2,0,melee,S1,s1_g1,0\n2,1,0,melee,S1,s2_g1,0\n2,2,0,melee,S1,s2_g1,0\n" +
            "3,1,0,melee,S1,s3_g1,0\n3,2,0,melee,S1,s3_g1,0\n";

        // ゲームオーバー用: S1-W1 に近接の敵 12 体（ライフ 10 より多い）。味方を置かないので全員ゴールに着く。
        private const string LosingWaves =
            "stage,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,1,0,melee,S1,s1_g1,0\n1,1,1,melee,S1,s1_g1,0\n1,1,2,melee,S1,s1_g1,0\n1,1,3,melee,S1,s1_g1,0\n" +
            "1,1,4,melee,S1,s1_g1,0\n1,1,5,melee,S1,s1_g1,0\n1,1,6,melee,S1,s1_g1,0\n1,1,7,melee,S1,s1_g1,0\n" +
            "1,1,8,melee,S1,s1_g1,0\n1,1,9,melee,S1,s1_g1,0\n1,1,10,melee,S1,s1_g1,0\n1,1,11,melee,S1,s1_g1,0\n" +
            "1,2,0,melee,S1,s1_g1,0\n2,1,0,melee,S1,s2_g1,0\n2,2,0,melee,S1,s2_g1,0\n" +
            "3,1,0,melee,S1,s3_g1,0\n3,2,0,melee,S1,s3_g1,0\n";

        // テスト中は Game シーンを読むたびに差し替える（リトライ後も同じ表）。
        private static string _waves;

        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
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
            // 当たりの払い出しで所持コインが揺れないよう、スロットは必ずハズレにする。
            SlotCabinet.RandomOverride = new AlwaysMissRandom();
            SceneManager.sceneLoaded += ReplaceWaves;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= ReplaceWaves;
            _waves = null;
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
        public IEnumerator Keyboard_Alone_Goes_From_Title_Through_Reel_Custom_And_Wave_1()
        {
            _waves = ShortWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            // 前のテストの Game シーンから非同期で読み替えた直後は、最初の Enter を UI が取りこぼすことがある（2026-11-04 に確認。
            // 実際の ◀ からの遷移では取りこぼさない: Title_Accepts_Input_After_Returning_From_The_Game）。少し待ってから押す。
            for (var i = 0; i < 30 || (i < 90 && !IsSelected("Start Button")); i++)
            {
                yield return null;
            }

            // タイトル → 難易度（初期選択の設定1 → 設定2） → Game。
            yield return Press(Key.Enter, () => IsSelected("Setting 1"));
            Assert.That(IsSelected("Setting 1"), Is.True, "Enter で難易度選択（設定1）に進まない");
            yield return Press(Key.RightArrow, () => IsSelected("Setting 2"));
            Assert.That(IsSelected("Setting 2"), Is.True, "→ で設定2に移らない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.GameScene), "Enter で Game に移らない");
            yield return WaitForGame();
            Assert.That(_cabinet.Setting, Is.EqualTo(2), "キーボードで設定2を選べない");

            // リールのカスタムは 2026-11-14 に廃止（T341 で画面を削除）。

            // 召喚・配置・向き（1 → 矢印 → Enter → ↑ → Enter）、強化（Shift + 1）、▶（Enter）。
            var target = FirstPlaceableCell();
            yield return Tap(Key.Digit1);
            yield return MoveCursorTo(target);
            yield return Tap(Key.Enter);
            yield return Tap(Key.UpArrow);
            yield return Tap(Key.Enter);
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(1), "キーボードで味方を置けない");
            AddUpgradeCoins();
            yield return Tap(Key.LeftShift, Key.Digit1);
            Assert.That(_battle.Upgrades.Level("tank"), Is.EqualTo(2), "Shift + 1 で強化できない");
            yield return Tap(Key.Enter);
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.Fighting), "Enter で戦闘が始まらない");

            // スロット: Space でベット＋レバー、J/K/L で止める。
            var coins = _cabinet.Wallet.Coins;
            yield return Tap(Key.Space);
            Assert.That((_cabinet.IsSpinning, _cabinet.Wallet.Coins < coins), Is.EqualTo((true, true)), "Space でレバーを引けない");
            foreach (var key in new[] { Key.J, Key.K, Key.L })
            {
                yield return Tap(key);
            }

            yield return WaitForReelsToStop();

            // 一時停止（P）と早見表（/）。再開（P）で早見表も閉じる。
            var help = Object.FindAnyObjectByType<HelpScreen>();
            yield return Tap(Key.P);
            Assert.That(_battle.IsPaused, Is.True, "P で一時停止しない");
            yield return Tap(Key.Slash);
            Assert.That(help.IsOpen, Is.True, "/ で早見表が開かない");
            yield return Tap(Key.P);
            Assert.That((_battle.IsPaused, help.IsOpen), Is.EqualTo((false, false)), "P で再開しない／早見表が閉じない");

            // 撤退（Tab で選んで X）。
            yield return Tap(Key.Tab);
            yield return Tap(Key.X);
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(0), "Tab と X で撤退できない");

            yield return WaitForWave1ToEnd();
        }

        [UnityTest]
        public IEnumerator Mouse_Alone_Goes_From_Title_Through_Reel_Custom_And_Wave_1()
        {
            _waves = ShortWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);

            // タイトル ▶ → 設定2 のプレート → Game。
            yield return Click(GameObject.Find("Start Button").transform);
            yield return Click(GameObject.Find("Setting 2").transform);
            yield return WaitForGame();
            Assert.That(_cabinet.Setting, Is.EqualTo(2), "マウスで設定2を選べない");

            // リールのカスタムは 2026-11-14 に廃止（T341 で画面を削除）。

            // 召喚・配置（アイコンからマスへドラッグ）・向き（左をクリック）、強化アイコン、▶。
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
            yield return Click(bar.Find("Start Button"));
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.Fighting), "▶ で戦闘が始まらない");

            // スロット: レバーと3つのストップボタン。
            var coins = _cabinet.Wallet.Coins;
            yield return Click(_cabinet.LeverRect);
            Assert.That((_cabinet.IsSpinning, _cabinet.Wallet.Coins < coins), Is.EqualTo((true, true)), "レバーのクリックで回らない");
            for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
            {
                yield return Click(_cabinet.StopRect(reel));
            }

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
            yield return Click(bar.Find("Retreat Button"));
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(0), "クリックと撤退アイコンで撤退できない");

            yield return WaitForWave1ToEnd();
        }

        [UnityTest]
        public IEnumerator Mouse_Clicks_Retry_And_Title_On_The_Result()
        {
            _waves = LosingWaves;
            // 前のテストの Game シーンを拾わないよう、タイトルから始める。
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            GameSession.StartGame(1);
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
            Assert.That((_battle != before, _battle.Run.Stage, _battle.Run.Wave),
                Is.EqualTo((true, 1, 1)), "↻ のクリックでリトライしない");

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

            var title = Object.FindAnyObjectByType<TitleScreen>();
            if (clickFirst)
            {
                yield return Click(GameObject.Find("Start Button").transform);
                Assert.That(title.IsChoosingDifficulty, Is.True, "Game から戻った後、▶ のクリックが効かない");
                yield break;
            }

            yield return Press(Key.Enter, () => title.IsChoosingDifficulty);
            Assert.That(title.IsChoosingDifficulty, Is.True, "Game から戻った後、Enter が効かない");
        }

        // 初期コイン（30）では召喚（15）と強化（20）を両方は払えないので、強化の分だけ足す（操作の確認が目的）。
        private void AddUpgradeCoins() => _battle.Wallet.Add(_battle.Upgrades.NextCost("tank").Value);

        private IEnumerator LoseTheGame()
        {
            yield return ReelCustomTestUtil.CloseWithDefault(_battle);
            _battle.StartWave();
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

        private IEnumerator WaitForWave1ToEnd()
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (_battle.Run.Wave == 1 && !_battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That((_battle.Run.Stage, _battle.Run.Wave, _battle.Run.Phase), Is.EqualTo((1, 2, WavePhase.Preparing)),
                "1ウェーブを終えて次の準備に進まない");
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
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            _camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
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

        private IEnumerator MoveCursorTo(GridPoint target)
        {
            var deployment = _battle.Deployment;
            while (deployment.Cursor.X != target.X)
            {
                yield return Tap(deployment.Cursor.X < target.X ? Key.RightArrow : Key.LeftArrow);
            }

            while (deployment.Cursor.Y != target.Y)
            {
                yield return Tap(deployment.Cursor.Y < target.Y ? Key.DownArrow : Key.UpArrow);
            }
        }

        private static bool IsSelected(string buttonName)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.name == buttonName;
        }

        private Vector2 CellScreen(GridPoint cell) => _camera.WorldToScreenPoint(_fieldView.CellTopToWorld(cell));

        private static Vector2 ScreenOf(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);

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

        private IEnumerator Tap(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Click(Transform target)
        {
            var screen = ScreenOf(target);
            yield return MouseAt(screen, false);
            yield return MouseAt(screen, true);
            yield return MouseAt(screen, false);
        }

        private IEnumerator Drag(Transform from, Transform to)
        {
            var start = ScreenOf(from);
            var end = ScreenOf(to);
            yield return MouseAt(start, false);
            yield return MouseAt(start, true);
            yield return MouseAt(Vector2.Lerp(start, end, 0.5f), true);
            yield return MouseAt(end, true);
            yield return MouseAt(end, false);
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
    }
}
