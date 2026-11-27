using UnityEngine;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// ステージ1の見せ場でレバーを待つ間の強調（spec 015 FR-004）。<see cref="SlotCabinet.LeverHighlighted"/> の間だけ、
    /// 筐体の矩形を除いた画面を暗幕で覆い（暗幕は入力も止める）、レバーを指す大きな矢印と案内（文字あり版だけ、<c>TextModeLabel</c>）を出す。
    /// 画面の最前面（Portrait Root の最後の子）に置き、暗幕の位置は毎フレーム筐体の矩形に合わせる。部品は Setup が作る。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SlotShowcaseGuide : MonoBehaviour
    {
        [SerializeField] private SlotCabinet cabinet;
        [Tooltip("筐体の上・下・左・右を覆う暗幕（anchor と pivot は左下）。")]
        [SerializeField] private RectTransform[] shades = new RectTransform[4];
        [Tooltip("暗幕・矢印・案内をまとめた子（出し入れする）。")]
        [SerializeField] private GameObject content;
        [Tooltip("レバーの真上から下を指す矢印（pivot は下端の中央）。")]
        [SerializeField] private RectTransform arrow;
        [Tooltip("筐体の真上に出す案内（pivot は下端の中央）。")]
        [SerializeField] private RectTransform message;
        [Tooltip("案内の下端と筐体の上端の間（px）。レバーの上の矢印に重ならない高さ。")]
        [SerializeField] private float messageLift = 260f;
        [Tooltip("矢印の先とレバーの間（px）。")]
        [SerializeField] private float arrowGap = 8f;
        [Tooltip("矢印の上下の揺れ幅（px）と1秒の往復回数。")]
        [SerializeField] private float arrowBounce = 36f;
        [SerializeField] private float arrowFrequency = 1.6f;

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;

        /// <summary>いま暗幕と矢印が出ているか。</summary>
        public bool IsShown { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            SetShown(false);
        }

        private void LateUpdate()
        {
            var show = cabinet != null && cabinet.LeverHighlighted;
            if (show != IsShown)
            {
                SetShown(show);
            }

            if (show)
            {
                FitShades();
                PlaceArrow();
            }
        }

        // レバーの上端の中央に矢印の先を合わせ、上下に揺らす（一時停止中も動くよう実時間）。
        private void PlaceArrow()
        {
            if (arrow == null)
            {
                return;
            }

            cabinet.LeverRect.GetWorldCorners(_corners);
            var top = ((Vector2)_rect.InverseTransformPoint(_corners[1]) + (Vector2)_rect.InverseTransformPoint(_corners[2])) * 0.5f;
            var wave = (1f - Mathf.Cos(Time.unscaledTime * arrowFrequency * 2f * Mathf.PI)) * 0.5f;
            arrow.anchoredPosition = top - _rect.rect.min + Vector2.up * (arrowGap + wave * arrowBounce);
        }

        private void SetShown(bool value)
        {
            IsShown = value;
            if (content != null)
            {
                content.SetActive(value);
            }
        }

        // 筐体の矩形をこの部品の座標に直し、その外側の4枚で画面を覆う。
        private void FitShades()
        {
            ((RectTransform)cabinet.transform).GetWorldCorners(_corners);
            var min = (Vector2)_rect.InverseTransformPoint(_corners[0]);
            var max = (Vector2)_rect.InverseTransformPoint(_corners[2]);
            var area = _rect.rect;
            // 黒帯の外まで覆えるよう、画面の端は大きめに伸ばす。
            var pad = Mathf.Max(area.width, area.height);
            var left = area.xMin - pad;
            var right = area.xMax + pad;
            var bottom = area.yMin - pad;
            var top = area.yMax + pad;
            Place(0, left, max.y, right, top);
            Place(1, left, bottom, right, min.y);
            Place(2, left, min.y, min.x, max.y);
            Place(3, max.x, min.y, right, max.y);
            if (message != null)
            {
                message.anchoredPosition = new Vector2((min.x + max.x) * 0.5f - area.xMin, max.y + messageLift - area.yMin);
            }

            void Place(int index, float x0, float y0, float x1, float y1)
            {
                var shade = index < shades.Length ? shades[index] : null;
                if (shade == null)
                {
                    return;
                }

                shade.anchoredPosition = new Vector2(x0 - area.xMin, y0 - area.yMin);
                shade.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
            }
        }
    }
}
