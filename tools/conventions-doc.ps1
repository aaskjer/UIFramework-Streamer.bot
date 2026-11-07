<#
.SYNOPSIS
  Writes the generated part of docs/Conventions.md from conventions/keys.json and conventions/lang/*.json.

.DESCRIPTION
  Everything between the markers <!-- generated:start --> and <!-- generated:end --> is replaced: the standard
  pages, the known keys with kind, default, page, aliases and English label, the placeholders, and how far each
  language file covers the English one. Run it after changing a conventions file; -Check only reports whether the
  doc is current (exit code 1 when not), for CI.
#>
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$doc = Join-Path $root 'docs/Conventions.md'

function Read-Json($path) { Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json }
function Cell($value) {
    if ($null -eq $value) { return '' }
    $text = if ($value -is [bool]) { $value.ToString().ToLowerInvariant() } else { [string]$value }
    if ($text -eq '') { return '`""`' }
    return '`' + $text.Replace('|', '\|') + '`'
}
function Plain($text) { if ($null -eq $text) { '' } else { ([string]$text).Replace('|', '\|') } }

$keys = Read-Json (Join-Path $root 'conventions/keys.json')
$en = Read-Json (Join-Path $root 'conventions/lang/en.json')

$out = New-Object System.Collections.Generic.List[string]
$out.Add('### Standard pages')
$out.Add('')
$out.Add('| Page | Title | Icon | Order |')
$out.Add('|---|---|---|---|')
foreach ($p in $keys.pages.PSObject.Properties) {
    $out.Add("| ``$($p.Name)`` | $(Plain $en.pages.($p.Name)) | ``$($p.Value.icon)`` | $($p.Value.order) |")
}
$out.Add('')
$out.Add('### Known keys')
$out.Add('')
$out.Add('A game that declares one of these keys gets its page, kind, default, label and help from here; it only')
$out.Add('names what differs. The aliases are the names the games used before; a settings file that still carries one')
$out.Add('is read and written back under the key.')
$out.Add('')
$out.Add('| Key | Kind | Default | Page | Label | Aliases |')
$out.Add('|---|---|---|---|---|---|')
foreach ($k in $keys.keys.PSObject.Properties) {
    $v = $k.Value
    $aliases = if ($v.aliases) { ($v.aliases | ForEach-Object { "``$_``" }) -join ', ' } else { '' }
    $out.Add("| ``$($k.Name)`` | $($v.kind) | $(Cell $v.default) | $($v.page) | $(Plain $en.keys.($k.Name).label) | $aliases |")
}
$out.Add('')
$out.Add('### Known placeholders')
$out.Add('')
$out.Add('| Placeholder | Meaning |')
$out.Add('|---|---|')
foreach ($name in $keys.placeholders.known) {
    $out.Add("| ``{$name}`` | $(Plain $en.placeholders.$name) |")
}
$out.Add('')
$out.Add('### Languages')
$out.Add('')
$out.Add('| File | Keys | Pages | Placeholders | Texts | Window texts |')
$out.Add('|---|---|---|---|---|---|')
foreach ($file in Get-ChildItem (Join-Path $root 'conventions/lang') -Filter *.json | Sort-Object Name) {
    $lang = Read-Json $file.FullName
    $counts = foreach ($section in 'keys', 'pages', 'placeholders', 'texts', 'ui') {
        $have = if ($lang.$section) { @($lang.$section.PSObject.Properties).Count } else { 0 }
        $all = if ($en.$section) { @($en.$section.PSObject.Properties).Count } else { 0 }
        if ($file.Name -eq 'en.json' -or $section -eq 'texts') { "$have" } else { "$have of $all" }
    }
    $out.Add("| ``lang/$($file.Name)`` | $($counts -join ' | ') |")
}

$generated = ($out -join "`n")
$text = (Get-Content -Raw -Encoding UTF8 $doc) -replace "`r`n", "`n"
$start = '<!-- generated:start -->'
$end = '<!-- generated:end -->'
$a = $text.IndexOf($start); $b = $text.IndexOf($end)
if ($a -lt 0 -or $b -lt $a) { throw "docs/Conventions.md has no $start ... $end block" }
$updated = $text.Substring(0, $a + $start.Length) + "`n" + $generated + "`n" + $text.Substring($b)

if ($Check) {
    if ($updated -ne $text) { Write-Host 'docs/Conventions.md is out of date: run tools/conventions-doc.ps1'; exit 1 }
    Write-Host 'docs/Conventions.md is current'
    exit 0
}
[System.IO.File]::WriteAllText($doc, $updated, (New-Object System.Text.UTF8Encoding $false))
Write-Host "wrote $doc"
