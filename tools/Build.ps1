#requires -Version 5.1
[CmdletBinding()]
param([switch]$Publish)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$localSdk=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet=if(Test-Path -LiteralPath $localSdk){$localSdk}else{(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
Push-Location $projectRoot
try {
    & $dotnet restore WindowsWorkbench.sln --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Dependency restore failed. Install the SDK in global.json; do not bypass lock validation.'}
    & $dotnet build WindowsWorkbench.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Build failed.'}
    if($Publish){
        $destination=Join-Path $projectRoot 'artifacts\WindowsWorkbench'
        foreach($project in @('src\Optimizer.App\Optimizer.App.csproj','src\Optimizer.Cli\Optimizer.Cli.csproj')) {
            & $dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RestoreLockedMode=true -o $destination
            if($LASTEXITCODE -ne 0){throw "Publish failed: $project"}
        }
        Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $destination 'README.md')
        Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $destination 'THIRD-PARTY-NOTICES.md')
        $manifest=@(Get-ChildItem -LiteralPath $destination -File | Where-Object Name -ne 'SHA256SUMS.json' | Sort-Object Name | ForEach-Object {
            [pscustomobject]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}
        })
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.json') -Encoding utf8
        Write-Output "Self-contained Windows x64 preview: $destination"
    }
} finally {Pop-Location}
