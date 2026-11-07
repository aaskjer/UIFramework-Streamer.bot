<#
.SYNOPSIS
  Writes conventions/emotes/twitch.json: Twitch's global emotes by name and their id.

.DESCRIPTION
  The window shows the picture beside a Twitch emote typed by name. Without a Twitch login in Streamer.bot it has
  no token for the Twitch API, so it falls back on this list for the global emotes (Kappa, LUL, ...). The list is
  read from a public mirror of Twitch's global emotes that needs no login (emotes.adamcy.pl); an id is the part of
  the picture address after /emoticons/v2/. Run it now and then and before a release; ids of global emotes
  practically never change, new emotes are added.
#>
param([string]$Source = 'https://emotes.adamcy.pl/v1/global/emotes/twitch')

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'conventions/emotes/twitch.json'

$answer = Invoke-RestMethod -Uri $Source -UserAgent 'UIFramework-build' -TimeoutSec 30
# Names are case-sensitive (:p and :P are two emotes); PowerShell's own dictionaries are not.
$emotes = New-Object 'System.Collections.Generic.SortedDictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($e in $answer) {
    $url = ($e.urls | Select-Object -First 1).url
    if ($url -match '/emoticons/v2/([^/]+)/' -and $e.code -and -not $emotes.ContainsKey($e.code)) {
        $emotes[$e.code] = $Matches[1]
    }
}
if ($emotes.Count -lt 100) { throw "Only $($emotes.Count) emotes came back from $Source; the list is left as it is." }

function Quote([string]$s) { '"' + $s.Replace('\', '\\').Replace('"', '\"') + '"' }
$note = "Twitch's global emotes, typed by name (case-sensitive), and their id; the picture is " +
        "https://static-cdn.jtvnw.net/emoticons/v2/<id>/default/<theme>/2.0. Used when the game hands over no " +
        "Twitch token (no Twitch login in Streamer.bot yet); with one, the window asks the Twitch API itself. " +
        "Written by tools/update-twitch-emotes.ps1 from $Source on $((Get-Date).ToString('yyyy-MM-dd'))."
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('{')
$lines.Add('  "note": ' + (Quote $note) + ',')
$lines.Add('  "emotes": {')
$i = 0
foreach ($pair in $emotes.GetEnumerator()) {
    $i++
    $lines.Add('    ' + (Quote $pair.Key) + ': ' + (Quote $pair.Value) + $(if ($i -lt $emotes.Count) { ',' } else { '' }))
}
$lines.Add('  }')
$lines.Add('}')
[System.IO.File]::WriteAllText($target, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))
Write-Host "wrote $($emotes.Count) emotes to $target"
