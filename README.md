# AEP Render

Windows 版 After Effects 批量渲染工具，支持中文、English、日本語。

## 下载

请在 [Releases](https://github.com/0living0/AEP-Render/releases) 下载完整发布包。
发布包包含程序、中英日说明视频及字幕、使用文档和源码，不包含视频制作工程。

## v3.8.9

设置新增「说明 / Instructions / 説明」标签，提供教程二维码占位和版本、作者、许可信息。二维码将于后续替换。

## 源码与构建

本仓库根目录是程序源码，Windows 下运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1` 构建。需要 Windows .NET Framework 4.x；WebView2 SDK 依赖随源码放在 lib 中，运行程序需要 Microsoft Edge WebView2 Runtime。

源码原始说明及版本历史见 [VERSION_HISTORY.md](VERSION_HISTORY.md)。

## 许可

GPL-3.0-only，详见 [LICENSE](LICENSE)、[LICENSE_INFO.md](LICENSE_INFO.md) 及 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
