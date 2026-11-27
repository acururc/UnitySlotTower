using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnitySlotTower
{
    /// <summary>スライダーを離したことを知らせる。</summary>
    public sealed class PointerUpRelay : MonoBehaviour, IPointerUpHandler
    {
        public event Action Released;

        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
    }
}
