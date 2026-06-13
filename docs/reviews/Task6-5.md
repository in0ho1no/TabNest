# Task6-5 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6
- レビュー対象: `git diff dev-re...Task6-5`
- 実施日: 2026-06-13

## 対象範囲

SPEC.md「Task 6-5: 表示の小改善（フォルダツリー表示切替・列区切りの実線）」

差分ファイル:
- `src/TabNest.App/MainPage.xaml`（ナビバーに `ToggleFolderTreeButton` 追加・列追加に伴う既存ボタンの列番号シフト・`FolderTreeView` に `Visibility` バインド追加・`ColumnSeparatorStyle` に `Rectangle` 追加）
- `src/TabNest.Core/Models/AppSettings.cs`（`IsFolderTreeVisible` プロパティ追加・既定 true）
- `src/TabNest.ViewModels/MainViewModel.cs`（`_isFolderTreeVisible` フィールド・`IsFolderTreeVisible` プロパティ追加・コンストラクタで復元・`CreateAppSettings` に出力追加）
- `tests/TabNest.Integration.Tests/SettingsServiceTests.cs`（`IsFolderTreeVisible = false` を含む既存サンプルへの追加と対応するアサート）
- `tests/TabNest.ViewModels.Tests/SessionRestoreTests.cs`（`フォルダツリー表示状態が復元される(true/false)` / `セッションなしの場合_フォルダツリーは既定で表示される` 追加）
- `tests/TabNest.ViewModels.Tests/SessionSaveTests.cs`（`フォルダツリー表示状態が保存内容に反映される_既定はtrue` / `フォルダツリーを非表示にすると保存内容にfalseが反映される` 追加）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `ToggleButton.IsChecked` は `bool?`（nullable bool）。`{x:Bind ViewModel.IsFolderTreeVisible, Mode=TwoWay}` で `bool` ↔ `bool?` の変換は x:Bind が自動で行うため動作上は問題ない。ただし `IsChecked` を `null` にセットするコードパスが将来追加された場合は VM 側が `false` を受け取る（暗黙の変換）。現時点では発生しないが、ドキュメントとして補足する価値がある。 | （未対応） |
| 2 | 情報 | `ColumnSeparatorStyle` の `Rectangle` は `Width="1"` / `HorizontalAlignment="Right"` で親 `Border`（`Width="8"`）の右端に 1px 線を描く。`Border` 自体は `Background="Transparent"` でヒット領域を 8px 全体に維持しているため、区切り線表示とダブルクリックのヒット領域は独立しており、区切り線の追加で既存のダブルクリック自動調整動作が壊れない設計になっている。確認済み・問題なし。 | （対応不要） |
| 3 | 情報 | ナビバーの `Grid.ColumnDefinitions` は dev-re では 6列（0–5）、Task6-5 では 7列（0–6）に拡張。`ToggleFolderTreeButton` を列3に挿入し、`PathTextBox` を 3→4、`AddTabButton` を 4→5、`AddGroupButton` を 5→6 にシフト。列数と各ボタンの `Grid.Column` 属性が完全に一致することを確認済み。ずれによる配置崩れなし。 | （対応不要） |
| 4 | 情報 | `FolderTreeView` の `Visibility` は `{x:Bind local:MainPage.VisibleWhen(ViewModel.IsFolderTreeVisible), Mode=OneWay}` で制御。左カラムの `Grid.RowDefinitions` は上段 `Height="Auto"`（お気に入り）・下段 `Height="*"`（ツリー）で、`FolderTreeView` が `Collapsed` になると下段が 0px まで縮まりお気に入り領域は残る。SPEC「非表示時は左カラム下部のフォルダツリー領域を畳む（お気に入り領域は残す）」を XAML 構造上充足している。 | （対応不要） |

Critical / Major の指摘なし。

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **フォルダツリー表示/非表示トグルの追加**: ナビバーの `Grid.Column="3"` に `ToggleButton`（`x:Name="ToggleFolderTreeButton"`）を追加。フォントアイコン `&#xEC50;`（パネルスライドアイコン）を使用し、`IsChecked` を `ViewModel.IsFolderTreeVisible` に TwoWay バインド。SPEC「ナビゲーションバーまたは左カラム上部にトグルを追加」を充足。
- **非表示時にツリー領域を畳む・お気に入りは残す**: `FolderTreeView`（`Grid.Row="1"`・`Height="*"`）に `Visibility="{x:Bind ..., Mode=OneWay}"` を付与。上段のお気に入り `StackPanel`（`Grid.Row="0"`・`Height="Auto"`）には `Visibility` を付与しておらず常に表示。SPEC の畳み方仕様を充足。
- **AppSettings に IsFolderTreeVisible を追加・settings.json 保存・復元**: `AppSettings.IsFolderTreeVisible`（既定 `true`）追加、`MainViewModel` コンストラクタで `_isFolderTreeVisible = session?.IsFolderTreeVisible ?? true` により復元、`CreateAppSettings` で保存。`MainWindow_Closed` が `CreateAppSettings` を呼び `_settingsService.Save(settings)` するため、終了時に自動保存される。SPEC「状態を settings.json に保存・復元する」を充足。
- **列区切りの視認できる実線**: `ColumnSeparatorStyle` の `ControlTemplate` に `Rectangle`（`Width="1"`・`HorizontalAlignment="Right"`・`Fill="{ThemeResource DividerStrokeColorDefaultBrush}"`）を追加。テーマに追従する色を使用。SPEC「ファイル一覧ヘッダーの列境界に視認できる区切り線」を充足。
- **ダブルクリックで列幅自動調整**: 既存の `NameColumnSeparator_DoubleTapped` / `TypeColumnSeparator_DoubleTapped` / `LastModifiedColumnSeparator_DoubleTapped` / `SizeColumnSeparator_DoubleTapped` → `AutoFitColumn` の実装は変更なし。区切り線追加後もヒット領域（8px の `Border`）は維持されており、ダブルクリック自動調整は回帰していない。SPEC「その位置のダブルクリックで列幅を自動調整」を充足。

### MVVM 層分離

- `src/TabNest.ViewModels/MainViewModel.cs` の using は `System.Collections.ObjectModel` / `TabNest.Core.*` のみ。`Microsoft.UI` 系 using なし。WinUI 非依存を確認。
- `src/TabNest.Core/Models/AppSettings.cs` に using なし（`namespace TabNest.Core.Models` のみ）。WinUI 非依存を確認。
- `tests/TabNest.ViewModels.Tests/TabNest.ViewModels.Tests.csproj` の `ProjectReference` は `TabNest.ViewModels` のみ。`TabNest.App` への参照なし。CLAUDE.md 禁止参照ルールを遵守。
- `tests/TabNest.Integration.Tests/TabNest.Integration.Tests.csproj` の `ProjectReference` は `TabNest.Core` のみ。同上。

### テストの実効性

- **SettingsServiceTests（結合）**:
  - `Save後にLoadすると内容が一致する`: `IsFolderTreeVisible = false` を含む `AppSettings` を実ファイルへ保存・再ロードし `Assert.False(loaded.IsFolderTreeVisible)` で実値確認。JSON シリアライズ・デシリアライズが正しく動作することを実証。
  - デフォルト値テスト: `Load` が存在しない場合のデフォルト動作で `Assert.True(settings.IsFolderTreeVisible)` を確認。既定 `true` の保証。
- **SessionRestoreTests（ViewModel 単体）**:
  - `[Theory][InlineData(true)][InlineData(false)]` でセッションの `true`/`false` 両値を `vm.IsFolderTreeVisible` で実値確認。`CreateSampleSession()` の `IsFolderTreeVisible` をテスト内で上書きして渡す（`session.IsFolderTreeVisible = saved`）のは適正な使い方。
  - `セッションなしの場合_フォルダツリーは既定で表示される`: `CreateViewModel(session: null)` で `IsFolderTreeVisible` が `true` であることを確認。null セッション時のデフォルト動作を網羅。
- **SessionSaveTests（ViewModel 単体）**:
  - 既定 `true` テスト: 初期化後の `CreateAppSettings` で `settings.IsFolderTreeVisible` が `true` であることを確認。
  - 変更後 `false` テスト: `vm.IsFolderTreeVisible = false` の後 `CreateAppSettings` で `false` が出力されることを確認。SetProperty によるプロパティ変更が `CreateAppSettings` の出力に反映されることを実値で検証。

SPEC 要件「IsFolderTreeVisible の保存・復元を単体または結合テストする」を複数のテスト（VM 単体×4 + 結合×2）で充足。形式的アサートなし・すべて実値検証。

### 状態整合性・回帰リスク

- **ナビバー列インデックスのシフト**: `ToggleFolderTreeButton` を列 3 に挿入することで既存の `PathTextBox`（3→4）・`AddTabButton`（4→5）・`AddGroupButton`（5→6）がシフト。`Grid.ColumnDefinitions` も対応して 6列→7列に拡張済み（`Width="Auto"` を 1個追加）。diff とネイティブの XAML 両方を確認し、ずれなし。
- **区切り線ヒット領域の保持**: `ColumnSeparatorStyle` のテンプレートは `Border`（`Background="Transparent"`）を外側に保ち、その内側に `Rectangle` を置く構造。`Button` 自体の幅は 8px のままで変更なし。ダブルクリックのヒット領域に影響なし。
- **IsFolderTreeVisible の TwoWay バインド**: `ToggleButton.IsChecked`（`bool?`）から `MainViewModel.IsFolderTreeVisible`（`bool`）への TwoWay バインドは x:Bind が自動変換する。チェック時 `true`・アンチェック時 `false` が正確に ViewModel に反映される。ViewModel の `SetProperty` が `PropertyChanged` を発火するため、`Visibility` バインドも追従してツリーの表示・非表示が切り替わる。

### AutomationId 付与

- `ToggleFolderTreeButton` に `AutomationProperties.AutomationId="ToggleFolderTreeButton"` および `AutomationProperties.Name="フォルダツリーの表示切替"` を付与。UI テストからトグル操作が識別可能。

### エラー処理・エッジケース

- `FolderTreeView` を `Collapsed` にするだけであり、ツリーの `ItemsSource` バインドや選択状態（`IsSelected` / `IsExpanded` の TwoWay バインド）には影響しない。再表示時に前回の展開・選択状態が維持される（WinUI の `TreeView` は非表示時も仮想化ツリーを保持する）。
- ウィンドウを閉じるまでに `IsFolderTreeVisible` を変更しなかった場合、`_isFolderTreeVisible` はコンストラクタで設定した復元値のまま `CreateAppSettings` に渡される。前回値が確実に保持される。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 1件・情報 3件）。

SPEC Task 6-5 の作業内容（トグル追加・非表示時ツリー畳み・お気に入り維持・IsFolderTreeVisible の保存・復元・列区切り実線表示）・完了条件（再起動後の状態保持・ダブルクリック自動調整の維持）・テスト要件（IsFolderTreeVisible 保存・復元の単体/結合テスト）をすべて満たしている。MVVM 層分離・禁止参照なし・AutomationId 付与・回帰リスクなし、いずれも適正。
