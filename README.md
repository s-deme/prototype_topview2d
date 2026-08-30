# Verdant Blade

Unity 6.3 LTS（`6000.3.22f1`）向けの、外部アセット不要なトップビュー 2D アクションアドベンチャーです。草原・敵・拾得物・UI はシーン開始時に生成されます。

Unity 6 移行内容と初回起動の注意点は [UNITY6_MIGRATION.md](UNITY6_MIGRATION.md) を参照してください。

「Sun Shard を 8 個集める → Gate Warden を倒す → Ancient Gate に戻る」が 1 回のプレイサイクルです。製品機能の棚卸しと実装内容は [PRODUCT_CHECKLIST.md](PRODUCT_CHECKLIST.md) にまとめています。

## 起動

1. Unity Hub でこのフォルダーを **Unity 6.3.22f1** として開きます。
2. `Assets/Scenes/Prototype.unity` を開き、Play を押します。
3. タイトルの `BEGIN JOURNEY` を押すか、`Enter`、`Space`、`Z`、コントローラー A で開始します。

## 操作

| 操作 | キーボード / マウス | コントローラー |
| --- | --- | --- |
| 移動 | WASD / 矢印 | 左スティック |
| 攻撃 | Space / Z / 左クリック | A (Button 0) |
| ダッシュ | Shift / X / 右クリック | B (Button 1) |
| ポーズ | Esc / P | Start (Button 7) |
| リスタート | R / Enter（結果画面） | A（結果画面） |

マウスで攻撃すると、カーソルの方向へ剣を向けます。金色のコンパス矢印は、常に次の目的地を示します。
タイトルでは矢印キー／D-padで項目を選択し、Enter／Aで決定できます。`SETTINGS & ACCESSIBILITY > KEYBOARD CONTROLS` から、移動・攻撃・ダッシュ・ポーズの主キーを変更できます。

## 実装したプロダクト機能

- クリック操作とキーボード操作に対応したタイトル、遊び方、設定、記録画面
- 探索・近接戦闘・遠隔敵・ボス戦・回復・壺破壊・スコア・Flow Combo
- HUD、状況に応じたガイド、目標コンパス、ボス警告、ダメージ／回復のフィードバック
- 安全な自動ポーズ、再開／再挑戦／タイトル復帰、勝敗リザルト
- ローカル保存される記録、最速クリア、累計統計、実績、二段階確認付きの記録リセット
- SFX、画面揺れ、点滅軽減、ハイコントラスト HUD、大きな文字のアクセシビリティ設定
- Explorer／Adventurer／Veteran の難易度選択（プレイヤー体力・敵体力・敵速度に反映）
- 保存されるキーリマップと、重複キーを防ぐ入力設定

## 開発・検証

- `GameInput.cs` に入力を集約しているため、将来の Unity Input System 移行やキーコンフィグ追加の入口が一か所です。
- `GameRules.cs` はスコア・Shard・タイム判定を Unity UI から分離した純粋なルール層です。
- `Assets/Tests/EditMode/VerdantBladeRulesTests.cs` には Edit Mode のルールテストがあります。Unity の **Window > General > Test Runner** から実行できます。
- Unity メニューの **Verdant Blade > Validate Prototype Setup** は、開始シーンと Bootstrap スクリプト、Build Settings を短時間で検証します。
- Editor / Development Build に限り、`F1` で診断 HUD、`F2` で Shard 追加、`F3` で全回復を利用できます。リリースビルドには含まれません。

### 手動スモークテスト

1. タイトルから `HOW TO PLAY`、`SETTINGS & ACCESSIBILITY`、`JOURNEY RECORD` を開いて戻れることを確認します。
2. 設定を変え、再起動後も SFX とアクセシビリティ設定が維持されることを確認します。
3. 8 個の Shard を集め、Warden を倒し、コンパスに従って Gate に入ります。
4. ポーズ、アプリ切替による自動ポーズ、再開、リトライ、タイトル復帰を確認します。
5. `KEYBOARD CONTROLS` でキーを変更し、重複キーが拒否され、再起動後も保存されることを確認します。
6. タイトルで難易度を切り替え、Explorer の 7 ライフと Veteran の 5 ライフ、および敵の体力・移動速度差を確認します。
7. 結果画面のスコア、統計、実績、記録更新を確認します。
