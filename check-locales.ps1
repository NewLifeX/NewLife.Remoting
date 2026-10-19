# 多语言词表一致性校验：语言清单、键集合、占位符、格式说明符、孤儿键
# 以基准语言文件（默认 zh-Hans.yml）为准，同一目录下所有语言必须提供完全相同的键集合
# 语言清单见 locales.json（与 NewLife.Core 对齐），各词表目录的语言文件必须与之完全一致
#
# 用法：
#   pwsh -File check-locales.ps1                                   # 本仓默认目录
#   pwsh -File check-locales.ps1 -LocaleDir src\Lib\Locales -SourceDir src\Lib
#   pwsh -File check-locales.ps1 -SkipOrphan                       # 只校验词表内部一致性
param(
    [String[]]$LocaleDir = @("NewLife.Remoting\Locales", "NewLife.Remoting.Extensions\Locales"),
    [String]$Baseline = "zh-Hans.yml",
    [String[]]$SourceDir = @("NewLife.Remoting", "NewLife.Remoting.Extensions"),
    [String]$LocaleSet = "locales.json",
    [Switch]$SkipOrphan
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
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

# 语言清单：有清单时校验各目录语言文件集合完全一致，防止漏提交或误加语言
$expectLocales = @()
if ($LocaleSet) {
    $setPath = if ([IO.Path]::IsPathRooted($LocaleSet)) { $LocaleSet } else { Join-Path $root $LocaleSet }
    if (Test-Path -LiteralPath $setPath) {
        $expectLocales = @((Get-Content -LiteralPath $setPath -Raw -Encoding UTF8 | ConvertFrom-Json).locales)
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
    $references += $reference
    foreach ($e in $reference.Errors) { $allErrors += "$($referencePath): $e" }

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
        }
    }
}

# 孤儿键：基准表中的键在源码里完全找不到（改文案后遗留的死条目）
if (!$SkipOrphan) {
    $sourceDirs = @($SourceDir | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $root $_ }
    } | Where-Object { Test-Path $_ })

    $sourceText = ($sourceDirs | ForEach-Object { Get-ChildItem $_ -Recurse -Filter *.cs } |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' } |
        ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

    foreach ($reference in $references) {
        foreach ($key in $reference.Entries.Keys) {
            # 源码中的字面量形式：制表符写作 \t，引号写作 \"
            $escaped = $key.Replace('\', '\\').Replace('"', '\"').Replace("`t", '\t').Replace("`r", '\r').Replace("`n", '\n')
            if (!$sourceText.Contains($key) -and !$sourceText.Contains($escaped)) {
                $allErrors += "孤儿键（源码中找不到）：$key"
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
