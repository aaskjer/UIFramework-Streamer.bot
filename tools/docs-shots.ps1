# Renders the window screenshots of the README and the docs again (docs\images\*.png), with the Release build of
# the preview: after a change to the window's chrome every picture with a header or a footer is out of date at once.
# emotes-youtube-dark.png is a page without the window around it and needs the network for its pictures; it is not
# made here (UIFramework.Preview -schema hosts\slotmachine\schema.json -shots <folder> -settle 3000).
#
#   dotnet build UIFramework.slnx -c Release
#   tools\docs-shots.ps1                # renders into %TEMP%\uiframework-docs-shots and copies into docs\images
#   tools\docs-shots.ps1 -NoCopy        # renders only, to look at them first
param(
    [string] $Out = (Join-Path $env:TEMP 'uiframework-docs-shots'),
    [string] $StreamerBotPath = 'C:\Users\Administrator\Downloads\Streamer.bot-x64-1.0.7',
    [switch] $NoCopy
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'src\UIFramework.Preview\bin\Release\net48\UIFramework.Preview.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Build the preview first: $exe" }
$utf8 = New-Object Text.UTF8Encoding $false
if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
New-Item -ItemType Directory -Force $Out | Out-Null
$work = Join-Path $Out 'work'
New-Item -ItemType Directory -Force $work | Out-Null

# One run of the preview: a settings folder of its own (so the layout file is this run's), the window per page.
function Shoot([string] $name, [string] $schema, [string] $settingsJson, [string] $layoutJson, [string[]] $more) {
    $dir = Join-Path $work $name
    New-Item -ItemType Directory -Force $dir | Out-Null
    $settings = Join-Path $dir 'settings.json'
    if ($settingsJson) { [IO.File]::WriteAllText($settings, $settingsJson, $utf8) }
    if ($layoutJson) { [IO.File]::WriteAllText((Join-Path $dir 'uiframework.layout.json'), $layoutJson, $utf8) }
    $shots = Join-Path $Out $name
    $list = @('-schema', $schema, '-settings', $settings, '-windowshots', $shots) + $more
    $p = Start-Process $exe -ArgumentList ($list | ForEach-Object { '"' + $_ + '"' }) -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "$name : the preview ended with $($p.ExitCode)" }
}

$bj = Join-Path $repo 'hosts\blackjack\schema.json'
$demo = Join-Path $repo 'examples\DemoGame\schema.json'
$bjSettings = '{ "defaultBet": 100 }'

Shoot 'bj-dark' $bj $bjSettings $null @('-theme', 'dark', '-pages', 'table,messages')
Shoot 'bj-light' $bj $bjSettings $null @('-theme', 'light', '-pages', 'table,log')
Shoot 'bj-customize' $bj $bjSettings $null @('-theme', 'dark', '-pages', 'table', '-customize')
Shoot 'demo-dark' $demo $null $null @('-theme', 'dark', '-pages', 'game,messages')
Shoot 'demo-light' $demo $null $null @('-theme', 'light', '-pages', 'game')
Shoot 'demo-tour' $demo $null $null @('-theme', 'dark', '-tour')
Shoot 'demo-helplines' $demo $null '{ "dark": true, "helpLines": true }' @('-pages', 'game')

# The problems panel: the demo with four mistakes in its declaration, and a layout file that does not parse.
Add-Type -Path (Join-Path $StreamerBotPath 'Newtonsoft.Json.dll') -ErrorAction SilentlyContinue
$reader = New-Object Newtonsoft.Json.JsonTextReader (New-Object IO.StringReader ([IO.File]::ReadAllText($demo, [Text.Encoding]::UTF8)))
$reader.DateParseHandling = [Newtonsoft.Json.DateParseHandling]::None
$schema = [Newtonsoft.Json.Linq.JObject]::Load($reader)
$settings = $schema.SelectToken('settings')
foreach ($extra in @(
    '{ "key": "diceColour", "default": "#F1C40F", "kind": "colr", "page": "game", "section": "Look", "label": "Dice colour" }',
    '{ "key": "bonusRoll", "default": false, "page": "game", "section": "Look", "label": "Bonus roll", "requires": "bonusMode" }',
    '{ "key": "rerolls", "default": 1, "page": "game", "section": "The die", "label": "Rerolls" }'
)) { $settings.Add([Newtonsoft.Json.Linq.JObject]::Parse($extra)) }
$schema.SelectToken('window.tour').Add([Newtonsoft.Json.Linq.JObject]::Parse('{ "target": "sace" }'))
$broken = Join-Path $work 'broken-schema.json'
[IO.File]::WriteAllText($broken, $schema.ToString(), $utf8)
Shoot 'demo-problems' $broken $null '{ "dark": true, "theme": ' @('-pages', 'game', '-problems')

$pictures = [ordered]@{
    'blackjack-table-dark.png'    = 'bj-dark\table.png'
    'blackjack-messages-dark.png' = 'bj-dark\messages.png'
    'blackjack-table-light.png'   = 'bj-light\table.png'
    'blackjack-log-light.png'     = 'bj-light\log.png'
    'customize-dark.png'          = 'bj-customize\table.png'
    'demo-game-dark.png'          = 'demo-dark\game.png'
    'demo-messages-dark.png'      = 'demo-dark\messages.png'
    'demo-game-light.png'         = 'demo-light\game.png'
    'demo-tour-dark.png'          = 'demo-tour\tour-03.png'
    'help-lines-dark.png'         = 'demo-helplines\game.png'
    'problems-dark.png'           = 'demo-problems\problems.png'
}
foreach ($name in $pictures.Keys) {
    $from = Join-Path $Out $pictures[$name]
    if (-not (Test-Path -LiteralPath $from)) { throw "not rendered: $from" }
    if ($NoCopy) { Write-Host "  ok   $from" }
    else { Copy-Item -LiteralPath $from -Destination (Join-Path $repo "docs\images\$name") -Force; Write-Host "  new  docs\images\$name" }
}
