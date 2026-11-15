using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Editor.Ui;

namespace UnitySlotTower.Tests.EditMode.Ui
{
    [Description("FR-003 / AC-001 Title 以外で文字を使わない")]
    public sealed class TextAuditTests
    {
        private const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private GameObject CreateText(string value)
        {
            _root = new GameObject("Root");
            var child = new GameObject("Label", typeof(RectTransform));
            child.transform.SetParent(_root.transform);
            child.AddComponent<TextMeshProUGUI>().text = value;
            return _root;
        }

        [Test]
        public void Text_With_Letters_Is_Reported()
        {
            Assert.That(TextAudit.FindViolations(CreateText("WAVE 3"), "test").Count, Is.EqualTo(1));
        }

        [Test]
        public void Numeric_Text_Is_Accepted()
        {
            Assert.That(TextAudit.FindViolations(CreateText("×" + "3"), "test"), Is.Empty);
        }

        [Test]
        public void Help_Mark_Is_Accepted()
        {
            // 一時停止中の説明を開く「?」（T404、plan 8章）。
            Assert.That(TextAudit.FindViolations(CreateText("?"), "test"), Is.Empty);
        }

        [Test]
        public void Text_With_Another_Font_Is_Reported_When_The_Numeric_Font_Is_Required()
        {
            var numeric = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var other = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.That(numeric != null && other != null, Is.True, "フォントが見つからない");
            var text = CreateText("3").GetComponentInChildren<TMP_Text>();

            text.font = numeric;
            var withNumeric = TextAudit.FindViolations(_root, "test", numeric).Count;
            text.font = other;
            var withOther = TextAudit.FindViolations(_root, "test", numeric).Count;

            Assert.That((withNumeric, withOther), Is.EqualTo((0, 1)));
        }

        [Test]
        public void Only_The_Exempt_Part_May_Use_Letters()
        {
            // Title シーンはロゴだけ文字を使ってよい（難易度選択などは走査する、T406）。
            var root = CreateText("SLOT TOWER");
            root.GetComponentInChildren<TMP_Text>().name = TextAudit.TitleLogoName;
            var exempt = TextAudit.FindViolations(root, "test", null, TextAudit.TitleLogoName).Count;
            root.GetComponentInChildren<TMP_Text>().name = "Plate";
            var plate = TextAudit.FindViolations(root, "test", null, TextAudit.TitleLogoName).Count;

            Assert.That((exempt, plate), Is.EqualTo((0, 1)));
        }

        [Test]
        public void Text_Mode_Labels_Are_Not_Audited()
        {
            // 文字あり版のラベル（TextModeLabel の子）は文字なし版では描かれないので対象外（T352、plan 16.9）。
            var root = CreateText("ライフ");
            var text = root.GetComponentInChildren<TMP_Text>();
            var inside = TextAudit.FindViolations(root, "test").Count;
            root.AddComponent<UnitySlotTower.Ui.TextModeLabel>();
            var exempt = TextAudit.FindViolations(root, "test").Count;

            Assert.That((inside, exempt, TextAudit.IsTextModeLabel(text.transform)), Is.EqualTo((1, 0, true)));
        }

        [Test]
        public void Scenes_And_Prefabs_Use_No_Letters()
        {
            var violations = TextAudit.AuditProject();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void Numeric_Font_Contains_Exactly_The_Allowed_Characters()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            var characters = new string(font.characterTable.Select(c => (char)c.unicode).OrderBy(c => c).ToArray());

            Assert.That(characters, Is.EqualTo(new string(NumericText.AllowedCharacters.OrderBy(c => c).ToArray())));
        }

        [Test]
        public void Numeric_Font_Keeps_Every_Glyph_In_Its_Single_Saved_Atlas()
        {
            // 2枚目以降のアトラスはアセットに保存されず、そこに入った字（T308 で見つけた / と %）は描けなかった。
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            var outside = new string(font.characterTable.Where(c => c.glyph.atlasIndex != 0).Select(c => (char)c.unicode).ToArray());

            Assert.That((font.atlasTextures.Length, outside, AssetDatabase.IsSubAsset(font.atlasTextures[0])),
                Is.EqualTo((1, "", true)));
        }

        [Test]
        public void Numeric_Font_Cannot_Add_Characters_At_Runtime()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(font, Is.Not.Null, FontAssetPath);

            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        }
    }
}
