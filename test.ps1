param([switch]$IncludeUi,[string]$ProjectSample,[string]$EngineSample)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$qa = Join-Path $env:TEMP ('AepRenderer-Tests33-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa | Out-Null
New-Item -ItemType Directory -Path "$qa\AE23","$qa\AE26" | Out-Null
$previousData = $env:AEP_BATCH_DATA
try {
    $refs = @("/resource:$root\Localization\strings.json,strings.json",'/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Web.Extensions.dll')
    $core = @("$root\RenderCore.cs","$root\UiText.cs","$root\OutputRedirect.cs","$root\RenderLogCleanup.cs","$root\ProcessScope.cs","$root\AepFileItem.cs","$root\AepVersion.cs","$root\EngineCatalog.cs","$root\TestFixtures.cs")
    & $compiler /nologo /target:exe /codepage:65001 "/out:$qa\AE23\aerender.exe" @refs "$root\Tests.cs" @core
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed to compile' }
    & $compiler /nologo /target:exe /codepage:65001 /define:AE26 "/out:$qa\AE26\aerender.exe" @refs "$root\Tests.cs" @core
    if ($LASTEXITCODE -ne 0) { throw 'Mock AE26 failed to compile' }
    $env:AEP_BATCH_DATA = Join-Path $qa 'core'
    & "$qa\AE23\aerender.exe" | Tee-Object -FilePath "$qa\core-results.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    & $compiler /nologo /target:exe /codepage:65001 "/out:$qa\RoutingTests.exe" @refs "$root\TestsRouting.cs" @core
    if ($LASTEXITCODE -ne 0) { throw 'Routing tests failed to compile' }
    $env:AEP_BATCH_DATA = Join-Path $qa 'routing'
    $sampleArgs=@()
    if($ProjectSample -and $EngineSample){$sampleArgs=@($ProjectSample,$EngineSample)}
    & "$qa\RoutingTests.exe" "$qa\AE23\aerender.exe" "$qa\AE26\aerender.exe" @sampleArgs | Tee-Object -FilePath "$qa\routing-results.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Routing tests failed' }
    & $compiler /nologo /target:exe /codepage:65001 "/out:$qa\TestsExplorer.exe" @refs "$root\TestsExplorer.cs" "$root\ExplorerModel.cs" @core
    if ($LASTEXITCODE -ne 0) { throw 'Explorer tests failed to compile' }
    $env:AEP_BATCH_DATA = Join-Path $qa 'ui'
    & "$qa\TestsExplorer.exe" | Tee-Object -FilePath "$qa\explorer-results.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Explorer tests failed' }
    if($IncludeUi) {
        $process = Start-Process -FilePath "$root\AepBatchRenderer.exe" -ArgumentList '--ui-test' -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(60000)) { Stop-Process -Id $process.Id; throw 'UI test timed out; close other renderer instances and retry' }
        if (Test-Path "$env:AEP_BATCH_DATA\ui-error.txt") { throw (Get-Content "$env:AEP_BATCH_DATA\ui-error.txt" -Raw -Encoding UTF8) }
        foreach ($name in @('bridge-results.txt','drag-results.txt','layout-results.txt','interaction-results.json','ui-results.json','favorites.png','settings-light.png','preview-light.png','queue-states.png')) {
            if (-not (Test-Path (Join-Path $env:AEP_BATCH_DATA $name))) { throw "Missing UI result: $name" }
        }
    }
    Write-Host "ALL REQUESTED TESTS PASSED. Results: $qa"
    if(-not $IncludeUi){Write-Host 'WebView UI test skipped intentionally.'}
} finally { $env:AEP_BATCH_DATA = $previousData }
