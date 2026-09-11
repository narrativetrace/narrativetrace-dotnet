<!-- source: documentation/guides/msbuild-cli.md blob a95d937be511 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — MSBuild 与 CLI 指南

[English](../msbuild-cli.md) | [Español](../es/guia-de-msbuild-y-cli.md) | [Português](../pt-BR/guia-de-msbuild-e-cli.md) | **简体中文**

NarrativeTrace 提供两个构建集成面：命令行工具 `dotnet-narrativetrace`，
以及包装了它的 `NarrativeTrace.MSBuild` 包。两者共同把命名清晰度变成构建
门禁，并把追踪配置转发给测试宿主 — 这是 JVM 侧 Gradle 插件在 `.NET` 中的
对应物。

这种分工是有意的：**所有决策都在 CLI 里**；MSBuild 包只是一层薄封装，仅
声明默认值并调用该工具。先学会 CLI，MSBuild 的接线自然就清楚了。

## 安装工具

```bash
# 全局工具：
dotnet tool install --global NarrativeTrace.Cli

# …或者添加到本地清单(为了 CI 可复现性，推荐这样做)：
dotnet new tool-manifest
dotnet tool install NarrativeTrace.Cli
```

MSBuild 目标按名称(`dotnet-narrativetrace`)调用该工具，因此在运行
`ClarityScan` / `ClarityCheck` 之前，它必须位于 `PATH` 上(全局安装)或已
从本地清单还原。

## CLI 子命令

`dotnet-narrativetrace <子命令> [选项]`。不带子命令 — 或子命令未知 —
时，工具会打印用法并以 `2` 退出。选项按 `--名称 值` 成对解析(像
`--warn-only` 这样的开关不带值)。

### `clarity-scan`

对编译好的程序集做仅反射扫描，生成清晰度报告。它在仅元数据的上下文中加载
程序集且从不执行它，因此在 CI 中是安全的。

```bash
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll \
    --output-dir narrativetrace \
    --format both
```

| 选项 | 是否必填 | 默认值 | 含义 |
|---|---|---|---|
| `--assembly <路径>` | 是 | — | 要扫描的程序集。 |
| `--output-dir <目录>` | 否 | `.` | 写入报告的目录(不存在则创建)。 |
| `--format <both\|md\|json>` | 否 | `both` | `json` 写出 `clarity-results.json`；`md` 写出 `clarity-report.md`；`both` 两者都写。 |

退出码：

| 码 | 何时 |
|---|---|
| `0` | 扫描完成并写出了报告。 |
| `1` | 找不到程序集文件。 |
| `2` | 缺少 `--assembly`，或 `--format` 取值未知。 |

### `clarity-aggregate`

把某个目录下按测试生成的 `*.clarity.json` 产物(由测试框架集成在运行时
写出)合并成单个 `clarity-results.json` 信封。当你希望门禁评分的是**真实
捕获到的追踪** — 真实的调用深度与嵌套 — 而不是深度为 1 的静态反射扫描
时，使用它。

```bash
dotnet-narrativetrace clarity-aggregate \
    --input-dir narrativetrace \
    --output-dir narrativetrace
```

| 选项 | 是否必填 | 默认值 | 含义 |
|---|---|---|---|
| `--input-dir <目录>` | 是 | — | 用于查找 `*.clarity.json` 文件的目录。 |
| `--output-dir <目录>` | 否 | `--input-dir` 的取值 | `clarity-results.json` 的写出位置。 |

退出码：

| 码 | 何时 |
|---|---|
| `0` | 聚合完成(即使没有匹配到任何文件)。 |
| `1` | 找不到输入目录。 |
| `2` | 缺少 `--input-dir`。 |

### `clarity-check`

门禁本身。解析 `clarity-results.json` 信封，当任一场景评分低于
`--min-score`，或 HIGH 严重级别问题多于 `--max-high-issues`(统计时不区分
大小写)时失败。

```bash
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 \
    --max-high-issues 0
```

| 选项 | 是否必填 | 默认值 | 含义 |
|---|---|---|---|
| `--results <路径>` | 是 | — | `clarity-results.json` 信封的路径。 |
| `--min-score <x>` | 否 | `0.0` | 总体评分低于该值的场景判为失败。 |
| `--max-high-issues <n>` | 否 | `2147483647`(`int.MaxValue`) | HIGH 问题多于该值的场景判为失败。 |
| `--warn-only` | 否 | 关闭 | 把门禁失败降级为警告(退出码 `0`)。 |

退出码：

| 码 | 何时 |
|---|---|
| `0` | 门禁通过，**或**指定了 `--warn-only`，**或**结果文件缺失(缺失会被跳过而不是失败 — 尚未扫描过的项目不会让 CI 挂掉)。 |
| `1` | 门禁失败(某个场景低于 `--min-score` 或超过 `--max-high-issues`)。 |
| `2` | 缺少 `--results`，或结果文件是格式错误的 JSON。 |

`--min-score` 与 `--max-high-issues` 按不变文化(invariant culture)解析
数值；无法解析的取值会回退到默认值而不是报错。

## MSBuild 集成

添加仅构建期的包。`PrivateAssets="all"` 可以让它不出现在你自己包的传递
依赖中：

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.3"
                  PrivateAssets="all" />
```

### 属性

可在使用方项目中或命令行上(`/p:名称=值`)覆盖其中任何一项。该包只声明
默认值 — 所有阈值逻辑都在 CLI 中。

| 属性 | 默认值 | 用途 |
|---|---|---|
| `NarrativeTraceOutput` | `false` | 为 `true` 时，在 `VSTest` 期间把 `NARRATIVETRACE_*` 转发给测试宿主。 |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | 扫描/聚合结果与追踪的写入位置。 |
| `NarrativeTraceFormat` | `markdown` | 追踪输出格式。有效值：`markdown`、`text`、`mermaid`、`plantuml`。 |
| `NarrativeTraceLevel` | `DETAIL` | 测试宿主的捕获级别。有效值：`OFF`、`ERRORS`、`SUMMARY`、`NARRATIVE`、`DETAIL`。 |
| `NarrativeTraceClaritySource` | `scan` | 门禁评分的数据来源：`scan`(静态反射扫描)或 `runtime`(聚合按测试捕获的追踪)。 |
| `ClarityMinScore` | `0.0` | 转发给 `clarity-check --min-score`。 |
| `ClarityMaxHighIssues` | `2147483647` | 转发给 `clarity-check --max-high-issues`。 |
| `ClarityWarnOnly` | `false` | 为 `true` 时转发 `--warn-only`(门禁失败变为警告)。 |

`NarrativeTraceLevel`、`NarrativeTraceFormat` 或
`NarrativeTraceClaritySource` 的无效取值会**尽早**让构建失败(通过
`_NarrativeTraceValidateConfig` 目标，在 `Build` / `VSTest` /
`ClarityScan` 之前)，并给出清晰的消息，而不是把拼写错误悄悄转发给测试
宿主。

> 这里 `NarrativeTraceFormat` 的允许值(`markdown`/`text`/`mermaid`/
> `plantuml`)是构建侧的追踪渲染格式，它与[配置指南](配置指南.md)中记录的
> 运行时 `NARRATIVETRACE_FORMAT` 环境变量取值
> (`Markdown`/`Text`/`Prose`/`Json`)并不相同。

### 目标

| 目标 | 依赖 | 运行什么 |
|---|---|---|
| `ClarityScan` | `Build` | `clarity-scan --assembly $(TargetPath) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityAggregate` | — | `clarity-aggregate --input-dir $(NarrativeTraceOutputDir) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityCheck` | `ClarityScan` 或 `ClarityAggregate`(取决于 `NarrativeTraceClaritySource`) | `clarity-check --results … --min-score … --max-high-issues … [--warn-only]` |

把门禁作为构建的一部分运行：

```bash
# 静态扫描来源(默认)：
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0

# 用真实捕获的追踪来评分，而不是静态扫描：
dotnet test   /p:NarrativeTraceOutput=true
dotnet build  /t:ClarityCheck /p:NarrativeTraceClaritySource=runtime /p:ClarityMinScore=0.80
```

当 `NarrativeTraceClaritySource=runtime` 时，`ClarityCheck` 依赖
`ClarityAggregate`(它合并按测试生成的 `*.clarity.json` 文件)，而不是
`ClarityScan`。请先以 `NarrativeTraceOutput=true` 运行测试套件来产出这些
按测试的文件，聚合才有内容可合并。

### 增量检查(stamp 文件)

`ClarityCheck` 是增量的。它声明了：

- **输入：** `$(NarrativeTraceOutputDir)/clarity-results.json`
- **输出：** `$(NarrativeTraceOutputDir)/clarity-check.stamp`

检查成功后它会 touch 该 `.stamp` 文件。下次构建时，MSBuild 比较时间戳，
并在 **`clarity-results.json` 未变化时完全跳过重复检查** — 因此没有改动
的项目不会为门禁付两次代价。删除 stamp(或输出目录)即可强制重新检查。

### 向测试宿主输出追踪

当 `NarrativeTraceOutput=true` 时，`_NarrativeTraceExportEnv` 目标(在
`VSTest` 之前运行)会把运行时配置追加到 `VSTestEnvironmentVariables`，于是
测试宿主会看到：

```
NARRATIVETRACE_OUTPUT=true
NARRATIVETRACE_OUTPUT_DIR=$(NarrativeTraceOutputDir)
NARRATIVETRACE_FORMAT=$(NarrativeTraceFormat)
NARRATIVETRACE_LEVEL=$(NarrativeTraceLevel)
```

```bash
dotnet test /p:NarrativeTraceOutput=true /p:NarrativeTraceLevel=NARRATIVE
```

## CI 配方

### 静态清晰度门禁(无需运行测试)

扫描构建产物程序集，低于阈值即失败 — 最轻量的门禁。

```bash
dotnet build -c Release
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll --output-dir narrativetrace
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

或者，让 MSBuild 用一条命令驱动整条链路：

```bash
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### 运行时清晰度门禁(为真实追踪评分)

先运行测试以产出按测试的捕获，然后基于聚合结果做门禁：

```bash
dotnet test /p:NarrativeTraceOutput=true
dotnet build /t:ClarityCheck \
    /p:NarrativeTraceClaritySource=runtime \
    /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### 逐步收紧而不打断构建

在把分数拉上去的过程中，先把清晰度作为警告输出，达标后再关掉
`ClarityWarnOnly`：

```bash
dotnet build /t:ClarityCheck \
    /p:ClarityMinScore=0.85 /p:ClarityWarnOnly=true
```

## 另请参阅

- [安装指南](安装指南.md) — 包与集成方式(方式 F 与 G)
- [配置指南](配置指南.md) — 追踪级别、环境变量，以及 MSBuild 属性参考
- [清晰度指南](清晰度指南.md) — 评分模型、`clarity-results.json` 契约与门禁语义
