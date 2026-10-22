# 多语言词表一致性校验：语言清单、键集合、占位符、格式说明符、未翻译、孤儿键、L.T 引用覆盖
# 以基准语言文件（默认 zh-Hans.yml）为准，同一目录下所有语言必须提供完全相同的键集合
# 语言清单见 locales.json（与 NewLife.Core 对齐），各词表目录的语言文件必须与之完全一致
# 本仓要求**全部语言全量覆盖**：不使用上游的可选键豁免，任何键缺失或仅回退英文都会失败
#
# 用法：
#   pwsh -File Tools/Scripts/check-locales.ps1                     # 本仓默认目录
#   pwsh -File Tools/Scripts/check-locales.ps1 -LocaleDir src\Lib\Locales -SourceDir src\Lib
#   pwsh -File Tools/Scripts/check-locales.ps1 -SkipOrphan         # 只校验词表内部一致性
param(
    [String[]]$LocaleDir = @("NewLife.Remoting\Locales", "NewLife.Remoting.Extensions\Locales"),
    [String]$Baseline = "zh-Hans.yml",
    [String[]]$SourceDir = @("NewLife.Remoting", "NewLife.Remoting.Extensions"),
    [String]$LocaleSet = "locales.json",
    [Switch]$SkipOrphan
)

$ErrorActionPreference = "Stop"

# 脚本位于 Tools/Scripts，仓库根目录需要上溯两级
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'
$placeholder = '\{(\d+)(?:,-?\d+)?(:[^}]*)?\}'

function Unescape-Yaml([string]$text) {
    # 与源生成器保持一致：还原 \\ \" \n \r \t \uXXXX
    return [regex]::Replace($text, '\\(u[0-9a-fA-F]{4}|.)', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v.Length -eq 5) { return [string][char][Convert]::ToInt32($v.Substring(1), 16) }
        switch -CaseSensitive ($v) {
            'n' { return "`n" }
            'r' { return "`r" }
            't' { return "`t" }
            '\\' { return '\' }
            '"' { return '"' }
            default { return $m.Value }
        }
    })
}

function Get-Placeholders([string]$text) {
    $set = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        [void]$set.Add([int]$m.Groups[1].Value)
    }
    return ,$set
}

function Get-PlaceholderSpecs([string]$text) {
    $map = @{}
    foreach ($m in [regex]::Matches($text, $placeholder)) {
        $index = [int]$m.Groups[1].Value
        if (!$map.ContainsKey($index)) { $map[$index] = $m.Groups[2].Value }
    }
    return $map
}

function Read-Locale([string]$path) {
    $entries = [ordered]@{}
    $errors = @()
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        $lineNo++
        if ($line.Trim().Length -eq 0 -or $line.TrimStart().StartsWith("#")) { continue }
        $m = [regex]::Match($line, $pattern)
        if (!$m.Success) {
            $errors += "第 $lineNo 行格式非法：$line"
            continue
        }
        $key = Unescape-Yaml $m.Groups[1].Value
        if ($entries.Contains($key)) {
            $errors += "第 $lineNo 行键重复：$key"
            continue
        }
        $entries[$key] = Unescape-Yaml $m.Groups[2].Value
    }
    return @{ Entries = $entries; Errors = $errors }
}

$allErrors = @()
$allWarnings = @()
$references = @()
$localeCount = 0

# 语言清单：校验各目录语言文件集合完全一致，防止漏提交或误加语言
# 清单缺失或为空时直接报错，避免“少一份语言文件也能通过”的静默退化
$expectLocales = @()
if ($LocaleSet) {
    $setPath = if ([IO.Path]::IsPathRooted($LocaleSet)) { $LocaleSet } else { Join-Path $root $LocaleSet }
    if (!(Test-Path -LiteralPath $setPath)) {
        $allErrors += "未找到语言清单：$setPath"
    }
    else {
        $config = $null
        try {
            $config = Get-Content -LiteralPath $setPath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        catch {
            $allErrors += "语言清单解析失败：$setPath（$($_.Exception.Message)）"
        }

        if ($null -ne $config) {
            $expectLocales = @($config.locales)
            if ($expectLocales.Count -eq 0) {
                $allErrors += "语言清单未配置任何语言：$setPath"
            }
            foreach ($group in ($expectLocales | Group-Object | Where-Object { $_.Count -gt 1 })) {
                $allErrors += "语言清单存在重复项：$($group.Name)"
            }

            # 最少语言数：防止同时删除语言文件与清单项后静默减少支持语种
            $minimum = if ($null -ne $config.minimumLocales) { [int]$config.minimumLocales } else { 0 }
            if ($minimum -gt 0 -and $expectLocales.Count -lt $minimum) {
                $allErrors += "语言清单仅配置 $($expectLocales.Count) 种语言，少于要求的 $minimum 种：$setPath"
            }
        }
    }
}

foreach ($item in $LocaleDir) {
    $dir = if ([IO.Path]::IsPathRooted($item)) { $item } else { Join-Path $root $item }
    if (!(Test-Path $dir)) {
        Write-Host "未找到语言目录：$dir" -ForegroundColor Red
        exit 1
    }

    $referencePath = Join-Path $dir $Baseline
    if (!(Test-Path -LiteralPath $referencePath)) {
        Write-Host "未找到基准语言文件：$referencePath" -ForegroundColor Red
        exit 1
    }
    $reference = Read-Locale $referencePath
    foreach ($e in $reference.Errors) { $allErrors += "$($referencePath): $e" }
    $references += $reference

    $present = @(Get-ChildItem $dir -Filter *.yml | ForEach-Object { $_.BaseName })
    if ($expectLocales.Count -gt 0) {
        foreach ($code in $expectLocales) {
            if ($present -notcontains $code) { $allErrors += "$dir 缺少语言文件：$code.yml" }
        }
        foreach ($code in $present) {
            if ($expectLocales -notcontains $code) { $allErrors += "$dir 存在清单外的语言文件：$code.yml" }
        }
    }

    foreach ($file in Get-ChildItem $dir -Filter *.yml | Sort-Object Name) {
        $localeCount++
        if ($file.Name -eq $Baseline) { continue }
        $locale = Read-Locale $file.FullName
        foreach ($e in $locale.Errors) { $allErrors += "$($file.FullName): $e" }

        foreach ($key in $reference.Entries.Keys) {
            if (!$locale.Entries.Contains($key)) {
                $allErrors += "$($file.FullName): 缺少键 $key"
            }
        }
        foreach ($key in $locale.Entries.Keys) {
            if (!$reference.Entries.Contains($key)) {
                $allErrors += "$($file.FullName): 多余键 $key"
                continue
            }

            $value = $locale.Entries[$key]
            if ($value.Trim().Length -eq 0) {
                $allErrors += "$($file.FullName): 空译文 → $key"
                continue
            }

            $expected = Get-Placeholders $key
            $actual = Get-Placeholders $value
            if (!$expected.SetEquals($actual)) {
                $allErrors += "$($file.FullName): 占位符不一致 → $key"
                continue
            }

            # 格式说明符只在译文保留该占位符时比对，缺失时仅告警
            $enSpecs = Get-PlaceholderSpecs $key
            $trSpecs = Get-PlaceholderSpecs $value
            foreach ($index in $enSpecs.Keys) {
                if ($trSpecs.ContainsKey($index) -and $trSpecs[$index] -ne $enSpecs[$index]) {
                    $allWarnings += "$($file.FullName): 格式说明符不一致 → $key"
                    break
                }
            }

            # 未翻译：句子级词条（3 个及以上空白分隔片段，或 2 个片段且以句末标点结尾）的译文不得与英文原文完全相同。
            # 单词型技术词（MD5、Encoder、Handler 等）在部分语言中就是本地通行写法，不在此列。
            $words = @($key -split '\s+' | Where-Object { $_ })
            $isSentence = $words.Count -ge 3 -or ($words.Count -ge 2 -and $key -match '[.!?。！？]$')
            if ($value -ceq $key -and $isSentence) {
                $allErrors += "$($file.FullName): 未翻译（译文与英文原文相同）→ $key"
            }
        }
    }
}

# 孤儿键：基准表中的键在源码里找不到对应的完整字符串字面量（改文案后遗留的死条目）
# 必须按带引号的完整字面量匹配：仅做子串匹配时，"[{0}]Cluster balancing: {1}" 会掩盖
# "Cluster balancing: {1}" 这类前缀被改掉却未同步词表的错位。
# 源文范围含 .cs 与 .csproj：程序集标题/描述等元数据写在 csproj 中（XML 元素值），同样是词表源文。
if (!$SkipOrphan) {
    $sourceDirs = @($SourceDir | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    } | Where-Object { Test-Path $_ })

    $sourceText = ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Filter *.cs } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' } |
        ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    $projectText = ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Filter *.csproj } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    foreach ($reference in $references) {
        foreach ($key in $reference.Entries.Keys) {
            # 源码中的字面量形式：制表符写作 \t，引号写作 \"
            $escaped = $key.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace("`r", '\r').Replace("`n", '\n')
            $inCode = $sourceText.Contains('"' + $escaped + '"')
            $inProject = $projectText.Contains('>' + $key + '<')
            if (!$inCode -and !$inProject) {
                $allErrors += "孤儿键（源码中找不到完整字符串字面量）：$key"
            }
        }
    }
}

# 源码引用校验：L.T("...") 的键必须存在于同组件的基准词表
# 词表目录与源码目录按参数顺序一一对应；两者数量不一致时无法归属，直接报错
if (!$SkipOrphan) {
    if ($LocaleDir.Count -ne $SourceDir.Count) {
        $allErrors += "LocaleDir 与 SourceDir 数量不一致，无法校验 L.T 引用：$($LocaleDir.Count) != $($SourceDir.Count)"
    }
    else {
        for ($i = 0; $i -lt $LocaleDir.Count; $i++) {
            $srcDir = if ([IO.Path]::IsPathRooted($SourceDir[$i])) { $SourceDir[$i] } else { Join-Path $root $SourceDir[$i] }
            if (!(Test-Path $srcDir)) { continue }

            $sourceText = (Get-ChildItem $srcDir -Recurse -Filter *.cs |
                Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' } |
                ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

            # 覆盖 L.T / L.TLog / Localization.Translate(ForLog)，含显式传入区域性作为首参的重载
            foreach ($m in [regex]::Matches($sourceText, '(?:L\.TLog|L\.T|Localization\.Translate(?:ForLog)?)\s*\(\s*(?:[A-Za-z_][\w\.]*\s*,\s*)?"((?:[^"\\]|\\.)*)"')) {
                $key = Unescape-Yaml $m.Groups[1].Value
                if (!$references[$i].Entries.Contains($key)) {
                    $allErrors += "缺少词条（源码已调用 L.T/Translate）：$key（$($LocaleDir[$i])）"
                }
            }
        }
    }
}

# 运行时消息覆盖：日志/异常/控制台/埋点调用中的自然语言字面量必须在基准词表
# 纯格式串（{0}=>{1}、rpc:、cmd:）与单词型技术标签不含自然语言，跳过
if (!$SkipOrphan -and $LocaleDir.Count -eq $SourceDir.Count) {
    # 埋点标签（AppendTag/NewSpan）与开发断言保持稳定英文，不要求翻译，故不纳入覆盖校验
    $callPattern = 'Log\??\.(Write|Error|Info|Debug|Warn|Fatal)\(|WriteLog\(|XTrace\.|Console\.Write(Line)?\(|WriteInfoEvent\(|WriteErrorEvent\(|NewError\(|OnWrite\(|BadRequest\(|throw new (?:XException|ApiException)\('
    for ($i = 0; $i -lt $LocaleDir.Count; $i++) {
        $srcDir = if ([IO.Path]::IsPathRooted($SourceDir[$i])) { $SourceDir[$i] } else { Join-Path $root $SourceDir[$i] }
        if (!(Test-Path $srcDir)) { continue }

        foreach ($file in (Get-ChildItem $srcDir -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' })) {
            $lines = [IO.File]::ReadAllLines($file.FullName)
            for ($n = 0; $n -lt $lines.Count; $n++) {
                $line = $lines[$n]
                $text = $line.Trim()
                if ($text.Length -eq 0 -or $text.StartsWith('//') -or $text.StartsWith('///') -or $text.StartsWith('[')) { continue }
                if ($line -notmatch $callPattern) { continue }
                if ($line -match '\$"' -or $line -match 'L\.T(?:Log)?\(') { continue }

                $m = [regex]::Match($line, '"((?:[^"\\]|\\.)*)"')
                if (!$m.Success) { continue }
                $message = Unescape-Yaml $m.Groups[1].Value
                $words = @($message -split '\s+' | Where-Object { $_ -match '^[A-Za-z]{3,}' })
                if ($words.Count -lt 2) { continue }
                if (!$references[$i].Entries.Contains($message)) {
                    $allErrors += "缺少词条（运行时消息）：$message（$($file.FullName):$($n + 1)）"
                }
            }
        }
    }
}

foreach ($e in $allErrors) { Write-Host $e -ForegroundColor Red }
foreach ($w in $allWarnings) { Write-Host $w -ForegroundColor Yellow }

if ($allErrors.Count -gt 0) {
    Write-Host "多语言词表校验未通过：$localeCount 个语言文件，$($allErrors.Count) 个错误" -ForegroundColor Red
    exit 1
}

$total = ($references | ForEach-Object { $_.Entries.Count } | Measure-Object -Sum).Sum
Write-Host "多语言词表校验通过：$localeCount 个语言文件，基准 $total 条" -ForegroundColor Green
