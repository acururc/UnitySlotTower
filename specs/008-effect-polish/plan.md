# エフェクトの改善 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-006、P-01〜P-06）。戦闘の数値と Core は変えない（FR-005）。

## 1. 魔法陣の絵（P-06）
- `Editor/Setup/MagicCircleTextures.cs`: 円・目盛り・小円・塗りの点・正多角形（三角・四角・六角）・線分の重ね合わせを、距離で縁をなめらかにして 1024px の白い PNG（透明度つき）に描く。毎回同じ絵になる。文字と星形は使わない。
- 保存先 `Art/VFX/MagicCircles/`: 緑7は3層（外・中・内、`Green7 Circle Outer/Middle/Inner.png`）、バーは1枚（`Bar Circle.png`）。材質は加算で `Art/VFX/` に同名の `.mat`。
- 緑7・バーの Setup から呼ぶので、どちらの Setup を再実行しても絵と材質が作られる。

## 2. 緑7の盤面全体の魔法陣（FR-003）
- `Green7HealEffect` に `circle`（3層を1つのパーティクルにまとめず、層ごとに `circleOuter/Middle/Inner`）と `circleFlash` を足す。地面に平ら（HorizontalBillboard）。
- 待ちの間は毎フレーム、盤面の中心に層ごとの粒を `SetParticles` で置く。大きさは盤面の長い辺 × `circleScale`（1.05）。外→中→内の順に現れ（待ち時間の 0〜35%・25〜60%・50〜85%）、現れるときは 1.15 倍から縮んで定まる。層ごとに逆向きに回り、明るさが上がる。
- 回復の瞬間（`GreenHealed`）: 置いていた層を消し、同じ絵を `circleFlash` から放出して広がりながら消す（0.6 秒）。待ちが 0 でも出す。柱が来ないまま待ちが終わったら層をそっと消す。

## 3. バーの足元の魔法陣（FR-004）
- `BarPowerEffect` の `chargeRing` の材質を `Bar Circle` にし、輪と同じ位置・縮み方で回す。芯は新しい `chargeCore`（光、circle_05）に分ける。

## 4. 当たりの衝撃（FR-001）
- `BattleActorsView` に `event Action<Vector3, bool> Impact`（位置、味方の攻撃なら true）。近接（味方: Role が Ranged でない、敵: Range 0）は攻撃の通知の中で相手の狙い位置に、遠距離は弾が届いた瞬間に弾の位置で通知する。`ImpactCount`（テスト用）。
- 新しい `HitImpactEffect`（`Runtime/Battle`）: `Impact` で閃光（scorch_02、0.15 秒、中心から外へ）・地面に平行な輪（circle_05）・火花（spark_04、6個）を出す。色は味方の攻撃＝白〜金、敵の攻撃＝赤。揺れ・ヒットストップなし。粒の上限で数を抑える。
- Setup `Tools/UnitySlotTower/Setup/Build Hit Impact Effect (spec 008)` が部品と材質（`Hit Flash/Ring/Spark`）を作り、`BattleActorsView` の弾の尾の材質（下記）も入れる。

## 5. 弾の尾（FR-002）
- `BattleActorsView` が弾を作るときに `TrailRenderer` を付ける（時間 0.12 秒、太さは頭 1 → 尾 0、色は矢＝金・敵の弾＝赤、材質は Setup が入れる `shotTrailMaterial`、白いテクスチャの加算）。撃つ時と片付ける時に `Clear()` して、使い回しで前の位置から線が伸びないようにする。材質がなければ尾は付けない。

## 6. 素材（FR-006）
- 新しい外部素材なし。Kenney の circle_05・spark_04・scorch_02 を流用、魔法陣は自作（`docs/ASSET_LICENSES.md` に自作として追記）。

## 7. リスク
- 当たりの数が多い場面で粒が増える: 上限（閃光 128・火花 512）で頭打ち。
- Setup の再実行で緑7・バーの Inspector 値は初期値に戻る（緑7は調整なし、バーは確認中）。

## 8. 検証
- PlayMode: 戦闘を進めて当たりの衝撃が `ImpactCount` だけ出て消えること、弾に尾が付き片付けで消えること、緑7で魔法陣が出て回復で消えること、バーで足元の魔法陣が出ること、部品が残らないこと（AC-002・AC-003）。既存テスト（AC-004）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
