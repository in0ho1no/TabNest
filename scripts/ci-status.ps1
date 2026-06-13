<#
.SYNOPSIS
  指定ブランチの GitHub Actions CI 結果を表示する。gh 不要・未認証で動く（公開リポジトリ）。
.DESCRIPTION
  最新の workflow run を取得して status / conclusion を出力する。
  最新の「完了」run が success なら exit 0、失敗なら exit 1、進行中・取得不能なら exit 2。
  → `powershell -File scripts/ci-status.ps1 dev-re; if ($LASTEXITCODE -eq 0) { ... }` のようにゲートに使える。
  owner/repo は origin リモートから導出するのでリポジトリ移設にも追従する。
  private リポジトリやレート制限時は環境変数 GITHUB_TOKEN を設定すると認証付きで叩く。
.EXAMPLE
  powershell -File scripts/ci-status.ps1            # 現在のブランチ
  powershell -File scripts/ci-status.ps1 dev-re     # 指定ブランチ
  powershell -File scripts/ci-status.ps1 dev-re 8   # 直近8件
.NOTES
  Windows PowerShell 5.1 / PowerShell 7 双方で動くよう、7 専用構文（??・三項）は使わない。
#>
param(
  [string]$Branch = (git rev-parse --abbrev-ref HEAD 2>$null),
  [int]$Limit = 5
)

$ErrorActionPreference = 'Stop'

# origin から owner/repo を導出（https / ssh 両形式に対応）
$remote = (git remote get-url origin 2>$null)
if (-not $remote) { Write-Host 'origin リモートが見つかりません。'; exit 2 }
if ($remote -match 'github\.com[/:]([^/]+)/(.+?)(\.git)?$') {
  $owner = $Matches[1]; $repo = $Matches[2]
} else {
  Write-Host "GitHub の owner/repo を導出できません: $remote"; exit 2
}

if (-not $Branch) { Write-Host 'ブランチを特定できません。引数で指定してください。'; exit 2 }

$uri = "https://api.github.com/repos/$owner/$repo/actions/runs?branch=$Branch&per_page=$Limit"
$headers = @{ 'User-Agent' = 'ci-status'; 'Accept' = 'application/vnd.github+json' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $($env:GITHUB_TOKEN)" }

try {
  $resp = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 20
} catch {
  Write-Host "GitHub API 呼び出しに失敗: $($_.Exception.Message)"; exit 2
}

$runs = $resp.workflow_runs
if (-not $runs -or $runs.Count -eq 0) {
  Write-Host "[$owner/$repo @ $Branch] CI run が見つかりません。"; exit 2
}

Write-Host "[$owner/$repo @ $Branch] 直近 $($runs.Count) 件:"
foreach ($r in $runs) {
  $concl = if ($r.conclusion) { $r.conclusion } else { '-' }
  '{0,-10} {1,-10} {2}  {3}  {4}' -f $r.status, $concl, $r.head_sha.Substring(0, 7), $r.created_at, $r.display_title
}

# 最新の「完了」run で合否判定
$latest = $runs | Where-Object { $_.status -eq 'completed' } | Select-Object -First 1
if (-not $latest) {
  Write-Host "`n最新 run は進行中。"; exit 2
}
if ($latest.conclusion -eq 'success') {
  Write-Host "`nOK: 最新の完了 run は success ($($latest.head_sha.Substring(0,7)))"; exit 0
} else {
  Write-Host "`nNG: 最新の完了 run は $($latest.conclusion) ($($latest.head_sha.Substring(0,7)))"; exit 1
}
