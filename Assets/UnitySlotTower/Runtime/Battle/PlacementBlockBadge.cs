using TMPro;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 向きを選んでいる間、置く予定のマスの上にブロック数の印（札と同じ絵＋数字、0 は ×）を出す（T385、FR-072、plan 18.5）。
    /// 画面の UI（Screen Space Overlay）の上で、盤面のマスの位置を毎フレーム写す。
    /// </summary>
    public sealed class PlacementBlockBadge : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [Tooltip("出し入れして動かす印（札のブロック数の写し）。")]
        [SerializeField] private RectTransform badge;
        [SerializeField] private TMP_Text value;
        [Tooltip("マスの上端からの上へのずれ（UI の座標）。")]
        [SerializeField] private float offsetY = 70f;

        private int _shownBlock = -1;

        /// <summary>印を出しているか。</summary>
        public bool IsShown => badge.gameObject.activeSelf;

        /// <summary>出している数字（テスト用）。</summary>
        public string Value => value.text;

        private void Awake()
        {
            badge.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var deployment = battle.Deployment;
            var show = deployment != null && deployment.Phase == DeployPhase.ChoosingFacing && deployment.Selected != null;
            if (badge.gameObject.activeSelf != show)
            {
                badge.gameObject.SetActive(show);
            }

            if (!show)
            {
                _shownBlock = -1;
                return;
            }

            var block = deployment.Selected.Block;
            if (block != _shownBlock)
            {
                _shownBlock = block;
                BlockText.Show(value, block);
            }

            var screen = battleCamera.WorldToScreenPoint(fieldView.CellTopToWorld(deployment.Cursor));
            var parent = (RectTransform)badge.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
            {
                badge.localPosition = local + new Vector2(0f, offsetY);
            }
        }
    }
}
