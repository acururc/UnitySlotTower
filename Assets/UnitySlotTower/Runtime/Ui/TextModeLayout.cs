using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Ui
{
    /// <summary>
    /// 文字あり版で部品の配置と見た目を差し替える（FR-006、D-76、plan 16.13）。部品は両方の版で同じものを使い、
    /// 文字なし版の値（<see cref="noText"/>）と文字あり版の値（<see cref="text"/>）を両方シリアライズして持つ。
    /// 値は Editor の <c>Setup/Build Text Mode Layout (T359)</c> が置く。文字なし版の値はシーンの値と同じでなければならない（EditMode テストで確かめる）。
    /// 実行中にコードが位置や色を変える部品には付けない（切り替えで上書きするため）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextModeLayout : MonoBehaviour
    {
        [Flags]
        public enum Parts
        {
            None = 0,
            Rect = 1,
            Font = 2,
            Color = 4,
            Visibility = 8,
        }

        [Serializable]
        public struct State
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
            public Vector2 pivot;
            public float fontSize;
            public bool autoSize;
            public float fontSizeMin;
            public float fontSizeMax;
            public Color color;
            public bool hidden;
        }

        [SerializeField] private Parts parts;
        [SerializeField] private State noText;
        [SerializeField] private State text;

        private RectTransform _rect;
        private TMP_Text _label;
        private Graphic _graphic;
        private CanvasGroup _group;

        public Parts Affected => parts;

        public State NoText => noText;

        public State Text => text;

        private void Awake() => Cache();

        private void OnEnable()
        {
            GameSession.TextModeChanged += Apply;
            Apply(GameSession.TextMode);
        }

        private void OnDisable() => GameSession.TextModeChanged -= Apply;

        /// <summary>どちらかの版の値を置く（Editor の Setup から使う）。</summary>
        public void Configure(Parts affected, State noTextState, State textState)
        {
            parts = affected;
            noText = noTextState;
            text = textState;
        }

        /// <summary>いまの部品の値を読み取る（Setup が文字なし版の値を記録するのに使う）。</summary>
        public static State Read(GameObject target)
        {
            var rect = target.GetComponent<RectTransform>();
            var label = target.GetComponent<TMP_Text>();
            var graphic = target.GetComponent<Graphic>();
            var group = target.GetComponent<CanvasGroup>();
            var state = new State();
            if (rect != null)
            {
                state.anchorMin = rect.anchorMin;
                state.anchorMax = rect.anchorMax;
                state.offsetMin = rect.offsetMin;
                state.offsetMax = rect.offsetMax;
                state.pivot = rect.pivot;
            }

            if (label != null)
            {
                state.fontSize = label.fontSize;
                state.autoSize = label.enableAutoSizing;
                state.fontSizeMin = label.fontSizeMin;
                state.fontSizeMax = label.fontSizeMax;
            }

            if (graphic != null)
            {
                state.color = graphic.color;
            }

            state.hidden = group != null && group.alpha <= 0f;
            return state;
        }

        /// <summary>指定した版の値を部品に書く。</summary>
        public void Apply(bool textMode)
        {
            Cache();
            var state = textMode ? text : noText;
            if ((parts & Parts.Rect) != 0 && _rect != null)
            {
                _rect.anchorMin = state.anchorMin;
                _rect.anchorMax = state.anchorMax;
                _rect.pivot = state.pivot;
                _rect.offsetMin = state.offsetMin;
                _rect.offsetMax = state.offsetMax;
            }

            if ((parts & Parts.Font) != 0 && _label != null)
            {
                _label.enableAutoSizing = state.autoSize;
                _label.fontSizeMin = state.fontSizeMin;
                _label.fontSizeMax = state.fontSizeMax;
                _label.fontSize = state.fontSize;
            }

            if ((parts & Parts.Color) != 0 && _graphic != null)
            {
                _graphic.color = state.color;
            }

            if ((parts & Parts.Visibility) != 0 && _group != null)
            {
                // GameObject を止めるとこの部品も止まるので、CanvasGroup で見た目とクリックだけを消す。
                _group.alpha = state.hidden ? 0f : 1f;
                _group.blocksRaycasts = !state.hidden;
                _group.interactable = !state.hidden;
            }
        }

        private void Cache()
        {
            if (_rect != null)
            {
                return;
            }

            _rect = GetComponent<RectTransform>();
            _label = GetComponent<TMP_Text>();
            _graphic = GetComponent<Graphic>();
            _group = GetComponent<CanvasGroup>();
        }
    }
}
