# 002 実装計画（タワーディフェンス主体への転換）

要求は [`spec.md`](spec.md)（E-01〜E-30、FR-101〜FR-117、AC-101〜AC-113）。ここでは要求を繰り返さず、ID で参照する。001 の設計（[`../001-slot-tower-defense/plan.md`](../001-slot-tower-defense/plan.md)）のうち、ここで変えないもの（盤面・配置・ブロック・倍速・スロー・カメラ・弾・文字あり版の土台）はそのまま使う。

## 1. 方針
- Core の決まりを先に EditMode テストで固め（`tdd`）、その後で Runtime の流れと画面を作り替え、最後に削除と見た目を仕上げる。
- 削除は「新しい流れが動いてから」行う。途中のコミットでもコンパイルと既存のテストが通る状態を保つ。
- シーンとプレハブは今までどおり Editor の Setup メニュー（`Editor/Setup/*`）を直して作り直す。手で YAML は触らない。
- 数値はすべて CSV。仮の値は下の 4章で、プレイで調整する。

## 2. Core の変更
### 2.1 ステージ進行（FR-101〜FR-106）
- 新しい `stages.csv`: `stage, map_id, wave_mode(clear|timed), boss`。8行。ルートの `RouteMap`・`RouteRules`・`RouteNodeKinds`・`route_rules.csv` と、`EncounterTable`・`encounters.csv` の抽選（kind・floor・weight・fixed_spins・報酬）は消して、ステージ番号で引く `StageTable` に置き換える。
- マップの割り当て（今ある8枚をそのまま使う）: 1 `first` / 2 `battle_a` / 3 `battle_b` / 4 `elite_a`（中ボス）/ 5 `battle_c` / 6 `battle_d`（2方向）/ 7 `battle_e`（本拠地2つ）/ 8 `boss_a`（最終ボス）。D-89・D-107 のマップ抽選はなくなる。
- `waves.csv` に `wave` 列を足し、`time` はウェーブの始まりからの秒数にする。ウェーブの始まりは新しい `stage_waves.csv`（`stage, wave, start_after`＝前のウェーブの始まりからの秒数）で決める。
- `WaveSchedule` を静的な時刻表から、ウェーブ単位で進む `WaveProgress` に変える:
  - `clear`（ステージ1）: 今のウェーブが出し終わって盤上の敵が0になったら、`wave_gap_seconds`（初期値1）おいて次のウェーブを始める。
  - `timed`（ステージ2〜8）: `start_after` に達するか、出し終わって盤上が0になってから `wave_gap_seconds` たったら、早いほうで次のウェーブを始める（E-24）。
  - 最後のウェーブを出し終わって盤上が0ならクリア。`RemainingEnemies` はステージ全体の未出現＋盤上の数。
- `RunState`: `Route`・`Current`・`Floor`・`Setting`・`Gauges`・`Stock`・`Heal` を消し、`StageIndex`（1〜8）と `AdvanceStage()` を持つ。ライフ・コイン・所持金・強化は `RunState` に残るので、そのまま持ち越される（E-16）。
- `BattleNodeRun` を `StageRun` に改名・整理: 終了時の無料ストック化と報酬（`RewardMoney`・`RewardCoins`）をなくす。`IsRunCleared` はステージ8のクリア。

### 2.2 召喚（FR-108）
- `UnitStock` を消し、`UnitSummoner` に「どの職にも使える無料の1回」（`FreeSummons`）を持たせる。ステージ1の開始時だけ1にする（T617 で `game_config.csv` の `first_stage_free_summons` に移し、0 にした）。無料の召喚は所持金を使わず、待ち時間を始めない。撤退の返還は0。

### 2.3 スロット（FR-110〜FR-112）
- `game_config.csv` の `bet` を1、`initial_coins` を3、`initial_money` を0。`SlotMachine` の回数固定（`StartFixedSpins`・`NoSpinsLeft`）を消す。
- `slot_patterns.csv` から `setting` 列をなくし、20行（赤・青・緑・黄 各3、バー4、コイン4）にする。ハズレの止め方のコードは残す（後で入れる可能性があるため。今の表には `miss` がない）。
- 抽選から外す決まり（E-11・E-26）: `SlotMachine.TryLever` に「今出せる出目か」を答える関数を渡し、20パターンのうち出せるものだけから等確率で選ぶ（これで元の比のまま100%に直る）。
- 新しい `SlotEffects`（`BattleBoosts` を置き換え）。値は `slot_rules.csv`:
  `red_damage`=5、`blue_stop_seconds`=3、`green_heal_rate`=0.5、`yellow_poison_damage`=1、`yellow_poison_tick_seconds`=1、`yellow_poison_seconds`=5、`bar_attack_rate`=2、`bar_seconds`=10、`coin_step`=0.1、`coin_cap`=1.5。`gauge_max`・`bar_step`・`bar_cap` は消す。
  - 時間のある効果（停止・毒・攻撃力）は残りステップ数で持ち、`Battlefield.Advance` の中で減らす。ステップは倍速・スロー・一時停止に従うので、追加の処理なしで E-26 の時間の決まりを満たす。
  - 停止中の敵は `Move` と `Attack`（範囲攻撃を含む）を飛ばす。毒は停止中も減る（E-27）。
  - 全体ダメージと毒は敵の HP を減らすだけにし、撃破とドロップは今の `RemoveDefeatedEnemies` に任せる（効果で倒してもドロップする）。ダメージは防御を無視した固定値。
  - 回復は配置中の味方の最大 HP × 率（四捨五入）、最大値で止める。
  - `IsActive(symbol)`: 停止・毒・攻撃力は残り時間がある間、コインは上限に達したら true。赤と緑は常に false。
- スキル（`SkillGauges`・`SkillTable`・`skills.csv`・`BattleUnit` のスキル関係・`Battlefield.ActivateSkill`）は消す（E-14）。

### 2.4 ドロップとダメージ（FR-107・FR-109）
- 今の「1回の抽選でコインか所持金」をやめ、所持金とコインを別々に抽選する。`enemies.csv` の `money_rate`=1、`money_min/max`=30（T617 で 50 に上げた）、`coin_rate`=0.15、`coin_min/max`=1（ボスは別の値）。所持金にだけコインの倍率をかける（今と同じ）。
- 小さい桁で端数が0になる問題への対処: 攻撃力が1以上ならダメージは最低1にする（今の「最低5%」は四捨五入で0になるため）。攻撃力0の職（タンク・バッファー）は攻撃しない。

## 3. Runtime・画面の変更
- `RunController`: ルートの選択・ノード画面・ショップ・回復をなくし、ステージ1→8を順に進める。ステージのクリアで勝利の帯（D-100 の帯）→ 次のステージの開始の演出 → 戦闘。ステージ8のクリアかライフ0で `ResultScreen`。
- タイトル: 難易度の選択をなくし、押したらすぐ始まる。`GameSession.SelectedSetting`・`MinSetting`・`MaxSetting` を消す。
- 消す画面と部品: `RouteScreen`・`RouteNodeIcons`・`ShopScreen`・`RestScreen`・`NodeScreen`（ステージ間の画面が要らなくなる分）・`SkillPictures`・`SkillHelpPanel`・`GuideOverlay`・`GuidePictures`・`Core/Guide/GuideSequence`・`Exchange`・`Shop`。対応する Setup メニュー（`RouteScreenSetup`・`NodeScreensSetup`・`SkillHelpSetup`・`GuideOverlaySetup`・`GuideHelpUpdate`）も消すか直す。スロット筐体のルート選び（`BeginRouteChoice`・`PickRouteReel`・`RouteReelChosen`）も消す。
- 上端の帯: 残りの敵の数（ステージ全体）。スキルゲージのボタンの場所に、発動中の効果のアイコンと残り時間（円のゲージ）を出す。コインの倍率は今の倍率の表示を使う。バーの攻撃力の表示は「残り時間のある×2」に変える。
- 操作バーの札: 無料の1回が残っている間、全ての札に「¥0」を出す。無料ストックのバッジは消す。
- 配置の表示（FR-115）: `UnitPlacementView` に、札を選んだ時点で置けるマス全部に緑の薄い板を並べる（プールして使い回す）。カーソルの板は、置けるマスの上では別の色（明るい黄、`hoverColor`）、置けないマスの上では出さない（今の赤い `blockedColor` は使わない）。向きを選ぶ段では今までどおり範囲を出す。
- 揃ったときの演出（FR-113、E-28）: `SlotAlignEffect` に出目ごとの盤面全体の演出を足す。仮の形: 赤＝盤面を横切る赤い光の帯と敵ごとの小さな爆発、青＝盤面を青く染めて敵に氷の色、緑＝味方から緑の粒が上がる、黄＝盤面に黄緑の霧と敵に毒の色、バー＝味方の頭上に上向き矢印、コイン＝上からコインが降る。長さは 1秒前後で、戦闘は止めない。本番の絵は M5（001 の T501 以降）で差し替える。
- 外枠と LED（FR-116、E-29）: `SlotCabinetSetup` を直し、画面全体を囲む枠と、枠に沿った LED の列（小さな円の UI を並べ、色をスクリプトで流す）を作る。普段はゆっくり流れ、揃ったら出目の色で速く点滅して戻る。枠の幅は画面の約3%。盤面と下部の比率は撮影を見て決める。
- 早見表（`HelpScreen`）: スロットのページを新しい6つの効果と確率に、味方のページはスキルを除く。
- 文字あり版: `ui_texts.csv` から消した画面の文言を消し、新しい効果の名前と説明を足す。`TextAudit` が通ること。

## 4. 仮の数値（プレイで調整）
- 味方（`units.csv`、`def` は全員0）: 剣士 HP20（T617 で 10 から上げた）・攻撃1・間隔0.5・ブロック2 / 弓 HP8・攻撃1・間隔0.5・ブロック0 / タンク HP30・攻撃0・ブロック3 / バッファー HP10・攻撃0・`buff_rate`2.0（1.3 では攻撃1が四捨五入で1のままになるため）。費用・待ち時間は今のまま。
- 敵（`enemies.csv`、`def` は全員0）: 近距離 HP6（T617 で 10 から下げた）・攻撃1・間隔1 / 遠距離 HP8・攻撃1・間隔1 / 空中 HP7・攻撃1・間隔1 / 重装 HP25・攻撃1・間隔1（`ranged_damage_rate` 0.5。最低ダメージ1があるので弓の攻撃1では軽減が効かない。弓の攻撃を上げるかは調整で決める）/ 中ボス HP120・攻撃2 / 最終ボス HP300・攻撃3。所持金50（T617 で 30 から上げた。ボスは 200 / 0）。
- 強化（`unit_upgrades.csv`）: 小さい桁では 1.1 倍が効かないので、HP 倍率 1/1.2/1.4/1.7/2.0、攻撃倍率 1/1/2/2/3 にする（攻撃0の職は HP だけ伸びる）。費用は今のまま。 T617 で費用を 400 / 700 / 1100 / 1600 に上げた（後半に所持金が余るため。全ステージの雑魚の所持金は約1万8千円）。開始時の所持金は100（剣士だけ置ける）。
- ステージ1（`clear`）: ウェーブ1＝近距離1体、2＝近距離2体同時、3＝近距離3体、4＝近距離4体（2.5秒間隔）と遠距離2体の6体（T617 で 8体から減らした）。1〜3の撃破で 180 円入り、剣士か弓を1体召喚できる。
- ステージ2〜8（`timed`）: 今の `waves.csv` の群れを 4〜6 ウェーブに分け、後ろのステージほど数と重装・空中を増やす。ステージ4と8の最後のウェーブにボス。
- ドロップ率の許容範囲（AC-104）: 10,000 回で 15% ± 1.5%。抽選の割合（AC-108）: 20,000 回で各出目 ±1.5%。

## 5. 影響するファイル（主なもの）
- Core: `Battle/Battlefield.cs`・`BattleNodeRun.cs`・`WaveSchedule.cs`・`UnitSummoner.cs`・`Damage.cs`・`BattleBoosts.cs`（→`SlotEffects`）・`BattleUnit.cs`・`BattleEnemy.cs`、`Run/RunState.cs`・`EncounterTable.cs`（→`StageTable`）、`Slot/SlotMachine.cs`・`SlotPatterns.cs`・`SlotRules.cs`、`Data/GameDataValidator.cs`。消す: `SkillGauges`・`SkillTable`・`UnitStock`・`RouteMap`・`RouteRules`・`RouteNodeKinds`・`Shop`・`Exchange`・`Guide/GuideSequence`。
- Runtime: `Flow/RunController.cs`・`TitleScreen.cs`・`GameSession.cs`・`ResultScreen.cs`・`HelpScreen.cs`、`Battle/BattleController.cs`・`UnitPlacementView.cs`・`UnitBarIcon.cs`・`OperationBarStatus.cs`・`SlotAlignEffect.cs`、`Slot/SlotCabinet.cs`。消す: 3章の一覧。
- Editor: `Setup/*` の該当メニュー、`Automation/BattlePreviewMenu.cs`。
- Data: `game_config`・`units`・`enemies`・`unit_upgrades`・`slot_patterns`・`slot_rules`・`waves`・`ui_texts` を直す。`stages`・`stage_waves` を足す。`encounters`・`route_rules`・`shop_items`・`skills` を消す（.meta も一緒に）。
- Scene: `Game.unity`・`Title.unity`（Setup メニューで作り直す）。
- Tests: ルート・ショップ・両替・回復・スキル・説明・難易度・無料ストック・回数固定のテストは消す。ステージ進行・ウェーブ・効果・ドロップ・無料の1体・配置の表示のテストを足す。PlayMode の共通部品（`RouteTestUtil`・`GuideSuppression`）は、ステージを進める部品に置き換える。

## 6. リスク
- 削除の範囲が広く、シーンの参照が外れる。消す前に、その部品を参照しているシーンの項目とスクリプトを Grep で確かめ、Setup メニューで作り直してから Console のエラー0を確かめる。
- 小さい桁の整数で、倍率（強化・バッファー・バー・遠距離軽減）が四捨五入で効かなくなる。4章の値で始め、効くことを EditMode で確かめる。
- PlayMode テストの多くがルートと説明を前提にしている。ステージを進める部品を先に作り、テストを順に直す。テスト件数の増減は tasks に残す。
- 敵が少なく弱い序盤で、テストの「群れの負荷」（`CrowdFrameTimeTests`）が意味を変える。ステージ7の群れで測り直す。

## 7. 検証
- 各タスクで `unity-verify` の手順（変更に合う最小の検証）。Core は EditMode、流れと入力は PlayMode、見た目は Editor の画像（文字なし・文字あり）。
- 最後に全テスト、ハーネス、開発プレイヤーのビルドと撮影、ユーザーの通しプレイ（AC-113）。
