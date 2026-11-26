using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    /// <summary>spec 011 AC-001（BET ボタン）・AC-005（ステージ1の見せ場）の PlayMode 部分。</summary>
    public sealed class SlotBetShowcaseTests
    {
        // ステージ1: ウェーブ1は近接1体、ウェーブ5（見せ場、stage_waves.csv の slot_showcase）は近接3体。ほかのステージは1体。
        private const string ShowcaseWaves =
            "stage,wave,time,enemy_id,spawn,path_id,is_boss,count,interval\n" +
            "1,1,0,melee,S2,first_g2,0,1,0\n1,5,0,melee,S2,first_g2,0,3,0.3\n" +
            "2,1,0,melee,S1,battle_a_g1,0,1,0\n3,1,0,melee,S1,battle_b_g1,0,1,0\n4,1,0,melee,S1,elite_a_g1,0,1,0\n" +
            "5,1,0,melee,S1,battle_c_g1,0,1,0\n6,1,0,melee,S1,battle_d_g1,0,1,0\n7,1,0,melee,S1,battle_e_g1,0,1,0\n" +
            "8,1,0,melee,S1,boss_a_g1,0,1,0\n";

        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;

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
            SlotCabinet.RandomOverride = new SeededRandom(3);
            SceneManager.sceneLoaded += UseShowcaseWaves;
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return StageTestUtil.WaitForIntro(_battle);
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= UseShowcaseWaves;
            SlotCabinet.RandomOverride = null;
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

        private static void UseShowcaseWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (battle != null)
            {
                typeof(BattleController)
                    .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(battle, new TextAsset(ShowcaseWaves));
            }
        }

        private Button BetButton() =>
            (Button)typeof(SlotCabinet).GetField("betButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cabinet);

        private Graphic[] LineMarks() =>
            (Graphic[])typeof(SlotCabinet).GetField("lineMarks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cabinet);

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator WaitForStop()
        {
            var until = Time.realtimeSinceStartup + 8f;
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(_cabinet.IsSpinning, Is.False, "リールが止まらない");
        }

        [UnityTest]
        [Description("spec 011 AC-001: BET ボタンで 1→2→3→1、コインの絵と有効ラインの印が枚数に合う。回転中は変わらない。レバーで枚数ぶん減る")]
        public IEnumerator Bet_Button_Cycles_And_Lever_Spends_The_Bet()
        {
            _cabinet.Wallet.Add(10);
            var button = BetButton();
            Assert.That(button, Is.Not.Null, "BET ボタンがない（Update Slot Bet (spec 011)）");
            var seen = new List<int> { _cabinet.SelectedBet };
            for (var i = 0; i < 3; i++)
            {
                button.onClick.Invoke();
                yield return null;
                seen.Add(_cabinet.SelectedBet);
            }

            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3, 1 }));

            button.onClick.Invoke();
            yield return null;
            var marks = LineMarks();
            Assert.That(marks[0].color.a > marks[2].color.a && marks[1].color.a > marks[2].color.a, Is.True,
                "2枚のとき上段・中段が明るく下段が暗い");

            var coins = _cabinet.Wallet.Coins;
            yield return Tap(Key.Space);
            Assert.That((_cabinet.IsSpinning, coins - _cabinet.Wallet.Coins, _cabinet.SpinBet), Is.EqualTo((true, 2, 2)));
            button.onClick.Invoke();
            Assert.That(_cabinet.SelectedBet, Is.EqualTo(2), "回転中に変わった");
            yield return WaitForStop();
        }

        [UnityTest]
        [Description("spec 011 AC-005: ウェーブ5が出そろうと盤面が止まり、コイン3枚・賭け3枚・レバーの点滅。引くと三重当たりで動き出し、大群に効く")]
        public IEnumerator Showcase_Holds_Until_The_Lever_Then_Triple_Hit_Strikes()
        {
            _cabinet.Wallet.Reset(0);
            Time.timeScale = 10f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (!_battle.Field.IsShowcaseHolding && !_battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_battle.Field.IsShowcaseHolding, Is.True, "ウェーブ5で止まらない");
            yield return null;
            Assert.That((_cabinet.LeverHighlighted, _cabinet.Bet, _cabinet.Wallet.Coins >= 3, _battle.Field.Enemies.Count),
                Is.EqualTo((true, 3, true, 3)), "見せ場の準備（点滅・賭け3枚・コイン3枚・大群が場にいる）");

            var tick = _battle.Field.Tick;
            var hp = _battle.Field.Enemies[0].Hp;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.EqualTo(tick), "止まっている間に戦闘が進む");

            yield return Tap(Key.Space);
            Assert.That(_cabinet.LeverHighlighted, Is.False, "引いても点滅が止まらない");
            yield return WaitForStop();
            Assert.That(_cabinet.Hits, Is.EqualTo(BattleController.ShowcaseHits), "決め打ちの三重当たりでない");
            Assert.That(_battle.Field.IsShowcaseHolding, Is.False, "揃っても動き出さない");

            var until = Time.realtimeSinceStartup + 5f;
            while (_battle.Field.Enemies.Count > 0 && _battle.Field.Enemies[0].Hp == hp && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            var effects = _battle.Field.Effects;
            Assert.That(_battle.Field.Enemies.Count == 0 || _battle.Field.Enemies[0].Hp <= hp - (int)(hp * 0.6 * 0.99), Is.True,
                "赤7（3枚）が大群に入らない");
            Assert.That((effects.IsStopped || effects.IsStopPending, effects.IsAttackUp || effects.IsAttackPending),
                Is.EqualTo((true, true)), "青7・バーが発動しない");
        }
    }
}
