using System.Collections;
using System.Collections.Generic;
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
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Tests.PlayMode.Battle;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-004 の一部: ゲームが終わるとリザルトが開き、リトライで同じ設定のまま初期化され、タイトルへも戻れる（FR-005, T402）")]
    public sealed class ResultFlowTests
    {
        // ゲームオーバー用: S1-W1 に近接の敵 12 体（ライフ 10 より多い）。味方を置かないので全員ゴールに着く。
        private const string LosingWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,1,1,melee,S1,first_g1,0\nfirst,1,2,melee,S1,first_g1,0\nfirst,1,3,melee,S1,first_g1,0\n" +
            "first,1,4,melee,S1,first_g1,0\nfirst,1,5,melee,S1,first_g1,0\nfirst,1,6,melee,S1,first_g1,0\nfirst,1,7,melee,S1,first_g1,0\n" +
            "first,1,8,melee,S1,first_g1,0\nfirst,1,9,melee,S1,first_g1,0\nfirst,1,10,melee,S1,first_g1,0\nfirst,1,11,melee,S1,first_g1,0\n" +
            "first,2,0,melee,S1,first_g1,0\nbattle_a,1,0,melee,S1,battle_a_g1,0\nbattle_b,1,0,melee,S1,battle_b_g1,0\nbattle_c,1,0,melee,S1,battle_c_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

        // クリア用: 全6ウェーブとも近接の敵1体だけ（ライフ1ずつ減ってウェーブが終わる）。
        private const string WinningWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\nbattle_a,1,0,melee,S1,battle_a_g1,0\nbattle_b,1,0,melee,S1,battle_b_g1,0\nbattle_c,1,0,melee,S1,battle_c_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

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
            var startCoins = battle.Wallet.Coins;
            var startLife = battle.Field.Life;
            yield return null;

            var result = Object.FindAnyObjectByType<ResultScreen>();
            yield return PlayUntilFinished(battle);
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.GameOver), "ライフ0でゲームオーバーにならない");
            yield return null;
            Assert.That((result.IsOpen, result.IsClear, Text(result, "waveText"), battle.IsInputBlocked, cabinet.InputLocked),
                Is.EqualTo((true, false, "1-1", true, true)), "ゲームオーバーのリザルトが開かない／操作を止めない");
            Assert.That(Text(result, "coinsText"), Is.EqualTo(battle.Wallet.Coins.ToString()));
            battle.SetPaused(true);
            Assert.That(battle.IsPaused, Is.False, "リザルト中に一時停止できてしまう");

            // AC-004: Enter（初期選択の ▶）でリトライ。同じ設定で、進行・ライフ・コインが初期状態に戻る。
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene &&
                                                Object.FindAnyObjectByType<BattleController>() != battle);
            yield return WaitForGame();
            battle = Object.FindAnyObjectByType<BattleController>();
            cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            result = Object.FindAnyObjectByType<ResultScreen>();
            Assert.That(battle.Run, Is.Not.Null, "リトライ後に進行が作られない");
            Assert.That((GameSession.SelectedSetting, cabinet.Setting), Is.EqualTo(((int?)4, 4)), "リトライで設定が変わった");
            Assert.That((battle.Floor, battle.Run.Wave, battle.Run.Phase, battle.Field.Life, battle.Wallet.Coins),
                Is.EqualTo((1, 1, WavePhase.Preparing, startLife, startCoins)), "リトライで進行が初期化されない");
            Assert.That(result.IsOpen, Is.False, "リトライ後にリザルトが開いている");
        }

        [UnityTest]
        public IEnumerator Clear_Shows_Result_And_Keyboard_Returns_To_Title()
        {
            _waves = WinningWaves;
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            yield return WaitForGame();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var result = Object.FindAnyObjectByType<ResultScreen>();
            yield return ReelCustomTestUtil.CloseWithDefault(battle);
            // ルートは最長7戦（T350a）で、敵1体ずつでもライフ 10 では足りないため、戦闘マスごとにライフを戻す。
            var run = Object.FindAnyObjectByType<RunController>();
            void Refill() => run.State.Heal(run.State.MaxLife);
            battle.NodeBegan += Refill;

            yield return PlayUntilFinished(battle);
            battle.NodeBegan -= Refill;
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.Completed), "最終ウェーブを終えてもクリアにならない");
            yield return null;
            Assert.That((result.IsOpen, result.IsClear, Text(result, "waveText")),
                Is.EqualTo((true, true, $"{run.Rules.Floors}-2")), "クリアのリザルトが開かない");

            yield return Press(Key.RightArrow, () => result.Selected == 1);
            Assert.That(result.Selected, Is.EqualTo(1), "→ で ◀（タイトル）を選べない");
            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.TitleScene);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene), "タイトルへ戻らない");
        }

        // ▶ を押し続け、ゲームが終わるまで速めて進める。
        private static IEnumerator PlayUntilFinished(BattleController battle)
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (!battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                battle.StartWave();
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
