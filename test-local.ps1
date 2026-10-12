$ErrorActionPreference = 'Stop'
$localQa = Join-Path $env:TEMP ('AepLocalRunner-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $localQa | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "/out:$localQa\TestsLocal.exe" "$PSScriptRoot\TestsLocal.cs" "$PSScriptRoot\FolderMonitor.cs" "$PSScriptRoot\LocalBridge.cs" "$PSScriptRoot\UiText.cs" "/resource:$PSScriptRoot\Localization\strings.json,strings.json"
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
& "$localQa\TestsLocal.exe"
if ($LASTEXITCODE -ne 0) { throw 'Local tests failed' }
