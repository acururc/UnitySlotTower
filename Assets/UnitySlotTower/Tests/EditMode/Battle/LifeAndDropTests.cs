using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// ゴール到達でのライフ減少・ゲームオーバー（FR-013）と撃破ドロップ（FR-033）。1 ステップ = 1/60 秒。
    /// 地図と敵は <see cref="BattleFixtures"/>（地上経路 g: (0,0)→(3,0)。速度1の敵は出現から 180 ステップでゴールに着く）。
    /// </summary>
    public sealed class LifeAndDropTests
    {
        [Test]
        [Description("FR-013: ゴールに着いた敵のライフ減少量だけライフが減る")]
        public void Life_Drops_By_The_Arrived_Enemys_Life_Damage()
        {
            var field = BattleFixtures.CreateField("0,heavy,S1,g,0\n");

            field.Advance(181);

            Assert.That(field.Life, Is.EqualTo(8));
        }

        [Test]
        [Description("AC-010: ライフ0でゲームオーバー。減少量が残りより多くても0で止まる")]
        public void Game_Is_Over_When_Life_Reaches_Zero()
        {
            var field = BattleFixtures.CreateField("0,heavy,S1,g,0\n", life: 1);

            field.Advance(181);

            Assert.That((field.Life, field.IsGameOver), Is.EqualTo((0, true)));
        }

        [Test]
        public void Battle_Stops_After_Game_Over()
        {
            var field = BattleFixtures.CreateField("0,melee,S1,g,0\n0.5,melee,S1,g,0\n", life: 1);

            field.Advance(180 + 60);

            Assert.That(field.Tick, Is.EqualTo(180));
        }

        /// <summary>
        /// 1 体だけ出る敵を剣士（(1,0) 右向き）に倒させる戦闘を、1 つの固定シード乱数で何度も行い、各戦闘で落ちたコインと所持金を返す。
        /// 敵は HP1 で、出現から 30 ステップで剣士の範囲に入り倒される。
        /// </summary>
        /// <param name="coinBoosts">戦闘の前にコインが揃った回数（所持金の倍率、1回 +0.1・上限 1.5）。</param>
        private static (int Coins, int Money)[] DropsOfDefeats(string enemyId, int trials, int coinBoosts = 0)
        {
            var wave = BattleFixtures.LoadWave($"0,{enemyId},S1,g,0\n");
            var ranges = BattleFixtures.LoadRanges();
            var sword = BattleFixtures.LoadUnits().Get("sword");
            var random = new SeededRandom(42);
            var drops = new (int, int)[trials];
            for (var i = 0; i < trials; i++)
            {
                var boosts = new SlotEffects(SlotTestRules.Create(coinStep: 0.1, coinCap: 1.5));
                for (var c = 0; c < coinBoosts; c++)
                {
                    boosts.AddCoin();
                }

                var field = new Battlefield(wave, ranges, 10, random, boosts);
                field.AddUnit(sword, new GridPoint(1, 0), Facing.Right);
                field.Advance(60);
                drops[i] = (field.DroppedCoins, field.DroppedMoney);
            }

            return drops;
        }

        [Test]
        [Description("AC-030: コイン40%で1枚の敵を 2000 回倒すと、コインを落とす割合は 40% ± 4%（標準偏差の約 3.6 倍）")]
        public void Defeated_Enemy_Drops_Coins_At_Its_Coin_Rate()
        {
            var drops = DropsOfDefeats("fodder", 2000);

            Assert.That(drops.Sum(d => d.Coins) / 2000.0, Is.EqualTo(0.4).Within(0.04));
        }

        [Test]
        [Description("AC-030: コイン25%で3枚の敵を 2000 回倒すと、コインを落とす割合は 25% ± 3.5%（標準偏差の約 3.6 倍）")]
        public void Split_Drop_Gives_Coins_At_The_Coin_Rate()
        {
            var drops = DropsOfDefeats("purse", 2000);

            Assert.That(drops.Count(d => d.Coins == 3) / 2000.0, Is.EqualTo(0.25).Within(0.035));
        }

        [Test]
        [Description("AC-030: 所持金40%で2の敵を 2000 回倒すと、所持金を落とす割合は 40% ± 4%")]
        public void Split_Drop_Gives_Money_At_The_Money_Rate()
        {
            var drops = DropsOfDefeats("purse", 2000);

            Assert.That(drops.Count(d => d.Money == 2) / 2000.0, Is.EqualTo(0.4).Within(0.04));
        }

        [Test]
        [Description("plan 16.2: 撃破時の抽選は1回だけ。コインと所持金を同時には落とさない")]
        public void One_Defeat_Never_Drops_Both_Coins_And_Money()
        {
            var drops = DropsOfDefeats("purse", 2000);

            Assert.That(drops.Count(d => d.Coins > 0 && d.Money > 0), Is.EqualTo(0));
        }

        [Test]
        [Description("AC-030: 必ず 2〜3 枚落とす敵を 200 回倒すと、量は 2 と 3 だけが出る")]
        public void Coin_Drop_Amount_Ranges_From_Min_To_Max()
        {
            var drops = DropsOfDefeats("rich", 200);

            Assert.That(drops.Select(d => d.Coins).Distinct().OrderBy(d => d), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        [Description("AC-030: 必ず所持金 2〜3 を落とす敵を 200 回倒すと、量は 2 と 3 だけが出て、コインは落ちない")]
        public void Money_Drop_Amount_Ranges_From_Min_To_Max()
        {
            var drops = DropsOfDefeats("banker", 200);

            Assert.That(drops.Distinct().OrderBy(d => d.Money).ToArray(), Is.EqualTo(new[] { (0, 2), (0, 3) }));
        }

        [Test]
        [Description("AC-042, plan 16.2: コインの倍率 1.5 なら所持金のドロップは 2→3、3→4.5→5（四捨五入）")]
        public void Money_Drop_Is_Multiplied_By_The_Coin_Boost()
        {
            var drops = DropsOfDefeats("banker", 200, coinBoosts: 5);

            Assert.That(drops.Select(d => d.Money).Distinct().OrderBy(d => d), Is.EqualTo(new[] { 3, 5 }));
        }

        [Test]
        [Description("plan 16.2: コインの倍率は所持金にだけ掛かり、コインのドロップは変わらない")]
        public void Coin_Drop_Is_Not_Multiplied_By_The_Coin_Boost()
        {
            var drops = DropsOfDefeats("rich", 200, coinBoosts: 5);

            Assert.That(drops.Select(d => d.Coins).Distinct().OrderBy(d => d), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        [Description("FR-033: ドロップは撃破時だけ。ゴールに着いた敵は落とさない")]
        public void Enemy_Reaching_The_Goal_Drops_Nothing()
        {
            var field = BattleFixtures.CreateField("0,purse,S1,g,0\n0,banker,S1,g,0\n0,rich,S1,g,0\n");

            field.Advance(180);

            Assert.That((field.ArrivedCount, field.DroppedCoins, field.DroppedMoney), Is.EqualTo((3, 0, 0)));
        }
    }
}
