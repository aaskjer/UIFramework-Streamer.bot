# Writes the window declaration of a host as C# from the spec its stub used to carry: hosts/<host>/spec.json
# (title, pages, senders, pills, samples, answers, overrides, log, help, about) and the CommandCooldowns of
# hosts/<host>/host-section.cs become one UiWindow builder expression for the game's host block, so that help
# and about texts are not typed in again. A one-off per host on the way to the generic stub.
#
#   tools\spec-to-window.ps1 -HostName highlow                       # prints the declaration
#   tools\spec-to-window.ps1 -HostName highlow -Into hosts\highlow\HostBlock.cs
#                                                                    # replaces the "Window = null; // __WINDOW__" line there
param(
    [Parameter(Mandatory = $true)] [string] $HostName,
    [string] $Into
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object Text.UTF8Encoding $false
# A relative -Into counts from where PowerShell stands: [IO.File] counts from the folder the console was started in.
if ($Into) { $Into = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Into) }
$specPath = Join-Path $root "hosts\$HostName\spec.json"
$sectionPath = Join-Path $root "hosts\$HostName\host-section.cs"
if (-not (Test-Path -LiteralPath $specPath)) { throw "Not found: $specPath" }

$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue
$spec = $serializer.DeserializeObject([IO.File]::ReadAllText($specPath, [Text.Encoding]::UTF8))

function Q($value) {
    # A C# string literal, or null.
    if ($null -eq $value) { return 'null' }
    # An answer that is an object (the odds panel) is its JSON, as the game answers it.
    $s = if ($value -is [System.Collections.IDictionary] -or $value -is [array]) { $serializer.Serialize($value) } else { [string]$value }
    return '"' + $s.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n') + '"'
}
function Has($map, [string] $key) { return ($map -is [System.Collections.IDictionary]) -and $map.ContainsKey($key) -and ($null -ne $map[$key]) }
function Items($value) { if ($null -eq $value) { return @() } ; return @($value) }

$lines = New-Object System.Collections.Generic.List[string]
$i1 = '    '; $i2 = '        '; $i3 = '            '; $i4 = '                '

# The constructor.
$ctor = @((Q $spec['title']))
foreach ($name in 'releases', 'logo', 'logPrefix') { if (Has $spec $name) { $ctor += "${name}: " + (Q $spec[$name]) } }
foreach ($name in 'width', 'height') { if (Has $spec $name) { $ctor += "${name}: " + [string]$spec[$name] } }
$lines.Add("${i1}public static readonly UiWindow Window = new UiWindow(" + ($ctor -join ', ') + ")")
if (Has $spec 'language') { Write-Warning "spec.language '$($spec['language'])' has no builder argument; set Window.Language after the declaration." }

# Pages: a standard page with nothing but its id stays a string.
if (Has $spec 'pages') {
    $pages = @()
    foreach ($page in Items $spec['pages']) {
        if ($page -is [string]) { $pages += (Q $page); continue }
        $args = @((Q $page['id']))
        $extra = (Has $page 'title') -or (Has $page 'icon') -or (Has $page 'fields')
        if (-not $extra) { $pages += (Q $page['id']); continue }
        $args += (Q $page['title'])
        if ((Has $page 'icon') -or (Has $page 'fields')) { $args += (Q $page['icon']) }
        foreach ($field in Items $page['fields']) { $args += (Q $field) }
        $pages += 'P(' + ($args -join ', ') + ')'
    }
    $lines.Add("${i2}.Pages(" + ($pages -join ', ') + ")")
}

# Senders: only the keys the spec names; null hides a switch.
if (Has $spec 'senders') {
    $senders = @()
    foreach ($name in 'useBot', 'sendAction', 'cooldownUseBot', 'cooldownSendAction', 'numberSeparator', 'cooldownGroup') {
        if ($spec['senders'].ContainsKey($name)) { $senders += "${name}: " + (Q $spec['senders'][$name]) }
    }
    if ($senders.Count -gt 0) { $lines.Add("${i2}.Senders(" + ($senders -join ', ') + ")") }
}

# Overrides.
if (Has $spec 'overrides') {
    foreach ($key in $spec['overrides'].Keys) {
        $over = $spec['overrides'][$key]
        $args = @((Q $key))
        foreach ($name in 'page', 'label', 'help', 'section') { if (Has $over $name) { $args += "${name}: " + (Q $over[$name]) } }
        if (Has $over 'hidden') { $args += 'hidden: ' + ([string]$over['hidden']).ToLower() }
        if (Has $over 'order') { $args += 'order: ' + [string]$over['order'] }
        $lines.Add("${i2}.Override(" + ($args -join ', ') + ")")
    }
}

# Header pills.
foreach ($pill in Items $spec['headerPills']) {
    $args = @((Q $pill['key']), (Q $pill['on']))
    if (Has $pill 'off') { $args += (Q $pill['off']) } elseif (Has $pill 'tooltip') { $args += 'null' }
    if (Has $pill 'tooltip') { $args += 'tooltip: ' + (Q $pill['tooltip']) }
    $lines.Add("${i2}.Pill(" + ($args -join ', ') + ")")
}

# Samples and answers.
if (Has $spec 'samples') {
    $pairs = @()
    foreach ($key in $spec['samples'].Keys) { $pairs += (Q $key); $pairs += (Q $spec['samples'][$key]) }
    if ($pairs.Count -gt 0) { $lines.Add("${i2}.Samples(" + ($pairs -join ', ') + ")") }
}
if (Has $spec 'answers') {
    foreach ($key in $spec['answers'].Keys) { $lines.Add("${i2}.Answer(" + (Q $key) + ', ' + (Q $spec['answers'][$key]) + ')') }
}

# Cooldowns, from the host section's CommandCooldowns.
$cooldowns = @()
if (Has $spec 'cooldowns') {
    foreach ($c in Items $spec['cooldowns']) { $cooldowns += 'C(' + (Q $c['command']) + ', ' + (Q $c['global']) + ', ' + (Q $c['user']) + ')' }
} elseif (Test-Path -LiteralPath $sectionPath) {
    $section = [IO.File]::ReadAllText($sectionPath, [Text.Encoding]::UTF8)
    $block = [regex]::Match($section, 'CommandCooldowns\s*=\s*\{(.*?)\n\s*\};', 'Singleline')
    if ($block.Success) {
        foreach ($m in [regex]::Matches($block.Groups[1].Value, 'new\[\]\s*\{\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"\s*\}')) {
            $cooldowns += 'C(' + (Q $m.Groups[1].Value) + ', ' + (Q $m.Groups[2].Value) + ', ' + (Q $m.Groups[3].Value) + ')'
        }
    }
}
if ($cooldowns.Count -eq 1) { $lines.Add("${i2}.Cooldowns(" + $cooldowns[0] + ")") }
elseif ($cooldowns.Count -gt 1) {
    $lines.Add("${i2}.Cooldowns(")
    for ($i = 0; $i -lt $cooldowns.Count; $i++) { $lines.Add($i3 + $cooldowns[$i] + $(if ($i -lt $cooldowns.Count - 1) { ',' } else { ')' })) }
}

# The log page.
if (Has $spec 'log') {
    $log = $spec['log']
    $files = $log['files']
    if (-not $files -and (Test-Path -LiteralPath $sectionPath)) {
        # The old stub's ReadLogLines knew the file pattern: Directory.GetFiles(LogsDir, "CCG-log*.txt").
        $m = [regex]::Match([IO.File]::ReadAllText($sectionPath), 'GetFiles\([^,]+,\s*"([^"]+)"')
        if ($m.Success) { $files = $m.Groups[1].Value }
    }
    $args = @((Q $log['folder']), (Q $files))
    foreach ($name in 'writeLogKey', 'writeLogHelp') { if (Has $log $name) { $args += "${name}: " + (Q $log[$name]) } }
    if (Has $log 'columns') {
        $cols = @()
        foreach ($col in Items $log['columns']) {
            if ($col -is [string]) { $cols += (Q $col) } else { $cols += (Q ($col['header'] + '|' + $col['field'])) }
        }
        $args += 'columns: new[] { ' + ($cols -join ', ') + ' }'
    }
    foreach ($name in 'results', 'platforms') {
        if (Has $log $name) { $args += "${name}: new[] { " + ((Items $log[$name] | ForEach-Object { Q $_ }) -join ', ') + ' }' }
    }
    foreach ($name in 'emptyText', 'writeLogLabel') { if (Has $log $name) { $args += "${name}: " + (Q $log[$name]) } }
    $lines.Add("${i2}.Log(" + ($args -join ', ') + ")")
}

function Line-Of($line) {
    if ($line -is [string]) { return (Q $line) }
    if (Has $line 'command') { return 'Cmd(' + (Q $line['command']) + ', ' + (Q $line['what']) + ')' }
    if (Has $line 'link') { return 'Link(' + (Q $line['link']) + ', ' + (Q $line['url']) + ')' }
    if (Has $line 'text') { return 'Link(' + (Q $line['text']) + ', ' + (Q $line['url']) + ')' }
    return 'Note(' + (Q $line['note']) + ')'
}
function Section-Lines($section, [string] $indent, [bool] $last) {
    # H("heading", line, line) across several lines; the closing paren takes the comma or the caller's end.
    $out = New-Object System.Collections.Generic.List[string]
    $items = @(Items $section['lines'])
    $tail = $(if ($last) { '' } else { ',' })
    if ($items.Count -eq 0) { $out.Add($indent + 'H(' + (Q $section['heading']) + ')' + $tail); return $out }
    $out.Add($indent + 'H(' + (Q $section['heading']) + ',')
    for ($i = 0; $i -lt $items.Count; $i++) {
        $out.Add($indent + '    ' + (Line-Of $items[$i]) + $(if ($i -lt $items.Count - 1) { ',' } else { ')' + $tail }))
    }
    return $out
}

# Help.
$help = @(Items $spec['help'])
if ($help.Count -gt 0) {
    $lines.Add("${i2}.Help(")
    for ($i = 0; $i -lt $help.Count; $i++) {
        $block = Section-Lines $help[$i] $i3 ($i -eq $help.Count - 1)
        if ($i -eq $help.Count - 1) { $block[$block.Count - 1] += ')' }
        foreach ($l in $block) { $lines.Add($l) }
    }
}

# About.
if (Has $spec 'about') {
    $about = $spec['about']
    $args = @()
    if (Has $about 'credits') { $args += 'credits: ' + (Q $about['credits']) }
    $sections = @(Items $about['sections'])
    $links = @(Items $about['links'])
    $multi = ($sections.Count -gt 0)
    if (-not $multi) {
        if ($links.Count -gt 0) { $args += 'links: new[] { ' + (($links | ForEach-Object { Line-Of $_ }) -join ', ') + ' }' }
        foreach ($name in 'streamerBotLink', 'authorLink') { if ($about.ContainsKey($name) -and $about[$name] -eq $false) { $args += "${name}: false" } }
        $lines.Add("${i2}.About(" + ($args -join ', ') + ');')
    } else {
        $lines.Add("${i2}.About(" + ($args -join ', ') + $(if ($args.Count -gt 0) { ',' } else { '' }))
        $lines.Add("${i3}sections: new[]")
        $lines.Add("${i3}{")
        for ($i = 0; $i -lt $sections.Count; $i++) { foreach ($l in (Section-Lines $sections[$i] $i4 ($i -eq $sections.Count - 1))) { $lines.Add($l) } }
        $rest = @()
        if ($links.Count -gt 0) { $rest += 'links: new[] { ' + (($links | ForEach-Object { Line-Of $_ }) -join ', ') + ' }' }
        foreach ($name in 'streamerBotLink', 'authorLink') { if ($about.ContainsKey($name) -and $about[$name] -eq $false) { $rest += "${name}: false" } }
        $lines.Add("${i3}}" + $(if ($rest.Count -gt 0) { ', ' + ($rest -join ', ') } else { '' }) + ');')
    }
} else {
    $lines[$lines.Count - 1] += ';'
}

$code = ($lines -join "`n")
if (-not $Into) { Write-Output $code; exit 0 }

$text = [IO.File]::ReadAllText($Into, [Text.Encoding]::UTF8)
$marker = [regex]::Match($text, '(?m)^[ \t]*public static readonly UiWindow Window = null;[ \t]*// __WINDOW__[ \t]*\r?$')
if (-not $marker.Success) { throw "$Into has no 'Window = null; // __WINDOW__' line to replace." }
$nl = $(if ($text.Contains("`r`n")) { "`r`n" } else { "`n" })
$text = $text.Substring(0, $marker.Index) + $code.Replace("`n", $nl) + $text.Substring($marker.Index + $marker.Length)
[IO.File]::WriteAllText($Into, $text, $utf8)
Write-Host "  ok   window of $HostName written into $Into ($($lines.Count) lines)" -ForegroundColor Green
exit 0
