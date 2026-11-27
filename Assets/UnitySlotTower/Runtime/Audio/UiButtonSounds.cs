using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Slot;

namespace UnitySlotTower
{
    /// <summary>
    /// シーンの Button を押したらボタン音を鳴らす（spec 014 FR-003）。後から作られたボタンも拾うため、
    /// 有効な Selectable を一定間隔で見直す。スロット台の中のボタン（レバー・BET）は自前の音を出すので除く。
    /// </summary>
    public sealed class UiButtonSounds : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float scanSeconds = 0.5f;

        private readonly HashSet<Button> _registered = new HashSet<Button>();
        private Selectable[] _buffer = new Selectable[64];
        private float _nextScan;

        private void Start() => Scan();

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                Scan();
            }
        }

        private void Scan()
        {
            _nextScan = Time.unscaledTime + scanSeconds;
            if (_buffer.Length < Selectable.allSelectableCount)
            {
                _buffer = new Selectable[Selectable.allSelectableCount * 2];
            }

            var count = Selectable.AllSelectablesNoAlloc(_buffer);
            for (var i = 0; i < count; i++)
            {
                if (_buffer[i] is Button button && _registered.Add(button) && !HasOwnSound(button))
                {
                    button.onClick.AddListener(OnClick);
                }

                _buffer[i] = null;
            }
        }

        private static bool HasOwnSound(Button button) => button.GetComponentInParent<SlotCabinet>(true) != null;

        private static void OnClick() => GameAudio.Play(SoundId.Button);
    }
}
