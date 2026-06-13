# TabNest.UiTests — GUI 自動テスト

WinAppDriver + Appium.WebDriver による GUI 自動テストプロジェクト。
WinAppDriver が起動していない環境ではテストは自動的にスキップされるため、
`dotnet test TabNest.slnx` は常に成功する。

## 前提条件(初回のみ)

1. **開発者モードを有効にする**
   設定 → システム → 開発者向け → 「開発者モード」をオン

2. **WinAppDriver をインストールする**
   <https://github.com/microsoft/WinAppDriver/releases> から v1.2.1 以降の
   `WindowsApplicationDriver.msi` をダウンロードしてインストールする
   (既定のインストール先: `C:\Program Files (x86)\Windows Application Driver\`)

## UI テストの実行手順(毎回)

```powershell
# 1. WinAppDriver を管理者権限で起動する(管理者ターミナルで実行)
Start-Process "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
#    → 「Listening for requests at: http://127.0.0.1:4723/」と表示されればOK

# 2. テスト対象アプリのパッケージを登録する(初回・ビルド更新後)
dotnet run --project src/TabNest.App/TabNest.App.csproj -p:Platform=x64
#    起動を確認したらアプリは閉じてよい(dotnet run がパッケージ登録を行う)

# 3. UI テストを実行する
dotnet test tests/TabNest.UiTests/TabNest.UiTests.csproj
```

## テスト対象アプリの起動方法

テストは WinAppDriver の `app` capability に **AUMID** を渡してインストール済み
パッケージを起動する(`Infrastructure/AppSession.cs`)。

- 既定の AUMID: `893E2482-2E47-47E9-A41F-73DAA6BE7D3A_1z32rh13vfry6!App`
  (`src/TabNest.App/Package.appxmanifest` の Identity から決まる)
- AUMID が異なる場合は環境変数で上書きする。
  **環境変数の設定と `dotnet test` は同一セッション(同じコマンドブロック)で実行すること**
  (ユーザー環境変数に保存しても別プロセスには即時反映されない):

```powershell
# AUMID の確認
Get-StartApps | Where-Object { $_.Name -like "*TabNest*" }

# 上書きして実行(同一セッションで)
$env:TABNEST_UI_TEST_APP_ID = "<確認した AUMID>"
dotnet test tests/TabNest.UiTests/TabNest.UiTests.csproj
```

## トラブルシューティング

- **テストが「ウィンドウが起動しませんでした」で失敗する / 「パラメーターが間違っています」ダイアログが出る**
  古い AUMID が使われている可能性が高い。ユーザー環境変数に過去の
  `TABNEST_UI_TEST_APP_ID` が残っていないか確認する:

  ```powershell
  [Environment]::GetEnvironmentVariable("TABNEST_UI_TEST_APP_ID", "User")
  # 古い値が残っていたら削除する
  [Environment]::SetEnvironmentVariable("TABNEST_UI_TEST_APP_ID", $null, "User")
  ```

- **セッション作成が120秒ハングする**
  WinAppDriver は無効な AUMID の活性化でエラーを返さずハングする。
  上記の環境変数と、パッケージ登録(`Get-StartApps`)を確認する。

## テストの書き方

- WinAppDriver を必要とするテストには `[Fact]` ではなく **`[UiFact]`** と
  `[Trait("Category", "UITest")]` を付ける(未起動時に自動スキップされる)
- アプリの起動・終了は `using var session = new AppSession();` で行う

## ファイル操作に関するサンドボックスポリシー(SPEC「Level 3」)

- ファイルの削除・コピー・移動といった操作は GUI テストの対象から原則除外する
- ナビゲーション系テストで実フォルダを参照する場合は
  `tests/TabNest.UiTests/TestFixtures/` 以下のみ使用する
- `TestFixtures/` 以外のパスへの書き込み・削除操作を含むテストは作成しない
