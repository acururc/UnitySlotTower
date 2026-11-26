# Resume state

## 現在の作業

更新: 2026-11-26 Asia/Tokyo / 状態: **sakurai スキルの全体評価（7項目）をすべて組み入れる作業中。spec 011（スロットの作り直し）は実装と自動検証まで完了、T1508 のユーザー確認待ち。確認 OK なら tasks.md を閉じて spec 012（味方ごとの強化）の聞き取りへ。**

- spec 011: [`spec.md`](../../specs/011-slot-rework/spec.md)（承認済み、D1〜D8）・[`plan.md`](../../specs/011-slot-rework/plan.md)・[`tasks.md`](../../specs/011-slot-rework/tasks.md)。賭け1〜3枚＝有効ライン数（中→上→下）で強さも枚数倍、1ライン60%（`line_hit_rate`）、同じ図柄は2ラインに揃わない。3×3 の見た目は `SlotMachine` が決め、`SlotReelView.StopAt(top, middle, bottom)` が帯の3マスを置き換えて止める（固定の帯では二重・三重を作れないため）。赤7・毒は最大HPの割合（ボス0.5倍）。ステージ1のウェーブ5（`stage_waves.csv` の `slot_showcase`）を出し終わると `Battlefield.IsShowcaseHolding` で戦闘が止まり、`BattleController` がコイン補充・賭け3枚・レバー点滅・決め打ち `ShowcaseHits` を出し、最初の揃いで再開。Setup `Update Slot Bet (spec 011)`・`Update Help Bet (spec 011)` は実行済み（位置は仮置き）。PlayMode テストは乱数の細工をやめ `SlotCabinet.ForcedHits` で出目を固定する。
- 最終検証（2026-11-26）: EditMode 601/601、PlayMode 成功72/失敗0/スキップ1（Explicit）、ハーネス 8/8。未確認: 見た目（BET ボタン・ライン印・矢印・早見表の説明の位置、文字あり版）と通しの手触り、コインの落ちる量（範囲外: 通しで数値だけ調整）。
- 評価の7項目と spec の順（ユーザーが「すべて組み入れる、順番は任せる」と指示）: 010 やり直し（#5）→ 011 スロットの作り直し（#1 回すか迷いがない＝ハズレなし・全部プラス・コイン余り、#2 赤7/毒が固定ダメージで後半弱い、#7 ステージ1にスロットの見せ場）→ 012 味方ごとの強化（#3。ユーザー案: 職ごとに強化内容を変え、モデルや攻撃方法〔連射→ためて一撃など〕も変わる）→ 013 ステージと敵の変化（#4 ステージ5〜7が同じ構成、敵の尖り不足）→ 014 音（#6 音声ファイルが1つもない）。各 spec は聞き取り→承認→実装の順。
- spec 010（ステージのやり直し）は完了。
- 気づいて未対応: 文字あり版で右上の「一時停止」の文字がボタンの下に少しはみ出す（ユーザーに伝え済み、指示待ち）。
- spec 009 まで（出目の演出の作り直し 003〜009）は完了。そのほかの候補: 001 の M5（T501〜T505）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザー指示で完了扱いでよい（tasks.md は未更新）。
- 完了済み: spec 009・008・007・006・005・004・003・002。リザルトのボタンは本体の Image が透明で、見た目は子の `Skin`（`UiSkin`/`UiPanel`）が描く。Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。行の集合で比べる。`PortraitScreenLayout`（ExecuteAlways）がカメラの表示範囲と CanvasScaler の match を Game ビューの大きさで書き換えるのは無害。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-26 spec 011 の検証後に停止を確認済み（8080 閉）。PlayMode テストは Editor を前面に出してもらう。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（16GB）。

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
