using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("spec 014 AC-002: 主な出来事で効果音の要求が出て、タイトル・戦闘・ボスで BGM が切り替わる（音そのものは確かめない）")]
    public sealed class SoundEventTests
    {
        private readonly List<SoundId> _sounds = new List<SoundId>();
        private readonly List<BgmTrack> _bgms = new List<BgmTrack>();
        private bool _runInBackground;

        [SetUp]
        public void SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _sounds.Clear();
            _bgms.Clear();
            GameAudio.Requested += OnSound;
            GameAudio.BgmRequested += OnBgm;
        }

        [TearDown]
        public void TearDown()
        {
            GameAudio.Requested -= OnSound;
            GameAudio.BgmRequested -= OnBgm;
            SlotCabinet.RandomOverride = null;
            Application.runInBackground = _runInBackground;
        }

        private void OnSound(SoundId id) => _sounds.Add(id);
        private void OnBgm(BgmTrack track) => _bgms.Add(track);

        [UnityTest]
        public IEnumerator Bgm_Switches_Between_Title_Battle_And_Boss()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            Assert.That(GameAudio.Instance, Is.Not.Null, "Title シーンに GameAudio がない（Setup Sound (spec 014)）");
            Assert.That(GameAudio.Instance.CurrentBgm, Is.EqualTo(BgmTrack.Title));

            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            Assert.That(GameAudio.Instance.CurrentBgm, Is.EqualTo(BgmTrack.Battle), "ステージ1で戦闘の BGM にならない");

            // ステージ4（中ボス）を始めるとボスの BGM。
            var state = Object.FindAnyObjectByType<RunController>().State;
            while (state.StageIndex < 4) state.AdvanceStage();
            battle.BeginStage(state);
            Assert.That(GameAudio.Instance.CurrentBgm, Is.EqualTo(BgmTrack.Boss), "中ボスのステージでボスの BGM にならない");
        }

        [UnityTest]
        public IEnumerator Lever_Reels_Line_Miss_And_Button_Request_Sounds()
        {
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return StageTestUtil.WaitForIntro(battle);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            cabinet.Wallet.Add(6);
            var lever = cabinet.LeverRect.GetComponent<Button>();

            cabinet.ForcedHits = new[] { new SlotLineHit(0, SlotSymbol.Red7) };
            _sounds.Clear();
            lever.onClick.Invoke();
            yield return WaitUntilStopped(cabinet);
            Assert.That(_sounds.Count(s => s == SoundId.ReelStop), Is.EqualTo(3), "リール1本ごとの停止音が出ない");
            Assert.That(_sounds, Has.Member(SoundId.Lever).And.Member(SoundId.ReelSpin).And.Member(SoundId.LineHit));
            Assert.That(_sounds, Has.No.Member(SoundId.Miss));
            Assert.That(GameAudio.Instance.IsLooping, Is.False, "止まってもリール回転のループが残る");

            cabinet.ForcedHits = System.Array.Empty<SlotLineHit>();
            _sounds.Clear();
            lever.onClick.Invoke();
            yield return WaitUntilStopped(cabinet);
            Assert.That(_sounds, Has.Member(SoundId.Miss).And.No.Member(SoundId.LineHit), "ハズレの音が出ない");

            // スロット台の外のボタン（倍速）はボタン音。台の中のレバーはボタン音を出さない。
            var speed = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(b => b.name == "Speed Button");
            _sounds.Clear();
            speed.onClick.Invoke();
            lever.onClick.Invoke();
            Assert.That(_sounds.Count(s => s == SoundId.Button), Is.EqualTo(1), "台の外のボタンだけボタン音を出す");
        }

        [UnityTest]
        public IEnumerator Archer_Attack_And_Enemy_Defeat_Request_Sounds()
        {
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return StageTestUtil.WaitForIntro(battle);
            var map = Object.FindAnyObjectByType<BattleFieldView>().Map;
            var archer = battle.Units.Get("archer");
            var field = battle.Field;
            for (var i = 0; i < 60 * 30 && field.Enemies.Count == 0; i++) field.Advance(1);
            Assert.That(field.Enemies.Count, Is.GreaterThan(0), "敵が出ない");

            var path = field.Enemies[0].Path;
            var placed = 0;
            for (var y = 0; y < map.Height && placed < 4; y++)
            {
                for (var x = 0; x < map.Width && placed < 4; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (!UnitPlacement.CanPlace(map, cell, archer) || field.Units.Any(u => u.Cell.Equals(cell))) continue;
                    foreach (Facing facing in System.Enum.GetValues(typeof(Facing)))
                    {
                        var unit = field.AddUnit(archer, cell, facing);
                        if (unit.RangeCells.Any(r => path.Cells.Skip(1).Contains(r)))
                        {
                            placed++;
                            break;
                        }

                        field.RemoveUnit(unit);
                    }
                }
            }

            Assert.That(placed, Is.GreaterThan(0), "敵の道を射程に入れて弓を置けない");
            _sounds.Clear();
            for (var i = 0; i < 600 && !_sounds.Contains(SoundId.EnemyDefeat) && battle.Field == field; i++)
            {
                field.Advance(4);
                yield return null;
            }

            Assert.That(_sounds, Has.Member(SoundId.BowAttack).And.Member(SoundId.EnemyDefeat), "弓の攻撃・撃破の音が出ない");
        }

        private static IEnumerator LoadGame(System.Action<BattleController> found)
        {
            SlotCabinet.RandomOverride = new SeededRandom(1);
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && battle.Field == null; i++) yield return null;
            Assert.That(battle.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return null;
            found(battle);
        }

        private static IEnumerator WaitUntilStopped(SlotCabinet cabinet)
        {
            yield return null;
            var until = Time.realtimeSinceStartup + 6f;
            while (cabinet.IsSpinning && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(cabinet.IsSpinning, Is.False, "リールが止まらない");
        }
    }
}
