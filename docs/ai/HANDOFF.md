# Resume state

## 現在の作業

更新: 2026-11-26 Asia/Tokyo / 状態: **spec 009（コインの噴水）変更1まで実装・検証済み、ユーザーの Play 確認待ち（[`tasks.md`](../../specs/009-coin-shower/tasks.md) T1307）。spec 007・008 は完了。**

- spec 009: [`spec.md`](../../specs/009-coin-shower/spec.md)（2026-11-26 承認、FR-001〜FR-008・AC-001〜AC-005）・[`plan.md`](../../specs/009-coin-shower/plan.md)・[`tasks.md`](../../specs/009-coin-shower/tasks.md)。流れ: 中段の真ん中のマスから金貨が噴き上がる（弱い揺れ）→回りながら降る→コインの倍率の表示（×1.1）へ吸い込まれ、最後の1枚が着く瞬間（`coin_delay_seconds`=1.5）に倍率が1段上がって倍率の文字が弾む→倍率の表示の周りがきらめく（1〜5個）。変更1（同日承認）: 倒した敵の所持金は緑のお札（1枚＋倍率で増える、1.5倍で6枚）が所持金の数字へ、スロットコインは枚数分の金色のコインが台のコインの数へ飛び、着いた瞬間に数字が増えて弾む（`OperationBarStatus.MoneyHeldBack`・`SlotCabinet.CreditHeldBack` で表示だけ遅らせる。同時の上限48、超えた分はすぐ表示）。
- 作ったもの: Core `SlotRules.CoinDelaySeconds`・`SlotEffects.QueueCoin`/`StepCoinDelay`/`PendingCoins`（待ちの分も上限の判定に入る）・`Battlefield.CoinGained`/`EnemyDefeated(enemy, money, coins)`、`BattleController` の中継、`OperationBarStatus.MoneyRect`、`Runtime/Battle/CoinFountainEffect.cs`（UI 層の Image、プール）、`Editor/Setup/CoinFountainTextures.cs`（自作 PNG 4枚（Coin・Bill・Sparkle・Coin Glow） `Art/VFX/Coin/`）・`CoinFountainEffectSetup.cs`（メニュー `Build Coin Fountain Effect (spec 009)`、Game シーンに適用・保存済み）、`SlotBoardEffect` からコインを外した（もうどの出目も出さない）。
- 検証（2026-11-26、変更1の後）: EditMode 576/576、PlayMode 成功69/失敗0/Explicit 1、ハーネス 8/8。ステージをまたいだきらめき・表示のそろい方はテストなし（`NodeBegan` で持ち越しを0に戻す）。
- 2026-11-26 調整1: 勝利の帯の間（timeScale 0）は `CoinFountainEffect` を実時間で進め、最後の敵のお札・コインを飛ばしきる。別件で `UnitBarIcon` の料金を、待ち時間中でもお金が足りなければ赤くした。
- 次の操作: ユーザーが Editor で Play して噴水の行き先、敵からお札・コインが飛ぶこと、クリアの帯の間も飛びきること、最初の剣士を置いた後の料金の赤を確認（AC-001）。調整は `CoinFountainEffect` の初期値を直して Setup を再適用。OK なら T1307 を閉じて spec 009 完了。
- そのほかの候補: 001 の M5（T501〜T505）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザー指示で完了扱いでよい（tasks.md は未更新）。
- 完了済み: spec 008・007・006・005・004・003・002。Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。行の集合で比べる。`PortraitScreenLayout`（ExecuteAlways）がカメラの表示範囲と CanvasScaler の match を Game ビューの大きさで書き換えるのは無害。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。
- ブランチ: main に直接コミットして origin へ push（2026-11-26 は push できた）。main は core.autocrlf で CSV が CRLF になる。
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
