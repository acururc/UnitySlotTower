# Resume state

## 現在の作業

更新: 2026-10-30 Asia/Tokyo / 状態: M0 完了（25cac48）。M1 の T101〜T109 完了。T110 は実装済みでユーザーの手動確認待ち

### M1 の進み（2026-10-29〜30）
- T110: `Core/Slot/SlotWait.cs`（前のレバーオンから待ち、待ち中のレバーは予約）、`Runtime/Slot/SlotCabinet.cs`（CSV を TextAsset で読み SlotMachine を組む。Space/J/K/L とボタン、ランプ4つ、数字3つ、先バレは仮チャイム）、`Runtime/Slot/SlotReelView.cs`（5コマで下向き回転、押し位置＝中段へ下りてくる図柄）、`Editor/Setup/SlotCabinetSetup.cs`（一回限りのメニューで Slot Area に組み、Battle Camera に AudioListener 追加）。図柄は仮の形（7 だけ数字フォント）。設定は SlotCabinet の `setting`（既定1）。plan 5章にウェイトと筐体の解釈を追記。
- 検証（T110）: red 5件→green、EditMode 207/207（筐体を組んだあとも）、ハーネス 8/8、Editor 画像 `Artifacts/Automation/Editor/t110-idle.png`・`t110-spin-2.png`・`t110-stop-2.png`（ハズレ2回で 30→24）。Unity 接続は停止済み（8080 閉）。
- 未確認（T110）: ユーザーの手動目押し（Space→J/K/L）、先バレの光と音、最初のフラグごとの制御表作成で一瞬止まらないか。`NumericOnly SDF.asset` の _ScaleRatio 変化は TMP が Bold 用に自動で書いたもの。
- 注意: Editor が非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）にする。`code execute` は `Object` が曖昧なので `UnityEngine.Object` と書く。CLI はテスト失敗や poll 中に exit 1 を返すので try で包む。
- 次の一手: ユーザーが Play で目押しを確認 → T110 を `[x]` にして M1 完了 → M2（T201）。
- T109: `Core/Slot/SlotTheory.cs`（`Calculate`・`CalculateAllSettings`、`SlotPlayStyle.Random/Perfect`、`TargetPayoutRates`）。ユーザー決定（2026-10-30）: 機械割の目標は適当打ち基準（plan 5章に計算方法と併せて記録）。`Editor/Slot/SlotTheoryReport.cs` のメニューは Console と `Artifacts/Slot/slot-theory.txt` に出す。`slot_probabilities.csv` のベル（通常・確変）を設定別 1/4.51〜1/3.33 に調整。理論値（適当打ち）100.04/104.04/107.96/112.06/118.08/125.09%、完全攻略の参考値 104.86〜131.29%。
- 検証（T109）: red（新規5件 NotImplemented）→green、`SlotSimulationTests` 100万G×6設定で機械割・BIG/REG 開始率が理論値の標準誤差4倍以内、目標 ±0.5pt。EditMode 202/202（約74秒）、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- 注意（T109）: `editor console` が Debug.Log もエラーも0件しか返さない状態だった（読み取り自体の不調の疑い。コンパイルはメニューの新コードが動いたことで確認）。ログを確認したいときはファイル出力を使う。refresh 直後はコンパイル中でテスト開始が失敗するので20秒ほど待つ。
- T108: `Core/Slot/SlotPremonition.cs`（`Rate(flag)`・`Draw(random, flag)`。表は単独役＋`hazure`（ガセ）、表にない役は0、重複当選は役の最大率で1回、率0なら乱数を使わない）。`SlotMachine` のコンストラクタに `SlotPremonition` を追加（probabilities の次）、`TryLever` で持ち越し処理後のフラグで抽選し `Premonition` に入れる。ユーザー決定（2026-10-29）: 先バレは 7揃いの BIG・REG だけ（spec D-31・FR-044・AC-042 を書き換え、spec 冒頭に変更承認を記録）。`slot_premonition.csv` は big 1/2・reg 1/2・ガセ0（率は仮）。重複当選・持ち越し・ボーナス中の扱いは plan 5章「先バレ」。
- 検証（T108）: red→green 3周（表: 新規6件失敗→175/175、台: 1件失敗→177/177、7揃いのみ: 1件失敗→178/178）、Console エラー0、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- T107: `SlotMachine.KakuhenGamesLeft`。BIG 終了時に `_random.NextDouble() < KakuhenEntryRate` で当たれば `KakuhenGames` をセット。ボーナス終了後は残り>0なら Kakuhen、0なら Normal。確変中に始めたゲームを `Finish` で1減らす（ボーナスを揃えたゲームも数え、ボーナス中は数えない）。ユーザー決定: 確変中 REG は残りゲームで復帰、確変中 BIG は突入を抽選し直す（plan 5章に追記）。テストは `ShortRules(games, rate)`（上限20）と `PlayBonusInKakuhen`。
- 検証（T107）: red→green 2周（BIG 後に確変100G、3G で終了）、ガード3件（突入率0、確変中 BIG で3に戻る、確変中 REG で残り2）は追加時点で green。EditMode 167/167、Console エラー0、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- T106: `SlotMachine` に `SlotRules`。ボーナス中の払い出しを `BonusPayout` に累計し上限で終了、上限またぎは全額払い（plan 5章）。テストヘルパー `BigThenBellMachine`。
- テスト実行のヘルパー: refresh→Console→開始1回→同じ job_id で poll を1スクリプトにまとめると安全（開始の出力を切り捨てて job_id を失わないこと。今回それで開始を二重送信した）。
- T105: `SlotMachine` に `CarriedBonus`（揃わなかった BIG/REG を持ち越し）・`State`（ボーナス役が揃うと Bonus）・`ActiveBonus`。持ち越し中に引いたフラグは `SlotProbabilities.WithoutBonus` でボーナス役を外す（big+watermelon→watermelon、big→ハズレ）。`SlotReelControl` の評価は（ボーナス役の数, ほかの成立役の数）の期待値を辞書式で比べる（ボーナス優先、plan 5章に追記）。`SlotFlag.BigRole/RegRole/IsBonusRole` 追加。
- 検証（T105）: EditMode 157/157、Console エラー0、ハーネス 8/8。
- T104: `Core/Economy/CoinWallet.cs` と `Core/Slot/SlotMachine.cs`（`TryLever()` 掛け金→抽選→制御開始、`Stop` で3本目を止めると揃った役を1回ずつ払い出し、リプレイなら次は掛け金なし）。テストは `SlotMachineTests`（抽選表を「役1つが確率1」の CSV 文字列で渡し、`BonusPresses` で揃う/揃わない押し位置を別の制御で探す）。
- T103: `Core/Slot/SlotLines.cs`（5ライン判定）と `SlotReelControl.cs`（1フラグ＋持ち越しごとに全状態 22^3 を先読み評価してキャッシュ。非成立役は揃えず、リプレイ・ベルは必ず揃える）。
- 注意: `editor refresh` は成功しても CLI が JSON の前に文字を出すため、ラッパーが exit 1 を返す（中身は `success: true`。`try {} catch {}` で包む）。`poll-test --wait` は数秒で戻るので、同じ job_id でループして待つ（再実行しない）。今回は EditMode 全体が約25秒。
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
