#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$localSdk=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet=if(Test-Path -LiteralPath $localSdk){$localSdk}else{(Get-Command dotnet -ErrorAction Stop).Source}
$cli=Join-Path $projectRoot 'src\Optimizer.Cli\bin\Release\net10.0-windows\WindowsWorkbench.Cli.dll'
$scratch=Join-Path $projectRoot ('artifacts\cli-tests\'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $scratch -Force)
$passed=0
function Invoke-ReadOnlyCli([string[]]$arguments,[int]$expected=0) {
    $previousPreference=$ErrorActionPreference
    try { $ErrorActionPreference='Continue'; $lines=& $dotnet $cli @arguments 2>&1; $code=$LASTEXITCODE }
    finally {$ErrorActionPreference=$previousPreference}
    if($code -ne $expected){throw "CLI exit $code expected $expected for $($arguments -join ' '): $lines"}
    return ($lines -join [Environment]::NewLine)
}
function Check([string]$name,[scriptblock]$probe) {
    & $probe
    $script:passed++
    Write-Output "PASS $name"
}
function Require([bool]$condition,[string]$message) {if(-not $condition){throw $message}}
Check 'Help explains preview scope' {Require ((Invoke-ReadOnlyCli @('help')) -match 'six current-user preferences') 'Missing scope'}
Check 'Unknown command rejected' {[void](Invoke-ReadOnlyCli @('optimize-everything') 2)}
Check 'Unknown option rejected' {[void](Invoke-ReadOnlyCli @('assess','--force','true') 2)}
Check 'Missing option value rejected' {[void](Invoke-ReadOnlyCli @('catalog','--mode') 2)}
Check 'Duplicate option rejected' {[void](Invoke-ReadOnlyCli @('catalog','--mode','Guided','--mode','Preference') 2)}
Check 'Catalog has exact shipped scope' {
    $catalog=Invoke-ReadOnlyCli @('catalog') | ConvertFrom-Json
    Require ($catalog.actions.Count -eq 103) 'Expected 103 catalog entries'
    Require (@($catalog.actions | Where-Object mode -eq 'Preference').Count -eq 6) 'Expected six native preferences'
    Require (@($catalog.actions | Where-Object mode -eq 'Guided').Count -eq 85) 'Expected 85 guides'
    Require (@($catalog.actions | Where-Object mode -eq 'External').Count -eq 12) 'Expected 12 tool references'
}
Check 'Preference filter cannot include guide execution' {
    $catalog=Invoke-ReadOnlyCli @('catalog','--mode','Preference') | ConvertFrom-Json
    Require ($catalog.actions.Count -eq 6) 'Wrong preference filter'
}
Check 'Unknown priority rejected' {[void](Invoke-ReadOnlyCli @('catalog','--profile','everything') 2)}
Check 'Unknown mode rejected' {[void](Invoke-ReadOnlyCli @('catalog','--mode','ExecuteScript') 2)}
Check 'Read-only assessment omits principal identifiers' {
    $json=Invoke-ReadOnlyCli @('assess')
    $assessment=$json | ConvertFrom-Json
    Require ($assessment.facts.Count -ge 15) 'Incomplete assessment'
    Require ($json -notmatch '"(machineId|userId)"|S-1-5-21-') 'Principal identifier leaked'
}
Check 'Report export does not overwrite existing files' {
    $path=Join-Path $scratch 'report.json'
    [void](Invoke-ReadOnlyCli @('assess','--out',$path))
    $hash=(Get-FileHash -LiteralPath $path).Hash
    [void](Invoke-ReadOnlyCli @('assess','--out',$path) 2)
    Require ((Get-FileHash -LiteralPath $path).Hash -eq $hash) 'Existing report changed'
}
Check 'Guidance plan contains no executable targets' {
    $path=Join-Path $scratch 'guided-plan.json'
    [void](Invoke-ReadOnlyCli @('plan','--actions','guide.p22,tool.win11debloat','--out',$path))
    $plan=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Require ($plan.items.Count -eq 2) 'Wrong selection'
    Require (@($plan.items | Where-Object status -ne 'Guided').Count -eq 0) 'Guidance became executable'
    Require (@($plan.items | Where-Object { $null -ne $_.desired }).Count -eq 0) 'Guidance has desired write state'
}
Check 'Unknown action selection rejected' {[void](Invoke-ReadOnlyCli @('plan','--actions','unknown.action','--out',(Join-Path $scratch 'invalid.json')) 2)}
Check 'Duplicate action selection rejected' {[void](Invoke-ReadOnlyCli @('plan','--actions','guide.p01,guide.p01','--out',(Join-Path $scratch 'duplicate.json')) 2)}
Check 'Malformed plan fails before application' {
    $path=Join-Path $scratch 'malformed.json'; '{invalid}' | Set-Content -LiteralPath $path -Encoding utf8
    [void](Invoke-ReadOnlyCli @('apply','--plan',$path,'--approve',('0'*64)) 2)
}
Check 'Duplicate plan fields rejected' {
    $path=Join-Path $scratch 'ambiguous.json'; '{"planId":"a","planId":"b"}' | Set-Content -LiteralPath $path -Encoding utf8
    $result=Invoke-ReadOnlyCli @('apply','--plan',$path,'--approve',('0'*64)) 2
    Require ($result -match 'Duplicate plan field') 'Duplicate properties not rejected'
}
Check 'Undo needs exact explicit run selection' {[void](Invoke-ReadOnlyCli @('undo','--run','invalid','--approve-run','different') 2)}
Check 'Invalid recovery ID rejected' {[void](Invoke-ReadOnlyCli @('undo','--run','invalid','--approve-run','invalid') 2)}
Check 'History can be inspected without applying settings' {[void](Invoke-ReadOnlyCli @('history'))}
Write-Output "$passed/$passed CLI checks passed. No valid apply or recovery command was executed."
exit 0
