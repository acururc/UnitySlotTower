# Resume state

## 現在の作業

更新: 2026-11-17 Asia/Tokyo / 状態: **M3V（spec D-77〜D-91、2026-11-16 承認 sha256 `eb605bccac38`）の実装中。T360〜T370 完了（tasks.md に詳細）。** 次は T371（掃除と文書）。
- T370: 文字あり版に残りの敵・速さ・戦闘開始のラベル、¥ の両替文、2枚で1回などの文言を反映（tasks.md）。`guide_routelever`・Core の `GuideHint.RouteLever`・`Battlefield.BeginWave` は T371 で片付ける。
- T369 の Claude の仮置き（plan 17.10 に記録）: 足止め・マスの中身はどちらも画面を押すと進む（0.6 秒後から）、足止めは最初の戦闘に限らず初めて止められたとき、絵（`GuidePictures` が実行時に作る）の形・色・位置。
- T368 のユーザー決定（2026-11-17）: 戦闘：下部は 5:5、左右の余白なしでもっと寄せる、大きくなった下部は札とリールを大きく（spec D-87・plan 17.9 に記録）。Claude の仮置き: 筐体の中の配置（`GameSceneSetup.ReelWindowPx` など、リール窓 1016×500）、文字あり版の筐体のラベルを窓の下の段へ、空中の敵は手前の角で左右がわずかに切れてよい。割合は Core の `PortraitLayout`（`BattleHeightRatio`・`SlotAreaTop`）の1か所。文字あり版の帯の倍速ボタン「×1」が大きすぎた件は T370 で直した。
- T367 の Claude の仮置き: 印の色と大きさ、着いてからの待ち 0.4 秒。plan 17.8 の「マップを動かす」は全階が1画面に入るので省いた（plan を書き換え済み）。Core の `GuideHint.RouteLever` は使われなくなった（T371 で片付ける）。
- T366 の Claude の仮置き: 光の色・不透明度・長さ、玉の大きさと弧。効果（ゲージ・倍率）は揃った時点で入り、演出は見た目だけ。
- T362 の Claude の仮置き: 群れの構成と数、money_rate 0.28。T375 の通しプレイで調整。
- T361 の Claude の仮置き: 演出の残りステップは戦闘に持ち越す、撤退の説明は2つ目の戦闘マスでレバーか次の召喚で終わる、光の粒の色と大きさ。`Battlefield.BeginWave` と ui_texts.csv の `bar_wave`・`bar_start` は未使用のまま残っている（T370・T371 で片付ける）。

- T355 の記録: [`playtest-t355.md`](../../specs/001-slot-tower-defense/playtest-t355.md)。T355 は完了扱い（設定6はユーザーが不要と判断）。文字あり版の確認は T375（文字なし版ではロゴ右下の隅の見えないボタンで切り替え）。
- 承認の範囲: spec 全体（D-01〜D-91、FR-001〜FR-064、AC-001〜AC-051、D-77〜D-91 の Claude 提案を含む）。plan 17章の数値（円は約8倍、bet 2、両替 10、1戦闘12回以上、intro 2秒など）は Claude の仮置きで承認の対象外。T368 の 5:5 はユーザー決定。
- T365 の Claude の仮置き: 残りの敵数（上の段の右、敵の顔）と倍速ボタン（下の段の左「×1」）の位置と絵、一時停止中は倍速を変えられない。倍速は `BattleController.BattleSpeed`（plan 17.5 を書き換え済み）。`Time.timeScale` は `BattleController.ApplyTimeScale` だけが書く。文字あり版のラベル（「残りの敵」「速さ」）は T370 で追加。
- 次の一手: tasks.md の M3V を T371 から順に（順序は plan 17.11）。Core のタスクは `tdd` スキル、終わるごとに `unity-verify` で検証して tasks.md に結果を書く。
- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。
- T357 のカメラ 55° はユーザー確定（D-87 で寄せるのは距離・余白）。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（デザイナー制作依頼書 pptx・メモ txt・スクリーンショット png）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 文字あり版の仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに両方の版の値）、値は `Editor/Setup/TextModeLayoutSetup`、ラベルは `TextModeSetup`（T352）。読みやすさの判定は `Tests/PlayMode/Flow/TextReadability.cs`（36 以上・1440×1920 内・省略なし・同じ親の中で重ならない）を `TextModeTests.Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart` が全9画面で使う。文言や配置を変えたらこのテストと撮影で確かめる。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。最新は `t359/20261117-023002/`（5:5）、文字なし版の基準は `t359/before/`。マップごとの撮影は Play 中に code execute で `BattleFieldView` の `LoadMap`→`Show` をリフレクションで呼び `Time.timeScale = 0` にしてから（T368、`t368/half-*.png`）。メニューを code execute の `EditorApplication.ExecuteMenuItem("…")` で呼ぶときは PowerShell のエスケープで引用符が崩れやすい（T368 で1回失敗、シーン未変更を確かめて再送）。撮影の前にシーンが dirty でないか確かめる（「Scene(s) Have Been Modified」ダイアログで止まった前例）。title 直後の NullReference で落ちることがある（原因未特定、再実行で成功）。完了待ちでフォルダを数えるときは `before` を除く。
- 検証: T370 で EditMode 585/585、PlayMode 72 件で 71 成功＋撮影1件スキップ、ハーネス 8/8、撮影 `t359/20261117-031019/`。T369 で EditMode 585/585、PlayMode 72 件で 71 成功＋撮影1件スキップ（Explicit）、ハーネス 8/8。説明の撮影は Play 中に code execute で `GuideOverlay` を止めて敵を出し、敵の道にタンクを置いてから戻し、`SlotCabinet` の private `OnLever` を呼ぶと足止めが出る（T369、`t369/`）。PlayMode 全件は非フォーカスで約4〜10分かかり、poll ループが切れたら同じ job_id で poll し直す。ルート画面を Play 中に開くには code execute で `RunController` の private `OnNodeCompleted` を呼ぶ（移動を止めて撮るなら `RouteScreen` の `moveSeconds` を大きくする）。ルートのリールが回り出すのを待つのは `RouteTestUtil.WaitForRouteSpin`。演出の途中を撮るには Play 中に code execute で `SlotAlignEffect` の private `OnAligned` をリフレクションで呼び、`Time.timeScale = 0.05` にしてから約5秒後に `camera screenshot --super-size 3`。T362 時点で群れのフレーム時間 平均 4.2 ms・最大 7.0 ms。Console（`editor console`）と Editor.log からテストの Debug.Log が読めないため、測定値はテストが `Artifacts/` にファイルで残す。CLI の `editor tests` にテスト名の絞り込みはない（全件で約3分）。scratchpad の poll ループは PlayMode 全件の途中で切れることがあり、同じ job_id で poll し直す。PlayMode でゲーム開始直後に操作するテストは `RouteTestUtil.WaitForIntro` で演出の終わりを待つ。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・ショップの矢印は Claude の仮置き。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。Unity 操作用の scratchpad スクリプト（tests.ps1 など）は会話ごとに作り直す。PowerShell で関数名 `R` は Invoke-History の別名と衝突する。Play 中の操作は code execute で型を名前（`GetType().Name`）で探してリフレクションで呼ぶ。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→code execute でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。`tests --async` が「tests did not start within timeout」で失敗しても Play が遅れて始まりテストが走っていることがある（2026-11-16）。`isPlaying` が false に戻るのを待ってから再送する（二重実行しない）。`code execute` のコードは位置引数（`--code` はない）。Bash の `python` と heredoc は動かないので、編集スクリプトは Write で scratchpad に置いて `node` で実行する。ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
