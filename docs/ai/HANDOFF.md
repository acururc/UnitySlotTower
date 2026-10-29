# Resume state

## 現在の作業

更新: 2026-10-29 Asia/Tokyo / 状態: M0 完了（25cac48）。M1 の T101〜T104 完了。次は T105

### M1 の進み（2026-10-29）
- T104: `Core/Economy/CoinWallet.cs`（TrySpend・Add。召喚でも共有する想定）と `Core/Slot/SlotMachine.cs`（`TryLever()` が掛け金投入→抽選→`SlotReelControl.Start`、`Stop(reel, press)` で3本目を止めると `SlotLines.Aligned` の役を1回ずつ払い出し、リプレイが揃えば `IsReplay` で次のレバーは掛け金なし）。状態はまだ Normal 固定、持ち越しボーナスは未対応（T105）。テストは `SlotMachineTests`（抽選表を「役1つが確率1」の CSV 文字列で渡す）。
- 検証（T104）: red→green 3周、最終 EditMode 139/139、Console エラー0、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- 注意: Editor が非フォーカスだと EditMode 全体が10分以上かかることがある。`poll-test` は同じ job_id で繰り返し待つ（再実行しない）。コンパイル直後の最初の MCP 呼び出しはドメインリロードで切断されることがある（読み取り系は再試行してよい）。
- 次の一手: T105（BIG/REG フラグの持ち越しと、揃えたときのボーナス開始）を `tdd` で。`SlotMachine` に持ち越し役を持たせ `SlotReelControl.Start(flag, carriedBonus)` に渡す。
- T103: `Core/Slot/SlotLines.cs`（5ラインの揃い判定。cherry は左リール上下段のラインのみ、center_cherry は中段のみ）と `Core/Slot/SlotReelControl.cs`（`Start(flag, carriedBonus)` → `SlotSpin.Stop(reel, press)` が停止位置を返す）。制御は1フラグ（＋持ち越し）ごとに全状態 22^3 を先読み評価してキャッシュ: 非成立役が揃う出目・必ず揃える役（全押し方で揃えられる最大の組＝リプレイ・ベル）の取りこぼしを禁止し、残りの押し方を一様とみなした「揃う成立役の数」の期待値が最大の滑りを選ぶ（同点は滑りが少ない方）。ボーナスと小役の優先度はまだ同じ重み（T105 で決める）。
- 検証（T103）: `SlotReelControlTests` red→green 2周、EditMode 133/133、Console エラー0、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- 注意: `editor refresh` は成功しても CLI が JSON の前に文字を出すため、ラッパーが exit 1 を返す（中身は `success: true`）。
- T101: `Core/Slot/`（SlotSymbol・SlotReels・SlotPayouts・SlotRules）と `Tests/EditMode/Slot/SlotTableTests.cs`。`slot_reels.csv`・`slot_payouts.csv` に中身を入れた（`slot_premonition.csv` はまだ見出しのみ）。
- T102: `Core/Slot/SlotProbabilities.cs`（SlotState Normal/Kakuhen/Bonus、SlotFlag、`Draw(IRandom, state, setting)` は乱数1回）と `Tests/EditMode/Slot/SlotLotteryTests.cs`。重複当選は role 列で `big+center_cherry` のようにつなぐ（plan 5章に追記）。各行は排他、残りがハズレ（Flags[0]）。表検証は `TableValidator.ValidateListReference` で `+` を分けて参照確認。`slot_probabilities.csv` の値は仮（T109 で機械割に合わせて調整）。ボーナス中は big/reg なし。
- 検証（T102）: EditMode red 15件（新規14＋表検証）→ green 118/118、Console エラー0、ハーネス 8/8。
- ユーザー決定（2026-10-29）: レア役（スイカ・角チェリー・中段チェリー）は取りこぼしあり。リプレイ・ベル・ハズレは必ず一致。plan 5章「リール制御」「払い出し」に反映済み。
- リール配列の前提（T103 で守る）: 停止位置 s で上段 s-1・中段 s・下段 s+1。押した位置 p から p, p-1, …, p-4 に滑る（下向き回転）。この前提で、全押し順×全押し位置でリプレイ・ベル・ハズレが実現でき、BIG/REG は先読みあり制御で約16%/23%、スイカ/角チェ/中段チェは約47%/86%/71% の押し位置で揃う（使い捨て node スクリプトでの総当たり。プロジェクト内のテストではない）。
- 払い出しは1ゲーム1回。cherry と center_cherry は同じ図柄（cherry,any,any）で、左リールの段で区別する。初期値 replay0/bell8/watermelon6/cherry2/center_cherry4/big0/reg0（T109 で調整）。
- 検証（T101）: EditMode 104/104、Console エラー0、ハーネス 8/8。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（M0 は `[x]` と各行末に結果）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M1 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る。
- 完成物（`Assets/UnitySlotTower/`）: Core（`Data/` CSV パーサー・KeyValueTable・GameConfig・TableValidator・GameDataValidator、`Simulation/` SimulationClock・SeededRandom(SplitMix64)・IRandom、`Layout/PortraitLayout`、`Ui/NumericText`）、Runtime（`PresentationCatalog`、`PortraitScreenLayout`）、Editor（`Setup/GameSceneSetup`・`NumericFontSetup` の一回限りメニュー、`Ui/TextAudit`）、`Data/Tables/` CSV 雛形17本（game_config・slot_rules は plan の値、他は見出しのみ、maps/w1〜w5 は同じ仮グリッド）、`Scenes/Game.unity`、`Art/Fonts/NumericOnly SDF.asset`。ほかに `Assets/TextMesh Pro/`（TMP Essentials）、`InputSystem_Actions` に SlotTower マップ、プレイヤー設定 720×1280 可変ウィンドウ、ビルド設定は Game のみ有効（SampleScene は無効化して残置）。
- 補助スクリプト: `Test-UnityPlayer.ps1` に `-Width/-Height`（既定 1280×720）、`Test-ProjectHarness.ps1` の空白検査から `Assets/TextMesh Pro/` を除外（ベンダーのシェーダーに行末空白）。
- URP 資産・GraphicsSettings の差分はビルド時に Unity が自動で書いたもの（意図した変更ではない）。
- 検証（実行済み）: EditMode 95/95 green（各 Core 機能は red 確認後に green）、Console エラー0、Windows 開発ビルド成功、プレイヤー画像 720×1080/1920×1080/2560×1080 で 9:16 中央・黒帯・上38%戦闘を目視確認、ハーネス 8/8。未実行: PlayMode テスト（まだ0件）、真の 1440p 表示（モニターが 1080p。計算は単体テストで確認）。
- 留意点: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので、Runtime 側で Shift 押下中は SelectUnit を無視する。Esc は Cancel と Pause の両方で、配置操作中かどうかで振り分ける（plan 3章）。マップのマス記号は G/H/X/E/S1〜S4、経路は上下左右の連続のみ検証（範囲・通行可否の検証は T201）。
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- テスト手順:  refresh → Console 確認 → `editor tests --async` を1回だけ → `poll-test` の順で、テスト開始を二重に送らない。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
