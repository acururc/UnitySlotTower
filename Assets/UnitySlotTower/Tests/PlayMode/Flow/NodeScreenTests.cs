using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T350c (FR-017, AC-012, AC-013, D-75): ショップ（品の購入と両替）と回復の画面。足りないと押せず、◀ でルート画面に戻る")]
    public sealed class NodeScreenTests
    {
        // どの戦闘マスも各ウェーブ近接の敵1体だけ（最初の戦闘を早く終える）。
        private const string ShortWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\n" +
            "battle_a,1,0,melee,S1,battle_a_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\n" +
            "battle_b,1,0,melee,S1,battle_b_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\n" +
            "battle_c,1,0,melee,S1,battle_c_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

        private bool _runInBackground;
        private RunController _run;
        private BattleController _battle;
        private SlotCabinet _cabinet;
        private RouteScreen _route;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            RunController.SeedOverride = null;
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Shop_Buys_Items_And_Exchanges_Only_When_Affordable()
        {
            yield return EnterSecondFloor(RouteNodeKinds.Shop);
            var state = _run.State;
            var shop = _run.OpenScreen as ShopScreen;
            Assert.That((shop != null, shop != null && shop.IsOpen, _route.IsOpen, _cabinet.HeldByScreen),
                Is.EqualTo((true, true, false, true)), "ショップマスでショップの画面が開かない");
            Assert.That(shop.OfferButtons.Count, Is.EqualTo(shop.Shop.Offers.Count).And.EqualTo(_run.Rules.ShopSlots),
                "並んだ品の数だけ札がない");

            // 画面が筐体を覆うあいだはレバー（Space も同じ処理）で回らない。
            _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(_cabinet.IsSpinning, Is.False, "ショップの画面の裏で筐体が回った");

            // 所持金0: 品も「所持金→コイン」も押せず、押しても何も変わらない（AC-013）。
            state.Money.TrySpend(state.Money.Coins);
            state.Coins.Add(shop.Exchange.CoinsPerTrade);
            yield return null;
            var coins = state.Coins.Coins;
            for (var i = 0; i < shop.OfferButtons.Count; i++)
            {
                Assert.That(shop.OfferButtons[i].interactable, Is.False, $"所持金0で品 {i} が押せる");
                shop.OfferButtons[i].onClick.Invoke();
            }

            Assert.That((shop.BuyCoinsButton.interactable, shop.SellCoinsButton.interactable), Is.EqualTo((false, true)));

            // 絵による説明（plan 16.10）: 押せるものの矢印だけが動き、押せないものの矢印は元の位置で止まる。
            Assert.That((shop.BuyCoinsArrow != null, shop.SellCoinsArrow != null), Is.EqualTo((true, true)), "両替の矢印がない");
            Assert.That((shop.BuyCoinsArrow.Playing, shop.SellCoinsArrow.Playing), Is.EqualTo((false, true)),
                "両替の矢印の動きが押せるかどうかと合わない");
            for (var i = 0; i < shop.OfferArrows.Count; i++)
            {
                Assert.That(shop.OfferArrows[i].Playing, Is.False, $"所持金0で品 {i} の矢印が動く");
            }

            var sellMax = Vector2.zero;
            var buyMax = Vector2.zero;
            yield return WatchArrows(0.6f, () =>
            {
                if (shop.SellCoinsArrow.Offset.magnitude > sellMax.magnitude)
                {
                    sellMax = shop.SellCoinsArrow.Offset;
                }

                if (shop.BuyCoinsArrow.Offset.magnitude > buyMax.magnitude)
                {
                    buyMax = shop.BuyCoinsArrow.Offset;
                }
            });
            Assert.That(sellMax.x, Is.GreaterThan(0.5f), "押せる両替の矢印が矢印の向き（右）へ動かない");
            Assert.That(buyMax, Is.EqualTo(Vector2.zero), "押せない両替の矢印が動いた");
            shop.BuyCoinsButton.onClick.Invoke();
            Assert.That((state.Money.Coins, state.Coins.Coins), Is.EqualTo((0, coins)), "足りないのに品か両替で何かが変わった");
            for (var i = 0; i < shop.OfferButtons.Count; i++)
            {
                Assert.That(shop.Shop.IsSold(i), Is.False);
            }

            // 両替: コイン → 所持金、所持金 → コイン（掛け金単位）。
            shop.SellCoinsButton.onClick.Invoke();
            Assert.That((state.Money.Coins, state.Coins.Coins),
                Is.EqualTo((shop.Exchange.MoneyPerTrade, coins - shop.Exchange.CoinsPerTrade)), "コインを売れない");
            shop.BuyCoinsButton.onClick.Invoke();
            Assert.That((state.Money.Coins, state.Coins.Coins), Is.EqualTo((0, coins)), "コインを買えない");

            // コインも0にすると「コイン→所持金」も押せない。
            state.Coins.TrySpend(state.Coins.Coins);
            yield return null;
            Assert.That(shop.SellCoinsButton.interactable, Is.False, "コイン0で売れる");
            shop.SellCoinsButton.onClick.Invoke();
            Assert.That((state.Money.Coins, state.Coins.Coins), Is.EqualTo((0, 0)));

            // 所持金を足すと、買える品が押せるようになり、買うと値段だけ減って売り切れになる。
            state.Money.Add(1000);
            yield return null;
            var bought = -1;
            for (var i = 0; i < shop.OfferButtons.Count && bought < 0; i++)
            {
                if (shop.Shop.Check(i) == ShopResult.Bought)
                {
                    Assert.That(shop.OfferButtons[i].interactable, Is.True, $"買える品 {i} が押せない");
                    bought = i;
                }
            }

            Assert.That(bought, Is.GreaterThanOrEqualTo(0), "所持金 1000 で買える品がない");
            Assert.That(shop.OfferArrows[bought].Playing, Is.True, "買える品の矢印が動かない");
            var offerMaxY = 0f;
            yield return WatchArrows(0.6f, () => offerMaxY = Mathf.Max(offerMaxY, shop.OfferArrows[bought].Offset.y));
            Assert.That(offerMaxY, Is.GreaterThan(0.5f), "買える品の矢印が上（品の方）へ動かない");
            var price = shop.Shop.PriceOf(bought);
            shop.OfferButtons[bought].onClick.Invoke();
            Assert.That((state.Money.Coins, shop.Shop.IsSold(bought), shop.OfferButtons[bought].interactable),
                Is.EqualTo((1000 - price, true, false)), "品を買えないか、買った品がまだ押せる");
            shop.OfferButtons[bought].onClick.Invoke();
            Assert.That(state.Money.Coins, Is.EqualTo(1000 - price), "同じ品を2回買えた");
            Assert.That(shop.OfferArrows[bought].gameObject.activeSelf, Is.False, "売り切れの品に矢印が残る");

            yield return GoBack(shop);
        }

        [UnityTest]
        public IEnumerator Rest_Heals_On_Entry_Up_To_Max_And_Back_Returns_To_Route()
        {
            yield return EnterSecondFloor(RouteNodeKinds.Rest, lifeLost: 1);
            var state = _run.State;
            var rest = _run.OpenScreen as RestScreen;
            Assert.That((rest != null, rest != null && rest.IsOpen, _route.IsOpen, _cabinet.HeldByScreen),
                Is.EqualTo((true, true, false, true)), "回復マスで回復の画面が開かない");
            // 減らしたのは1だけなので、回復量（rest_heal）より少なく最大ライフで止まる（AC-012）。
            Assert.That((state.Life, _run.LastHealed), Is.EqualTo((state.MaxLife, 1)), "回復が最大ライフで止まらない");
            yield return GoBack(rest);
        }

        // ◀ でルート画面に戻り、次のマスを選べる。
        // 実時間で seconds 秒、毎フレーム sample を呼ぶ（矢印の動きは実時間で進む）。
        private static IEnumerator WatchArrows(float seconds, System.Action sample)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                sample();
            }
        }

        private IEnumerator GoBack(NodeScreen screen)
        {
            var current = _run.State.Current;
            screen.BackButton.onClick.Invoke();
            yield return null;
            Assert.That((screen.IsOpen, _run.OpenScreen == null, _route.IsOpen, _run.IsChoosingRoute, _cabinet.IsChoosingRoute,
                    _cabinet.HeldByScreen, _run.State.Current),
                Is.EqualTo((false, true, true, true, true, false, current)), "◀ でルート画面に戻らない");
        }

        /// <summary>
        /// 2階に <paramref name="kind"/> のマスがあるシードで Game を読み、最初の戦闘を終えてそのマスへ進む。
        /// </summary>
        private IEnumerator EnterSecondFloor(string kind, int lifeLost = 0)
        {
            // 1回目の読み込みでルートの表を得て、2階にその種類があるシードを探す（RunController と同じ生成）。
            RunController.SeedOverride = 1;
            yield return LoadGame();
            ulong? seed = null;
            for (ulong s = 1; s < 5000 && seed == null; s++)
            {
                var route = RouteGenerator.Generate(_run.Rules, new SeededRandom(s));
                foreach (var next in route.Start.Next)
                {
                    if (next.Kind == kind)
                    {
                        seed = s;
                        break;
                    }
                }
            }

            Assert.That(seed, Is.Not.Null, $"2階に {kind} のあるシードが見つからない");
            RunController.SeedOverride = seed;
            yield return LoadGame();

            var first = _battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (!_run.IsChoosingRoute && first.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                _battle.StartWave();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            _run.State.Heal(_run.State.MaxLife);
            _run.State.LoseLife(lifeLost);

            var current = _run.State.Current;
            var reel = -1;
            for (var r = 0; r < 3 && reel < 0; r++)
            {
                if (current.NextOnReel(r)?.Kind == kind)
                {
                    reel = r;
                }
            }

            _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(_cabinet.PickRouteReel(reel), Is.True, "行き先のリールを止められない");
            limit = Time.realtimeSinceStartup + 5f;
            while (_run.OpenScreen == null && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(_run.State.Current.Kind, Is.EqualTo(kind), $"{kind} のマスに進まない");
        }

        private IEnumerator LoadGame()
        {
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync("Game");
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            _run = Object.FindAnyObjectByType<RunController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _route = Object.FindAnyObjectByType<RouteScreen>(FindObjectsInactive.Include);
            for (var i = 0; i < 60 && (_battle == null || _battle.Run == null); i++)
            {
                yield return null;
            }

            Assert.That((_run != null && _run.State != null, _battle != null && _battle.Run != null, _route != null),
                Is.EqualTo((true, true, true)), "Game シーンの進行が始まらない");
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }
    }
}
