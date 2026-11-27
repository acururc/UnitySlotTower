using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower
{
    /// <summary>
    /// BGM と効果音を鳴らす（spec 014）。シーンをまたいで1つだけ残る（2つ目は自分を消す）。
    /// どこからでも <see cref="Play(SoundId)"/> で鳴らせ、無ければ何もしない。
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private SoundCatalog catalog;
        [SerializeField, Min(1)] private int sePoolSize = 16;
        [SerializeField, Min(0f)] private float bgmFadeSeconds = 0.5f;

        private AudioSource[] _se;
        private AudioSource _loop;
        private AudioSource[] _bgm;
        private SoundCatalog.SoundEntry _loopEntry;
        private int _bgmActive;
        private float _bgmTarget;
        private bool _bgmFadingIn;
        private int _nextSe;
        private readonly VoiceLimiter _limiter = new VoiceLimiter();

        public static GameAudio Instance { get; private set; }

        /// <summary>効果音の要求があった（割り当てがなく鳴らなかったときも呼ぶ。テスト用）。</summary>
        public static event Action<SoundId> Requested;

        /// <summary>BGM の要求があった（テスト用）。</summary>
        public static event Action<BgmTrack> BgmRequested;

        public SoundSettings Settings { get; private set; }
        public SoundCatalog Catalog => catalog;
        public BgmTrack CurrentBgm { get; private set; }
        public bool IsSePaused { get; private set; }
        public bool IsLooping => _loop != null && _loopEntry != null;

        /// <summary>効果音を鳴らす。鳴らしたら true。</summary>
        public static bool Play(SoundId id) => Instance != null ? Instance.PlaySe(id) : NotifyOnly(id);

        /// <summary>高さ（pitch、1 が元の高さ）を変えて鳴らす（spec 015 FR-023 の揃いの n 本目）。</summary>
        public static bool Play(SoundId id, float pitch) => Instance != null ? Instance.PlaySe(id, false, pitch) : NotifyOnly(id);

        public static void PlayBgm(BgmTrack track)
        {
            if (Instance != null) Instance.ChangeBgm(track);
            else BgmRequested?.Invoke(track);
        }

        private static bool NotifyOnly(SoundId id)
        {
            Requested?.Invoke(id);
            return false;
        }

        /// <summary>Editor ツールとテスト用。</summary>
        public void SetCatalog(SoundCatalog value) => catalog = value;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Settings != null) Settings.Changed -= ApplyVolumes;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopLoop();
            SetSePaused(false);
        }

        private void EnsureInitialized()
        {
            if (Settings != null)
            {
                return;
            }

            Settings = new SoundSettings(new PlayerPrefsSoundStore());
            Settings.Changed += ApplyVolumes;
            _se = new AudioSource[sePoolSize];
            for (var i = 0; i < _se.Length; i++) _se[i] = CreateSource(false);
            _loop = CreateSource(true);
            _bgm = new[] { CreateSource(true), CreateSource(true) };
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>効果音を鳴らす（一時停止中・割り当てなし・同時数の上限なら鳴らさない）。</summary>
        public bool PlaySe(SoundId id, bool evenIfPaused = false, float pitch = 1f)
        {
            Requested?.Invoke(id);
            EnsureInitialized();
            if ((IsSePaused && !evenIfPaused) || catalog == null || !catalog.TryGet(id, out var entry))
            {
                return false;
            }

            var clip = entry.Next();
            if (clip == null || !_limiter.TryStart((int)id, Time.unscaledTimeAsDouble, clip.length, entry.maxVoices))
            {
                return false;
            }

            var source = FreeSource();
            source.clip = clip;
            source.volume = entry.volume * Settings.Se;
            source.pitch = pitch;
            source.Play();
            return true;
        }

        // 空いている音源。全部鳴っていたら一番前に使ったもの。
        private AudioSource FreeSource()
        {
            for (var i = 0; i < _se.Length; i++)
            {
                var source = _se[(_nextSe + i) % _se.Length];
                if (!source.isPlaying)
                {
                    _nextSe = (_nextSe + i + 1) % _se.Length;
                    return source;
                }
            }

            var oldest = _se[_nextSe];
            _nextSe = (_nextSe + 1) % _se.Length;
            return oldest;
        }

        /// <summary>ループの効果音（リール回転）を始める。同時に1つだけ。</summary>
        public void StartLoop(SoundId id)
        {
            Requested?.Invoke(id);
            EnsureInitialized();
            if (catalog == null || !catalog.TryGet(id, out var entry))
            {
                StopLoop();
                return;
            }

            var clip = entry.Next();
            if (clip == null)
            {
                StopLoop();
                return;
            }

            _loopEntry = entry;
            _loop.clip = clip;
            _loop.volume = entry.volume * Settings.Se;
            if (!IsSePaused) _loop.Play();
        }

        public void StopLoop()
        {
            _loopEntry = null;
            if (_loop != null) _loop.Stop();
        }

        /// <summary>一時停止中は効果音を止める（BGM は流し続ける、FR-005）。</summary>
        public void SetSePaused(bool paused)
        {
            if (IsSePaused == paused)
            {
                return;
            }

            IsSePaused = paused;
            EnsureInitialized();
            if (paused)
            {
                foreach (var source in _se) source.Stop();
                _limiter.Clear();
                _loop.Pause();
            }
            else if (_loopEntry != null)
            {
                _loop.UnPause();
                if (!_loop.isPlaying) _loop.Play();
            }
        }

        /// <summary>BGM を切り替える。同じ曲なら流し続ける。None か割り当てなしなら消す（FR-004）。</summary>
        public void ChangeBgm(BgmTrack track)
        {
            BgmRequested?.Invoke(track);
            EnsureInitialized();
            var current = _bgm[_bgmActive];
            if (track == CurrentBgm && (track == BgmTrack.None || current.isPlaying))
            {
                return;
            }

            CurrentBgm = track;
            _bgmActive = 1 - _bgmActive;
            if (catalog == null || !catalog.TryGet(track, out var entry))
            {
                // 今の曲はフェードアウトだけ（Update で前の曲として 0 へ）。
                _bgmTarget = 0f;
                return;
            }

            var next = _bgm[_bgmActive];
            next.clip = entry.clip;
            next.volume = 0f;
            next.Play();
            _bgmTarget = entry.volume;
            _bgmFadingIn = true;
        }

        public void StopBgm() => ChangeBgm(BgmTrack.None);

        private void Update()
        {
            if (_bgm == null)
            {
                return;
            }

            // BGM のフェード（0→1 を bgmFadeSeconds で）: 今の曲を目標の音量へ、前の曲を 0 へ。
            // フェードインが終わった後は音量の変更をすぐ反映する（FR-006）。
            var step = bgmFadeSeconds > 0f ? Time.unscaledDeltaTime / bgmFadeSeconds : 1f;
            for (var i = 0; i < _bgm.Length; i++)
            {
                var source = _bgm[i];
                if (!source.isPlaying)
                {
                    continue;
                }

                if (i != _bgmActive)
                {
                    source.volume = Mathf.MoveTowards(source.volume, 0f, step);
                    if (source.volume <= 0f) source.Stop();
                    continue;
                }

                var target = _bgmTarget * Settings.Bgm;
                if (_bgmFadingIn)
                {
                    source.volume = Mathf.MoveTowards(source.volume, target, step);
                    _bgmFadingIn = source.volume < target;
                }
                else
                {
                    source.volume = target;
                }
            }
        }

        private void ApplyVolumes()
        {
            if (_loopEntry != null) _loop.volume = _loopEntry.volume * Settings.Se;
        }
    }
}
