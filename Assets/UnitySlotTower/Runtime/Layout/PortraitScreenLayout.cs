using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Layout;

namespace UnitySlotTower.Layout
{
    /// <summary>
    /// 画面サイズに合わせて 3:4 の領域を中央に置く（plan 2章）。戦闘カメラは上半分から上端の帯を除く。
    /// UI は 1440×1920 の基準領域に収め、領域の外は背景カメラの黒で塗る。3:4 の領域全体にスロット台の 3D モデルを写す（T614）。
    /// </summary>
    [ExecuteAlways]
    public sealed class PortraitScreenLayout : MonoBehaviour
    {
        [SerializeField] private Camera battleCamera;
        [SerializeField] private CanvasScaler canvasScaler;

        [Tooltip("スロット台の 3D モデルを写すカメラ（T614）。3:4 の領域全体に写す。")]
        [SerializeField] private Camera cabinetCamera;

        private int _width;
        private int _height;

        private void OnEnable()
        {
            _width = 0;
            _height = 0;
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            var width = Screen.width;
            var height = Screen.height;
            if (width == _width && height == _height)
            {
                return;
            }

            _width = width;
            _height = height;

            if (battleCamera != null)
            {
                var battle = PortraitLayout.BattleView(width, height);
                battleCamera.rect = new Rect((float)battle.X, (float)battle.Y, (float)battle.Width, (float)battle.Height);
            }

            if (cabinetCamera != null)
            {
                var box = PortraitLayout.Letterbox(width, height);
                cabinetCamera.rect = new Rect((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);
            }

            if (canvasScaler != null)
            {
                canvasScaler.matchWidthOrHeight = PortraitLayout.MatchHeight(width, height) ? 1f : 0f;
            }
        }
    }
}
