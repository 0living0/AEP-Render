$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$root\IconBuilder.exe" "$root\IconBuilder.cs"
if ($LASTEXITCODE -ne 0) { throw 'Icon builder failed' }
& "$root\IconBuilder.exe" "$root\app.ico"
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed' }
$sources = @('AppNotice.cs','Bootstrap.cs','WebForm.cs','ExplorerModel.cs','AepFileItem.cs','RenderCore.cs','UiText.cs','OutputRedirect.cs','RenderLogCleanup.cs','ProcessScope.cs','FolderPicker.cs','AepVersion.cs','EngineCatalog.cs','FolderMonitor.cs','LocalBridge.cs') | ForEach-Object { Join-Path $root $_ }
$refs = @('/r:System.dll','/r:System.Xml.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll',"/r:$root\lib\Microsoft.Web.WebView2.Core.dll","/r:$root\lib\Microsoft.Web.WebView2.WinForms.dll")
$resources = @("/resource:$root\Localization\strings.json,strings.json","/resource:$root\Localization\runtime.js,localization.js","/resource:$root\ui.html,ui.html","/resource:$root\lib\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll","/resource:$root\lib\Microsoft.Web.WebView2.WinForms.dll,Microsoft.Web.WebView2.WinForms.dll","/resource:$root\lib\WebView2Loader.dll,WebView2Loader.dll")
& $compiler /nologo /target:winexe /platform:x64 /codepage:65001 /optimize+ "/win32manifest:$root\app.manifest" "/win32icon:$root\app.ico" "/out:$root\AepBatchRenderer.exe" @refs @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built: $root\AepBatchRenderer.exe"
