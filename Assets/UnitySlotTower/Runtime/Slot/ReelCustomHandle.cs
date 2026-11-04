using UnityEngine;
using UnityEngine.EventSystems;

namespace UnitySlotTower.Slot
{
    /// <summary>カスタム画面のマスと図柄の札が受けるクリックとドラッグを <see cref="ReelCustomScreen"/> へ渡す（T322）。</summary>
    public sealed class ReelCustomHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const int PaletteKind = 0;
        public const int CellKind = 1;

        private ReelCustomScreen _screen;
        private int _kind;
        private int _index;

        internal void Bind(ReelCustomScreen screen, int kind, int index)
        {
            _screen = screen;
            _kind = kind;
            _index = index;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
            {
                _screen.OnHandleClick(_kind, _index);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _screen.OnHandleBeginDrag(_kind, _index, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData) => _screen.OnHandleDrag(eventData);

        public void OnEndDrag(PointerEventData eventData) => _screen.OnHandleEndDrag(eventData);
    }
}
