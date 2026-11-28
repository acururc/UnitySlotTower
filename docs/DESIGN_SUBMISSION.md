# デザイン素材の提出方法

デザイナーから素材を受け取るときの形式と書き出し設定。何を作るかは [`DESIGN_ASSETS.md`](DESIGN_ASSETS.md) を見る。見た目は自由で、ここには Unity に入れるための条件だけを書く。

## 渡し方

- 受け渡しの場所（共有ドライブ・zip など）は企画側が決めて伝える
- フォルダは下の形にまとめる（1回の提出で全部そろっていなくてよい）

```
submit_YYYYMMDD_名前/
  models/      chr_*.fbx（モデル）
  anims/       anim_*.fbx（モーション）
  textures/    tex_*.png
  sprites/     icon_reel_*.png、cabinet_*.png
  source/      .ma / .mb / .blend / .psd など元データ
  notes.txt    モーション一覧と当たるフレーム、気になる点
  credits.txt  既存の素材を使ったときだけ（名前・URL・ライセンス）
```

- 直したものを出し直すときは、同じファイル名で上書きして出す（名前を変えない）

## ファイル名

半角の英小文字・数字・`_` だけ。

| 種類 | 形 | 例 |
|---|---|---|
| キャラのモデル | `chr_{ally/enemy}_{種類}.fbx` | `chr_ally_tank.fbx` |
| モーション | `anim_{ally/enemy}_{種類}_{動き}.fbx` | `anim_enemy_heavy_walk.fbx` |
| テクスチャ | `tex_{ally/enemy}_{種類}.png` | `tex_ally_tank.png` |
| 図柄 | `icon_reel_{図柄}.png` | `icon_reel_red7.png` |
| 筐体 | `cabinet_{部品}.png` | `cabinet_lever.png` |

- 種類: 味方 `tank` `sword` `archer` `buffer`、敵 `melee` `ranged` `flying` `heavy` `midboss` `boss`
- 動き: `idle` `attack` `death` `spawn` `hit` `walk` `fly` `area`（最終ボスの範囲攻撃）
- 図柄: `red7` `blue7` `green7` `yellow7` `bar` `coin`
- 筐体: `frame` `window` `lever` `lever_down` `coin`

## 3D モデル（Maya の場合）

### 書き出す前に
- 原点は足元の中心（空中の敵は体の下の中心）。正面は +Z、上は +Y
- Freeze Transformations と Delete History をしておく。一番上のノードは位置 0・回転 0・スケール 1
- 骨の名前に名前空間（`rig:` など）を付けない。モデルとモーションで骨の名前と階層を同じにする
- 骨の一番上に `root` という骨を原点（足元の中心）に置き、腰の骨をその子にする。四足・空中の敵も同じ
- バインドポーズ:
  - 人型は T ポーズ（A ポーズでもよい）。Humanoid に必要な骨（腰・背骨・胸・首・頭・上腕・前腕・手・太もも・すね・足）を入れる
  - 四足など人型でないものは、脚をまっすぐ下ろして立った自然なポーズ。骨の数・つなぎ方は自由（Unity では Generic として読み込む）
- 1頂点に効く骨は4本まで
- 武器は手の骨の子にする（別メッシュでも、同じメッシュで手の骨に100%でもよい）
- マテリアルは Lambert か Phong にテクスチャを1枚つなぐだけでよい。Arnold などのシェーダーは Unity に持ち込めないので、Unity 側でマテリアルを作り直す
- 法線マップ・光沢マップはいらない
- ブレンドシェイプ（表情など）は使っても使わなくてもよい。使うときはモデルとモーションでブレンドシェイプの名前を同じにする

### FBX の書き出し設定
- Geometry: Smoothing Groups オン、Triangulate はどちらでもよい
- Animation: モデルだけのファイルはオフ、モーションのファイルはオン
- Skins オン。Blend Shapes はブレンドシェイプを使うときだけオン
- Constraints・Cameras・Lights オフ
- Embed Media オフ（テクスチャは `textures/` に PNG で別に出す）
- Units: Automatic（Maya の cm のままでよい）
- Up Axis: Y
- FBX のバージョンは 2020 以降

## モーション

- 1動きを1ファイルにする。中身は骨組みだけでよい（メッシュは入れなくてもよい）。ブレンドシェイプを動かすモーションはメッシュも入れる
- **その場で動く（in place）。** 歩き・飛行でも体を前に進めない。移動はゲーム側で動かす
  - `root` の骨は動かさない（位置 0・回転 0 のまま）
  - 腰の上下・左右の揺れはよいが、前に進んでいかないようにする
- Bake Animation オン（IK やコンストレイントを骨のキーに焼き込む）、Resample All オン
- 30fps。書き出す範囲はその動きのフレームだけ
- ループするもの（待機・歩き・飛行）は最初と最後のポーズを同じにする
- 攻撃は1秒以内が目安。超えてもよい（ゲーム側で再生速度を変えて攻撃の間隔に合わせる。長いほど速く再生される）。当たる瞬間のフレーム番号を `notes.txt` に書く
- 死亡は最後のポーズで止まる形でよい（ゲーム側で消す）
- 骨にスケールのキーを付けない

`notes.txt` の書き方の例:

```
anim_ally_sword_attack.fbx   0-20   当たる: 12
anim_enemy_heavy_walk.fbx    0-32   ループ
anim_enemy_flying_fly.fbx    0-24   ループ
```

## テクスチャ

- PNG、sRGB、1024×1024 を1キャラ1枚（武器を分けるなら2枚まで）
- 透けない部分しかなければアルファはいらない
- 色の調整はテクスチャに入れる（ゲームの画面は Linear 色空間で、Unity 側でそのまま表示する）

## 2D（図柄・筐体）

- 透過 PNG（8bit RGBA、sRGB）。アルファは普通の透過（白や黒をにじませない）
- 図柄: 512×512、外側 32px は余白。6枚とも同じ大きさ・同じ中心で描く
- 筐体: 全体 1440×720 の中の位置が分かるように、部品はそれぞれ 1440×720 のキャンバスに置いた形でも、切り抜いて位置を `notes.txt` に書く形でもよい
- レバーは通常（`cabinet_lever`）と倒した状態（`cabinet_lever_down`）を同じキャンバスの大きさで出す
- 文字は入れない
- 元データ（.psd など）は `source/` に入れる
