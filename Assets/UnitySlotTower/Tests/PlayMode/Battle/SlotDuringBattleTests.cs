using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-011 の一部: スロット操作中も敵が進む。財布はスロットと戦闘で1つ（FR-011, FR-040, T301）")]
    public sealed class SlotDuringBattleTests
    {
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
            // Editor が非フォーカスでもフレームと入力が進むようにする（後で戻す）。
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

            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return RouteTestUtil.WaitForIntro(_battle);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
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
        [Description("AC-011: 最初の戦闘は回数固定（D-68）。Space で回り、コインは減らず残り回数が減る。回転中も敵が進む")]
        public IEnumerator Enemies_Keep_Moving_While_The_Reels_Spin_And_The_First_Battle_Uses_Fixed_Spins()
        {
            Assert.That(_battle.Run.State.Coins, Is.SameAs(_cabinet.Wallet));
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            for (var i = 0; i < 60 && _battle.Field.Enemies.Count == 0; i++)
            {
                yield return null;
            }

            var spinsBefore = _cabinet.FixedSpinsLeft;
            Assert.That(spinsBefore, Is.EqualTo(_battle.Run.Encounter.FixedSpins).And.GreaterThan(0), "最初の戦闘が回数固定でない");
            var coinsBefore = _cabinet.Wallet.Coins;
            yield return Tap(Key.Space);
            for (var i = 0; i < 180 && !reels[0].IsSpinning; i++)
            {
                yield return null;
            }

            Assert.That(reels[0].IsSpinning, Is.True, "Space でリールが回らない");
            Assert.That((_cabinet.Wallet.Coins, _cabinet.FixedSpinsLeft), Is.EqualTo((coinsBefore, spinsBefore - 1)),
                "回数固定なのにコインが減るか、残り回数が減らない");

            var enemy = _battle.Field.Enemies[0];
            var tick = _battle.Field.Tick;
            var distance = enemy.Distance;
            yield return new WaitForSeconds(0.5f);

            Assert.That((reels[0].IsSpinning, _battle.Field.Tick > tick, enemy.Distance > distance),
                Is.EqualTo((true, true, true)));
        }

        [UnityTest]
        public IEnumerator Dropped_Coins_Go_Into_The_Shared_Wallet()
        {
            // 置けるマスすべてに剣士を置き、W1 を最後まで進める。
            var map = Object.FindAnyObjectByType<BattleFieldView>().Map;
            var sword = _battle.Units.Get("sword");
            // W1 を終えると次のウェーブの戦闘に作り直されるので、W1 の戦闘を持っておく（T305）。
            var field = _battle.Field;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (UnitPlacement.CanPlace(map, new GridPoint(x, y), sword))
                    {
                        field.AddUnit(sword, new GridPoint(x, y), Facing.Right);
                    }
                }
            }

            yield return null;
            var coinsBefore = _cabinet.Wallet.Coins;
            var moneyBefore = _battle.Wallet.Coins;
            var droppedBefore = (field.DroppedCoins, field.DroppedMoney);
            field.Advance(60 * 60);
            // 進行が次のステップでウェーブの終わりに気づくまで待つ（ステップが0のフレームもある）。
            for (var i = 0; i < 60 && _battle.Run.Phase == WavePhase.Fighting; i++)
            {
                yield return null;
            }

            // コインは筐体のコインへ、所持金は召喚の財布へ（plan 16.2。ライフボーナスは廃止）。
            Assert.That((_cabinet.Wallet.Coins - coinsBefore, _battle.Wallet.Coins - moneyBefore),
                Is.EqualTo((field.DroppedCoins - droppedBefore.DroppedCoins,
                    field.DroppedMoney - droppedBefore.DroppedMoney)));
            Assert.That(field.ArrivedCount, Is.LessThan(5), "1体も倒せていない");
        }

        [UnityTest]
        [Description("AC-041: コインで回すと賭け枚数だけ減り、止まった中段は出目どおり（揃いは3本とも同じ図柄、ハズレはどの段も揃わない）")]
        public IEnumerator Coin_Spins_Take_The_Bet_And_The_Reels_Stop_On_The_Outcome()
        {
            _cabinet.SetFixedSpins(0);
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            for (var row = -1; row <= 1; row++)
            {
                var a = reels[0].ShownChild(row);
                Assert.That(a == reels[1].ShownChild(row) && a == reels[2].ShownChild(row), Is.False,
                    $"回す前の表示で {row} 段が揃って当たりに見える");
            }

            for (var spin = 0; spin < 3; spin++)
            {
                var coinsBefore = _cabinet.Wallet.Coins;
                yield return Tap(Key.Space);
                Assert.That((_cabinet.IsSpinning, coinsBefore - _cabinet.Wallet.Coins), Is.EqualTo((true, _cabinet.Bet)),
                    $"{spin + 1}回目: 回らないか、賭け枚数だけ減らない");
                yield return WaitForReelsToStop();

                if (_cabinet.Outcome is Core.Slot.SlotSymbol aligned)
                {
                    Assert.That(new[] { reels[0].ShownChild(0), reels[1].ShownChild(0), reels[2].ShownChild(0) },
                        Is.All.EqualTo((int)aligned), $"{spin + 1}回目: 中段が出目 {aligned} に揃っていない");
                }
                else
                {
                    for (var row = -1; row <= 1; row++)
                    {
                        var a = reels[0].ShownChild(row);
                        Assert.That(a == reels[1].ShownChild(row) && a == reels[2].ShownChild(row), Is.False,
                            $"{spin + 1}回目: ハズレなのに {row} 段が揃っている");
                    }
                }
            }
        }

        [UnityTest]
        [Description("plan 16.8: 回数固定のあいだはコインの代わりに残り回数を出し、0 になったらレバーを受け付けない")]
        public IEnumerator Fixed_Spins_Replace_The_Coin_Count_And_Stop_At_Zero()
        {
            var credit = _cabinet.transform.Find("Credit").GetComponent<TMPro.TMP_Text>();
            var coinIcon = _cabinet.transform.Find("Coin Icon").GetComponent<UnityEngine.UI.Graphic>();
            var fixedIcon = _cabinet.transform.Find("Fixed Spins Icon").gameObject;

            _cabinet.SetFixedSpins(1);
            yield return null;
            Assert.That((credit.text, coinIcon.enabled, fixedIcon.activeSelf), Is.EqualTo(("1", false, true)),
                "回数固定の残り回数と印が出ない");

            var coins = _cabinet.Wallet.Coins;
            yield return Tap(Key.Space);
            yield return WaitForReelsToStop();
            yield return Tap(Key.Space);
            Assert.That((_cabinet.IsSpinning, _cabinet.FixedSpinsLeft, _cabinet.Wallet.Coins), Is.EqualTo((false, (int?)0, coins)),
                "残り0でも回る");

            // 回せなかったときの点滅（1秒）が終わってから見る。
            _cabinet.SetFixedSpins(0);
            yield return new WaitForSeconds(1.1f);
            Assert.That((credit.text, coinIcon.enabled, fixedIcon.activeSelf), Is.EqualTo((coins.ToString(), true, false)),
                "コインで回す状態に戻ってもコインの数が出ない");
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

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
