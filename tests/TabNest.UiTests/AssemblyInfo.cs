// UI テストは1つずつ実行する(テストクラスの並列実行を無効化)。
// 並列にすると WinAppDriver への同時セッション要求でアプリ起動が競合し、
// タイムアウトや「パラメーターが間違っています」エラーになる。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
