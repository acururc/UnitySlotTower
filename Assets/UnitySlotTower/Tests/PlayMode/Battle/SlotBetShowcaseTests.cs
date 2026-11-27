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
        // ステージ1: ウェーブ1（見せ場、stage_waves.csv の slot_showcase）は3レーンから近接2体ずつ、ウェーブ2は近接1体。ほかのステージは1体。
        private const string ShowcaseWaves =
            "stage,wave,time,enemy_id,spawn,path_id,is_boss,count,interval\n" +
            "1,1,0,melee,S1,first_g1,0,2,0.3\n1,1,0,melee,S2,first_g2,0,2,0.3\n1,1,0,melee,S3,first_g3,0,2,0.3\n" +
            "1,2,0,melee,S2,first_g2,0,1,0\n" +
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
            // 見せ場は PlayMode テストの既定で飛ばす（PlayModeTestSetup）。見せ場のテストだけ起こす。
            BattleController.SkipShowcase = !TestContext.CurrentContext.Test.MethodName.StartsWith("Showcase");
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
            BattleController.SkipShowcase = true;
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
        [Description("spec 015 AC-001/AC-002/AC-008: ウェーブ1（3レーン）が出そろうと盤面が止まり、賭け3枚・レバーの点滅・暗幕と矢印。見せ場の間は置けない。コイン0でも引けて減らない。赤7・青7・黄7で群れが全滅し、お金は増えず、青7と黄7の効果が消え、置けるようになり、賭けが1枚に戻る")]
        public IEnumerator Showcase_Holds_Until_The_Lever_Then_Triple_Hit_Strikes()
        {
            // 見せ場が始まるまではレバーも BET も効かない（spec 015 FR-009）。
            _cabinet.Wallet.Reset(5);
            var bet = _cabinet.SelectedBet;
            yield return Tap(Key.Space);
            BetButton().onClick.Invoke();
            Assert.That((_battle.Field.ShowcaseStarted, _cabinet.InputLocked, _cabinet.IsSpinning, _cabinet.Wallet.Coins, _cabinet.SelectedBet),
                Is.EqualTo((false, true, false, 5, bet)), "見せ場の前にスロットを回せる・賭けを変えられる");
            _cabinet.Wallet.Reset(0);
            Time.timeScale = 10f;
            var limit = Time.realtimeSinceStartup + 60f;
            while (!_battle.Field.IsShowcaseHolding && !_battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_battle.Field.IsShowcaseHolding, Is.True, "ウェーブ1で止まらない");
            Assert.That(_battle.Field.Waves.StartedWaves, Is.EqualTo(1), "見せ場がウェーブ1でない");
            yield return null;
            Assert.That((_cabinet.LeverHighlighted, _cabinet.Bet, _cabinet.Wallet.Coins, _battle.Field.Enemies.Count),
                Is.EqualTo((true, 3, 0, 6)), "見せ場の準備（点滅・賭け3枚・コインは補わない・群れが場にいる）");
            var lanes = new HashSet<string>();
            foreach (var enemy in _battle.Field.Enemies)
            {
                lanes.Add(enemy.Path.Id);
            }

            Assert.That(lanes, Is.EquivalentTo(new[] { "first_g1", "first_g2", "first_g3" }), "群れが3レーンから来ない（spec 015 FR-001）");
            var unitId = _battle.Units.All[0].Id;
            Assert.That(_battle.Deployment.Select(unitId), Is.EqualTo(UnitySlotTower.Core.Battle.SummonResult.Locked),
                "見せ場の間に置ける（spec 015 FR-006）");
            var money = _battle.Wallet.Coins;
            var guide = Object.FindAnyObjectByType<SlotShowcaseGuide>();
            Assert.That(guide, Is.Not.Null, "見せ場の案内がない（Setup の Showcase Guide (spec 015)）");
            Assert.That(guide.IsShown, Is.True, "止めている間に暗幕と矢印が出ない（spec 015 FR-004）");
            yield return null;
            var shades = (RectTransform[])typeof(SlotShowcaseGuide).GetField("shades", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guide);
            var top = shades[0];
            Assert.That((top.gameObject.activeInHierarchy, top.sizeDelta.y > 100f, top.GetComponent<Image>().color.a > 0.5f),
                Is.EqualTo((true, true, true)), "筐体の上（戦闘フィールド）が暗幕で覆われない");
            var cabinetCorners = new Vector3[4];
            ((RectTransform)_cabinet.transform).GetWorldCorners(cabinetCorners);
            var topCorners = new Vector3[4];
            top.GetWorldCorners(topCorners);
            Assert.That(topCorners[0].y, Is.EqualTo(cabinetCorners[1].y).Within(1f), "上の暗幕の下端が筐体の上端に合わない");

            var tick = _battle.Field.Tick;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.EqualTo(tick), "止まっている間に戦闘が進む");

            yield return Tap(Key.Space);
            Assert.That((_cabinet.IsSpinning, _cabinet.SpinBet, _cabinet.Wallet.Coins), Is.EqualTo((true, 3, 0)),
                "コイン0で賭け3枚の見せ場が引けない、またはコインが減った（spec 015 FR-001）");
            Assert.That(_cabinet.LeverHighlighted, Is.False, "引いても点滅が止まらない");
            yield return null;
            Assert.That(guide.IsShown, Is.False, "引いても暗幕と矢印が消えない");
            yield return WaitForStop();
            Assert.That(_cabinet.Hits, Is.EqualTo(BattleController.ShowcaseHits), "決め打ちの三重当たり（赤7・青7・黄7）でない");
            Assert.That(_battle.Field.IsShowcaseHolding, Is.False, "揃っても動き出さない");
            yield return null;
            Assert.That(_cabinet.SelectedBet, Is.EqualTo(1), "見せ場の後に賭けが1枚に戻らない（spec 015 FR-003）");

            // 赤7 60%＋毒 12%×5回（賭け3枚）で群れが全滅する（spec 015 FR-002）。
            Time.timeScale = 3f;
            var until = Time.realtimeSinceStartup + 10f;
            while (_battle.Field.Enemies.Count > 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_battle.Field.Enemies.Count, Is.EqualTo(0), "三重当たりで群れが全滅しない");
            yield return null;
            var effects = _battle.Field.Effects;
            Assert.That((_battle.Field.ShowcaseFinished, effects.IsStopped, effects.IsStopPending, effects.IsPoisoned, effects.IsPoisonPending),
                Is.EqualTo((true, false, false, false, false)), "群れの全滅で青7と黄7の効果が消えない（spec 015 FR-007）");
            Assert.That((_battle.Wallet.Coins, _cabinet.Wallet.Coins), Is.EqualTo((money, 0)), "見せ場の群れがお金を落とした（spec 015 FR-005）");
            _battle.Wallet.Add(_battle.Units.All[0].Cost);
            Assert.That(_battle.Deployment.Select(unitId), Is.EqualTo(UnitySlotTower.Core.Battle.SummonResult.Summoned),
                "見せ場が終わっても置けない（spec 015 FR-006）");
        }
    }
}
