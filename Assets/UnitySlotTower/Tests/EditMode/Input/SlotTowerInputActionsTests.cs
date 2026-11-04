using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace UnitySlotTower.Tests.EditMode.Input
{
    [Description("FR-002 / plan 3章 マウスとキーボードの操作割り当て")]
    public sealed class SlotTowerInputActionsTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";

        private static InputActionMap Map() =>
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath).FindActionMap("SlotTower", true);

        [TestCase("SelectUnit1", "<Keyboard>/1")]
        [TestCase("SelectUnit2", "<Keyboard>/2")]
        [TestCase("SelectUnit3", "<Keyboard>/3")]
        [TestCase("SelectUnit4", "<Keyboard>/4")]
        [TestCase("Upgrade1", "<Keyboard>/shift", "<Keyboard>/1")]
        [TestCase("Upgrade2", "<Keyboard>/shift", "<Keyboard>/2")]
        [TestCase("Upgrade3", "<Keyboard>/shift", "<Keyboard>/3")]
        [TestCase("Upgrade4", "<Keyboard>/shift", "<Keyboard>/4")]
        [TestCase("Cursor", "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow")]
        [TestCase("Confirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter")]
        [TestCase("Cancel", "<Keyboard>/escape")]
        [TestCase("CycleUnit", "<Keyboard>/tab")]
        [TestCase("Retreat", "<Keyboard>/x")]
        [TestCase("Lever", "<Keyboard>/space")]
        [TestCase("StopLeft", "<Keyboard>/j")]
        [TestCase("StopCenter", "<Keyboard>/k")]
        [TestCase("StopRight", "<Keyboard>/l")]
        [TestCase("Pause", "<Keyboard>/p", "<Keyboard>/escape")]
        [TestCase("Help", "<Keyboard>/slash")]
        [TestCase("ClearCell", "<Keyboard>/backspace", "<Keyboard>/delete")]
        [TestCase("Point", "<Mouse>/position")]
        [TestCase("Press", "<Mouse>/leftButton")]
        public void Action_Has_Planned_Bindings(string action, params string[] paths)
        {
            var bound = Map().FindAction(action, true).bindings
                .Where(b => !b.isComposite)
                .Select(b => b.path)
                .ToArray();

            Assert.That(bound, Is.EquivalentTo(paths));
        }

        [Test]
        [Description("FR-020: 味方は4種なので、5 と Shift+5 の操作はない（2026-11-01 にバッファーを廃止）")]
        public void Fifth_Unit_Keys_Are_Not_Bound()
        {
            var map = Map();

            Assert.That((map.FindAction("SelectUnit5"), map.FindAction("Upgrade5"),
                    map.bindings.Count(b => b.path == "<Keyboard>/5")),
                Is.EqualTo(((InputAction)null, (InputAction)null, 0)));
        }

        [Test]
        public void Every_Binding_Belongs_To_Keyboard_And_Mouse_Scheme()
        {
            var ungrouped = Map().bindings
                .Where(b => !b.isComposite && b.groups != "Keyboard&Mouse")
                .Select(b => b.path);

            Assert.That(ungrouped, Is.Empty);
        }
    }
}
