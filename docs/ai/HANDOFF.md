# Resume state

## 現在の作業

更新: 2026-11-04 Asia/Tokyo / 状態: M0・M1・M2 完了、M3 の T301〜T308 完了（T309 は後回し）、M4 の T401・T402 完了。2026-11-01 の仕様変更（spec 再承認、plan 13章、tasks の M3R）を M4 の残りより先に反映中。M3R の T310〜T318 完了。M4 の T403 は実装と自動検証まで済み、ユーザーの手動プレイと第三者確認待ち（未完了 `[ ]` のまま）。T404 完了（2026-11-04、`af4aad0`）。2026-11-04 にユーザー依頼の仕様変更（リールのカスタムをロック6マス＋ドラッグ＆ドロップに、カスタムは開始時だけ、有効ライン3本: spec の D-50〜D-53 と【2026-11-04 変更】の項目）を spec.md に反映し、2026-11-04 にユーザーが承認（マス数は 18、6図柄×2 をロック: plan 5章・14章）。tasks の M3S（T320〜T324）を T405 より先に実装する。次は T320

- T403（2026-11-04）: 結果と未確認の項目は tasks.md の T403 行、進め方は plan 8章。ユーザーに頼んだこと: 手動プレイで説明の流れ（カスタム→召喚・配置・向き→スロット→ボーナス→強化→W2 準備で撤退）と実画面の暗幕の濃さを確かめ、第三者確認（AC-003）を行う。結果が出たら T403 を `[x]` にする。PlayMode テストで説明を出したいときは SetUp で `GuideOverlay.DisabledForTests = false`、TearDown で true に戻す（`GuideTests` 参照）。
- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-11-01 再承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（完了タスクは `[x]` と行末に結果・API）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M3 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る（T108 で先バレ対象を BIG・REG に変えたときは承認済み）。T208 は 2026-10-30 にユーザーが手動確認済み。
- ブランチ: `feature/001-t402-result`（M3R もここで進めている。main に直接コミットしない）。
- 画面比: 3:4（基準 1440×1920、初期ウィンドウ 960×1280。plan 2章）。プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、Editor 外の既存問題で未調査）。
- 次の一手: T405（キーボードだけ・マウスだけで1ウェーブ通す確認と不足操作の補完、tasks.md の T405 行）。T404 の結果と未確認の項目は tasks.md の T404 行（PlayMode 24/24、EditMode 403/403、ハーネス 8/8、画像 `t404-help-5.png`・`t404-paused.png`）。早見表を作り直すときは Game シーンの `Help` を消して Editor「Build Help Screen (T404)」を再実行する。Editor の Play 中に画像を撮るときは code execute で `Application.runInBackground = true` にしないとフレームが進まない（撮ったら false に戻す）。T402 の結果は tasks.md の T402 行（2026-11-04: PlayMode 21/21、EditMode 383/383、ハーネス 8/8、画像 `t402-gameover.png`・`t402-clear-2.png`。未確認: 手動プレイ、マウスでのボタン押下）。リザルトを開いた状態の画像は Play 中に code execute で `ResultScreen` の非公開 `Open(battle.Run)` を呼べば撮れる。ゲームを早く終わらせる PlayMode テストは `ResultFlowTests` の短い waves 表を参照。T318 の結果は tasks.md の T318 行（2026-11-04: EditMode 383/383、PlayMode 19/19、ハーネス 8/8）。T317 の結果は tasks.md の T317 行（検証 2026-11-04: EditMode 376/376、PlayMode 19/19、ハーネス 8/8、画像 `t317-battle.png`・`t317-reel-custom.png`。未確認: 戦闘中の敵・味方の大きさ、Console エラー、手動プレイ。フィールドの上下に余白が残り、マップを広げるかは T309 で判断）。シーン配置を変えるときは Editor メニュー「Apply 6:4 Layout (T317)」の値を直して再実行すればよい。PlayMode で「数フレーム後に tick が進む」を確かめるときは、フレームが速いと実時間が足りないので `WaitForSeconds` で待つ。T316 の未確認: 条件違反の赤い点滅、実画面での暗幕の透け具合〔Editor 画像ではかなり透けて見えた〕。T316 で決めたこと: 表示の%に小数点が要るため許可文字と数字フォントに「.」を追加（plan 8章）。カスタム画面のキーボードは EventSystem のナビゲーションではなく既存の `Cursor`/`Confirm`/`Lever` アクション（plan 13章を更新）。Game シーンの PlayMode テストは開始時にカスタム画面が開くので、SetUp で Run ができた後に `ReelCustomTestUtil.CloseWithDefault(_battle)` を呼ぶ。スロットの結果を固定したい PlayMode テストは、シーン読み込み前に `SlotCabinet.RandomOverride` を設定し TearDown で null に戻す（`SlotCabinetEffectTests` 参照）。ステージ切り替えを早く起こすには、Start 前（`sceneLoaded`）に `BattleController.wavesTable` を短い表に差し替える（`StageTransitionTests` 参照）。T401 で決めたこと: 難易度画面の初期選択は常に設定1、前回の設定は覚えない。T309（5ウェーブ通しの手動プレイとCSV調整）は 2026-10-31 にユーザーが「バランスはまた後で調整する」として後回し（未完了のまま残す）。T309 で確認したい点: ウェーブの長さ（各約1分で plan の目安より短い）、ボスの見た目と範囲攻撃の演出（今はなし）、W3〜W5 の見た目、タンクの色（青灰）が高台マスと見分けにくい件、操作バーの強化欄（数字と段階の点が小さい）。
- T308 で決めたこと（plan 3章に ▶ のキーがなかったため）: ▶ は「配置操作中でないときの Enter」と操作バーの ▶。撤退は配置中の味方をクリック／Tab で選び X か撤退アイコン。準備中は `BattleController.autoStart`=false で ▶ を待つ（PlayMode テストは `StartWave()` を呼んでから戦闘を確かめる）。
- M2 の要点: Core/Battle に地図・味方/敵の表・召喚・配置可否と範囲・ウェーブ・ブロック・攻撃/回復/強化・ライフとドロップ・配置操作（`UnitDeployment`）。Runtime/Battle に `BattleFieldView`・`BattleController`（W1〜W5、`StartWave`/`TogglePause`）・`UnitPlacementInput`（選択・配置・強化・撤退・▶・一時停止の入力、`SelectedUnit`）/`UnitPlacementView`・`BattleActorsView`・操作バー（`UnitBarIcon`・`UnitUpgradeIcon`・`OperationBarStatus`）。シーンへの追加は Editor `Setup/*Setup`（実行済み）。
- 検証（T401 時点、旧スロット）: EditMode 388/388、PlayMode 14/14、Console エラー0、ハーネス 8/8、Editor 画像 `Artifacts/Automation/Editor/t401-title.png`・`t401-difficulty.png` を目視。手動プレイは未実施（T309）。
### M1 の成果（旧スロット。T313 で Core と CSV を置き換え済み、以下は経緯のみ）
- Core/Slot: 表（SlotReels・SlotPayouts・SlotRules・SlotProbabilities・SlotPremonition）、SlotLines、SlotReelControl（フラグ＋持ち越しごとに 23^3 状態をメモ化評価）、SlotMachine（持ち越し・ボーナス上限・確変・先バレ）、SlotTheory、SlotWait。Core/Economy/CoinWallet。
- Runtime/Slot: SlotCabinet（CSV を TextAsset で読む。`setting` 既定1）、SlotReelView（`symbolsPerSecond` 16。ユーザー確認で 28 から変更）。Editor: `Setup/SlotCabinetSetup`、`Slot/SlotTheoryReport`。
- 理論機械割（適当打ち、ユーザー決定）: 100.04〜125.09%。ルール値は `slot_rules.csv`（BIG 240・REG 96・確変100G・突入率1・ウェイト2秒・最大4コマ滑り）。
- 最終検証（2026-10-30）: EditMode 207/207、Console エラー0、ハーネス 8/8、ユーザーの手動目押しと先バレの光・音を確認。PlayMode テストはまだ0件。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉、2026-11-04 T403 後）。Editor の camera screenshot は半透明の UI（暗幕など）が薄く写る。濃さは画像で判断しない。テスト実行スクリプトは refresh 後の Console に `error CS` があれば止める（古いアセンブリのままテストが流れて全件成功に見えるため）。Editor は開いたまま。テスト実行は refresh → 25秒待つ → Console → tests --async → poll-test のループを1スクリプトにまとめる（結果は `progress.failures_so_far` と `result.summary`）。`editor refresh` は成功しても exit 1 を返す。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 実機デバイスはゲームパッドも含めて SetUp で全部止める（DualShock4 が接続されていることがある）。仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
