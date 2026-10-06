# Nihongo Desk Memo

C# / WPF 日语单词桌面悬浮工具。支持纯文字透明显示、单元练习、间隔复习和全局一键隐藏。

**当前开发版本：v0.3.0，可设置全局隐藏快捷键。** [每版更新内容](CHANGELOG.md) · [正式版本下载](https://github.com/LiaoYun77/NihongoDeskMemo/releases) · [迭代与发布流程](docs/RELEASING.md)。正式发布状态以 Releases 页面为准。

## 功能

- 导入 TXT / CSV 词表，支持日语单词、50 音假名读音、中文意思。
- 支持完整 XML 词库：保留单元、来源编号及原文；不同课的同形词不会互相覆盖。重复导入按来源编号跳过，保留已有编辑和复习进度；导入前自动备份到 exe 同目录的 `backups`。
- 词库搜索、新增、修改、删除，以及多选词条批量划分单元。
- 按全部词库或同时勾选多个单元抽题；可设置自动换词间隔。保留原有到期时间与难度权重。
- 四种方向：日语认中文、中文回忆日语、假名回忆汉字、汉字回忆读音。
- 日语认中文模式下假名常驻，点击文字或按 F8 显示答案，也可设置答案常显。
- 答案显示后，可用“不会 / 模糊 / 记得 / 熟练”或数字键 1–4 评级；评级按钮和数字键可一并关闭。
- 简化间隔复习：不会 5 分钟、模糊 1 天、记得 3 天、熟练 7 天；连续答对逐步延长到 60 天。
- 背景透明但文字、按钮不透明；支持置顶、拖动和调整大小。
- 自定义单键或 Ctrl / Alt / Shift 组合键全局隐藏、恢复主窗口及对话框；隐藏期间暂停换词，并保留未保存的编辑。

## 运行与编译

仅支持 Windows。当前版本保持原有 .NET Framework 4.0 目标，不需要 NuGet 包。

编译需要 .NET Framework C# 编译器和 4.0 引用程序集。脚本会检查缺少的组件，不会自动更改系统环境。

双击 `build.bat`，或者在项目目录运行：

```powershell
.\build.ps1
```

产物位于 `bin/Release/NihongoDeskMemo.exe`。也可以在支持该目标框架的 Visual Studio 中打开 `NihongoDeskMemo.csproj`。

需要带版本号的发布包时双击 `release.bat`，测试通过后在 `dist` 生成 ZIP、exe、版本信息与 SHA256 校验码。GitHub PR 自动检查，合并到 main 后自动发布尚未发布的新版本；无需手动重命名 exe 或整理更新说明。

程序运行数据与 exe 同目录：`words.xml` 是词库和复习记录，`config.xml` 是设置。仓库不包含个人数据；首次运行会读取同目录数据，或兼容读取旧版 AppData 数据。请勿同时运行多个版本操作同一份词库。

## 操作

| 操作 | 入口 |
| --- | --- |
| 显示答案 | 点击文字，或 F8 |
| 复习评级 | 答案显示后按 1 / 2 / 3 / 4，需启用评级按钮 |
| 打开设置 | 工具栏“设置”，或 Ctrl+, |
| 打开词库 | 工具栏“词库”，或 Ctrl+L |
| 固定 / 取消固定工具栏 | F9；右键文字可固定 |
| 普通隐藏 | 点击“隐藏”，留下“显示”按钮 |
| 全局隐藏 / 恢复 | 默认 F10，设置中可录入自定义单键或组合键，后台也有效 |

点击设置中的“全局隐藏快捷键”输入框，直接按下所需快捷键，例如 Q、F12、Ctrl+Q、Alt+Z、Ctrl+Shift+X，点击“保存并关闭”立即生效，重启后保留。录入期间原键暂停，离开输入框恢复；Esc 取消本次录入。新键启用失败时保留原键并提示重选。启动时 F10/F11 被占用则临时使用 Ctrl+Alt+对应键并弹窗告知。

推荐用组合键：单个字母或数字会占用其他程序正常输入；自定义为 F8/F9 会覆盖本软件原有答案/工具栏快捷键。不能只绑定 Ctrl、Alt、Shift，Win 组合及 Ctrl+Alt+Delete 等系统保留键不可绑定，裸 Esc 用于取消录入。

Windows 将 F12 保留给调试器，因此本软件通过专用线程监听所选 F12 组合，而不使用 RegisterHotKey；不记录任何键盘输入。只拦截精确匹配的组合，例如 Ctrl+F12 不拦截裸 F12。全局隐藏不需要管理员权限，但不覆盖锁屏、UAC 安全桌面、其他软件钩子冲突或程序无响应的情况。[Windows 热键约束](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)

## 导入格式

推荐使用 UTF-8 文本，每行三列，第二列是平假名 / 片假名，不是罗马音。示例见 `examples/words.txt`：

```text
勉強,べんきょう,学习
会社,かいしゃ,公司
会議,かいぎ,会议
```

兼容两列“日语单词、中文意思”，并支持 Tab 等分隔符。导入后可在词库中多选词条，批量划分单元，再到设置中选择练习范围。

完整 XML 词库通过“设置 → 导入词表”选取，根节点为 `WordDatabase`，`Words/WordItem` 包含 `Japanese`、`Pinyin`、`Chinese`、`Unit`、`SourceId`、`SourceText`。每条词条的 `SourceId` 必须非空且唯一。此格式用于保留重复词、跨课词条与来源原文，不要把个人 `words.xml` 当作可替换整个数据库的导入文件。原文缺少释义的项目应明确标注，不应自行猜测。

`tools/convert_vocabulary.py` 是针对本次标日 1–48 课文档的转换工具，使用 Python 3.9+ 标准库，保留原文及来源段落核对记录；不是通用 DOCX 排版识别器。教材原文和生成的词库不纳入仓库。

```powershell
python tools/convert_vocabulary.py "输入文档.docx" "输出目录"
# 编译测试后，额外核对这份完整教材的实际导入及存储往返：
.\bin\Tests\ImportTests.exe "输出目录\标日1-48课-完整词库.xml"
```

## 测试

```powershell
# 普通回归测试，不发送快捷键
.\build.ps1 -RunTests

# 桌面集成测试，需要交互式 Windows 桌面
.\build.ps1 -RunTests -IncludeDesktopTests
```

桌面测试会临时注册并发送 F10-F12 和 Ctrl+Shift+Q，打开测试窗口，验证后台隐藏、恢复、按键切换、F12 长按、模态窗口内容保留、快捷键冲突及换词暂停。运行前关闭本软件，确保这些按键及 Ctrl+Alt+F10 未被占用。测试数据写入 `bin/Tests`，不会写入原软件目录。`bin/Tests/GlobalHideTests.exe --registration` 单独检查任意组合注册、录入暂停恢复及冲突回退，不发送系统按键。

## 项目结构

```text
src/
  Program.cs          数据模型、XML 存储、导入器及保留的旧 WinForms 实现
  WpfMain.cs          WPF 程序入口、主窗口和复习逻辑
  WpfDialogs.cs       WPF 设置与词库管理
  GlobalHideHotkey.cs Windows 全局热键与窗口恢复
  StructuredWordImporter.cs 完整 XML 词库校验与无覆盖合并
tests/                功能回归与桌面集成测试
examples/             最小导入示例
NihongoDeskMemo.csproj Visual Studio / MSBuild 项目
build.ps1             编译与测试入口
build.bat             双击编译入口
```

启动入口固定为 `NihongoDeskMemoWpf.WpfProgram`。此次整理保留旧代码，避免为拆分目录引入行为变化。历史 exe、压缩包、截图、教材和个人 XML 不纳入版本控制。
