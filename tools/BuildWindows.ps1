param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath
)

$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectRoot 'Logs'
$logPath = Join-Path $logDirectory 'build-windows.log'

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity.exe が見つかりません: $UnityPath"
}

New-Item -ItemType Directory -Force $logDirectory | Out-Null
& $UnityPath -batchmode -nographics -quit -projectPath $projectRoot -executeMethod VerdantBlade.EditorTools.VerdantBladeBuild.BuildWindows64 -logFile $logPath
if ($LASTEXITCODE -ne 0) {
    throw "Windowsビルドに失敗しました。ログ: $logPath"
}
