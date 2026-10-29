using UnityEngine;
using UnityEngine.EventSystems;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 操作バーの味方アイコン（T208 時点の仮）。押すとその味方を選び、そのままフィールドへドラッグして離すとマスが決まる。
    /// コスト・待ち時間などの表示は T308 で付ける。
    /// </summary>
    public sealed class UnitBarIcon : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private UnitPlacementInput placementInput;
        [Tooltip("units.csv の並びで 0 始まり。")]
        [SerializeField] private int slot;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                placementInput.SelectSlot(slot);
            }
        }
    }
}
