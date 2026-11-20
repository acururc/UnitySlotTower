using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 戦闘マスの開始の演出（FR-019、plan 17.2、T361）。演出のあいだ、敵の出口から各経路を光の粒が走り、
    /// 暗い背景から盤面を見せた後、中央のマスの種類の絵が消える。進み具合は <see cref="BattleNodeRun.IntroProgress"/>（戦闘の時計）に合わせるので、
    /// 一時停止中は止まる。操作は受け付けない（拒否は <see cref="BattleController.IsInputBlocked"/>）。
    /// </summary>
    public sealed class BattleIntroView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private Material primitiveMaterial;
        [Tooltip("中央のアイコンの入れ物（透明度で出し入れする）。")]
        [SerializeField] private CanvasGroup playGroup;
        [Tooltip("既存の開始マーク。実行時にマスの種類の絵へ差し替える。")]
        [SerializeField] private RectTransform playMark;
        [Tooltip("経路の線（黄・水色）に紛れないよう白に近い色。")]
        [SerializeField] private Color groundLightColor = new Color(1f, 1f, 0.85f);
        [SerializeField] private Color airLightColor = new Color(0.85f, 1f, 1f);
        [Tooltip("1経路あたりの光の粒の数（先頭が一番大きい）。")]
        [SerializeField, Min(1)] private int lightsPerPath = 5;
        [Tooltip("粒の間隔（経路の長さに対する割合）。")]
        [SerializeField, Range(0f, 0.2f)] private float lightSpacing = 0.05f;
        [SerializeField] private float lightSize = 0.7f;
        [Tooltip("光が経路の端まで走り切るのは演出のこの割合の時点。")]
        [SerializeField, Range(0.1f, 1f)] private float runPortion = 0.85f;
        [Tooltip("暗い背景が消え始めるのは演出のこの割合の時点。")]
        [SerializeField, Range(0.05f, 1f)] private float openPortion = 0.3f;
        [Tooltip("アイコンが消え始めるのは演出のこの割合の時点。")]
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.75f;
        [SerializeField] private float groundLightHeight = 0.25f;

        private readonly List<Transform> _lights = new List<Transform>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private Transform _root;
        private BattleNodeRun _shownRun;
        private bool _shown;
        private RectTransform _boardOverlay;
        private Image _dark;
        private RectTransform _nodeIcon;
        private TMP_Text _nodeName;
        private RectTransform _victory;
        private TMP_Text _victoryName;
        private Sprite _knob;

        public float BackgroundAlpha => _dark.gameObject.activeSelf ? _dark.color.a : 0f;
        public float IconAlpha => _shown ? playGroup.alpha : 0f;
        public bool IsVictoryBandVisible => _victory.gameObject.activeSelf;
        public bool IsNodeNameVisible => _nodeName.isActiveAndEnabled;
        public bool IsVictoryNameVisible => _victoryName.isActiveAndEnabled;

        /// <summary>いま開始の演出を見せているか。</summary>
        public bool IsShowing => _shown;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _root = new GameObject("Intro Lights").transform;
            _root.SetParent(fieldView.transform, false);
            BuildPresentation();
            SetPlayVisible(false);
        }

        private void LateUpdate()
        {
            if (_victory.gameObject.activeSelf != battle.IsShowingVictory)
            {
                FitOverlay();
                _victory.gameObject.SetActive(battle.IsShowingVictory);
            }
            _victoryName.gameObject.SetActive(GameSession.TextMode);
            _nodeName.gameObject.SetActive(GameSession.TextMode);
            var run = battle.Run;
            if (run == null || run.Phase != WavePhase.Intro)
            {
                if (_shown)
                {
                    Hide();
                }

                return;
            }

            if (run != _shownRun || !_shown)
            {
                Begin(run);
            }

            var progress = (float)run.IntroProgress;
            UpdateLights(run.Map, progress);
            UpdatePlay(progress);
        }

        private void Begin(BattleNodeRun run)
        {
            fieldView.ShowAllPaths();
            _shownRun = run;
            _shown = true;
            EnsureLights(run.Map.Paths.Count * lightsPerPath);
            FitOverlay();
            _dark.gameObject.SetActive(true);
            if (_nodeIcon != null) Destroy(_nodeIcon.gameObject);
            _nodeIcon = RouteNodeIcons.CreateRect("Node Type", playMark, Vector2.zero, Vector2.one);
            RouteNodeIcons.Create(_nodeIcon, battle.NodeKind, _knob, null);
            _nodeName.GetComponent<TextModeLabel>().SetIds(battle.NodeKind switch
            {
                RouteNodeKinds.First => "node_first",
                RouteNodeKinds.Elite => "node_elite",
                RouteNodeKinds.Boss => "node_boss",
                _ => "node_battle",
            });
            SetPlayVisible(true);

            // アイコンはマップの中央（カメラに写る位置）に置く。
            var parent = (RectTransform)playGroup.transform.parent;
            var map = run.Map;
            var center = fieldView.PositionToWorld((map.Width - 1) * 0.5, (map.Height - 1) * 0.5, false);
            Vector2 screen = battleCamera.WorldToScreenPoint(center);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
            {
                var rect = (RectTransform)playGroup.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = local + parent.rect.size * (parent.pivot - new Vector2(0.5f, 0.5f));
            }
        }

        private void Hide()
        {
            // A fighting step may already have replaced the intro lines with a spawn cue.
            if (fieldView.ShowingAllPaths) fieldView.HideAllPaths();
            _shown = false;
            _shownRun = null;
            for (var i = 0; i < _lights.Count; i++)
            {
                _lights[i].gameObject.SetActive(false);
            }

            SetPlayVisible(false);
            _dark.gameObject.SetActive(false);
        }

        private void UpdateLights(BattleMap map, float progress)
        {
            var run = progress / runPortion;
            var index = 0;
            for (var p = 0; p < map.Paths.Count; p++)
            {
                var path = map.Paths[p];
                var color = path.IsAir ? airLightColor : groundLightColor;
                for (var k = 0; k < lightsPerPath; k++, index++)
                {
                    var light = _lights[index];
                    var along = run - k * lightSpacing;
                    if (along < 0f || along > 1f)
                    {
                        light.gameObject.SetActive(false);
                        continue;
                    }

                    light.gameObject.SetActive(true);
                    light.position = Along(path, along);
                    light.localScale = Vector3.one * (lightSize * (1f - k / (float)(lightsPerPath + 1)));
                    _block.SetColor(BaseColor, color);
                    _renderers[index].SetPropertyBlock(_block);
                }
            }
        }

        // 経路の先頭（出現口）を 0、最後（ゴール）を 1 とした位置のワールド座標。
        private Vector3 Along(MapPath path, float t)
        {
            var cells = path.Cells;
            var position = t * (cells.Count - 1);
            var a = Mathf.Clamp(Mathf.FloorToInt(position), 0, cells.Count - 1);
            var b = Mathf.Min(a + 1, cells.Count - 1);
            var f = position - a;
            var x = Mathf.Lerp(cells[a].X, cells[b].X, f);
            var y = Mathf.Lerp(cells[a].Y, cells[b].Y, f);
            var world = fieldView.PositionToWorld(x, y, path.IsAir);
            return path.IsAir ? world : world + fieldView.transform.up * groundLightHeight;
        }

        private void UpdatePlay(float progress)
        {
            // 背景の切り替えが終わってからアイコンを消す。全体の時計は intro_seconds のまま。
            var color = UiStyle.ScreenFill;
            color.a = 1f - Mathf.InverseLerp(openPortion, fadeFrom - 0.1f, progress);
            _dark.color = color;
            playGroup.alpha = progress < fadeFrom ? 1f : Mathf.Clamp01(1f - (progress - fadeFrom) / (1f - fadeFrom));
        }

        private void BuildPresentation()
        {
            var old = playMark.GetComponent<Image>();
            _knob = old != null ? old.sprite : null;
            playMark.gameObject.SetActive(false);
            var host = (RectTransform)playGroup.transform.parent;
            _boardOverlay = RouteNodeIcons.CreateRect("Board Presentation", host, Vector2.zero, Vector2.one);
            _boardOverlay.SetSiblingIndex(playGroup.transform.GetSiblingIndex());
            _dark = RouteNodeIcons.CreateImage("Intro Background", _boardOverlay, Vector2.zero, Vector2.one, UiStyle.ScreenFill, null);
            _dark.gameObject.SetActive(false);
            playMark = RouteNodeIcons.CreateRect("Node Mark", (RectTransform)playGroup.transform,
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            var fontSource = GetComponentInParent<Canvas>().GetComponentInChildren<TMP_Text>(true);
            var captionSource = GetComponentInParent<Canvas>().GetComponentInChildren<TextModeLabel>(true);
            var font = captionSource != null && captionSource.Text != null ? captionSource.Text.font
                : fontSource != null ? fontSource.font : TMP_Settings.defaultFontAsset;
            _nodeName = Label("Node Name", (RectTransform)playGroup.transform, font,
                new Vector2(-0.4f, -0.3f), new Vector2(1.4f, 0.08f), captionSource, "node_battle");
            // 同じ場所にあった T352 の「戦闘開始」はマスの名前に置き換えたので出さない（T389）。
            foreach (var startLabel in GetComponentsInChildren<TextModeLabel>(true))
            {
                if (startLabel.Id == "battle_start") startLabel.SetIds();
            }

            _victory = RouteNodeIcons.CreateRect("Victory Band", _boardOverlay, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));
            var panel = _victory.gameObject.AddComponent<UiPanel>();
            panel.color = UiStyle.BandFill;
            panel.SetBorder(UiStyle.Ready);
            panel.raycastTarget = false;
            var iconArea = RouteNodeIcons.CreateRect("Victory Art", _victory, new Vector2(0.4f, 0.32f), new Vector2(0.6f, 0.98f));
            var icon = RouteNodeIcons.CreateRect("Victory Icon", iconArea, Vector2.zero, Vector2.one);
            var fit = icon.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            RouteNodeIcons.Create(icon, RouteNodeKinds.Boss, null, null).color = UiStyle.Ready;
            _victoryName = Label("Victory Name", _victory, font, new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.32f), captionSource, "battle_end");
            _victory.gameObject.SetActive(false);
        }

        private static TMP_Text Label(string name, RectTransform parent, TMP_FontAsset font, Vector2 min, Vector2 max, TextModeLabel source, string id)
        {
            var label = RouteNodeIcons.CreateRect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 46f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = UiStyle.TextColor;
            label.raycastTarget = false;
            var caption = label.gameObject.AddComponent<TextModeLabel>();
            caption.CopyTableFrom(source);
            caption.SetIds(id);
            return label;
        }

        private void FitOverlay()
        {
            var parent = (RectTransform)_boardOverlay.parent;
            var pixels = battleCamera.pixelRect;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pixels.min, null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pixels.max, null, out var max);
            _boardOverlay.anchorMin = _boardOverlay.anchorMax = parent.pivot;
            _boardOverlay.anchoredPosition = (min + max) * 0.5f;
            _boardOverlay.sizeDelta = max - min;
        }

        private void SetPlayVisible(bool visible)
        {
            playGroup.alpha = visible ? 1f : 0f;
            playGroup.blocksRaycasts = false;
            playGroup.interactable = false;
            if (playGroup.gameObject.activeSelf != visible)
            {
                playGroup.gameObject.SetActive(visible);
            }
        }

        private void EnsureLights(int count)
        {
            while (_lights.Count < count)
            {
                var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                light.name = "Intro Light";
                // 配置の当たり判定（マスの選択）を邪魔しない。
                Destroy(light.GetComponent<Collider>());
                light.transform.SetParent(_root, false);
                var lightRenderer = light.GetComponent<Renderer>();
                if (primitiveMaterial != null)
                {
                    lightRenderer.sharedMaterial = primitiveMaterial;
                }

                lightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                light.SetActive(false);
                _lights.Add(light.transform);
                _renderers.Add(lightRenderer);
            }
        }
    }
}
