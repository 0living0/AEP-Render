# 界面语言资源

`strings.json` 是应用界面文案唯一来源。每个资源键包含 `zh`、`en`、`ja` 三个非空值，参数使用 `{0}`、`{1}`，三种语言必须保留相同参数。默认语言为 zh。

- HTML：`data-i18n="资源键"`，属性使用 `data-i18n-title` 等。
- JavaScript：`t(key, ...args)` 用于显示；`C(key)` 仅用于兼容已保存的中文任务状态。
- C#：`UiText.Get(key)` / `UiText.Format(key, args)` 用于显示；`UiText.Source(key)` 用于兼容任务状态。
- CSS 状态使用 waiting / running / completed / failed 等稳定键，不依赖翻译结果。

语言预览随设置窗口生效，保存持久化，取消恢复原语言。资源在构建时嵌入 EXE，不需要用户维护额外的运行时语言文件。新增文案必须放入本文件，并运行 `node test-localization.cjs` 检查完整性与参数。

项目名、路径、用户内容、AE 原始日志不作为界面文案翻译。系统对话框由 Windows 提供的部分跟随系统语言。
