using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Battle;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("AC-013 / FR-017, plan 16.2: ショップ（重みで重複なしに並べ、各品1回、拒否で何も変えない）")]
    public sealed class ShopTests
    {
        private const string Header = "id,kind,unit_id,price,weight\n";

        // 重みが正の4品と、重み0の1品。
        private const string Rows =
            "upgrade_archer,upgrade,archer,20,1\n" +
            "upgrade_buffer,upgrade,buffer,15,1\n" +
            "upgrade_sword,upgrade,sword,0,1\n" +
            "upgrade_tank,upgrade,tank,0,1\n" +
            "never,upgrade,archer,20,0\n";

        private static ShopTable LoadTable(string rows = Rows) =>
            ShopTable.Load(CsvParser.Parse(ShopTable.FileName, Header + rows));

        /// <summary>重みが正の4品が全部並ぶショップ。</summary>
        private static (RunState run, Shop shop) Create(int money = 30, ulong seed = 1)
        {
            var run = RunFixtures.CreateRun(money: money);
            return (run, new Shop(LoadTable(), 4, run, new SeededRandom(seed)));
        }

        private static int IndexOf(Shop shop, string id)
        {
            for (var i = 0; i < shop.Offers.Count; i++)
            {
                if (shop.Offers[i].Id == id)
                {
                    return i;
                }
            }

            Assert.Fail($"品 '{id}' が並んでいない");
            return -1;
        }

        [Test]
        public void Row_Is_Read_Into_An_Item()
        {
            var item = LoadTable().All[1];

            Assert.That((item.Id, item.Kind, item.UnitId, item.Price, item.Weight),
                Is.EqualTo(("upgrade_buffer", ShopItemKind.Upgrade, "buffer", 15, 1)));
        }

        [Test]
        public void Duplicate_Id_Is_An_Error()
        {
            Assert.Throws<CsvFormatException>(() => LoadTable(Rows + "never,upgrade,tank,20,1\n"));
        }

        [Test]
        public void Unknown_Kind_Is_An_Error()
        {
            var error = Assert.Throws<CsvFormatException>(() => LoadTable(Rows + "x,potion,tank,20,1\n")).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((7, 2)));
        }

        [Test]
        [Description("plan 16.2: shop_slots 品を重複なしに選ぶ")]
        public void Offers_The_Slot_Count_Without_Duplicates()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var shop = new Shop(LoadTable(), 3, RunFixtures.CreateRun(),
                    new SeededRandom(seed));

                var ids = shop.Offers.Select(o => o.Id).ToList();

                Assert.That((ids.Count, ids.Distinct().Count()), Is.EqualTo((3, 3)));
            }
        }

        [Test]
        public void Item_With_Zero_Weight_Is_Never_Offered()
        {
            var shop = new Shop(LoadTable(), 10, RunFixtures.CreateRun(),
                new SeededRandom(1));

            Assert.That(shop.Offers.Select(o => o.Id),
                Is.EquivalentTo(new[] { "upgrade_archer", "upgrade_buffer", "upgrade_sword", "upgrade_tank" }));
        }

        [Test]
        public void Same_Seed_Gives_The_Same_Offers()
        {
            List<string> Offers() => new Shop(LoadTable(), 2, RunFixtures.CreateRun(),
                new SeededRandom(9)).Offers.Select(o => o.Id).ToList();

            Assert.That(Offers(), Is.EqualTo(Offers()));
        }

        [Test]
        [Description("plan 16.2: 重みで選ぶ（3 : 1 → 75% ± 3%）")]
        public void Offers_Are_Chosen_By_Weight()
        {
            var table = LoadTable("heavy,upgrade,sword,20,3\nlight,upgrade,tank,20,1\n");
            var heavy = 0;
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                var shop = new Shop(table, 1, RunFixtures.CreateRun(),
                    new SeededRandom(seed));
                heavy += shop.Offers[0].Id == "heavy" ? 1 : 0;
            }

            Assert.That(heavy / 2000.0, Is.EqualTo(0.75).Within(0.03));
        }

        [Test]
        [Description("plan 16.2: 強化の品の値段はその職の次の強化費用")]
        public void Upgrade_Price_Is_The_Next_Upgrade_Cost()
        {
            var (_, shop) = Create();

            Assert.That(shop.PriceOf(IndexOf(shop, "upgrade_sword")), Is.EqualTo(20));
        }

        [Test]
        public void Buying_Upgrade_Pays_And_Raises_The_Level()
        {
            var (run, shop) = Create(money: 30);

            Assert.That((shop.TryBuy(IndexOf(shop, "upgrade_sword")), run.Money.Coins, run.Upgrades.Level("sword")),
                Is.EqualTo((ShopResult.Bought, 10, 2)));
        }

        [Test]
        [Description("plan 16.2: 各品は1回だけ買える")]
        public void Bought_Item_Is_Sold_Out()
        {
            var (run, shop) = Create(money: 100);
            var index = IndexOf(shop, "upgrade_sword");
            shop.TryBuy(index);

            Assert.That((shop.IsSold(index), shop.TryBuy(index), run.Money.Coins, run.Upgrades.Level("sword")),
                Is.EqualTo((true, ShopResult.SoldOut, 80, 2)));
        }

        [Test]
        [Description("AC-013: 所持金が足りなければ拒否し、何も変えない")]
        public void Buying_Without_Enough_Money_Is_Refused()
        {
            var (run, shop) = Create(money: 10);
            var index = IndexOf(shop, "upgrade_sword");

            Assert.That((shop.TryBuy(index), shop.IsSold(index), run.Money.Coins, run.Upgrades.Level("sword")),
                Is.EqualTo((ShopResult.NotEnoughMoney, false, 10, 1)));
        }

        [Test]
        [Description("plan 16.2: 強化が最大段階なら買えない")]
        public void Upgrade_Is_Unavailable_At_Max_Level()
        {
            var (run, shop) = Create(money: 30);

            Assert.That((shop.Check(IndexOf(shop, "upgrade_tank")), run.Money.Coins),
                Is.EqualTo((ShopResult.Unavailable, 30)));
        }
    }
}
