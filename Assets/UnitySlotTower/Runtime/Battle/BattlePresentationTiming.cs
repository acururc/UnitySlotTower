namespace UnitySlotTower.Battle
{
    /// <summary>FR-077 の演出時間（秒）。開始だけは game_config の intro_seconds を使う。</summary>
    public static class BattlePresentationTiming
    {
        public const float ExitFlash = 0.3f;
        public const float PathCue = 2f;
        public const float PathCueFade = 0.3f;
        public const float PathCueGap = 3f;
        public const float BaseFlash = 0.5f;
        /// <summary>画面の縁の赤い光（FR-078）。通常の時間で進む。</summary>
        public const float EdgeFlash = 0.45f;
        public const float VictoryBand = 1.2f;
        public const float HitFlash = 0.12f;
    }
}
