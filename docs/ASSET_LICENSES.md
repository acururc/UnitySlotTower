# 素材のライセンス一覧

ゲームに含める外部素材とそのライセンス（plan 11章、T501）。素材を足したらここに1行足す。ライセンス本文が必要なものは素材の隣に置く。

| 素材 | 置き場所 | 入手元 | ライセンス | 確認日 | 用途 |
|---|---|---|---|---|---|
| Noto Sans JP Regular（サブセット OTF） | `Assets/UnitySlotTower/Art/Fonts/NotoSansJP-Regular.otf`（本文 `NotoSansJP-OFL.txt`） | https://github.com/notofonts/noto-cjk （`Sans/SubsetOTF/JP/NotoSansJP-Regular.otf`、sha256 先頭12桁 dff723ba59d5） | SIL Open Font License 1.1（商用利用可、フォント単体の販売は不可、改変版は別名） | 2026-11-15 | 文字あり版のラベル（T352）。TMP の静的フォントアセット `TextMode SDF.asset` は `ui_texts.csv` に出てくる字だけを持つ |
| Kenney Particle Pack 1.1（透過 PNG 8枚: flame_01・fire_01・smoke_04・circle_05・scorch_02・star_06・star_08・spark_04） | `Assets/UnitySlotTower/Art/VFX/Kenney/`（本文 `License.txt`） | https://kenney.nl/assets/particle-pack | CC0 1.0（クレジット不要） | 2026-11-23 | 赤7の息吹の演出（spec 003）、青7の凍結の演出（spec 004、star_06・star_08・spark_04 は 2026-11-23 に追加）、緑7の回復の演出（spec 005、circle_05・star_06・star_08 を流用、追加なし）、黄7の毒の演出（spec 006、circle_05・smoke_04 を流用、追加なし）、バーの闘気の演出（spec 007、circle_05・flame_01・spark_04・scorch_02 を流用、追加なし）、当たりの衝撃（spec 008、scorch_02・circle_05・spark_04 を流用、追加なし） |
| 魔法陣の絵（自作、PNG 4枚: Green7 Circle Outer・Middle・Inner、Bar Circle） | `Assets/UnitySlotTower/Art/VFX/MagicCircles/` | 自作（`Editor/Setup/MagicCircleTextures.cs` が描く、外部素材なし） | 自作のため不要 | 2026-11-23 | 緑7の盤面全体の魔法陣・バーの足元の魔法陣（spec 008） |
| コインの絵（自作、PNG 4枚: Coin・Bill・Sparkle・Coin Glow） | `Assets/UnitySlotTower/Art/VFX/Coin/` | 自作（`Editor/Setup/CoinFountainTextures.cs` が描く、外部素材なし） | 自作のため不要 | 2026-11-26 | コインの噴水の演出の金貨・倍率の表示のきらめき・閃光、倒した敵から飛ぶお札とコイン（spec 009） |
