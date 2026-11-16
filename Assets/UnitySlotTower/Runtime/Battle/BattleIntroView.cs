using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 戦闘マスの開始の演出（FR-019、plan 17.2、T361）。演出のあいだ、敵の出口から各経路を光の粒が走り、
    /// 戦闘フィールドの中央に大きな ▶ が開いて消える。進み具合は <see cref="BattleNodeRun.IntroProgress"/>（戦闘の時計）に合わせるので、
    /// 一時停止中は止まる。操作は受け付けない（拒否は <see cref="BattleController.IsInputBlocked"/>）。
    /// </summary>
    public sealed class BattleIntroView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private Material primitiveMaterial;
        [Tooltip("中央の ▶ のまとまり（透明度で出し入れする）。")]
        [SerializeField] private CanvasGroup playGroup;
        [Tooltip("開く動きで拡大する ▶ の本体。")]
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
        [Tooltip("▶ が開き切るのは演出のこの割合の時点。")]
        [SerializeField, Range(0.05f, 1f)] private float openPortion = 0.3f;
        [Tooltip("▶ が消え始めるのは演出のこの割合の時点。")]
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.75f;
        [SerializeField] private float groundLightHeight = 0.25f;

        private readonly List<Transform> _lights = new List<Transform>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private Transform _root;
        private BattleNodeRun _shownRun;
        private bool _shown;

        /// <summary>いま開始の演出を見せているか。</summary>
        public bool IsShowing => _shown;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _root = new GameObject("Intro Lights").transform;
            _root.SetParent(fieldView.transform, false);
            SetPlayVisible(false);
        }

        private void LateUpdate()
        {
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
            _shownRun = run;
            _shown = true;
            EnsureLights(run.Map.Paths.Count * lightsPerPath);
            SetPlayVisible(true);

            // ▶ はマップの中央（カメラに写る位置）に置く。
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
            _shown = false;
            _shownRun = null;
            for (var i = 0; i < _lights.Count; i++)
            {
                _lights[i].gameObject.SetActive(false);
            }

            SetPlayVisible(false);
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
            var open = Mathf.Clamp01(progress / openPortion);
            // 少し行き過ぎてから戻る開き方（easeOutBack）。
            const float overshoot = 1.70158f;
            var e = open - 1f;
            var scale = 1f + (overshoot + 1f) * e * e * e + overshoot * e * e;
            playMark.localScale = Vector3.one * Mathf.Max(0f, scale);
            playGroup.alpha = progress < fadeFrom ? 1f : Mathf.Clamp01(1f - (progress - fadeFrom) / (1f - fadeFrom));
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
