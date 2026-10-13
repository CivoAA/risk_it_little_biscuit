# Globale Spielmetriken von Steam holen und als HTML-Seite im Browser anzeigen.
# Start per Doppelklick auf "Metriken anzeigen.bat". Gleiche Logik wie steam_metriken.py,
# braucht aber kein Python.
#
# Der Publisher-Web-API-Key wird beim ersten Start abgefragt und als Windows-
# Benutzervariable STEAM_PUBLISHER_KEY gespeichert - nie im Repo.
param([int]$Tage = 0, [switch]$NeuerKey)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$AppId = 4036370
$Url = 'https://partner.steam-api.com/ISteamUserStats/GetGlobalStatsForGame/v1/'
$Root = Split-Path $PSScriptRoot -Parent
$Chunk = 40

# Nur ASCII im Skript (PowerShell 5.1 liest .ps1 ohne BOM als ANSI) - Umlaute als HTML-Entities.
$Chars = @('Keks', 'Marmelade', 'Onigiri', 'Toast', 'Zwiebelritter', 'Mochi', 'Inari', 'Schleimk&ouml;nig', 'Tofu')
$Maps = [ordered]@{ world1 = 'K&uuml;che'; world2 = 'Wald'; world5 = 'Eis'; world6 = 'Geisterwald'; world4 = 'Vulkan' }

function AskKey {
    Write-Host ''
    Write-Host 'Steam-Publisher-Web-API-Key eingeben (Rechtsklick fuegt ein, dann Enter)'
    Write-Host '(Steamworks > Benutzer und Berechtigungen > Gruppen verwalten > Gruppe > Web-API;'
    Write-Host ' unter "WebAPI-Schluessel verwalten" muss "Allgemein" angehakt sein):'
    $k = (Read-Host).Trim()
    if (-not $k) { throw 'Kein Key eingegeben.' }
    [Environment]::SetEnvironmentVariable('STEAM_PUBLISHER_KEY', $k, 'User')
    Write-Host 'Key gespeichert (Windows-Benutzervariable STEAM_PUBLISHER_KEY).'
    return $k
}

# Benutzervariable zuerst - die Prozessvariable kann noch einen alten Key enthalten.
$key = [Environment]::GetEnvironmentVariable('STEAM_PUBLISHER_KEY', 'User')
if (-not $key) { $key = $env:STEAM_PUBLISHER_KEY }
if (-not $key -or $NeuerKey) { $key = AskKey }

$start = $null; $end = $null
if ($Tage -gt 0) {
    $end = [int][double]::Parse((Get-Date -UFormat %s))
    $start = $end - $Tage * 86400
}

function Fetch([string[]]$names) {
    $q = "key=$([Uri]::EscapeDataString($key))&appid=$AppId&count=$($names.Count)"
    for ($i = 0; $i -lt $names.Count; $i++) { $q += "&name%5B$i%5D=$($names[$i])" }
    if ($start) { $q += "&startdate=$start&enddate=$end" }
    try {
        $resp = (Invoke-RestMethod -Uri "$Url`?$q" -TimeoutSec 30).response
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if ($code -eq 401 -or $code -eq 403) { throw 'KEY_ABGELEHNT' }
        throw
    }
    if ($resp.result -ne 1) { return $null }
    $out = @{}
    foreach ($p in $resp.globalstats.PSObject.Properties) {
        if ($null -ne $p.Value.total) { $out[$p.Name] = [long]$p.Value.total }
        elseif ($p.Value.history) { $out[$p.Name] = ($p.Value.history | ForEach-Object { [long]$_.total } | Measure-Object -Sum).Sum }
    }
    return $out
}

$names = Get-Content (Join-Path $Root 'steam_metriken_stats.txt') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$v = @{}; $missing = @()
Write-Host 'Hole Daten von Steam ...'
# Erst mit einem Stat pruefen, ob Steam den Key annimmt - sonst neuen Key abfragen.
for ($try = 1; ; $try++) {
    try { $null = Fetch @($names[0]); break }
    catch {
        if ($_.Exception.Message -ne 'KEY_ABGELEHNT') { throw }
        Write-Host ''
        Write-Host 'Steam lehnt den Key ab (HTTP 403): Key geloescht/falsch oder "Allgemein" nicht angehakt.'
        if ($try -ge 3) { throw 'Key wird weiterhin abgelehnt.' }
        $key = AskKey
    }
}
for ($i = 0; $i -lt $names.Count; $i += $Chunk) {
    $part = @($names[$i..([Math]::Min($i + $Chunk, $names.Count) - 1)])
    $got = Fetch $part
    if ($null -eq $got) {
        # Ein nicht angelegter / nicht aggregierter Stat laesst die ganze Anfrage scheitern - einzeln nachfragen.
        $got = @{}
        foreach ($n in $part) {
            $single = Fetch @($n)
            if ($null -eq $single) { $missing += $n } else { $single.GetEnumerator() | ForEach-Object { $got[$_.Key] = $_.Value } }
        }
    }
    $got.GetEnumerator() | ForEach-Object { $v[$_.Key] = $_.Value }
}
function G([string]$n) { if ($v.ContainsKey($n)) { $v[$n] } else { 0 } }
function Pct($part, $whole) { if ($whole) { '{0:0.0} %' -f (100 * $part / $whole) } else { '&ndash;' } }
function Bar($part, $whole) { $w = if ($whole) { [Math]::Round(100 * $part / $whole, 1) } else { 0 }; "<div class=bar><span style='width:$($w.ToString([Globalization.CultureInfo]::InvariantCulture))%'></span></div>" }
function Num($n) { '{0:N0}' -f $n }
function Hours($min) { '{0:N1}' -f ($min / 60) }

$runsTotal = 0; foreach ($c in 0..($Chars.Count - 1)) { $runsTotal += G "m_char${c}_runs" }
$winsTotal = 0; foreach ($c in 0..($Chars.Count - 1)) { $winsTotal += G "m_char${c}_wins" }
$minTotal = 0; foreach ($c in 0..($Chars.Count - 1)) { $minTotal += G "m_char${c}_min" }

$charRows = 0..($Chars.Count - 1) | Sort-Object { - (G "m_char$($_)_runs") } | ForEach-Object {
    $r = G "m_char$($_)_runs"; $w = G "m_char$($_)_wins"; $d = G "m_char$($_)_deaths"; $m = G "m_char$($_)_min"
    "<tr><td>$($Chars[$_])</td><td class=n>$(Num $r)</td><td class=b>$(Bar $r $runsTotal)<small>$(Pct $r $runsTotal)</small></td><td class=n>$(Pct $w $r)</td><td class=n>$(Num $d)</td><td class=n>$(Num ([Math]::Max(0, $r - $w - $d)))</td><td class=n>$(Hours $m)</td></tr>"
}

$mapStoryTotal = 0; foreach ($m in $Maps.Keys) { $mapStoryTotal += G "m_map_${m}_story" }
$mapRows = foreach ($m in $Maps.Keys) {
    $s = G "m_map_${m}_story"; $e = G "m_map_${m}_endless"; $w = G "m_map_${m}_wins"; $mi = G "m_map_${m}_min"
    "<tr><td>$($Maps[$m])</td><td class=n>$(Num $s)</td><td class=b>$(Bar $s $mapStoryTotal)<small>$(Pct $s $mapStoryTotal)</small></td><td class=n>$(Num $e)</td><td class=n>$(Pct $w $s)</td><td class=n>$(Hours $mi)</td></tr>"
}

function PickTable([string]$prefix, [string[]]$exclude) {
    $list = $names | Where-Object { $_.StartsWith($prefix) } | Where-Object { $p = $_; -not ($exclude | Where-Object { $p.StartsWith($_) }) }
    $list | Sort-Object { - (G $_) } | ForEach-Object {
        $label = $_.Substring($prefix.Length).Replace('_', ' ')
        $c = G $_
        "<tr><td>$label</td><td class=n>$(Num $c)</td><td class=b>$(Bar $c $runsTotal)<small>$(Pct $c $runsTotal)</small></td></tr>"
    }
}
$weaponRows = PickTable 'm_pick_' @('m_pick_buff_', 'm_pick_evo_')
$buffRows = PickTable 'm_pick_buff_' @()
$evoRows = PickTable 'm_pick_evo_' @()

$zeitraum = if ($Tage -gt 0) { "letzte $Tage Tage" } else { 'gesamt' }
$stand = Get-Date -Format 'dd.MM.yyyy HH:mm'
$missingHtml = if ($missing.Count) { "<p class=warn>$($missing.Count) Stats fehlen in Steamworks oder sind nicht &bdquo;Aggregated&ldquo;: $($missing -join ', ')</p>" } else { '' }
$emptyHtml = if ($runsTotal -eq 0) { '<p class=note>Noch keine L&auml;ufe gez&auml;hlt. Editor, Development Builds und Sandbox z&auml;hlen nicht mit; neue L&auml;ufe brauchen etwas, bis sie in der Summe auftauchen.</p>' } else { '' }

$html = @"
<!doctype html><html lang=de><head><meta charset=utf-8><meta name=viewport content="width=device-width,initial-scale=1">
<title>Biscuit-Metriken</title><style>
:root{--bg:#f6f4ef;--card:#fff;--fg:#222;--mut:#777;--line:#e4e0d8;--acc:#d08a3a;--warn:#b3402a}
@media (prefers-color-scheme:dark){:root{--bg:#1b1a18;--card:#252320;--fg:#eee;--mut:#999;--line:#38352f;--acc:#e0a050;--warn:#e07060}}
body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.4 system-ui,Segoe UI,sans-serif}
main{max-width:960px;margin:0 auto;padding:24px 16px}
h1{margin:0 0 4px}h2{margin:32px 0 8px;font-size:18px}.sub{color:var(--mut);margin:0 0 20px}
.kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px}
.kpi{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px}
.kpi b{display:block;font-size:24px}.kpi span{color:var(--mut);font-size:13px}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:10px;overflow:hidden}
th,td{padding:7px 10px;border-bottom:1px solid var(--line);text-align:left}th{color:var(--mut);font-weight:500;font-size:13px}
tr:last-child td{border-bottom:0}.n{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
.b{width:35%}.b small{color:var(--mut);font-size:12px}
.bar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin-bottom:2px}.bar span{display:block;height:100%;background:var(--acc)}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:16px}
.note{color:var(--mut)}.warn{color:var(--warn)}
</style></head><body><main>
<h1>Risk it Little Biscuit &ndash; Metriken</h1>
<p class=sub>Summe &uuml;ber alle Spieler ($zeitraum) &middot; Stand $stand</p>
$emptyHtml
<div class=kpis>
<div class=kpi><b>$(Num $runsTotal)</b><span>L&auml;ufe</span></div>
<div class=kpi><b>$(Pct $winsTotal $runsTotal)</b><span>Siegquote</span></div>
<div class=kpi><b>$(Hours $minTotal)</b><span>Stunden gespielt</span></div>
</div>
<h2>Charaktere</h2>
<table><tr><th>Charakter</th><th class=n>L&auml;ufe</th><th>Anteil</th><th class=n>Siegquote</th><th class=n>Tode</th><th class=n>Abgebrochen</th><th class=n>Stunden</th></tr>
$($charRows -join "`n")</table>
<h2>Welten</h2>
<table><tr><th>Welt</th><th class=n>Story</th><th>Anteil</th><th class=n>Endless</th><th class=n>Siegquote</th><th class=n>Stunden</th></tr>
$($mapRows -join "`n")</table>
<h2>Was am Laufende im Besitz war</h2>
<p class=note>Anteil = in wie viel Prozent aller L&auml;ufe das Teil am Ende dabei war.</p>
<div class=grid>
<div><h2>Waffen</h2><table>$($weaponRows -join "`n")</table></div>
<div><h2>Evos</h2><table>$($evoRows -join "`n")</table><h2>Buffs</h2><table>$($buffRows -join "`n")</table></div>
</div>
$missingHtml
</main></body></html>
"@

$outFile = Join-Path $env:TEMP 'biscuit_metriken.html'
[IO.File]::WriteAllText($outFile, $html, (New-Object Text.UTF8Encoding $false))
Write-Host "Fertig: $outFile"
Start-Process $outFile
