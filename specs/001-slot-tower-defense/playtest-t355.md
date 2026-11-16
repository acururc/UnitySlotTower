# T355 通しプレイ記録

[tasks.md](tasks.md) の T355。設定1と設定6で各1回以上、最後（ボス）まで遊ぶ（途中で負けたらそこまでで可）。感想は短くてよい。チャットで伝えてもらえれば Claude がここに書き写す。

## 遊び方
- Unity Editor で `Assets/UnitySlotTower/Scenes/Title` を開いて Play。▶ → 難易度（設定1〜6）を選ぶ。
- 操作: 職のアイコンを押して戦闘マスに配置、レバーはクリックか Space（コイン3枚で1回）。ルート選びはレバーで行き先のリールを回して止める。
- 文字あり版: タイトルのロゴの右下（「あ」の印の所）を押すと切り替わる。1回は文字あり版で遊び、配置・文言も見る。

## 確かめること（括弧は調整する CSV、`Assets/UnitySlotTower/Data/Tables/`）
| # | 項目 | 見る所 | CSV |
|---|---|---|---|
| 1 | ルートの長さ | 1回が長すぎ・短すぎないか（いまは8階） | `route_rules.csv` floors・columns・paths |
| 2 | マスの種類の配分 | 戦闘・強敵・ショップ・休憩所の出方 | `route_rules.csv` weight_*・elite_min_floor |
| 3 | 忙しさ | 戦闘中にスロットと配置の両方をこなせるか | `slot_rules.csv` first_stop_seconds・stop_interval_seconds、`waves.csv` |
| 4 | お金2種類のやりくり | 所持金とコインが足りない／余るのはいつか | `game_config.csv` initial_*・bet、`encounters.csv` reward_* |
| 5 | 色の偏り・抽選表 | 7 の色が偏りすぎないか、ハズレの多さ（設定1と6の差） | `slot_patterns.csv` |
| 6 | 図柄の効果 | バー・コインの効果の強さ | `slot_rules.csv` bar_*・coin_* |
| 7 | 両替レート | ショップの両替を使う気になるか | `route_rules.csv` exchange_money_per_coin |
| 8 | ショップの値段 | 買いたい物が買えるか | `shop_items.csv` price・weight、`route_rules.csv` shop_slots |
| 9 | 休憩所 | 回復量（いまはライフ3） | `route_rules.csv` rest_heal |
| 10 | 回数固定 | 最初の戦闘の固定回数（いまは6回）が多すぎ・少なすぎないか | `encounters.csv` first の fixed_spins |
| 11 | ルートのリール選び | どのリールで止めるとどこへ行くか分かるか | — |
| 12 | 戦闘の難しさ | 強敵・ボスの強さ、ライフの減り方 | `enemies.csv`・`waves.csv`、`game_config.csv` life |
| 13 | 文字あり版（T359 の仮置き） | 文字の大きさ・配置・文言・設定の名前（やさしい〜最難関）が読みやすいか | `ui_texts.csv`（配置は Claude が直す） |

## 記録
| 日付 | 設定 | 文字 | 結果（到達した階・勝敗） | 気づいたこと（上の # で） |
|---|---|---|---|---|
|  | 1 | なし／あり |  |  |
|  | 6 | なし／あり |  |  |

## 調整したこと
（CSV を変えたらここに 変更前→変更後 と理由を書く）
