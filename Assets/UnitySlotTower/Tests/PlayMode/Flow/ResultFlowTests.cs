using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-004: ゲームが終わるとリザルト（到達階・所持金・コイン）が開き、リトライで同じ設定のままルートから作り直し、タイトルへも戻れる（FR-005, T402, T350d）")]
    public sealed class ResultFlowTests
    {
        // ゲームオーバー用: 最初の戦闘に近接の敵 13 体（ライフ 10 より多い）。味方を置かないので全員ゴールに着く。
        private const string LosingWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,1,melee,S1,first_g1,0\n1,2,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "1,4,melee,S1,first_g1,0\n1,5,melee,S1,first_g1,0\n1,6,melee,S1,first_g1,0\n1,7,melee,S1,first_g1,0\n" +
            "1,8,melee,S1,first_g1,0\n1,9,melee,S1,first_g1,0\n1,10,melee,S1,first_g1,0\n1,11,melee,S1,first_g1,0\n" +
            "1,3,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n2,3,melee,S1,battle_a_g1,0\n3,3,melee,S1,battle_b_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        // クリア用: どの戦闘マスも近接の敵2体だけ（ライフ1ずつ減って戦闘が終わる）。
        private const string WinningWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n2,3,melee,S1,battle_a_g1,0\n3,3,melee,S1,battle_b_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n4,3,melee,S1,elite_a_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        private static string _waves;

        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;

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
            // 実機の入力（ゲームパッド・マウスを含む）は止め、仮想キーボードだけにする。
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
            SceneManager.sceneLoaded += ReplaceWaves;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= ReplaceWaves;
            _waves = null;
            Time.timeScale = 1f;
            InputSystem.RemoveDevice(_keyboard);
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
        public IEnumerator Game_Over_Shows_Result_And_Retry_Restarts_With_Same_Setting()
        {
            _waves = LosingWaves;
            GameSession.StartGame(4);
            yield return WaitForGame();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            var run = Object.FindAnyObjectByType<RunController>();
            var oldState = run.State;
            var startMoney = oldState.Money.Coins;
            var startCoins = oldState.Coins.Coins;
            var startLife = battle.Field.Life;
            yield return null;

            var result = Object.FindAnyObjectByType<ResultScreen>();
            yield return PlayUntilFinished(battle);
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.GameOver), "ライフ0でゲームオーバーにならない");
            yield return null;
            Assert.That((result.IsOpen, result.IsClear, Text(result, "floorText"), battle.IsInputBlocked, cabinet.InputLocked),
                Is.EqualTo((true, false, "1", true, true)), "ゲームオーバーのリザルトが開かない／到達階が違う／操作を止めない");
            Assert.That((Text(result, "moneyText"), Text(result, "slotCoinsText")),
                Is.EqualTo(("¥" + oldState.Money.Coins, oldState.Coins.Coins.ToString())), "所持金・コインの表示が違う");
            battle.SetPaused(true);
            Assert.That(battle.IsPaused, Is.False, "リザルト中に一時停止できてしまう");

            // AC-004: Enter（初期選択の ▶）でリトライ。同じ設定で、ルート・進行・ライフ・所持金・コインが初期状態に戻る。
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene &&
                                                Object.FindAnyObjectByType<BattleController>() != battle);
            yield return WaitForGame();
            battle = Object.FindAnyObjectByType<BattleController>();
            cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            result = Object.FindAnyObjectByType<ResultScreen>();
            run = Object.FindAnyObjectByType<RunController>();
            Assert.That(battle.Run, Is.Not.Null, "リトライ後に進行が作られない");
            Assert.That((GameSession.SelectedSetting, cabinet.Setting), Is.EqualTo(((int?)4, 4)), "リトライで設定が変わった");
            Assert.That(run.State, Is.Not.SameAs(oldState), "リトライで RunState が作り直されない");
            Assert.That((run.Visited.Count, run.State.Floor), Is.EqualTo((1, 1)), "リトライでルートの最初から始まらない");
            Assert.That(run.State.Current, Is.SameAs(run.State.Route.Start), "リトライで今のマスがルートの最初ではない");
            Assert.That((battle.Floor, battle.NodeCount, battle.Field.Life, run.State.Money.Coins, run.State.Coins.Coins),
                Is.EqualTo((1, 1, startLife, startMoney, startCoins)), "リトライで進行が初期化されない");
            Assert.That(result.IsOpen, Is.False, "リトライ後にリザルトが開いている");
        }

        [UnityTest]
        [Description("AC-004 の通し: タイトル→難易度→ルート→戦闘→ルート→各マス（回復を含む）→ボス→リザルト→タイトル")]
        public IEnumerator Title_To_Clear_Through_Route_Shows_Result_And_Keyboard_Returns_To_Title()
        {
            _waves = WinningWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            var title = Object.FindAnyObjectByType<TitleScreen>();
            yield return Press(Key.Enter, () => title.IsChoosingDifficulty);
            Assert.That(title.IsChoosingDifficulty, Is.True, "Enter で難易度選択に進まない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene);
            yield return WaitForGame();
            Assert.That(GameSession.SelectedSetting, Is.EqualTo(1), "難易度の初期選択（設定1）で始まらない");

            var battle = Object.FindAnyObjectByType<BattleController>();
            var result = Object.FindAnyObjectByType<ResultScreen>();
            yield return null;
            // ルートは最長7戦（T350a）で、敵1体ずつでもライフ 10 では足りないため、戦闘マスごとにライフを戻す。
            var run = Object.FindAnyObjectByType<RunController>();
            void Refill() => run.State.Heal(run.State.MaxLife);
            battle.NodeBegan += Refill;

            yield return PlayUntilFinished(battle);
            battle.NodeBegan -= Refill;
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.Completed), "最終ウェーブを終えてもクリアにならない");
            // ルートを1階からボスまで1階ずつ進み、ボスの直前の回復マスも通った。
            var floors = new List<int>();
            var kinds = new List<string>();
            foreach (var node in run.Visited)
            {
                floors.Add(node.Floor);
                kinds.Add(node.Kind);
            }

            Assert.That(floors, Is.EqualTo(Enumerable.Range(1, run.Rules.Floors)), "ルートを1階ずつ通っていない");
            Assert.That((kinds[0], kinds[kinds.Count - 2], kinds[kinds.Count - 1]),
                Is.EqualTo((RouteNodeKinds.First, RouteNodeKinds.Rest, RouteNodeKinds.Boss)), "最初・回復・ボスのマスを通っていない");
            yield return null;
            Assert.That((result.IsOpen, result.IsClear, Text(result, "floorText")),
                Is.EqualTo((true, true, run.Rules.Floors.ToString())), "クリアのリザルトが開かない／到達階が違う");
            Assert.That((Text(result, "moneyText"), Text(result, "slotCoinsText")),
                Is.EqualTo(("¥" + run.State.Money.Coins, run.State.Coins.Coins.ToString())), "所持金・コインの表示が違う");

            yield return Press(Key.RightArrow, () => result.Selected == 1);
            Assert.That(result.Selected, Is.EqualTo(1), "→ で ◀（タイトル）を選べない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.TitleScene);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene), "タイトルへ戻らない");
        }

        // ルート画面では行き先を選び（T350b）、ゲームが終わるまで速めて進める。
        private static IEnumerator PlayUntilFinished(BattleController battle)
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 90f + 7 * BattlePresentationTiming.VictoryBand;
            while (!battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                RouteTestUtil.Step();
                // 勝利の帯が時間を 0→1 に戻すので、次の戦闘では早送りを再開する。
                if (!battle.IsShowingVictory && (battle.IsInIntro || battle.Run.Phase == WavePhase.Fighting))
                    Time.timeScale = 20f;
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(battle.IsFinished, Is.True, "時間内にゲームが終わらない");
        }

        private static IEnumerator WaitForGame()
        {
            BattleController battle = null;
            for (var i = 0; i < 300; i++)
            {
                battle = SceneManager.GetActiveScene().name == GameSession.GameScene
                    ? Object.FindAnyObjectByType<BattleController>()
                    : null;
                if (battle != null && battle.Run != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(battle != null && battle.Run != null, Is.True, "Game シーンの進行が作られない");
            yield return null;
        }

        // 1フレームだけ押すと取りこぼすことがあるので、効果が出るまで押し続けて離す（TitleFlowTests と同じ）。
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

        private static string Text(ResultScreen result, string field) =>
            ((TMP_Text)typeof(ResultScreen).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result))
            .text;

        // sceneLoaded は Awake の後・Start の前に呼ばれるので、BattleController が表を読む前に差し替えられる。リトライ後は本来の表。
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
            _waves = null;
        }
    }
}
