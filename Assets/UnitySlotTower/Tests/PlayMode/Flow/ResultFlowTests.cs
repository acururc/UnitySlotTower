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
    [Description("AC-004, spec 002 FR-101: ゲームが終わるとリザルト（到達ステージ・所持金・コイン）が開き、リトライでステージ1から作り直し、タイトルへも戻れる（FR-005, T402, T350d）")]
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
        [Description("spec 010 AC-203: ゲームオーバーのリザルトは やり直し（同じシーンでステージの始めへ）と タイトル の2つ")]
        public IEnumerator Game_Over_Shows_Result_With_Stage_Retry_And_Title()
        {
            _waves = LosingWaves;
            GameSession.StartGame();
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
                Is.EqualTo((true, false, "1", true, true)), "ゲームオーバーのリザルトが開かない／到達ステージが違う／操作を止めない");
            Assert.That((Text(result, "moneyText"), Text(result, "slotCoinsText")),
                Is.EqualTo(("¥" + oldState.Money.Coins, oldState.Coins.Coins.ToString())), "所持金・コインの表示が違う");
            battle.SetPaused(true);
            Assert.That(battle.IsPaused, Is.False, "リザルト中に一時停止できてしまう");
            Assert.That((result.StageRetryButton.gameObject.activeSelf, result.RetryButton.gameObject.activeSelf, result.Selected),
                Is.EqualTo((true, false, 0)), "spec 010 AC-203: ゲームオーバーで やり直し が出ない／最初から が出ている／最初に選ばれていない");

            // spec 010 AC-203: Enter（初期選択の やり直し）で、同じシーンのままステージ1の始めに戻る。
            var nodes = battle.NodeCount;
            yield return Press(Key.Enter, () => !result.IsOpen);
            Assert.That((Object.FindAnyObjectByType<BattleController>() == battle, run.State == oldState, result.IsOpen),
                Is.EqualTo((true, true, false)), "やり直しでシーンが読み込み直された／リザルトが閉じない");
            Assert.That((battle.StageNumber, battle.NodeCount, battle.Field.Life, oldState.Money.Coins, oldState.Coins.Coins),
                Is.EqualTo((1, nodes + 1, startLife, startMoney, startCoins)), "やり直しでステージ1の開始時に戻らない");

            // もう一度負けると、また やり直し とタイトルの2つだけ（最初から は出ない）。→ でタイトルを選んで Enter。
            yield return PlayUntilFinished(battle);
            yield return null;
            Assert.That((result.IsOpen, result.StageRetryButton.gameObject.activeSelf, result.RetryButton.gameObject.activeSelf,
                    result.Selected), Is.EqualTo((true, true, false, 0)),
                "2回目のゲームオーバーのリザルトが やり直し・タイトル の2つにならない");
            yield return Press(Key.RightArrow, () => result.Selected == 1);
            Assert.That(result.Selected, Is.EqualTo(1), "→ でタイトルを選べない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.TitleScene);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene), "タイトルへ戻らない");
        }

        // spec 010 用: ステージ1は近接1体（ゴールしてライフ9でクリア）、ステージ2で近接13体に負ける。
        private const string LoseAtStageTwoWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n" +
            "2,0,melee,S1,battle_a_g1,0\n2,1,melee,S1,battle_a_g1,0\n2,2,melee,S1,battle_a_g1,0\n2,3,melee,S1,battle_a_g1,0\n" +
            "2,4,melee,S1,battle_a_g1,0\n2,5,melee,S1,battle_a_g1,0\n2,6,melee,S1,battle_a_g1,0\n2,7,melee,S1,battle_a_g1,0\n" +
            "2,8,melee,S1,battle_a_g1,0\n2,9,melee,S1,battle_a_g1,0\n2,10,melee,S1,battle_a_g1,0\n2,11,melee,S1,battle_a_g1,0\n" +
            "2,12,melee,S1,battle_a_g1,0\n";

        [UnityTest]
        [Description("spec 010 AC-202: ステージ2で負けて やり直し を選ぶと、ステージ2の開始時の値（ライフ・所持金・コイン・強化）で、" +
                     "盤上に味方・敵がなく、コインの倍率が初期値のまま同じステージが始まる")]
        public IEnumerator Retrying_Stage_Two_Restores_The_Stage_Start()
        {
            _waves = LoseAtStageTwoWaves;
            GameSession.StartGame();
            yield return WaitForGame();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var run = Object.FindAnyObjectByType<RunController>();
            var result = Object.FindAnyObjectByType<ResultScreen>();
            var state = run.State;

            // ステージ2が始まるまで早送り（ステージ1の1体はゴールに着く）。
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (battle.StageNumber < 2 && Time.realtimeSinceStartup < limit)
            {
                if (!battle.IsShowingVictory) Time.timeScale = 20f;
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(battle.StageNumber, Is.EqualTo(2), "ステージ2へ進まない");
            var start = (state.Life, state.Money.Coins, state.Coins.Coins, state.Upgrades.Level("sword"));
            var total = battle.Run.RemainingEnemies;

            // ステージ2の中で値を変える: 所持金を足して強化し、コインを使い、コインの倍率を上げる。
            state.Money.Add(1000);
            Assert.That(state.Upgrades.TryUpgrade("sword"), Is.EqualTo(UpgradeResult.Upgraded), "強化できない");
            state.Coins.TrySpend(1);
            battle.Run.ApplySlot(SlotSymbol.Coin);

            yield return PlayUntilFinished(battle);
            yield return null;
            Assert.That((battle.Run.Phase, result.IsOpen, battle.StageNumber), Is.EqualTo((WavePhase.GameOver, true, 2)),
                "ステージ2でゲームオーバーのリザルトが開かない");

            result.StageRetryButton.onClick.Invoke();
            yield return null;
            Assert.That((result.IsOpen, battle.StageNumber, run.State == state), Is.EqualTo((false, 2, true)),
                "やり直しでステージ2が同じ RunState のまま始まらない");
            Assert.That((state.Life, state.Money.Coins, state.Coins.Coins, state.Upgrades.Level("sword")), Is.EqualTo(start),
                "ライフ・所持金・コイン・強化がステージ2の開始時の値に戻らない");
            Assert.That((battle.Field.Units.Count, battle.Field.Enemies.Count, battle.Run.RemainingEnemies, battle.Field.Effects.Coins),
                Is.EqualTo((0, 0, total, 0)), "盤面・残りの敵の数・コインの倍率がステージの開始時と違う");
        }

        [UnityTest]
        [Description("AC-004・AC-101 の通し: タイトル→ステージ1〜8→クリアのリザルト→タイトル（難易度の選択はない）")]
        public IEnumerator Title_To_Clear_Through_Eight_Stages_Shows_Result_And_Keyboard_Returns_To_Title()
        {
            _waves = StageTestUtil.ShortWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene);
            yield return WaitForGame();

            var battle = Object.FindAnyObjectByType<BattleController>();
            var result = Object.FindAnyObjectByType<ResultScreen>();
            var run = Object.FindAnyObjectByType<RunController>();
            var stages = new List<int> { battle.StageNumber };
            void OnBegan(int stage) => stages.Add(stage);
            run.StageBegan += OnBegan;
            yield return null;

            yield return PlayUntilFinished(battle);
            run.StageBegan -= OnBegan;
            Assert.That((battle.Run.Phase, battle.Run.IsRunCleared), Is.EqualTo((WavePhase.Completed, true)),
                "ステージ8を終えてもクリアにならない");
            Assert.That(stages, Is.EqualTo(Enumerable.Range(1, 8)), "ステージを1から8の順に通っていない");
            yield return null;
            Assert.That((result.IsOpen, result.IsClear, Text(result, "floorText")),
                Is.EqualTo((true, true, "8")), "クリアのリザルトが開かない／到達ステージが違う");
            Assert.That((result.RetryButton.gameObject.activeSelf, result.StageRetryButton.gameObject.activeSelf),
                Is.EqualTo((true, false)), "spec 010 FR-204: クリアのリザルトが もう一度・タイトル の2つでない");
            Assert.That((Text(result, "moneyText"), Text(result, "slotCoinsText")),
                Is.EqualTo(("¥" + run.State.Money.Coins, run.State.Coins.Coins.ToString())), "所持金・コインの表示が違う");

            yield return Press(Key.RightArrow, () => result.Selected == 1);
            Assert.That(result.Selected, Is.EqualTo(1), "→ で ◀（タイトル）を選べない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.TitleScene);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene), "タイトルへ戻らない");
        }

        // ゲームが終わるまで速めて進める。
        private static IEnumerator PlayUntilFinished(BattleController battle)
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 90f + 7 * BattlePresentationTiming.VictoryBand;
            while (!battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
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
