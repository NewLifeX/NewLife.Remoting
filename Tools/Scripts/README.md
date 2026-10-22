# Tools/Scripts

本目录存放仓库级校验与同步脚本。所有脚本默认把仓库根目录作为工作目录（脚本位于 `Tools/Scripts`，自动上溯两级），也可用 `-Root` 指定其它仓库根目录。

| 脚本 | 作用 | 常用命令 |
|------|------|----------|
| `check-locales.ps1` | 校验语言清单与各 `Locales/*.yml` 的键集合、空译文、占位符、格式说明符、未翻译句条、孤儿键、`L.T` 缺少词条与日志/异常/事件运行时消息覆盖 | `pwsh -File Tools/Scripts/check-locales.ps1` |
| `check-english-exceptions.ps1` | 检查运行时消息（异常、日志、控制台、埋点标签）是否全部为英文；支持盘点与导出 CSV | `pwsh -File Tools/Scripts/check-english-exceptions.ps1` |
| `sync-readme-languages.ps1` | 按 `readme-languages.json` 重写各 README 的语言切换行；`-Check` 只校验 | `pwsh -File Tools/Scripts/sync-readme-languages.ps1 -Check` |
| `sync-readme-metrics.ps1` | 按 `readme-metrics.json` 同步 README 指标徽章与数字；`-Check` 只校验 | `pwsh -File Tools/Scripts/sync-readme-metrics.ps1 -Check` |
| `sync-readme-package.ps1` | 从 `README.md` 生成 `README.nuget.md`；`-Check` 校验生成物 | `pwsh -File Tools/Scripts/sync-readme-package.ps1 -Check` |

## CI 门禁

- `.github/workflows/test.yml` 在构建后执行 `check-english-exceptions.ps1` 与 `check-locales.ps1`。
- `.github/workflows/readme.yml` 在 README、语言/指标配置或脚本变动时执行三项 `-Check`。

## 新增词条

1. 源码中的运行时消息使用英文模板；异常用 `L.T(...)`，日志用格式字符串 + 参数。
2. 在对应组件 `Locales/` 下的 31 个语言文件各加一行，键为英文原文。
3. 运行 `check-locales.ps1` 与 `check-english-exceptions.ps1`，通过后提交。

## 语言清单

`locales.json` 的 `locales` 数组是语言集合的唯一事实源，`minimumLocales` 锁定最低语言数（本仓 31）。增删语言时先改清单，再补/删对应 `Locales/<code>.yml`；`check-locales.ps1` 会校验每个词表目录与清单完全一致，清单缺失、重复、语言数不足或译文为空都会失败。

详细约定见 `Doc/多语言本地化接入.md` 与 `Doc/README多语言规范.md`。
