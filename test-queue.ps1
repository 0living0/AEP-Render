$ErrorActionPreference='Stop'
$queueQa=Join-Path $env:TEMP ('AepQueueTests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $queueQa | Out-Null
$oldData=$env:AEP_BATCH_DATA
try {
 $env:AEP_BATCH_DATA=$queueQa
 $sources=@('TestsQueue.cs','RenderCore.cs','UiText.cs','OutputRedirect.cs','RenderLogCleanup.cs','ProcessScope.cs','AepFileItem.cs','AepVersion.cs','EngineCatalog.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /codepage:65001 "/resource:$PSScriptRoot\Localization\strings.json,strings.json" /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Web.Extensions.dll "/out:$queueQa\TestsQueue.exe" @sources
 if($LASTEXITCODE -ne 0){throw 'Queue tests compile failed'}
 & "$queueQa\TestsQueue.exe"
 if($LASTEXITCODE -ne 0){throw 'Queue tests failed'}
} finally { $env:AEP_BATCH_DATA=$oldData }
