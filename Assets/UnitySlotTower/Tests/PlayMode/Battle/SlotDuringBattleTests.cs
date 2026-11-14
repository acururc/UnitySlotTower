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
            yield return ReelCustomTestUtil.CloseWithDefault(_battle);
            _battle.StartWave();
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
        public IEnumerator Enemies_Keep_Moving_While_The_Reels_Spin_And_The_Bet_Comes_From_The_Shared_Wallet()
        {
            Assert.That(_battle.Run.State.Coins, Is.SameAs(_cabinet.Wallet));
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            for (var i = 0; i < 60 && _battle.Field.Enemies.Count == 0; i++)
            {
                yield return null;
            }

            var coinsBefore = _cabinet.Wallet.Coins;
            yield return Tap(Key.Space);
            for (var i = 0; i < 180 && !reels[0].IsSpinning; i++)
            {
                yield return null;
            }

            Assert.That(reels[0].IsSpinning, Is.True, "Space でリールが回らない");
            Assert.That(_cabinet.Wallet.Coins, Is.LessThan(coinsBefore), "ベットがコインから引かれない");

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

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
