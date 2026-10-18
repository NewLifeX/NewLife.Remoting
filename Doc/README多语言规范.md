# README 多语言规范

根目录的 `Readme.MD` 与 `README.*.md` 是同一份说明文档的多语种版本。语言切换行由脚本从 `readme-languages.json` 生成，本文规定切换行的**顺序**与**维护方式**。

## 定序规则

1. **固定前三**：中文（`Readme.MD`，权威版与 GitHub 默认落地页）→ 繁體中文（`README.zh-TW.md`，与简体同源，紧邻便于对照）→ English（通用语，非中文读者的第一选择）。
2. **其余按技术内容语种规模排序**：以 W3Techs「内容语种占比」（全球 top 1000 万站点首页）为依据，一次定序后**冻结**。
3. **新语种一律追加到末尾**，不做全局重排（同一语言的不同书写系统例外：紧跟其主语言，如 `繁體中文` 紧随简体）；只有拿到更可靠的读者分布数据时，才允许做一次整体校正。

### 当前顺序

| # | 语种 | 文件 | 依据（W3Techs，2025-12-03） |
|---|------|------|------|
| 1 | 中文（简体） | `Readme.MD` | 权威版与默认落地页，固定第一位 |
| 2 | 繁體中文 | `README.zh-TW.md` | 与简体同源，紧随简体便于对照 |
| 3 | English | `README.en.md` | 通用语，固定第三位 |
| 4 | Español | `README.es.md` | 6.0% |
| 5 | Deutsch | `README.de.md` | 5.9% |
| 6 | 日本語 | `README.ja.md` | 5.1% |
| 7 | Français | `README.fr.md` | 4.5% |
| 8 | Português | `README.pt.md` | 4.1% |
| 9 | Русский | `README.ru.md` | 3.7% |
| 10 | 한국어 | `README.ko.md` | 0.8% |
| 11 | العربية | `README.ar.md` | 0.5% |

> 运行时本地化用 `zh-Hant` 统一承接 `zh-TW` / `zh-HK` / `zh-MO`，见 [多语言本地化接入](多语言本地化接入.md)。

## 为什么不按语言人口排序

- README 的读者是**开发者**，语种价值取决于「多少开发者能读」，而不是「多少人会说」。
- 人口数据逐年变化，按人口排意味着反复重排；而切换行只有 10 个链接、一屏可扫完，**排序的边际收益很低，重排成本（11 个文件重复编辑与评审噪声）很高**。
- 固定前一/前三可避免通用语被某个大语种挤到后面。

## 维护方式

- **单一事实源**：`readme-languages.json`（`code` / `name` / `file`，数组顺序即展示顺序）。
- **生成与校验**：`sync-readme-languages.ps1` 按配置重写各 README 的切换块；加 `-Check` 只校验不写入，不一致时退出码 1。
- **指标同步**：`readme-metrics.json` 保存会随发布变化的数字（AOT 冒烟场景数、TFM 数量、运行时词表语言数），`sync-readme-metrics.ps1` 重写徽章块与 `<!--aot-scenarios:start-->…<!--aot-scenarios:end-->`、`<!--aot-smoke:start-->…<!--aot-smoke:end-->` 标记内的数字。
- **CI 门禁**：`.github/workflows/readme.yml` 在 README、配置或脚本变动时执行两项 `-Check`，顺序不一致、副本漂移或指标过期都会失败。
- 各 README 只有下面标记块之间的内容由脚本生成，其余内容不受影响：

```markdown
<!-- lang:start -->
🌐 [中文](Readme.MD) | [English](README.en.md) | ...
<!-- lang:end -->

<!-- metrics:start -->
[![AOT smoke](...)](...)
[![TFM](...)](...)
[![locales](...)](...)
<!-- metrics:end -->
```

## 新增一种语言

1. 翻译整篇 README，文件名用 `README.{BCP 47 语言标签}.md`（如 `README.zh-TW.md`；中文版固定为 `Readme.MD`）。
2. 在 `readme-languages.json` 的 `languages` 数组**末尾**追加 `{ "code", "name", "file" }`，`name` 用本族语名称。
3. 补齐徽章块（`<!-- metrics:start -->` 起）与切换行标记块，正文开头写明「本翻译可能滞后，以中文版为准」。
4. 运行 `pwsh -File sync-readme-languages.ps1` 生成切换行，再运行 `pwsh -File sync-readme-metrics.ps1` 对齐徽章。
5. 两项 `-Check` 均通过后提交；根目录新增了 README 却没登记到配置，脚本会直接报错。

## 徽章规则（本仓取值）

必选：License、NuGet 下载量/版本/预发布（两个包各一组）、NativeAOT 与 test 工作流徽章、TFM、平台、贡献者。
可选：AOT 冒烟场景数、locales 数量——两者均由 `readme-metrics.json` 驱动，禁止手工改数字。

- 能用动态 GitHub Actions 徽章的优先用动态；静态数字必须配同步脚本与 CI 校验。
- 徽章顺序：许可证 → NuGet → CI → 指标 → 平台/指令集 → 贡献者。
- 语言数量用 `locales-{n}`，不要写"支持 N 种语言"之类正文数字；数字变化时只改配置。

## 展示约定

- 语言名一律用**本族语名称**（`日本語`、`Español`），不用英文名，也不用国旗——语言 ≠ 国家。
- 当前页面的语种在切换行中省略自身，避免点到当前页。
- 分隔符统一为 ` | `，行首保留 `🌐`。
