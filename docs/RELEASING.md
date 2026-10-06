# 轻量版本与迭代流程

## 你怎么看更新

- 软件设置标题显示当前版本，例如 `v0.2.0`。
- [CHANGELOG.md](../CHANGELOG.md) 按“新增 / 改进 / 修复 / 升级注意”说明每版变化。
- [GitHub Releases](https://github.com/LiaoYun77/NihongoDeskMemo/releases) 是正式版本下载入口，包含对应 Tag、中文说明、exe、ZIP 和 SHA256 校验码。
- PR 保存这次需求、实现范围、测试结果及合并记录；不再只留下“上传了代码”。

## 每轮流程

1. 从 main 新建短期功能分支，明确需求与验收条件。
2. 实现和测试，更新 `src/VersionInfo.cs` 中唯一版本号及 `CHANGELOG.md` 对应条目。
3. 提交 PR，自动执行配置迁移、抽题范围、导入保护和设置布局测试，并生成候选包。
4. 检查通过后合并。main 的自动流程再次构建测试，然后发布新版本。
5. 核对 Release 和资产。后续修复使用新版本，不覆盖旧 Tag 或发布包。

平时由开发助手维护这些记录，你只需提出需求并查看版本说明。需要自己打包时，双击 `release.bat`，它会先编译和测试，再生成 `dist` 下的版本包；本地打包不会自动上传或发布。

## 版本规则

- `0.2.0 → 0.3.0`：新增功能。
- `0.2.0 → 0.2.1`：修复和小调整。
- 将来有不兼容的数据格式变化时，明确升级与回退步骤，再决定主版本升级。
- v0.2.0 是第一个正式编号版本；之前的提交仅作为历史基线记录。
- PR 改动 src 时必须提高版本号，并有相应更新日志；文档更新不重复发布已有版本。

## 自动化与边界

- `.github/workflows/build-release.yml`：Windows 构建和测试、候选包、main 发布。
- GitHub Actions 使用临时 `GITHUB_TOKEN`，不需要保存个人访问令牌。第三方 Actions 固定到完整提交 SHA。
- CI 使用微软 .NET Framework 4.0 引用程序集；不升级软件目标框架。
- 包内只放 exe、说明、版本/提交信息和最小示例，不带个人 XML、教材、完整词库或测试数据。
- F10-F12 原生快捷键测试需要交互式桌面，CI 不模拟系统按键；本地修改热键相关代码时单独运行 `build.bat -IncludeDesktopTests`。
- 分支保护需要仓库设置权限单独开启；此文档不表示已经强制启用。建议 main 禁止强推，并将 `build` 检查设为必需。
- 自动化失败时保留失败记录，修复后重新运行；不将“上传成功”等同于“发布成功”。

## 参考

- [GitHub Flow](https://docs.github.com/en/get-started/using-github/github-flow)：短期分支与 PR。
- [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)：面向用户的分类更新记录。
- [Microsoft PowerToys Releases](https://github.com/microsoft/PowerToys/releases)：版本说明与可执行资产在同一发布页面。

这里采用适合个人小软件的精简版，不引入长期 develop/release 多分支维护。
