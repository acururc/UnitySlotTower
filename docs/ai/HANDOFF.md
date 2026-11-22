# Resume state

## 現在の作業

更新: 2026-11-23 Asia/Tokyo / 状態: **spec 002 の実装が完了（T601〜T617 すべて済み）。次の作業は未決定で、ユーザーに選んでもらう。**

- 対象: [`spec.md`](../../specs/002-td-focus/spec.md)・[`plan.md`](../../specs/002-td-focus/plan.md)・[`tasks.md`](../../specs/002-td-focus/tasks.md)。001 の spec は土台で、食い違いは 002 が優先。
- T617 の結果（ユーザーが全ステージをクリアし、了承済み）: 近距離の敵 HP6、剣士 HP20、雑魚の所持金50、ステージ1のウェーブ4は6体、開始時の所持金100・無料の召喚0（`game_config.csv` の `first_stage_free_summons`）、強化費用 400/700/1100/1600、`stages.csv` の敵 HP 倍率 `enemy_hp_rate`（1/1/1.3/2/2.2/2.6/3/3.5、強化しないとステージ4のボスから厳しくなる狙い）。spec の E-05・E-07・AC-105 と plan の 2.4・4章に記録。コミット `241c2a4`・`3e7321b`（main、push 済み）。倍率を入れた後の通しプレイはまだ。
- 検証（2026-11-23 最後の実行）: EditMode 545/545、PlayMode 68 成功/失敗0/Explicit 1、ハーネス 8/8。
- 次の候補（ユーザーが決める）: (1) 001 の M5（[001 の tasks](../../specs/001-slot-tower-defense/tasks.md) の T501〜T505: 素材・グレーボックスの差し替え・最終調整・ビルド。002 で前提が変わっているので見直しが要る。T501 のデザインの方向は未決定）、(2) ユーザーが作る本番の 3D 台モデルの差し替え（差し替え先は `Cabinet Model`）、(3) 倍率を入れた後の通しプレイからの数値調整。方針は「デザインは後で、先に場所どり」。左右の黒帯は 3:4 の固定によるもの（ユーザーに説明済み、変更しない）。
- 001 の残りの T399・T429（ユーザーの通しプレイ）は 002 の T617 で置き換わったものとして扱ってよいか、ユーザーに未確認。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-23 に停止を確認済み（ポート 8080 閉）。Editor は開いたまま。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。

### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約4分。スクリプトは毎回 `$env:TEMP` などに作り直す。
- PlayMode の実行中に Play を止めて打ち切ると、ジョブが `tests_running` のまま残り、Editor の再起動まで新しいテストを流せない。止めずに最後まで待つ。
- ラッパーが失敗すると出力が捨てられる。原因を見るときは `.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py ...` を直接呼ぶ。
- 実データを読むテスト（`TablePaths`）は数値の調整で落ちる。CSV を変えたら `Get("melee")` などの期待値を grep で探して一緒に直す。
- Bash で C# の `\n` を含む置換をするとバックスラッシュが崩れる。そういう置換は Edit ツールで行う。`node -e "..."` の中のバッククォートはシェルに展開されるので、node のコードは quoted heredoc でファイルに書いてから実行する。
- Unity 操作中に PC が落ちた前例がある。操作は1回の呼び出しに詰め込まず分ける。
- Codex に任せるのは小さく閉じた作業だけ（T604・T605 の委任は失敗）。HANDOFF・tasks を触らせず、テストは数と中身を確かめる。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
