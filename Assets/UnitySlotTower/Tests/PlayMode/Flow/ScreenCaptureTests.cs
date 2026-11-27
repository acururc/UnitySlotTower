using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using Object = UnityEngine.Object;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    /// <summary>
    /// 全画面を文字なし・文字ありの両方で開いて PNG に保存する撮影用の手順（T359、plan 16.13）。判定はしない。
    /// 通常のテスト実行では動かない（Explicit）。Editor の <c>Tools/UnitySlotTower/Automation/Capture Screens (T359)</c> で実行し、
    /// <c>Artifacts/Automation/Editor/t359/&lt;日時&gt;/</c> に <c>{notext|text}-{画面}.png</c> を書く。
    /// </summary>
    [Explicit("撮影用。Capture Screens (T359) メニューから実行する")]
    [Category(Category)]
    public sealed class ScreenCaptureTests
    {
        public const string Category = "ScreenCapture";

        // どの戦闘マスも各ウェーブ近接の敵1体だけ（最初の戦闘を早く終える）。
        private const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "2,0,melee,S1,battle_a_g1,0\n2,3,melee,S1,battle_a_g1,0\n" +
            "3,0,melee,S1,battle_b_g1,0\n3,3,melee,S1,battle_b_g1,0\n" +
            "5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n4,3,melee,S1,elite_a_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        private const float Settle = 0.4f;
        private const float BoardMoment = 0.4f;

        private bool _runInBackground;
        private string _folder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // 見せ場の暗幕と案内も撮る（PlayModeTestSetup の既定では飛ばす）。
            BattleController.SkipShowcase = false;
            _folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "Automation", "Editor", "t359",
                DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(_folder);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.SetTextMode(false);
            Time.timeScale = 1f;
            BattleController.SkipShowcase = true;
            Application.runInBackground = _runInBackground;
            Debug.Log($"[ScreenCapture] {_folder}");
        }

        [UnityTest]
        public IEnumerator Capture_All_Screens_In_Both_Modes()
        {
            foreach (var textMode in new[] { false, true })
            {
                yield return CaptureMode(textMode);
            }
        }

        private IEnumerator CaptureMode(bool textMode)
        {
            var prefix = textMode ? "text" : "notext";
            GameSession.SetTextMode(textMode);

            // タイトル（難易度の選択はない: spec 002 E-18）。
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return Capture(prefix + "-title");

            // 戦闘と早見表。
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return Capture(prefix + "-game");
            // 配置中の置けるマスの緑とカーソル（T612、AC-111）。開始の演出の後、カーソルは中央の行の左から最初の置けるマスに置く。
            yield return StageTestUtil.WaitForIntro(battle);
            // ステージ1の見せ場（spec 015 FR-004）: ウェーブ1が出そろって止まり、暗幕・矢印・案内が出たところ（敵の HP ゲージも写る）。
            var showcaseLimit = Time.realtimeSinceStartup + 15f;
            while (!battle.Field.IsShowcaseHolding && Time.realtimeSinceStartup < showcaseLimit)
            {
                yield return null;
            }

            yield return Capture(prefix + "-showcase", 0.3f);
            StageTestUtil.SkipShowcase(battle);
            var deployment = battle.Deployment;
            var map = Object.FindAnyObjectByType<BattleFieldView>().Map;
            // T617: 開始時の所持金100ではタンク（130）を選べないので足す。
            battle.Wallet.Add(battle.Units.Get("tank").Cost);
            deployment.Select("tank");
            for (var n = 0; n < map.Width * map.Height; n++)
            {
                deployment.SetCursor(new GridPoint(n % map.Width, (map.Height / 2 + n / map.Width) % map.Height));
                if (deployment.CanPlace(deployment.Cursor))
                {
                    break;
                }
            }

            yield return Capture(prefix + "-placing");
            deployment.Cancel();
            // 時間のある効果が発動中の上端の帯（T611）。
            battle.Field.Effects.StartStop();
            battle.Field.Effects.StartPoison();
            battle.Field.Effects.StartAttack();
            yield return Capture(prefix + "-effects");
            // 揃ったときの盤面全体の演出（T613、FR-113）。攻撃力0の味方を2体置き（敵を倒さない）、出目ごとに演出の途中を撮る。
            battle.Wallet.Add(500);
            PlaceUnit(deployment, map, "tank");
            PlaceUnit(deployment, map, "buffer");
            var board = Object.FindAnyObjectByType<SlotBoardEffect>();
            Assert.That(board, Is.Not.Null, "Game シーンに盤面全体の演出がない（Build Slot Board Effect (T613)）");
            var lights = Object.FindAnyObjectByType<UnitySlotTower.Slot.SlotCabinetLights>();
            Assert.That(lights, Is.Not.Null, "Game シーンに外枠の LED がない（Build Cabinet Frame (T614)）");
            foreach (var symbol in new[] { SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Coin })
            {
                yield return Capture(null);
                board.Play(symbol);
                lights.Flash(symbol); // 外枠の LED も出目の色で光る（T614、FR-116）。
                yield return Capture(prefix + "-board-" + symbol.ToString().ToLowerInvariant(), BoardMoment);
                if (symbol == SlotSymbol.Red7)
                {
                    // 外枠の光の波がリール窓から広がる途中（T614）。
                    yield return Capture(prefix + "-wave-red7-2", 0.25f);
                    yield return Capture(prefix + "-wave-red7-3", 0.6f);
                }
                while (board.IsPlaying)
                {
                    yield return null;
                }
            }

            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return Capture(prefix + "-help-slot");
            help.UnitTab.onClick.Invoke();
            yield return Capture(prefix + "-help-units");
            help.Close();
            battle.SetPaused(false);

            // 最初のステージを終えて次のステージ、そこからリザルトを直接開く（ルート・ショップ・回復はない: spec 002 FR-101）。
            yield return LoadGame(b => battle = b);
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (battle.StageNumber == 1 && battle.Run.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                StageTestUtil.SkipShowcase(battle);
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(battle.StageNumber, Is.EqualTo(2), "最初のステージを終えても次のステージに進まない");
            yield return Capture(prefix + "-stage2");

            var result = Object.FindAnyObjectByType<ResultScreen>();
            typeof(ResultScreen).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(result, new object[] { battle.Run });
            yield return Capture(prefix + "-result");
        }

        // 置けるマスを中央の行から探して置く（向きは右のまま）。
        private static void PlaceUnit(UnitDeployment deployment, BattleMap map, string unitId)
        {
            Assert.That(deployment.Select(unitId), Is.EqualTo(SummonResult.Summoned), $"{unitId} を召喚できない");
            for (var n = 0; n < map.Width * map.Height; n++)
            {
                var cell = new GridPoint(n % map.Width, (map.Height / 2 + n / map.Width) % map.Height);
                if (deployment.CanPlace(cell))
                {
                    deployment.SetCursor(cell);
                    break;
                }
            }

            Assert.That(deployment.ChooseCell(), Is.True, $"{unitId} を置けるマスがない");
            Assert.That(deployment.Confirm(), Is.EqualTo(SummonResult.Summoned), $"{unitId} を置けない");
        }

        // 名前が null なら待つだけ。
        private IEnumerator Capture(string name, float settle = Settle)
        {
            var until = Time.realtimeSinceStartup + settle;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (name == null)
            {
                yield break;
            }

            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static IEnumerator LoadGame(Action<BattleController> found)
        {
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            found(battle);
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSession.GameScene)
            {
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }
    }
}
