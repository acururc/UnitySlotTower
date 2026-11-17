using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 札と詳細の板の職の絵（T384）。盤上の味方の形と色（<see cref="BattleActorsView"/> の unitLooks）を units.csv の並びで読み、
    /// 横から見た影絵（箱・縦長の箱・円柱・球）を UI で作る。影絵は職の色を暗くした色。
    /// </summary>
    internal static class UnitLooks
    {
        private const string UnitsPath = "Assets/UnitySlotTower/Data/Tables/units.csv";
        private const string ShapeName = "Job Shape";

        internal struct Look
        {
            public string Id;
            public PrimitiveType Shape;
            public Vector3 Size;
            public Color Color;
        }

        /// <summary>units.csv の並びの見た目。BattleActorsView がなければ空。</summary>
        internal static List<Look> Read()
        {
            var result = new List<Look>();
            var actors = Object.FindAnyObjectByType<BattleActorsView>(FindObjectsInactive.Include);
            if (actors == null)
            {
                return result;
            }

            var byId = new Dictionary<string, Look>();
            var looks = new SerializedObject(actors).FindProperty("unitLooks");
            for (var i = 0; i < looks.arraySize; i++)
            {
                var element = looks.GetArrayElementAtIndex(i);
                var look = new Look
                {
                    Id = element.FindPropertyRelative("id").stringValue,
                    Shape = (PrimitiveType)element.FindPropertyRelative("shape").enumValueIndex,
                    Size = element.FindPropertyRelative("size").vector3Value,
                    Color = element.FindPropertyRelative("color").colorValue,
                };
                byId[look.Id] = look;
            }

            var units = CsvParser.Parse("units.csv", AssetDatabase.LoadAssetAtPath<TextAsset>(UnitsPath).text);
            foreach (var row in units.Rows)
            {
                var id = row.Get("id");
                result.Add(byId.TryGetValue(id, out var look) ? look : new Look { Id = id, Shape = PrimitiveType.Cube, Size = Vector3.one * 0.5f, Color = Color.gray });
            }

            return result;
        }

        /// <summary>parent（正方形に近い枠）の中に影絵を作り直す。parent の中央下寄りに、いちばん長い辺が枠の短い辺の 0.75 になる大きさ。</summary>
        internal static void BuildShape(RectTransform parent, Look look)
        {
            var old = parent.Find(ShapeName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var shape = UnitDetailSetup.Ensure(parent, ShapeName);
            shape.anchorMin = shape.anchorMax = new Vector2(0.5f, 0.5f);
            shape.pivot = new Vector2(0.5f, 0.5f);
            shape.anchoredPosition = Vector2.zero;
            var pw = parent.rect.width > 0f ? parent.rect.width : 100f;
            var ph = parent.rect.height > 0f ? parent.rect.height : 100f;
            var longest = Mathf.Max(look.Size.x, look.Size.y, 0.01f);
            var scale = Mathf.Min(pw, ph) * 0.75f / longest;
            var width = look.Size.x * scale;
            var height = look.Size.y * scale;
            var dark = Color.Lerp(look.Color, Color.black, 0.6f);
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // 枠の大きさが変わっても形が崩れないよう、割合で置く。
            var w = width / pw;
            var h = height / ph;
            Anchor(shape, 0.5f - w / 2f, 0.5f - h / 2f, 0.5f + w / 2f, 0.5f + h / 2f);

            switch (look.Shape)
            {
                case PrimitiveType.Sphere:
                    Part(shape, "Ball", 0f, 0f, 1f, 1f, dark, knob);
                    break;
                case PrimitiveType.Cylinder:
                    // 側面の四角と上下の楕円。
                    Part(shape, "Side", 0f, 0.15f, 1f, 0.85f, dark, null);
                    Part(shape, "Bottom", 0f, 0f, 1f, 0.3f, dark, knob);
                    Part(shape, "Top", 0f, 0.7f, 1f, 1f, Color.Lerp(dark, Color.white, 0.25f), knob);
                    break;
                default:
                    Part(shape, "Box", 0f, 0f, 1f, 1f, dark, null);
                    break;
            }
        }

        private static void Part(RectTransform parent, string name, float left, float bottom, float right, float top, Color color,
            Sprite sprite)
        {
            var rect = UnitDetailSetup.Ensure(parent, name);
            Anchor(rect, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Anchor(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
