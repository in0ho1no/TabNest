<#
.SYNOPSIS
  指定ブランチの GitHub Actions CI 結果を表示する。gh 不要・未認証で動く（公開リポジトリ）。
.DESCRIPTION
  指定ブランチの HEAD SHA を GitHub から取得し、その SHA に対応する workflow run を判定する。
  対象 SHA の run が success なら exit 0、失敗なら exit 1、進行中・未作成・取得不能なら exit 2。
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

$encodedBranch = [Uri]::EscapeDataString($Branch)
$headers = @{ 'User-Agent' = 'ci-status'; 'Accept' = 'application/vnd.github+json' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $($env:GITHUB_TOKEN)" }

try {
  $branchUri = "https://api.github.com/repos/$owner/$repo/branches/$encodedBranch"
  $branchResp = Invoke-RestMethod -Uri $branchUri -Headers $headers -TimeoutSec 20
} catch {
  Write-Host "GitHub ブランチ情報の取得に失敗: $($_.Exception.Message)"; exit 2
}

$targetSha = $branchResp.commit.sha
if (-not $targetSha) {
  Write-Host "[$owner/$repo @ $Branch] HEAD SHA を取得できません。"; exit 2
}

try {
  $runsUri = "https://api.github.com/repos/$owner/$repo/actions/runs?branch=$encodedBranch&per_page=$Limit"
  $resp = Invoke-RestMethod -Uri $runsUri -Headers $headers -TimeoutSec 20
} catch {
  Write-Host "GitHub Actions run の取得に失敗: $($_.Exception.Message)"; exit 2
}

$runs = $resp.workflow_runs
if (-not $runs -or $runs.Count -eq 0) {
  Write-Host "[$owner/$repo @ $Branch] CI run が見つかりません。"; exit 2
}

Write-Host "[$owner/$repo @ $Branch] HEAD: $($targetSha.Substring(0, 7))"
Write-Host "直近 $($runs.Count) 件:"
foreach ($r in $runs) {
  $concl = if ($r.conclusion) { $r.conclusion } else { '-' }
  '{0,-10} {1,-10} {2}  {3}  {4}' -f $r.status, $concl, $r.head_sha.Substring(0, 7), $r.created_at, $r.display_title
}

# ブランチ HEAD と同じ SHA の run だけをゲート判定に使う。
$targetRun = $runs | Where-Object { $_.head_sha -eq $targetSha } | Select-Object -First 1
if (-not $targetRun) {
  Write-Host "`n対象 HEAD ($($targetSha.Substring(0,7))) の CI run が見つかりません。未実行または取得件数不足です。"; exit 2
}
if ($targetRun.status -ne 'completed') {
  Write-Host "`n対象 HEAD の CI は $($targetRun.status) です。"; exit 2
}
if ($targetRun.conclusion -eq 'success') {
  Write-Host "`nOK: 対象 HEAD の CI は success ($($targetRun.head_sha.Substring(0,7)))"; exit 0
} else {
  Write-Host "`nNG: 対象 HEAD の CI は $($targetRun.conclusion) ($($targetRun.head_sha.Substring(0,7)))"; exit 1
}
