using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの操作バー（T308、plan 2章）を組み立てる一回限りの手順。T208 の仮アイコン4つに待ち時間ゲージ・コスト・ストック数・
    /// 選択の印を足し、その下に種類ごとの強化アイコン、右にスキルゲージ（T349）、右側にバーとコインの倍率、ライフ・所持金・ウェーブ n/2 と
    /// ▶・撤退・一時停止のボタンを置く。
    /// 既に付いていれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class OperationBarSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const int SlotCount = 4;
        private const int MaxLevel = 5;

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _font;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Operation Bar")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 操作バーは既にあるため作成しない。");
                return;
            }

            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<UnitPlacementView>(FindObjectsInactive.Include);
            if (bar == null || battle == null || input == null || view == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Operation Bar / 戦闘の進行 / 配置操作が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Assign(view, "placementInput", input);
            Assign(battle, "autoStart", false);

            for (var i = 0; i < SlotCount; i++)
            {
                var x = ColumnX(i);
                var icon = bar.Find($"Unit Icon {i + 1}") as RectTransform;
                if (icon == null)
                {
                    Debug.LogError($"[SlotTower Setup] Unit Icon {i + 1} が見つからない。");
                    return;
                }

                BuildUnitIcon(icon, x, battle, input);
                BuildUpgradeIcon(bar, i, x, battle, input);
                BuildSkillGauge(bar, i, x, icon.GetComponent<UnitBarIcon>());
            }

            BuildStatus(bar, battle, input);
            BuildRates(bar, Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include));

            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            EditorSceneManager.SaveScene(bar.gameObject.scene);
            Debug.Log("[SlotTower Setup] 操作バーを作成した。");
        }

        /// <summary>
        /// 既にある操作バーを作り直す（T349、plan 16.8）。旧 攻撃力・攻撃速度アップの残り秒数（Slot Effects）を消し、
        /// 味方アイコン・強化アイコンを詰めて各アイコンの右にスキルゲージ（5点のボタン）を足し、状態表示との間にバーとコインの倍率を置く。
        /// 戦闘の進行にスキル表を設定する。ゲージ・倍率が既にあれば作らない（並べ直しと表の設定はする）。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Setup/Rebuild Operation Bar (T349)")]
        private static void Rebuild()
        {
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var bar = status != null ? status.transform.parent as RectTransform : null;
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            if (bar == null || battle == null)
            {
                Debug.LogError("[SlotTower Setup] 操作バーか戦闘の進行が見つからない。");
                return;
            }

            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var effects = bar.Find("Slot Effects");
            if (effects != null)
            {
                Object.DestroyImmediate(effects.gameObject);
            }

            for (var i = 0; i < SlotCount; i++)
            {
                var x = ColumnX(i);
                var icon = bar.Find($"Unit Icon {i + 1}") as RectTransform;
                var upgrade = bar.Find($"Upgrade Icon {i + 1}") as RectTransform;
                if (icon == null || upgrade == null)
                {
                    Debug.LogError($"[SlotTower Setup] Unit Icon {i + 1} か Upgrade Icon {i + 1} が見つからない。");
                    return;
                }

                SetAnchors(icon, new Vector2(x, 0.38f), new Vector2(x + IconWidth, 0.94f));
                SetAnchors(upgrade, new Vector2(x, 0.06f), new Vector2(x + IconWidth, 0.34f));
                if (bar.Find($"Skill Gauge {i + 1}") == null)
                {
                    BuildSkillGauge(bar, i, x, icon.GetComponent<UnitBarIcon>());
                }
            }

            SetAnchors(status.GetComponent<RectTransform>(), new Vector2(StatusLeft, 0f), Vector2.one);
            if (bar.Find("Rates") == null)
            {
                BuildRates(bar, status);
            }

            Assign(battle, "skillsTable",
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/UnitySlotTower/Data/Tables/skills.csv"));
            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            EditorSceneManager.SaveScene(bar.gameObject.scene);
            Debug.Log("[SlotTower Setup] 操作バーを作り直した（スキルゲージ・倍率、旧効果の表示を削除）。");
        }

        private const float IconWidth = 0.1f;
        private const float StatusLeft = 0.655f;

        // 味方の列（アイコン・強化・ゲージ）の左端。
        private static float ColumnX(int slot) => 0.02f + slot * 0.135f;

        /// <summary>アイコンの右に縦長のスキルゲージ（5点、下から点く）を作る。アイコン本体とは別のボタン（plan 16.8）。</summary>
        private static void BuildSkillGauge(RectTransform bar, int slot, float x, UnitBarIcon barIcon)
        {
            var left = x + IconWidth + 0.003f;
            var button = CreateButton($"Skill Gauge {slot + 1}", bar, new Vector2(left, 0.06f), new Vector2(left + 0.026f, 0.94f),
                new Color(0.12f, 0.12f, 0.14f));
            var rect = button.GetComponent<RectTransform>();
            var dots = new Object[MaxLevel];
            for (var level = 0; level < MaxLevel; level++)
            {
                var bottom = 0.05f + level * 0.185f;
                var dot = CreateImage($"Dot {level + 1}", rect, new Vector2(0.12f, bottom), new Vector2(0.88f, bottom + 0.15f),
                    new Color(1f, 1f, 1f, 0.15f), _knob);
                dot.preserveAspect = true;
                dots[level] = dot;
            }

            var serialized = new SerializedObject(barIcon);
            serialized.FindProperty("skillButton").objectReferenceValue = button;
            serialized.FindProperty("skillBackground").objectReferenceValue = button.GetComponent<Image>();
            var dotProperty = serialized.FindProperty("gaugeDots");
            dotProperty.arraySize = dots.Length;
            for (var i = 0; i < dots.Length; i++)
            {
                dotProperty.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            }

            var colorProperty = serialized.FindProperty("gaugeColors");
            colorProperty.arraySize = SlotSymbolArt.SevenColors.Length;
            for (var i = 0; i < SlotSymbolArt.SevenColors.Length; i++)
            {
                colorProperty.GetArrayElementAtIndex(i).colorValue = SlotSymbolArt.SevenColors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>バーの攻撃力倍率（攻撃力の印）とコインの所持金倍率（所持金の印）を上下2段で置く（plan 16.8）。</summary>
        private static void BuildRates(RectTransform bar, OperationBarStatus status)
        {
            var rates = CreateRect("Rates", bar, new Vector2(0.56f, 0.04f), new Vector2(StatusLeft - 0.005f, 0.96f));
            var attack = BuildRate(rates, "Attack Rate", new Vector2(0f, 0.52f), Vector2.one, status, "attackRateGroup",
                "attackRateText");
            SlotSymbolArt.Attack(CreateRect("Icon", attack, Vector2.zero, new Vector2(0.36f, 1f)));
            var money = BuildRate(rates, "Money Rate", Vector2.zero, new Vector2(1f, 0.48f), status, "moneyRateGroup",
                "moneyRateText");
            CreateImage("Icon", money, new Vector2(0.03f, 0.15f), new Vector2(0.33f, 0.85f), new Color(0.98f, 0.8f, 0.2f), _knob)
                .preserveAspect = true;
        }

        private static RectTransform BuildRate(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            OperationBarStatus status, string groupProperty, string textProperty)
        {
            var row = CreateRect(name, parent, anchorMin, anchorMax);
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var value = CreateNumber("Value", row, new Vector2(0.38f, 0f), Vector2.one, Color.white);
            value.text = "×1.0";
            value.alignment = TextAlignmentOptions.Left;
            Assign(status, groupProperty, group);
            Assign(status, textProperty, value);
            return row;
        }

        private static void BuildUnitIcon(RectTransform icon, float x, BattleController battle, UnitPlacementInput input)
        {
            SetAnchors(icon, new Vector2(x, 0.38f), new Vector2(x + IconWidth, 0.94f));
            var gauge = CreateImage("Cooldown Gauge", icon, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f), null);
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Vertical;
            gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
            gauge.fillAmount = 0f;
            var frame = CreateImage("Selected Frame", icon, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.35f), null);
            frame.enabled = false;
            var stock = CreateNumber("Stock", icon, new Vector2(0.04f, 0.55f), new Vector2(0.7f, 0.98f), Color.white);
            stock.alignment = TextAlignmentOptions.TopLeft;
            stock.gameObject.SetActive(false);
            var cost = CreateNumber("Cost", icon, new Vector2(0.3f, 0.02f), new Vector2(0.96f, 0.45f), Color.white);
            cost.alignment = TextAlignmentOptions.BottomRight;

            var barIcon = icon.GetComponent<UnitBarIcon>();
            Assign(barIcon, "battle", battle);
            Assign(barIcon, "placementInput", input);
            Assign(barIcon, "body", icon.GetComponent<Image>());
            Assign(barIcon, "cooldownGauge", gauge);
            Assign(barIcon, "selectedFrame", frame);
            Assign(barIcon, "costText", cost);
            Assign(barIcon, "stockText", stock);
        }

        private static void BuildUpgradeIcon(RectTransform bar, int slot, float x, BattleController battle,
            UnitPlacementInput input)
        {
            var background = CreateImage($"Upgrade Icon {slot + 1}", bar, new Vector2(x, 0.06f), new Vector2(x + IconWidth, 0.34f),
                new Color(0.12f, 0.12f, 0.14f), _box);
            background.raycastTarget = true;
            var rect = background.rectTransform;

            var arrow = CreateRect("Arrow", rect, new Vector2(0.03f, 0.12f), new Vector2(0.27f, 0.88f))
                .gameObject.AddComponent<TriangleGraphic>();
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            arrow.raycastTarget = false;
            var cost = CreateNumber("Cost", rect, new Vector2(0.3f, 0.36f), new Vector2(0.97f, 0.98f), Color.white);
            cost.alignment = TextAlignmentOptions.Right;

            var pips = new Object[MaxLevel];
            for (var level = 0; level < MaxLevel; level++)
            {
                var left = 0.32f + level * 0.13f;
                pips[level] = CreateImage($"Level {level + 1}", rect, new Vector2(left, 0.1f), new Vector2(left + 0.1f, 0.3f),
                    Color.gray, null);
            }

            var upgrade = background.gameObject.AddComponent<UnitUpgradeIcon>();
            Assign(upgrade, "battle", battle);
            Assign(upgrade, "placementInput", input);
            Assign(upgrade, "arrow", arrow);
            Assign(upgrade, "costText", cost);
            var serialized = new SerializedObject(upgrade);
            serialized.FindProperty("slot").intValue = slot;
            var pipProperty = serialized.FindProperty("levelPips");
            pipProperty.arraySize = pips.Length;
            for (var i = 0; i < pips.Length; i++)
            {
                pipProperty.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildStatus(RectTransform bar, BattleController battle, UnitPlacementInput input)
        {
            var status = CreateRect("Status", bar, new Vector2(StatusLeft, 0f), Vector2.one);

            CreateImage("Life Icon", status, new Vector2(0.03f, 0.58f), new Vector2(0.13f, 0.9f), new Color(0.9f, 0.25f, 0.3f), _knob)
                .preserveAspect = true;
            var life = CreateNumber("Life", status, new Vector2(0.14f, 0.52f), new Vector2(0.3f, 0.96f), Color.white);
            // 名前は以前のまま（文字あり版の設定が名前で探す）。中身は所持金のお札（T360）。
            MoneyIconArt.Apply(CreateImage("Coin Icon", status, new Vector2(0.33f, 0.58f), new Vector2(0.43f, 0.9f),
                new Color(0.98f, 0.8f, 0.2f), _knob), _box, _knob);
            var coins = CreateNumber("Coins", status, new Vector2(0.44f, 0.52f), new Vector2(0.7f, 0.96f), Color.white);
            var wave = CreateNumber("Wave", status, new Vector2(0.73f, 0.52f), new Vector2(0.98f, 0.96f), Color.white);

            var start = CreateButton("Start Button", status, new Vector2(0.06f, 0.06f), new Vector2(0.34f, 0.46f),
                new Color(0.25f, 0.6f, 0.3f));
            CreateRect("Play", start.GetComponent<RectTransform>(), new Vector2(0.38f, 0.18f), new Vector2(0.66f, 0.82f))
                .gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;

            var retreat = CreateButton("Retreat Button", status, new Vector2(0.38f, 0.06f), new Vector2(0.64f, 0.46f),
                new Color(0.65f, 0.2f, 0.2f));
            var retreatRect = retreat.GetComponent<RectTransform>();
            for (var i = 0; i < 2; i++)
            {
                var stroke = CreateImage($"Stroke {i + 1}", retreatRect, new Vector2(0.44f, 0.12f), new Vector2(0.56f, 0.88f),
                    Color.white, null);
                stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 45f : -45f);
            }

            retreat.gameObject.SetActive(false);

            var pause = CreateButton("Pause Button", status, new Vector2(0.68f, 0.06f), new Vector2(0.96f, 0.46f),
                new Color(0.3f, 0.3f, 0.34f));
            var pauseRect = pause.GetComponent<RectTransform>();
            CreateImage("Bar Left", pauseRect, new Vector2(0.36f, 0.2f), new Vector2(0.46f, 0.8f), Color.white, null);
            CreateImage("Bar Right", pauseRect, new Vector2(0.54f, 0.2f), new Vector2(0.64f, 0.8f), Color.white, null);

            var component = status.gameObject.AddComponent<OperationBarStatus>();
            Assign(component, "battle", battle);
            Assign(component, "placementInput", input);
            Assign(component, "lifeText", life);
            Assign(component, "moneyText", coins);
            Assign(component, "waveText", wave);
            Assign(component, "startButton", start);
            Assign(component, "retreatButton", retreat);
            Assign(component, "pauseButton", pause);
            Assign(component, "pauseBackground", pause.GetComponent<Image>());
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);
            return rect;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = "0";
            text.color = color;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(Object target, string property, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
