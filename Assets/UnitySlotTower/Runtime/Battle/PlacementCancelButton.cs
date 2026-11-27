using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 向きを選んでいる間、置く予定のマスの右上に × のボタンを出し、押したら配置をやめる（味方の選びも外す）。
    /// T385 のブロック数の印の置き換え（2026-11-27 ユーザー指示: 印は邪魔なので消し、取り消しの × にする）。
    /// 画面の UI（Screen Space Overlay）の上で、盤面のマスの位置を毎フレーム写す。
    /// </summary>
    public sealed class PlacementCancelButton : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [Tooltip("出し入れして動かす × のボタン。")]
        [SerializeField] private Button button;
        [Tooltip("マスの上端からのずれ（UI の座標、右上へ）。")]
        [SerializeField] private Vector2 offset = new Vector2(90f, 90f);

        /// <summary>× を出しているか。</summary>
        public bool IsShown => button.gameObject.activeSelf;

        /// <summary>× のボタン（テスト用）。</summary>
        public Button Button => button;

        private void Awake()
        {
            button.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            var deployment = battle.Deployment;
            if (deployment == null || battle.IsInputBlocked)
            {
                return;
            }

            // Cancel は1段階ずつ戻る（向き選び→マス選び→選択なし）。× は選択なしまで戻す。
            while (deployment.Phase != DeployPhase.Idle)
            {
                deployment.Cancel();
            }
        }

        private void LateUpdate()
        {
            var deployment = battle.Deployment;
            var show = deployment != null && deployment.Phase == DeployPhase.ChoosingFacing && deployment.Selected != null
                && !battle.IsInputBlocked;
            if (button.gameObject.activeSelf != show)
            {
                button.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            var screen = battleCamera.WorldToScreenPoint(fieldView.CellTopToWorld(deployment.Cursor));
            var rect = (RectTransform)button.transform;
            var parent = (RectTransform)rect.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
            {
                rect.localPosition = local + offset;
            }
        }
    }
}
