param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Runtime = "win-x64",
    [string]$Channel = "stable",
    [string]$PackId = "Polyaga.LolLiveCoach",
    [string]$PackTitle = "LoL Live Coach",
    [string]$PackAuthors = "Polyaga",
    [string]$UpdateFeedUrl = "",
    [string]$PlatformBaseUrl = "",
    [string]$ReleaseNotesFile = "",
    [string]$SignTemplate = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$desktopProject = Join-Path $repoRoot "LolLiveCoach.Desktop\LolLiveCoach.Desktop.csproj"
$apiProject = Join-Path $repoRoot "LolLiveCoach.Api\LolLiveCoach.Api.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts\desktop"
$publishRoot = Join-Path $artifactsRoot "publish"
$releaseRoot = Join-Path $artifactsRoot "release\$Runtime\$Channel"
$bundleDir = Join-Path $publishRoot "bundle\$Runtime"
$desktopPublishDir = Join-Path $publishRoot "desktop\$Runtime"
$apiPublishDir = Join-Path $publishRoot "api\$Runtime"

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refus de nettoyer un dossier hors du workspace: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "La commande dotnet a echoue: dotnet $($Arguments -join ' ')"
    }
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

Reset-Directory -Path $desktopPublishDir
Reset-Directory -Path $apiPublishDir
Reset-Directory -Path $bundleDir
Reset-Directory -Path $releaseRoot

Push-Location $repoRoot
try {
    Invoke-DotNet -Arguments @("tool", "restore")
    $env:DOTNET_ROLL_FORWARD = "Major"

    Invoke-DotNet -Arguments @(
        "publish", $apiProject,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-o", $apiPublishDir,
        "/p:PublishSingleFile=false",
        "/p:PublishReadyToRun=true",
        "/p:Version=$Version",
        "/p:InformationalVersion=$Version"
    )

    $desktopPublishArgs = @(
        "publish", $desktopProject,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "true",
        "-o", $desktopPublishDir,
        "/p:PublishSingleFile=false",
        "/p:PublishReadyToRun=true",
        "/p:Version=$Version",
        "/p:FileVersion=$Version.0",
        "/p:AssemblyVersion=$Version.0",
        "/p:InformationalVersion=$Version",
        "/p:UpdateChannel=$Channel",
        "/p:UpdateFeedUrl=$UpdateFeedUrl",
        "/p:PlatformBaseUrl=$PlatformBaseUrl"
    )

    Invoke-DotNet -Arguments $desktopPublishArgs

    Copy-DirectoryContent -Source $desktopPublishDir -Destination $bundleDir

    $backendDir = Join-Path $bundleDir "backend"
    New-Item -ItemType Directory -Path $backendDir -Force | Out-Null
    Copy-DirectoryContent -Source $apiPublishDir -Destination $backendDir

    $developmentSettings = Join-Path $backendDir "appsettings.Development.json"
    if (Test-Path -LiteralPath $developmentSettings) {
        Remove-Item -LiteralPath $developmentSettings -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($PlatformBaseUrl)) {
        $appSettingsPath = Join-Path $backendDir "appsettings.json"
        $appSettings = Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json
        $appSettings.Platform.BaseUrl = $PlatformBaseUrl
        $appSettings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $appSettingsPath
    }

    $vpkArgs = @(
        "tool", "run", "vpk",
        "pack",
        "--packId", $PackId,
        "--packVersion", $Version,
        "--packDir", $bundleDir,
        "--mainExe", "LolLiveCoach.Desktop.exe",
        "--channel", $Channel,
        "--runtime", $Runtime,
        "--outputDir", $releaseRoot,
        "--packTitle", $PackTitle,
        "--packAuthors", $PackAuthors,
        "--shortcuts", "Desktop,StartMenuRoot"
    )

    if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesFile)) {
        $releaseNotesPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReleaseNotesFile))
        $vpkArgs += @("--releaseNotes", $releaseNotesPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($SignTemplate)) {
        $vpkArgs += @("--signTemplate", $SignTemplate)
    }

    Invoke-DotNet -Arguments $vpkArgs
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Release desktop generee dans:"
Write-Host "  $releaseRoot"
