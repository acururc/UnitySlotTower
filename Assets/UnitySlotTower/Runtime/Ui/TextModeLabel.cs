using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Ui
{
    /// <summary>
    /// 文字あり版だけに出る日本語のラベル（FR-006、plan 16.9）。文言は <c>ui_texts.csv</c> の id で引く。
    /// 文字なし版では文字も下地も描かない（部品の配置は変えず、上に足すだけ）。文字チェック（TextAudit）はこの部品の子を対象外にする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextModeLabel : MonoBehaviour
    {
        // 表ごとに一度だけ読む。ドメインの再読み込みを切っているので Play のたびに捨てる。
        private static readonly Dictionary<TextAsset, UiTexts> Cache = new Dictionary<TextAsset, UiTexts>();

        [SerializeField] private TextAsset table;
        [Tooltip("ui_texts.csv の id。空なら文言なし（表示しない）。")]
        [SerializeField] private string id;
        [SerializeField] private TMP_Text text;
        [Tooltip("文字の下地（なくてもよい）。文字と一緒に出し入れする。")]
        [SerializeField] private Graphic background;

        private readonly StringBuilder _builder = new StringBuilder();
        private bool _resolved;
        private bool _hasText;

        public string Id => id;

        public TMP_Text Text => text;

        public Graphic Background => background;

        /// <summary>シーンのラベルから文言表を借りる。</summary>
        public void CopyTableFrom(TextModeLabel source)
        {
            table = source != null ? source.table : null;
            text ??= GetComponent<TMP_Text>();
            Resolve();
            Apply(GameSession.TextMode);
        }

        /// <summary>いま文字が描かれるか（文字あり版で、文言がある）。</summary>
        public bool IsShown => text != null && text.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Cache.Clear();

        /// <summary>表の id の文言を返す。表や id がなければ false。</summary>
        public static bool TryResolve(TextAsset source, string textId, out string value)
        {
            value = null;
            if (source == null || string.IsNullOrEmpty(textId))
            {
                return false;
            }

            if (!Cache.TryGetValue(source, out var texts))
            {
                texts = UiTexts.Parse(source.text);
                Cache.Add(source, texts);
            }

            return texts.TryGet(textId, out value);
        }

        private void OnEnable()
        {
            GameSession.TextModeChanged += Apply;
            if (!_resolved)
            {
                Resolve();
            }

            Apply(GameSession.TextMode);
        }

        private void OnDisable() => GameSession.TextModeChanged -= Apply;

        /// <summary>
        /// 文言を差し替える。複数の id は改行でつなぐ。id を渡さなければ文言なし（文字あり版でも出さない）。
        /// </summary>
        public void SetIds(params string[] ids)
        {
            _builder.Clear();
            if (ids != null)
            {
                foreach (var textId in ids)
                {
                    if (!TryResolve(table, textId, out var value))
                    {
                        Debug.LogWarning($"[TextModeLabel] {name}: ui_texts.csv に id '{textId}' がない", this);
                        continue;
                    }

                    if (_builder.Length > 0)
                    {
                        _builder.Append('\n');
                    }

                    _builder.Append(value);
                }
            }

            id = ids != null && ids.Length > 0 ? ids[0] : string.Empty;
            SetResolved(_builder.ToString());
            Apply(GameSession.TextMode);
        }

        /// <summary>
        /// 数字の入る文言（<c>{0}</c>・<c>{1}</c> を含む）に値を入れて出す。画面を開いたときに使う（毎フレームは呼ばない）。
        /// </summary>
        public void SetFormat(string textId, int value0, int value1 = 0)
        {
            if (!TryResolve(table, textId, out var format))
            {
                Debug.LogWarning($"[TextModeLabel] {name}: ui_texts.csv に id '{textId}' がない", this);
                SetIds();
                return;
            }

            id = textId;
            SetResolved(string.Format(format, value0, value1));
            Apply(GameSession.TextMode);
        }

        private void Resolve()
        {
            if (string.IsNullOrEmpty(id))
            {
                SetResolved(string.Empty);
                return;
            }

            if (!TryResolve(table, id, out var value))
            {
                Debug.LogWarning($"[TextModeLabel] {name}: ui_texts.csv に id '{id}' がない", this);
                value = string.Empty;
            }

            SetResolved(value);
        }

        private void SetResolved(string value)
        {
            _resolved = true;
            _hasText = value.Length > 0;
            if (text != null)
            {
                text.text = value;
            }
        }

        private void Apply(bool textMode)
        {
            var show = textMode && _hasText;
            if (text != null)
            {
                text.enabled = show;
            }

            if (background != null)
            {
                background.enabled = show;
            }
        }
    }
}
