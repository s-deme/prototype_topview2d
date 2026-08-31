# Unity 6.3 移行メモ

このプロジェクトは **Unity 6.3 LTS（`6000.3.22f1`）** を対象に更新済みです。

## 更新内容

- `ProjectSettings/ProjectVersion.txt` を `6000.3.22f1` に更新
- Unity 6.3 が要求する `com.unity.feature.2d`、Test Framework、UGUI、IDE 統合、Collab の互換版へ更新
- Unity 6 の既定モジュール（アクセシビリティ、Adaptive Performance、Vector Graphics、Multiplayer Center）を manifest に追加
- Unity 6 で非推奨となる `FindObjectsOfType` を `FindObjectsByType` に置換

## 初回の開き方

1. Unity Hub の **プロジェクト** から `E:\script\prototype_topview2d` を追加します。
2. Editor に **Unity 6.3 LTS（`6000.3.22f1`）** を指定します。
3. インターネット接続した状態で開き、Package Manager の初回依存解決を完了させます。
4. `Assets/Scenes/Prototype.unity` を開き、Play を押します。

`Packages/packages-lock.json` は依存関係を固定するためリポジトリにコミット済みです。`Packages/manifest.json` を変更したときだけUnityで依存解決を行い、更新されたmanifestとlock fileを同じ変更としてコミットしてください。CIのキャッシュキーもこのlock fileを使用します。

## 検証

- Unity メニュー: **Verdant Blade > Validate Prototype Setup**
- 自動テスト: **Window > General > Test Runner > EditMode**
- 手動確認項目: [README.md](README.md) の「手動スモークテスト」

このプロジェクトでは旧 Input Manager API を使用しています。Input System パッケージを追加する場合は、Player Settings の **Active Input Handling** を `Both` に維持してください。
