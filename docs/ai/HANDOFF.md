# Resume state

## 現在の作業

更新: 2026-11-16 Asia/Tokyo / 状態: T359 完了。**T355（通しプレイ）: ユーザーが設定1で1回プレイ → 小さい修正を完了・コミット（`418bc10`）**。次は通しプレイの指摘のうち仕様の変更を `spec` で書く。

- T355 の記録と指摘・決定・調整: [`playtest-t355.md`](../../specs/001-slot-tower-defense/playtest-t355.md)（ユーザーのメモを種類別に転記済み。元の test.txt は削除済み）。設定6は未プレイ（ユーザー「必要を感じない」）。文字あり版は未確認（私の説明の誤り。文字なし版ではロゴ右下の隅の見えないボタン）→ 次のプレイで見てもらう。
- ユーザー決定（2026-11-16）: 小さい物を先に、仕様の変更は spec にまとめて承認後に実装。ルートは「3列で均等、全体を見せる」。
- 済んだ小修正: Run In Background オン、職アイコンに待ち時間の残り秒数と押せない時の赤い光り（`UnitBarIcon`、実行時に数字を作る）、first_stop_seconds 0.6・rest_heal 5（仮値）。秒数の見た目は画像で未確認。
- 次の一手: playtest-t355.md の「気づいたこと」から残り（お金とコインの役割の入れ替え〔召喚はお金・最安 100 円基準、両替 10 円=1 コイン、スロット1回2枚〕、1マス1ウェーブ、準備フェーズ廃止と開始演出、敵を群れで・地上敵を増やす、最初は中央レーンのみ、遠距離の攻撃の優先順位、範囲はクリック時のみ、残りの敵数、倍速、揃った時の演出、ルートの遷移アニメ、リールを最初から回す・選んだリールから止める、3列均等のルート・同じステージの連続を避ける・戦闘回数の救済、タンクだけが止める説明、スロットを回す回数を増やす、カメラを寄せる）を `spec` スキルで spec 変更（D-77 以降）にまとめ、曖昧な所はユーザーに聞いて承認を取る。
- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。
- 承認: 2026-11-15 ユーザーが spec 全体を明示承認（sha256 2b639e0feca9）。新しい変更は未承認。T357 のカメラ 55° はユーザー確定（寄せるのは俯角でなく距離の話として spec で確かめる）。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（デザイナー制作依頼書 pptx・メモ txt・スクリーンショット png）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 文字あり版の仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに両方の版の値）、値は `Editor/Setup/TextModeLayoutSetup`、ラベルは `TextModeSetup`（T352）。読みやすさの判定は `Tests/PlayMode/Flow/TextReadability.cs`（36 以上・1440×1920 内・省略なし・同じ親の中で重ならない）を `TextModeTests.Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart` が全9画面で使う。文言や配置を変えたらこのテストと撮影で確かめる。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。最新は `t359/20261115-235209/`、文字なし版の基準は `t359/before/`。撮影の前にシーンが dirty でないか確かめる（「Scene(s) Have Been Modified」ダイアログで止まった前例）。title 直後の NullReference で落ちることがある（原因未特定、再実行で成功）。完了待ちでフォルダを数えるときは `before` を除く。
- 検証（418bc10 後）: EditMode 554/554、PlayMode 62 件で 61 成功＋撮影1件スキップ（Explicit）、ハーネス 8/8。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・columns 7・ショップの矢印は Claude の仮置き。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→code execute でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。`tests --async` が「tests did not start within timeout」で失敗しても Play が遅れて始まりテストが走っていることがある（2026-11-16）。`isPlaying` が false に戻るのを待ってから再送する（二重実行しない）。Bash の `python` は動かない。ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
