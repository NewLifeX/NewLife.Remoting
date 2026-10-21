# 收集允许部分语言缺失的可选键（元数据、语言名等），写入各组件的 metadata-keys.json
# 默认只输出「在部分语言中缺失」的键，并保留清单中已有的手工条目；
# 加 -All 输出源码中扫描到的全部元数据键（用于人工筛查）。
# 源文范围：csproj 的 AssemblyTitle/Description，以及 .cs 的 [DisplayName]/[Description] 特性参数。
#
# 用法：
#   pwsh -File collect-metadata-keys.ps1
#   pwsh -File collect-metadata-keys.ps1 -All
#   pwsh -File collect-metadata-keys.ps1 -OutFile metadata-coverage.csv
param(
    [String[]]$LocaleDir = @("NewLife.Remoting\Locales", "NewLife.Remoting.Extensions\Locales"),
    [String[]]$SourceDir = @("NewLife.Remoting", "NewLife.Remoting.Extensions"),
    [String]$Output = "metadata-keys.json",
    [String]$Baseline = "zh-Hans",
    [Switch]$All,
    [String]$OutFile
)

$ErrorActionPreference = "Stop"

if ($LocaleDir.Count -ne $SourceDir.Count) {
    Write-Host "LocaleDir 与 SourceDir 数量不一致：$($LocaleDir.Count) != $($SourceDir.Count)" -ForegroundColor Red
    exit 1
}

$root = $PSScriptRoot
$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'

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

for ($i = 0; $i -lt $LocaleDir.Count; $i++) {
    $dir = if ([IO.Path]::IsPathRooted($LocaleDir[$i])) { $LocaleDir[$i] } else { Join-Path $root $LocaleDir[$i] }
    $srcDir = if ([IO.Path]::IsPathRooted($SourceDir[$i])) { $SourceDir[$i] } else { Join-Path $root $SourceDir[$i] }
    if (!(Test-Path $dir)) {
        Write-Host "未找到语言目录：$dir" -ForegroundColor Red
        exit 1
    }
    $name = Split-Path $dir -Parent | Split-Path -Leaf
    $outputPath = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $dir $Output }

    # 扫描源码：csproj 的程序集元数据与 .cs 的显示名/描述特性（跳过注释行，避免把注释掉的特性当成源文）
    $attributeKeys = [Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
    if (Test-Path $srcDir) {
        foreach ($file in (Get-ChildItem $srcDir -Recurse -Include *.cs, *.csproj |
            Where-Object { $_.FullName -notmatch '\\(obj|bin|Locales)\\' })) {
            if ($file.Extension -eq ".csproj") {
                $text = [IO.File]::ReadAllText($file.FullName)
                foreach ($m in [regex]::Matches($text, '<(?:AssemblyTitle|Description)>([^<]+)</(?:AssemblyTitle|Description)>')) {
                    $value = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value.Trim())
                    if ($value.Length -gt 0) { [void]$attributeKeys.Add($value) }
                }
                continue
            }

            $inBlock = $false
            foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
                $trimmed = $line.Trim()
                if ($inBlock) {
                    if ($trimmed.Contains("*/")) { $inBlock = $false }
                    continue
                }
                if ($trimmed.StartsWith("/*")) { $inBlock = !$trimmed.Contains("*/"); continue }
                if ($trimmed.StartsWith("//") -or $trimmed.StartsWith("///")) { continue }

                foreach ($m in [regex]::Matches($trimmed, '\[(?:DisplayName|Description)\("((?:[^"\\]|\\.)*)"')) {
                    $value = $m.Groups[1].Value.Replace('\"', '"').Replace('\\', '\')
                    if ($value.Length -gt 0) { [void]$attributeKeys.Add($value) }
                }
            }
        }
    }

    # 读取各语言词条
    $tables = [ordered]@{}
    foreach ($file in Get-ChildItem $dir -Filter *.yml | Sort-Object Name) {
        $entries = [Collections.Generic.HashSet[String]]::new([StringComparer]::Ordinal)
        foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
            $m = [regex]::Match($line, $pattern)
            if ($m.Success) { [void]$entries.Add((Unescape-Yaml $m.Groups[1].Value)) }
        }
        $tables[$file.BaseName] = $entries
    }

    # 基准语言必须覆盖全部元数据键
    $base = $tables[$Baseline]
    if ($null -eq $base) {
        Write-Host "未找到基准语言：$Baseline.yml（$dir）" -ForegroundColor Red
        exit 1
    }
    $missingInBaseline = @($attributeKeys | Where-Object { !$base.Contains($_) } | Sort-Object)

    # 默认输出的键：基准语言里有，但不是每个语言都有
    $partial = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
    foreach ($key in $attributeKeys) {
        if (!$base.Contains($key)) { continue }
        foreach ($culture in $tables.Keys) {
            if ($culture -eq "en") { continue }
            if (!$tables[$culture].Contains($key)) { [void]$partial.Add($key); break }
        }
    }

    # 保留清单中已有的条目（语言名等非特性来源的可选键）
    $existing = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
    if (Test-Path -LiteralPath $outputPath) {
        foreach ($item in (Get-Content -LiteralPath $outputPath -Encoding UTF8 -Raw | ConvertFrom-Json).keys) {
            if (![String]::IsNullOrEmpty($item)) { [void]$existing.Add($item) }
        }
    }

    $keys = [Collections.Generic.SortedSet[String]]::new([StringComparer]::Ordinal)
    if ($All) {
        foreach ($key in $attributeKeys) { [void]$keys.Add($key) }
    }
    else {
        foreach ($key in $partial) { [void]$keys.Add($key) }
    }
    foreach ($key in $existing) { [void]$keys.Add($key) }

    $json = [ordered]@{
        "comment" = "允许只出现在部分语言的可选键（元数据、语言名等）。由 collect-metadata-keys.ps1 生成，缺失时回退源文。"
        "updated" = (Get-Date -Format "yyyy-MM-dd")
        "keys"    = @($keys)
    }
    [IO.File]::WriteAllText($outputPath, ($json | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))

    if ($OutFile) {
        $rows = @()
        foreach ($key in $attributeKeys) {
            $row = [ordered]@{ Key = $key }
            foreach ($culture in $tables.Keys) { $row[$culture] = if ($tables[$culture].Contains($key)) { "Y" } else { "" } }
            $rows += [pscustomobject]$row
        }
        $csv = if ([IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $root $OutFile }
        if ($LocaleDir.Count -gt 1) { $csv = [IO.Path]::Combine([IO.Path]::GetDirectoryName($csv), "$([IO.Path]::GetFileNameWithoutExtension($csv)).$name.csv") }
        $rows | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
        Write-Host "已导出覆盖矩阵：$csv"
    }

    Write-Host "[$name] 可选键清单：$($keys.Count) 条 → $outputPath"
    Write-Host "[$name] 源码元数据键：$($attributeKeys.Count) 条"
    if ($missingInBaseline.Count -gt 0) {
        Write-Host "[$name] 基准语言缺少以下元数据键，请补入 $Baseline.yml：" -ForegroundColor Yellow
        foreach ($key in $missingInBaseline) { Write-Host "  $key" -ForegroundColor Yellow }
    }
}
