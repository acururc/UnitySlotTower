using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Flow;

namespace UnitySlotTower
{
    /// <summary>
    /// BGM と効果音の音量スライダー（spec 014 FR-006）。動かすとすぐ反映して保存し、効果音のスライダーを離すと確認音を鳴らす。
    /// battle があれば一時停止中（早見表・確認の板が出ていないとき）だけ panel を出す。無ければ（タイトル）常に出す。
    /// </summary>
    public sealed class SoundVolumePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private BattleController battle;
        [SerializeField] private HelpScreen help;
        [SerializeField] private QuitToTitle quit;

        public Slider BgmSlider => bgmSlider;
        public Slider SeSlider => seSlider;
        public bool IsShown => panel != null && panel.activeSelf;

        private void Start()
        {
            var settings = GameAudio.Instance != null ? GameAudio.Instance.Settings : null;
            if (settings != null)
            {
                bgmSlider.SetValueWithoutNotify(settings.Bgm);
                seSlider.SetValueWithoutNotify(settings.Se);
            }

            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            seSlider.onValueChanged.AddListener(OnSeChanged);
            Relay(bgmSlider).Released += PlayerPrefsSoundStore.Flush;
            Relay(seSlider).Released += OnSeReleased;
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            if (seSlider != null) seSlider.onValueChanged.RemoveListener(OnSeChanged);
        }

        private void Update() => UpdateVisibility();

        private void UpdateVisibility()
        {
            if (battle == null || panel == null)
            {
                return;
            }

            var show = battle.IsPaused && (help == null || !help.IsOpen) && (quit == null || !quit.IsConfirmOpen);
            if (panel.activeSelf != show) panel.SetActive(show);
        }

        private static void OnBgmChanged(float value)
        {
            if (GameAudio.Instance != null) GameAudio.Instance.Settings.Bgm = value;
        }

        private static void OnSeChanged(float value)
        {
            if (GameAudio.Instance != null) GameAudio.Instance.Settings.Se = value;
        }

        private static void OnSeReleased()
        {
            PlayerPrefsSoundStore.Flush();
            if (GameAudio.Instance == null) return;

            // 一時停止中でも確認音は鳴らす。
            GameAudio.Instance.PlaySe(SoundId.Button, evenIfPaused: true);
        }

        private static PointerUpRelay Relay(Slider slider)
        {
            var relay = slider.GetComponent<PointerUpRelay>();
            return relay != null ? relay : slider.gameObject.AddComponent<PointerUpRelay>();
        }
    }
}
