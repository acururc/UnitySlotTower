namespace UnitySlotTower.Core.Audio
{
    /// <summary>効果音を鳴らす出来事（spec 014 FR-001〜FR-003）。音の割り当ては Runtime の SoundCatalog。</summary>
    public enum SoundId
    {
        // スロット（FR-001）
        Bet,
        Lever,
        ReelSpin,
        ReelStop,
        LineHit,
        Miss,
        Red7,
        Blue7,
        Green7,
        Yellow7,
        Bar,
        CoinSymbol,

        // 戦闘（FR-002）
        Place,
        SwordAttack,
        BowAttack,
        Hit,
        EnemyDefeat,
        BossDefeat,
        BaseHit,
        WaveStart,
        BossAppear,
        Victory,
        Defeat,

        // UI（FR-003）
        Button,
        Reject,
        CoinGet,

        // spec 015 FR-023: 揃いに重ねる上昇音（既存の番号を変えないよう末尾に足す）。
        LineFanfare,
    }

    /// <summary>BGM（spec 014 FR-004）。</summary>
    public enum BgmTrack
    {
        None,
        Title,
        Battle,
        Boss,
    }
}
