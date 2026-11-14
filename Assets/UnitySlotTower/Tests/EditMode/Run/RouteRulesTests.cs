using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("plan 16.7 / FR-050: route_rules.csv の読み込み")]
    public sealed class RouteRulesTests
    {
        [Test]
        public void Values_Are_Read()
        {
            var rules = RunFixtures.LoadRules();

            Assert.That(
                (rules.Floors, rules.Columns, rules.Paths, rules.EliteMinFloor, rules.RestHeal, rules.ShopSlots,
                    rules.ExchangeMoneyPerCoin),
                Is.EqualTo((8, 4, 4, 3, 3, 3, 2)));
        }

        [Test]
        public void Weight_Is_Read_Per_Kind()
        {
            var rules = RunFixtures.LoadRules();

            Assert.That((rules.WeightOf("battle"), rules.WeightOf("elite"), rules.WeightOf("rest")),
                Is.EqualTo((50, 14, 14)));
        }

        [Test]
        [Description("D-69: 登録表の種類に重みがなければエラー")]
        public void Missing_Weight_Of_A_Registered_Kind_Is_An_Error()
        {
            var body = RunFixtures.RulesBody.Replace("weight_shop,12\n", "");

            Assert.Throws<CsvFormatException>(() => RunFixtures.LoadRules(body));
        }

        [Test]
        [Description("D-69: 登録表にない種類の重みはエラー")]
        public void Weight_Of_An_Unregistered_Kind_Is_An_Error()
        {
            var error = Assert.Throws<CsvFormatException>(() =>
                RunFixtures.LoadRules(RunFixtures.RulesBody + "weight_casino,5\n")).Error;

            Assert.That(error.Line, Is.EqualTo(14));
        }

        [Test]
        public void Fewer_Than_Three_Floors_Is_An_Error()
        {
            Assert.Throws<CsvFormatException>(() => RunFixtures.LoadRulesWith(("floors", "2")));
        }
    }
}
