using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// Title シーン（T401、plan 4章）。ロゴと ▶ の画面で、▶ を押すとすぐ Game が始まる（難易度の選択はない: spec 002 E-18）。
    /// ロゴ右下の透明なボタンで文字あり版に切り替える（FR-006）。マウスはクリック、キーボードは Enter（UI のナビゲーション）。
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [Tooltip("ロゴ右下の透明なボタン。押すと文字あり／なしを切り替える（FR-006）。")]
        [SerializeField] private Button textModeButton;

        /// <summary>▶（押すと Game が始まる）。</summary>
        public Button StartButton => startButton;

        /// <summary>文字あり／なしを切り替える隠しボタン。</summary>
        public Button TextModeButton => textModeButton;

        private void Awake()
        {
            startButton.onClick.AddListener(GameSession.StartGame);
            if (textModeButton != null)
            {
                textModeButton.onClick.AddListener(ToggleTextMode);
            }

            SelectStart();
        }

        // 背景をクリックすると選択が外れてキーボードで操作できなくなるので、▶ を選び直す。
        private void Update()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
            {
                eventSystem.SetSelectedGameObject(startButton.gameObject);
            }
        }

        // 隠しボタンは選択の対象にしない（キーボードの Enter は ▶ のまま）。
        private void ToggleTextMode()
        {
            GameSession.SetTextMode(!GameSession.TextMode);
            SelectStart();
        }

        private void SelectStart()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
        }
    }
}
