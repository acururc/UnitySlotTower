# Resume state

## 現在の作業

更新: 2026-11-26 Asia/Tokyo / 状態: **spec 009（コインの噴水と敵のドロップが表示へ飛ぶ演出）完了（ユーザー確認 OK）。出目の演出の作り直し（spec 003〜009）はすべて完了。次の作業は未定（ユーザーに聞く）。**

- spec 009: [`spec.md`](../../specs/009-coin-shower/spec.md)・[`plan.md`](../../specs/009-coin-shower/plan.md)・[`tasks.md`](../../specs/009-coin-shower/tasks.md)。中心は `Runtime/Battle/CoinFountainEffect.cs`（UI 層）と Setup `Build Coin Fountain Effect (spec 009)`。Core に `coin_delay_seconds`・`SlotEffects.QueueCoin`・`Battlefield.CoinGained`/`EnemyDefeated(enemy, money, coins)`。表示の遅れは `OperationBarStatus.MoneyHeldBack`・`SlotCabinet.CreditHeldBack`。
- 同日の別件の修正: `UnitBarIcon` の料金は待ち時間中でもお金が足りなければ赤。
- 最終検証（2026-11-26）: EditMode 576/576、PlayMode 成功69/失敗0/Explicit 1、ハーネス 8/8。
- そのほかの候補: 001 の M5（T501〜T505、002・003 に合わせて見直す）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザー指示で完了扱いでよい（tasks.md は未更新）。
- 完了済み: spec 009・008・007・006・005・004・003・002。Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。行の集合で比べる。`PortraitScreenLayout`（ExecuteAlways）がカメラの表示範囲と CanvasScaler の match を Game ビューの大きさで書き換えるのは無害。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-26 に停止を確認済み（8080 閉）。PlayMode テストは Editor を前面に出してもらう。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（16GB）。

### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約4分。スクリプトは毎回 `$env:TEMP` などに作り直す。
- PlayMode の実行中に Play を止めて打ち切ると、ジョブが `tests_running` のまま残り、Editor の再起動まで新しいテストを流せない。止めずに最後まで待つ。
- ラッパーが失敗すると出力が捨てられる。原因を見るときは `.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py ...` を直接呼ぶ。
- 実データを読むテスト（`TablePaths`）は数値の調整で落ちる。CSV を変えたら `Get("melee")` などの期待値を grep で探して一緒に直す。
- Bash で C# の `\n` を含む置換をするとバックスラッシュが崩れる。そういう置換は Edit ツールで行う。`node -e "..."` の中のバッククォートはシェルに展開されるので、node のコードは quoted heredoc でファイルに書いてから実行する。Git Bash の `sed -i` は CRLF を LF にするので `sed -i -b` を使う。
- Unity 操作中に PC が落ちた前例がある。操作は1回の呼び出しに詰め込まず分ける。
- Codex に任せるのは小さく閉じた作業だけ（T604・T605 の委任は失敗）。HANDOFF・tasks を触らせず、テストは数と中身を確かめる。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
