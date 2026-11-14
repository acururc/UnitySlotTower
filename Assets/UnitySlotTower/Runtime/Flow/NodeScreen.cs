using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 戦闘以外のマスの画面（plan 16.7、D-69 の Runtime 側の登録表）。2D の全画面オーバーレイで、◀ で閉じてルート画面に戻る。
    /// マスの種類を増やすときは、これを継いだ画面を作って <see cref="RunController"/> の画面の一覧に足す。
    /// </summary>
    public abstract class NodeScreen : MonoBehaviour
    {
        [Tooltip("開いているあいだ出す本体（下のフィールド・操作バー・筐体へのクリックを遮る）。")]
        [SerializeField] protected GameObject panel;
        [SerializeField] private Button backButton;

        /// <summary>この画面が受け持つマスの種類（<see cref="RouteNodeKinds"/> の id）。</summary>
        public abstract string Kind { get; }

        public bool IsOpen => panel != null && panel.activeSelf;

        public Button BackButton => backButton;

        /// <summary>◀ で閉じた。</summary>
        public event Action Closed;

        protected virtual void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }
        }

        /// <summary>マスに入った直後に開く（そのマスの処理は <see cref="RunController"/> が済ませている）。</summary>
        public void Open(RunController run, RouteNode node)
        {
            panel.SetActive(true);
            OnOpen(run ?? throw new ArgumentNullException(nameof(run)), node);
        }

        /// <summary>閉じて <see cref="Closed"/> を通知する。開いていなければ何もしない。</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            panel.SetActive(false);
            OnClose();
            Closed?.Invoke();
        }

        /// <summary>シーンの読み込み時に閉じておく（通知しない）。</summary>
        public void HideImmediate()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        protected abstract void OnOpen(RunController run, RouteNode node);

        protected virtual void OnClose()
        {
        }
    }
}
