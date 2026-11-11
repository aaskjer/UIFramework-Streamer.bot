# Refreshes the UIFramework host block in a game's action file from stub/HostSchema.cs, keeping what the game put
# into it: its Window declaration and its settings table. The block is found by its markers
# ("// --- UIFramework host block" to "// --- end UIFramework host block ---"), at whatever indentation it sits.
#
#   tools\update-host-block.ps1 -File "C:\...\Blackjack Game\BlackjackGame.cs"
#   tools\update-host-block.ps1 -File ... -Check      # writes nothing; fails when the file's block is behind
#   tools\update-host-block.ps1 -File ... -Force      # writes even over lines of the game's own in the block
#   tools\update-host-block.ps1 -File ... -Into "C:\...\BlackjackStats.cs" -HostName blackjack
#       afterwards copies the file's block, as it then is, into the game's other action and into
#       hosts\blackjack\HostBlock.cs (tools\copy-host-block.ps1); with -Check the copies are checked too
#
# A block from before the window declaration gets the template's "Window = null" line; set it after.
# Before it writes, the file is copied to <file>.bak. It refuses, and writes nothing, when the old block holds code
# outside the Window and the Table that the template does not have (the swap would drop it), or when the Table's
# end cannot be told for sure; -Force writes anyway.
param(
    [Parameter(Mandatory = $true)] [string] $File,
    [switch] $Check,
    [switch] $Force,
    [string] $Template,
    [string[]] $Into = @(),
    [string] $HostName
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
# Relative paths count from where PowerShell stands: [IO.File] counts from the folder the console was started in.
function Full([string] $path) { if ($path) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path) } else { $path } }
$File = Full $File
$Template = Full $Template
$Into = @($Into | ForEach-Object { Full $_ })
if (-not $Template) { $Template = Join-Path $root 'stub\HostSchema.cs' }
$utf8 = New-Object Text.UTF8Encoding $false
$nl = "`n"

# The copies of the file's block, once the file itself is as it should be.
function Copy-Out {
    if ($Into.Count -eq 0 -and -not $HostName) { return 0 }
    & (Join-Path $PSScriptRoot 'copy-host-block.ps1') -File $File -Into $Into -HostName $HostName -Check:$Check | Out-Host
    return $LASTEXITCODE
}

function Split-Block([string[]] $lines) {
    # The block's head (everything before the window comment), its region (window and table) and its tail.
    $window = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*// ---- The window:' })
    $table = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^\s*// ---- The table:' })
    if ($table -lt 0) { throw 'The block has no "// ---- The table:" comment.' }
    $start = if ($window -ge 0) { $window } else { $table }
    # The table's "};" sits at the indentation of its comment: a "};" deeper inside (an initializer in the table)
    # is not its end.
    $tableIndent = [regex]::Match($lines[$table], '^\s*').Value
    $end = -1
    for ($i = $table; $i -lt $lines.Length; $i++) { if ($lines[$i] -match ('^' + [regex]::Escape($tableIndent) + '\};\s*$')) { $end = $i; break } }
    if ($end -lt 0) { throw 'The table has no closing "};" at the indentation of its "// ---- The table:" comment.' }
    return @{ Head = $lines[0..($start - 1)]; Region = $lines[$start..$end]; Tail = $lines[($end + 1)..($lines.Length - 1)]; HasWindow = ($window -ge 0); WindowAt = $window; TableAt = $table }
}

$templateText = [IO.File]::ReadAllText($Template, [Text.Encoding]::UTF8).Replace("`r`n", "`n").TrimEnd("`n")
$templateLines = $templateText.Split("`n")
# Only what stands between the template's two markers is the block; the note above them stays in the template.
$tFirst = [Array]::FindIndex($templateLines, [Predicate[string]] { param($l) $l -match '^\s*// --- UIFramework host block' })
$tLast = [Array]::FindIndex($templateLines, [Predicate[string]] { param($l) $l -match '^\s*// --- end UIFramework host block ---' })
if ($tFirst -lt 0 -or $tLast -le $tFirst) { throw "No UIFramework host block (its two markers) in the template $Template" }
$templateLines = $templateLines[$tFirst..$tLast]
$fileText = [IO.File]::ReadAllText($File, [Text.Encoding]::UTF8)
$crlf = $fileText.Contains("`r`n")
$fileLines = $fileText.Replace("`r`n", "`n").Split("`n")

$first = [Array]::FindIndex($fileLines, [Predicate[string]] { param($l) $l -match '^\s*// --- UIFramework host block' })
$last = [Array]::FindIndex($fileLines, [Predicate[string]] { param($l) $l -match '^\s*// --- end UIFramework host block ---' })
if ($first -lt 0 -or $last -lt 0 -or $last -le $first) { throw "No UIFramework host block (its two markers) in $File" }
$indent = [regex]::Match($fileLines[$first], '^\s*').Value

$blockLines = $fileLines[$first..$last] | ForEach-Object { if ($_.StartsWith($indent)) { $_.Substring($indent.Length) } else { $_ } }
$have = Split-Block $blockLines
$want = Split-Block $templateLines

# The template's head and tail around the file's own window and table; a file without a window gets the
# template's window lines in front of its table.
$region = @()
if (-not $have.HasWindow) { $region += $want.Region[0..($want.TableAt - $want.WindowAt - 1)] }
$region += $have.Region
$newBlock = @($want.Head) + $region + @($want.Tail)
$newBlockText = (($newBlock | ForEach-Object { if ($_.Length -gt 0) { $indent + $_ } else { $_ } }) -join $nl)
$oldBlockText = ($fileLines[$first..$last] -join $nl)

if ($newBlockText -ceq $oldBlockText) {   # -ceq: a change in letter case is a change too
    Write-Host "  ok   $File carries the current host block"
    exit (Copy-Out)
}
if ($Check) {
    Write-Host "  FAIL $File carries an older host block; run tools\update-host-block.ps1 -File on it" -ForegroundColor Red
    exit 1
}

# What the swap would drop: code in the old block's head and tail (outside the Window and the Table) that the
# template has nowhere. Comments and blank lines do not count; an older template's code does, so read the list.
$templateSet = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($l in $templateLines) { [void]$templateSet.Add($l.Trim()) }
# Every earlier version of the template counts as the template too: a block one or more releases behind carries
# their lines, which are not the game's own. Read from this repository's history when git is at hand.
$repoRoot = Split-Path -Parent $PSScriptRoot
try {
    $revisions = @(git -C $repoRoot log --format=%H -- stub/HostSchema.cs 2>$null)
    foreach ($rev in $revisions) {
        foreach ($l in @(git -C $repoRoot show "${rev}:stub/HostSchema.cs" 2>$null)) { [void]$templateSet.Add(([string]$l).Trim()) }
    }
} catch { }
$dropped = @(@($have.Head) + @($have.Tail) | Where-Object {
    $t = "$_".Trim()
    $t.Length -gt 0 -and -not $t.StartsWith('//') -and -not $templateSet.Contains($t)
})
# The Table must close what it opens, or its end was misread and part of it would be replaced.
$regionText = (@($have.Region) -join $nl) -replace '"(?:[^"\\]|\\.)*"', '""' -replace '//[^\n]*', ''
$opened = ([regex]::Matches($regionText, '\{')).Count
$closed = ([regex]::Matches($regionText, '\}')).Count
if (-not $Force -and ($dropped.Count -gt 0 -or $opened -ne $closed)) {
    if ($opened -ne $closed) { Write-Host "  STOP the Window and Table in $File open $opened braces and close $closed; their end is unclear" -ForegroundColor Red }
    if ($dropped.Count -gt 0) {
        Write-Host "  STOP the update would drop these lines of $File's host block (outside its Window and Table):" -ForegroundColor Red
        $dropped | Select-Object -First 15 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
    }
    Write-Host "       Nothing was written. Move your code out of the block, or run again with -Force." -ForegroundColor Red
    exit 1
}
$before = if ($first -gt 0) { ($fileLines[0..($first - 1)] -join $nl) + $nl } else { '' }
$after = if ($last -lt $fileLines.Length - 1) { $nl + ($fileLines[($last + 1)..($fileLines.Length - 1)] -join $nl) } else { '' }
$result = $before + $newBlockText + $after
if ($crlf) { $result = $result.Replace("`n", "`r`n") }
Copy-Item -LiteralPath $File -Destination ($File + '.bak') -Force
# A file that started with a byte-order mark keeps it.
$bytes = [IO.File]::ReadAllBytes($File)
$bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
[IO.File]::WriteAllText($File, $result, (New-Object Text.UTF8Encoding $bom))
Write-Host "  updated the host block in $File"
exit (Copy-Out)
