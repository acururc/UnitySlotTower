# tasks: 音（spec 014）

- [x] T1701 (FR-005, FR-006) Core: `SoundId`・`BgmTrack`・`SoundSettings`・`VoiceLimiter` と EditMode テスト — 検証: EditMode 全件。2026-11-27 620/620（新規 `SoundSettingsTests` 5・`VoiceLimiterTests` 5）
- [x] T1702 (FR-004〜FR-007) Runtime: `SoundCatalog`・`GameAudio`・`PlayerPrefsSoundStore`・`UiButtonSounds`、未割り当てで鳴らさずエラーなしの EditMode テスト — 検証: EditMode 全件。2026-11-27 620/620（新規 `GameAudioTests` 5）
- [x] T1703 (FR-001〜FR-005) つなぎ: `SlotCabinet` のイベント追加（`BetChanged`・`LeverRefused`・`ReelStopped`・`Settled`）、`BattleSound`、配置・拒否（`UnitPlacementInput`・`UnitBarIcon`）、一時停止（`BattleSound` が `IsPaused` を見る）、タイトルの BGM — 検証: コンパイル成功、EditMode 全件
- [x] T1704 (FR-001〜FR-004, FR-007) 素材のコピーと取り込み設定、`SoundSetup`（カタログ・シーン配置）、`docs/ASSET_LICENSES.md` — 検証: `Setup/Sound (spec 014)` 実行、カタログ 効果音26件（クリップ34）・BGM 3件、Game に Game Audio・UI Button Sounds・Battle Sound・Sound Volume
- [x] T1705 (FR-006) 音量パネル（タイトル・一時停止中） — 検証: Setup 実行で両シーンに配置（位置は仮置き）。見た目は T1707
- [x] T1706 (AC-002) PlayMode `SoundEventTests` — 検証: 2026-11-27 PlayMode 成功75/失敗0/スキップ1（Explicit）、ハーネス 8/8
- [x] T1707 (AC-003) ユーザーが Editor で通しで聞いて確認、音の差し替え — 2026-11-27 テストプレイで確認、指摘は spec 015
