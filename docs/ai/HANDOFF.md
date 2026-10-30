# Resume state

## 現在の作業

更新: 2026-10-30 Asia/Tokyo / 状態: M0・M1・M2 完了、M3 の T301〜T305 完了（財布統合・撤退と返還・強化・ウェーブ進行とストック・W2〜W5 のマップと出現。結果は tasks.md の各行）。次は T306

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（完了タスクは `[x]` と行末に結果。M2 各タスクの成果・API はそこを見る）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M3 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る（T108 で先バレ対象を BIG・REG に変えたときは承認済み）。T208 は 2026-10-30 にユーザーが手動確認済み。
- ブランチ: `feature/001-m0-foundation`（main に直接コミットしない）。
- 画面比: 3:4（基準 1440×1920、初期ウィンドウ 960×1280。plan 2章）。プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、Editor 外の既存問題で未調査）。
- 次の一手: T306（中ボス・最終ボスと最終ボス撃破でのクリア。`enemies.csv` にボスを足し、`waves.csv` の `is_boss`。W1 末に中ボス、W5 末に最終ボス＝plan 7章。クリア判定は今 `WaveRun` の最終ウェーブ終了で `Completed`）。ウェーブごとのマップは T305 で済（`BattleFieldView.LoadWaveMap`/`Show`、`BattleController` が `WaveBegan` で差し替え）。▶ の入力はまだなく `autoStart` で準備を即終了している（▶ は T308 の操作バーと合わせる想定。入れたら `autoStart` を false に）。撤退・強化の入力（撤退アイコン・X、Shift+数字）も T308。
- M2 の要点: Core/Battle に地図・味方/敵の表・召喚・配置可否と範囲・ウェーブ・ブロック・攻撃/回復/強化・ライフとドロップ・配置操作（`UnitDeployment`）。Runtime/Battle に `BattleFieldView`（`CellTopToWorld`・`PositionToWorld`）・`BattleController`（ウェーブ進行の組み立て。W1〜W5）・`UnitPlacementInput`/`UnitPlacementView`・`BattleActorsView`（T209 の味方・敵・HP ゲージ・範囲）。シーンへの追加は Editor `Setup/*Setup`（実行済み）。
- 最終検証（T305）: EditMode 377/377、PlayMode 7/7、Console エラー0、ハーネス 8/8、Play で W1→W2 のマップ切り替えを画像確認。W3〜W5 の見た目・空中の敵の見た目・手動プレイは未確認（T309）。ウェーブの長さは各約1分で plan の目安より短い（T309 で調整）。タンクの色（青灰）が高台マスの色に近く見分けにくい — M3 のプレイ確認で必要なら色を変える（操作バーのアイコン色も揃える）。
### M1 の成果（詳細は tasks.md 各行と plan 5章）
- Core/Slot: 表（SlotReels・SlotPayouts・SlotRules・SlotProbabilities・SlotPremonition）、SlotLines、SlotReelControl（フラグ＋持ち越しごとに 23^3 状態をメモ化評価）、SlotMachine（持ち越し・ボーナス上限・確変・先バレ）、SlotTheory、SlotWait。Core/Economy/CoinWallet。
- Runtime/Slot: SlotCabinet（CSV を TextAsset で読む。`setting` 既定1）、SlotReelView（`symbolsPerSecond` 16。ユーザー確認で 28 から変更）。Editor: `Setup/SlotCabinetSetup`、`Slot/SlotTheoryReport`。
- 理論機械割（適当打ち、ユーザー決定）: 100.04〜125.09%。ルール値は `slot_rules.csv`（BIG 240・REG 96・確変100G・突入率1・ウェイト2秒・最大4コマ滑り）。
- 最終検証（2026-10-30）: EditMode 207/207、Console エラー0、ハーネス 8/8、ユーザーの手動目押しと先バレの光・音を確認。PlayMode テストはまだ0件。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
