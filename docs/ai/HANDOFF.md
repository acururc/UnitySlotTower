# Resume state

## 現在の作業

更新: 2026-10-29 Asia/Tokyo / 状態: M0 基盤（T001〜T008）完了、未コミット。次は M1 スロット単体

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（M0 は `[x]` と各行末に結果）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M1 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る。
- 完成物（`Assets/UnitySlotTower/`）: Core（`Data/` CSV パーサー・KeyValueTable・GameConfig・TableValidator・GameDataValidator、`Simulation/` SimulationClock・SeededRandom(SplitMix64)・IRandom、`Layout/PortraitLayout`、`Ui/NumericText`）、Runtime（`PresentationCatalog`、`PortraitScreenLayout`）、Editor（`Setup/GameSceneSetup`・`NumericFontSetup` の一回限りメニュー、`Ui/TextAudit`）、`Data/Tables/` CSV 雛形17本（game_config・slot_rules は plan の値、他は見出しのみ、maps/w1〜w5 は同じ仮グリッド）、`Scenes/Game.unity`、`Art/Fonts/NumericOnly SDF.asset`。ほかに `Assets/TextMesh Pro/`（TMP Essentials）、`InputSystem_Actions` に SlotTower マップ、プレイヤー設定 720×1280 可変ウィンドウ、ビルド設定は Game のみ有効（SampleScene は無効化して残置）。
- 補助スクリプト: `Test-UnityPlayer.ps1` に `-Width/-Height`（既定 1280×720）、`Test-ProjectHarness.ps1` の空白検査から `Assets/TextMesh Pro/` を除外（ベンダーのシェーダーに行末空白）。
- URP 資産・GraphicsSettings の差分はビルド時に Unity が自動で書いたもの（意図した変更ではない）。
- 検証（実行済み）: EditMode 95/95 green（各 Core 機能は red 確認後に green）、Console エラー0、Windows 開発ビルド成功、プレイヤー画像 720×1080/1920×1080/2560×1080 で 9:16 中央・黒帯・上38%戦闘を目視確認、ハーネス 8/8。未実行: PlayMode テスト（まだ0件）、真の 1440p 表示（モニターが 1080p。計算は単体テストで確認）。
- 留意点: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので、Runtime 側で Shift 押下中は SelectUnit を無視する。Esc は Cancel と Pause の両方で、配置操作中かどうかで振り分ける（plan 3章）。マップのマス記号は G/H/X/E/S1〜S4、経路は上下左右の連続のみ検証（範囲・通行可否の検証は T201）。
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- 次の一手: ユーザーの確認後に M0 をコミットするかを決め、M1 の T101（`slot_reels.csv`・`slot_payouts.csv`・`slot_rules.csv` の内容と読み込み）から `tdd` スキルで進める。テストは refresh → Console 確認 → `editor tests --async` を1回だけ → `poll-test` の順で、テスト開始を二重に送らない。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
