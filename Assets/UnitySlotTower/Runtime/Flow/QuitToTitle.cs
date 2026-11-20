using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Flow
{
    public sealed class QuitToTitle : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private HelpScreen help;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        public bool IsConfirmOpen => panel.activeSelf;
        public Button HomeButton => homeButton;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;

        private void OnEnable()
        {
            homeButton.onClick.AddListener(Open);
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            homeButton.onClick.RemoveListener(Open);
            confirmButton.onClick.RemoveListener(Confirm);
            cancelButton.onClick.RemoveListener(Close);
        }

        private void LateUpdate()
        {
            if (!battle.IsPaused && IsConfirmOpen) Close();

            var showHome = battle.IsPaused && !help.IsOpen && !IsConfirmOpen;
            if (homeButton.gameObject.activeSelf != showHome) homeButton.gameObject.SetActive(showHome);
        }

        private void Open()
        {
            if (!battle.IsPaused || help.IsOpen) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }

        public void Close()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            panel.SetActive(false);
        }

        private void Confirm()
        {
            if (!IsConfirmOpen || !battle.IsPaused) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameSession.TitleScene);
        }
    }
}
