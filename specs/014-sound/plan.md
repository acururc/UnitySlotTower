# plan: 音（spec 014）

## 方針

- 出来事 → 音の対応は Runtime の `SoundCatalog`（ScriptableObject）1つにまとめる（FR-007）。コードは `SoundId`（列挙）で鳴らすだけ。
- 鳴らす側は1つの `GameAudio`（シーンをまたいで残る）。BGM はフェードで切り替え、効果音は AudioSource の小さなプールで鳴らす。同時発音の上限と音量の値は Core に置き、EditMode でテストする。
- 戦闘とスロットの出来事は、既存のイベント（`Battlefield`/`BattleController`/`SlotCabinet`/`BattleActorsView.Impact`）を `BattleSound` が購読して鳴らす。既存の表示クラスには手を入れず、足りないイベントだけ `SlotCabinet` に足す。
- 素材は `Artifacts/audio-src/`（git 外）から選んだ ogg だけを `Assets/UnitySlotTower/Audio/` にコピーする。

## 構成

| 層 | ファイル | 役割 |
| --- | --- | --- |
| Core | `Core/Audio/SoundId.cs` | 効果音の列挙（FR-001〜FR-003）と `BgmTrack`（None/Title/Battle/Boss） |
| Core | `Core/Audio/SoundSettings.cs` | BGM・効果音の音量（0〜1 に丸める、初期値 0.7/0.8）、`ISoundSettingsStore` への保存と読み込み（FR-006） |
| Core | `Core/Audio/VoiceLimiter.cs` | id ごとに鳴っている数を終了時刻で数え、上限を超える要求を断る（FR-005） |
| Runtime | `Runtime/Audio/SoundCatalog.cs` | `SoundId` → クリップ（複数なら順に選ぶ）・音量・同時数上限、`BgmTrack` → クリップ・音量。割り当てがなければ false（FR-007） |
| Runtime | `Runtime/Audio/GameAudio.cs` | 1つだけ（2つ目は自分を消す）、`DontDestroyOnLoad`。BGM 2系統でフェード（0.5秒）、同じ曲なら続ける。効果音のプール、ループ1本（リール回転）。一時停止中は効果音を止め、ループも止める。`Requested` イベント（テスト用）。静的 `GameAudio.Play(id)` は無ければ何もしない |
| Runtime | `Runtime/Audio/PlayerPrefsSoundStore.cs` | `ISoundSettingsStore` の PlayerPrefs 実装 |
| Runtime | `Runtime/Audio/UiButtonSounds.cs` | `Selectable.AllSelectablesNoAlloc` で Button を拾い（0.5秒ごと、新しいものだけ）、押したらボタン音。スロット台の中のボタンと味方の札は除く（自前の音を出す） |
| Runtime | `Runtime/Audio/BattleSound.cs` | Game シーン。下の表の出来事を鳴らし、ステージ開始で BGM（`BossId` があれば Boss）、勝利・敗北で BGM を止めてジングル |
| Runtime | `Runtime/Audio/SoundVolumePanel.cs` | スライダー2本をつなぐ。値は即反映して保存、効果音のスライダーを離したら確認音 |
| Editor | `Editor/Setup/SoundSetup.cs` | メニュー `Setup Sound (spec 014)`: カタログのアセットを作って割り当て、両シーンに `GameAudio`・`UiButtonSounds`、Game に `BattleSound`、タイトルと一時停止中に音量パネルを置く |

## 出来事のつなぎ先

| 出来事 | 拾う場所 |
| --- | --- |
| 賭け・レバー・リール停止・ハズレ・ライン成立・お金不足のレバー | `SlotCabinet` に `BetChanged`・`ReelStopped(int)`・`Settled(int hits)`・`LeverRefused` を追加。既存 `LeverPulled`・`Aligned` |
| リール回転のループ | `LeverPulled` で開始、最後の `ReelStopped` で停止 |
| 赤7/青7/緑7/黄7/BAR/コインの発動 | `BattleController` の `RedStruck`/`BlueFroze`/`GreenHealed`/`YellowBurst`/`BarBurst`/`CoinGained` |
| 剣・弓の攻撃 | `BattleController.UnitAttacked`（職 id が sword / archer） |
| 敵への命中 | 剣は攻撃と同時、弓は `BattleActorsView.Impact(…, arrow: true)` |
| 敵の撃破・ボスの撃破・コインの獲得 | `BattleController.EnemyDefeated`（`IsBoss`、コイン > 0） |
| 本拠地への被弾 | `Field.BaseReached` |
| ウェーブ開始・ボスの登場 | `Run.Waves.StartedWaves` の増加、`Field.Enemies` に `IsBoss` が現れた（毎フレームの比較、割り当てなし） |
| 勝利・敗北 | `IsShowingVictory` の立ち上がり、`Run.Phase == GameOver` の立ち上がり |
| 配置・拒否 | `UnitPlacementInput` の `Confirm()` 成功、`UnitBarIcon` の失敗（お金不足・上限） |
| 一時停止 | `BattleController.SetPaused` → `GameAudio.SetSePaused` |
| タイトルの BGM | `TitleScreen` の Start |

## 素材（すべて CC0）

Kenney: Interface Sounds・Impact Sounds・RPG Audio・Casino Audio・Digital Audio・Sci-fi Sounds・Music Jingles。BGM: Juhani Junkala「Chiptune Adventures」（OpenGameArt）の Stage Select（タイトル）・Stage 1（戦闘）・Boss Fight（ボス）。割り当ては `SoundSetup` の表が正本。BGM の取り込みは Vorbis・Streaming、効果音は Decompress On Load。

## リスク

- 名前だけで選んだ音は合わないことがある → AC-003 で差し替え（`SoundSetup` の表を直して再実行するか、カタログを直接編集）。
- Setup を再実行するとシーンの差分が大きく見える（既知）。
- PlayMode テストで音が鳴る: テストは `Requested` を数えるだけ。音量は既存の PlayerPrefs の値に従う。
- 0.5秒ごとの `Selectable` 走査: 割り当てなし（配列は使い回し）。

## 検証

AC-001: EditMode（`SoundSettingsTests`・`VoiceLimiterTests`・カタログ未割り当て）。AC-002: PlayMode（`SoundEventTests`）。AC-003: ユーザーの通し確認。各段階でハーネス。
