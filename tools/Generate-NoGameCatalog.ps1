# Generates NoGameParameterCatalog.Data.cs from ALL Assembly-CSharp dump files
$ErrorActionPreference = 'Stop'
$dump = 'C:\Users\at747\Desktop\CH\_Nuclear_Option_\Assembly-CSharp'
$out = 'C:\Users\at747\source\repos\NuclearOptionSDK_Engine\src\NuclearOptionSDK.Studio\Services\NoGameParameterCatalog.Data.cs'

function Escape-Cs($s) { ($s -replace '\\', '\\\\') -replace '"', '\"' }

function Get-Category($typeName) {
    $exact = @{
        Aircraft='FlightTelemetry'; Unit='FlightTelemetry'; ControlInputs='PilotInputs'; Pilot='PilotInputs'
        WeaponManager='Weapons'; WeaponStation='Weapons'; Weapon='Weapons'; CountermeasureManager='Countermeasures'
        Missile='Weapons'; Radar='RadarEw'; TargetDetector='RadarEw'; CombatHUD='CombatHud'; MissileWarning='RadarEw'
        GameManager='GameSession'; FlightHud='HudFlightHud'; PlayerSettings='PlayerSettings'
        DynamicMap='MapTactical'; MusicManager='Audio'; SoundManager='Audio'; CameraCockpitState='Camera'
        NightVision='Camera'; VirtualMFD='MfdHmd'; HeadMountedDisplay='MfdHmd'; TacScreen='MfdHmd'
    }
    if ($exact.ContainsKey($typeName)) { return $exact[$typeName] }

    $t = $typeName
    if ($t -match 'Countermeasure|FlareEjector|Chaff|Dispenser|Jammer') { return 'Countermeasures' }
    if ($t -match 'FlightHud') { return 'HudFlightHud' }
    if ($t -match 'HUD|Hud|Gauge|Display|Instruments|AoA|Speed|Altitude|FuelGauge|GIndicator|Climbrate|Mach|RPM|Compass|PitchLadder|Bearing|Waterline|StatusGauges') { return 'HudGauges' }
    if ($t -match 'Combat|MFD|HMD|TacScreen|VirtualMFD|HeadMounted|Bombing|MissileState|TurretState|Boresight') { return 'CombatHud' }
    if ($t -match 'Weapon|Missile|Bomb|Gun|Torpedo|Hardpoint|Station|Warhead|Projectile|Ammo') { return 'Weapons' }
    if ($t -match 'Radar|Ecm|Jam|EW|MWS|RWR|Track|Seeker|Emission') { return 'RadarEw' }
    if ($t -match 'Map|Nav|Tactical|Waypoint|Frontline|UnitMap|IconLayer') { return 'MapTactical' }
    if ($t -match 'Camera|Cockpit|NVG|NightVision|ViewState|FOV') { return 'Camera' }
    if ($t -match 'Audio|Sound|Music|Voice|Speaker') { return 'Audio' }
    if ($t -match 'Input|Control|Joystick|Throttle|Pedal|Stick') { return 'PilotInputs' }
    if ($t -match 'Player|Settings|Prefs|Option|Profile') { return 'PlayerSettings' }
    if ($t -match 'Mission|Game|Session|Lobby|Network|Mirage|Sortie|Menu|UI(?!Element)|Gameplay') { return 'GameSession' }
    if ($t -match 'Aero|Engine|Fuel|Gear|Landing|Flight|Helo|Jet|Rotor|Wing|Unit|Part|Cockpit|Autopilot') { return 'FlightTelemetry' }
    return 'LogicGeneral'
}

function Get-MemberDesc($typeName, $memberName, $memberKind, $valueType) {
    $hints = @{
        speed='Speed (m/s)'; radarAlt='Radar altitude AGL'; gForce='G-load'; fuelLevel='Fuel 0-1'
        throttle='Throttle 0-1'; pitch='Pitch input'; roll='Roll input'; yaw='Yaw input'; ammo='Ammo count'
        GetInputs='ControlInputs pitch/roll/yaw/throttle'; GetFuelLevel='Fuel 0-1'; GetTargetList='Target list'
        EnableCanvas='Show/hide FlightHud canvas'; Fire='Fire/launch'; ApplyPrefs='Apply player settings'
    }
    if ($hints.ContainsKey($memberName)) { return $hints[$memberName] }
    switch ($memberKind) {
        'method' { return "Method $typeName.$memberName -> $valueType" }
        'uiField' { return "SerializeField $typeName.$memberName ($valueType)" }
        'property' { return "Property $typeName.$memberName ($valueType)" }
        'enumValue' { return "Enum value $typeName.$memberName" }
        default { return "Field $typeName.$memberName ($valueType)" }
    }
}

function Format-ParamHints($paramList) {
    if ([string]::IsNullOrWhiteSpace($paramList)) { return 'none' }
    $depth = 0; $current = ''; $parts = @()
    foreach ($ch in $paramList.ToCharArray()) {
        if ($ch -eq '<') { $depth++ }
        elseif ($ch -eq '>') { $depth-- }
        if ($ch -eq ',' -and $depth -eq 0) {
            if ($current.Trim()) { $parts += $current.Trim() }
            $current = ''
        } else { $current += $ch }
    }
    if ($current.Trim()) { $parts += $current.Trim() }
    $hints = @()
    foreach ($p in $parts) {
        if ($p -match '^([\w<>\[\],\.\?\s]+?)\s+(\w+)$') {
            $hints += "$($Matches[2]): $($Matches[1].Trim())"
        } elseif ($p -match '^\w') { $hints += $p }
    }
    if ($hints.Count -eq 0) { return 'none' }
    return ($hints -join ' | ')
}

function Should-SkipMember($name, $typeName, $line) {
    if ($name -match '^<|>k__BackingField|_003E') { return $true }
    if ($name -in @('class','struct','enum','interface','event','operator','implicit','explicit')) { return $true }
    if ($line -match '\bevent\b') { return $true }
    if ($line -match '\bdelegate\b') { return $true }
    if ($name -eq $typeName -and $line -match '\(') { return $true } # ctor
    if ($name -match '^(get_|set_|add_|remove_)') { return $true }
    return $false
}

function Parse-EnumFile($path, $typeName) {
    $lines = Get-Content $path -Encoding UTF8
    $results = @()
    foreach ($line in $lines) {
        if ($line -match '^\s+(\w+)\s*,?\s*(?://|$)' -and $line -notmatch '^\s*//' -and $Matches[1] -notin @('public','enum')) {
            $val = $Matches[1]
            if ($val -match '^\d') { continue }
            $results += [pscustomobject]@{
                Type=$typeName; Name=$val; Kind='enumValue'
                ValueType='enum'; Params=''; Writable=$false; Readable=$true
            }
        }
    }
    return $results
}

function Get-TypeDecl($content) {
    if ($content -match '(?:public|internal)\s+(?:(?:abstract|sealed|partial|static|readonly)\s+)*(?:class|struct|enum|interface)\s+(\w+)') {
        return $Matches[1]
    }
    return $null
}

function Get-CatalogKey($fileName) {
    return ([System.IO.Path]::GetFileNameWithoutExtension($fileName) -replace '[^a-zA-Z0-9_]', '_')
}

function Test-TypeFile($content, $typeName) {
    return $null -ne (Get-TypeDecl $content)
}

function Parse-ClassFile($path, $typeName) {
    $lines = Get-Content $path -Encoding UTF8
    $results = @()
    $skipPropertyBlock = $false
    $braceDepth = 0

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*//') { continue }

        if ($skipPropertyBlock) {
            if ($line -match '\{') { $braceDepth++ }
            if ($line -match '\}') {
                $braceDepth--
                if ($braceDepth -le 0) { $skipPropertyBlock = $false; $braceDepth = 0 }
            }
            continue
        }

        # SerializeField (private/protected)
        if ($line -match '^\s+\[SerializeField\]' -and ($i + 1) -lt $lines.Count) {
            $next = $lines[$i + 1]
            if ($next -match '^\s+(?:private|protected)\s+(?:readonly\s+)?([\w<>\[\],\.\?\s]+)\s+(\w+)') {
                $results += [pscustomobject]@{
                    Type=$typeName; Name=$Matches[2]; Kind='uiField'
                    ValueType=$Matches[1].Trim(); Params=''; Writable=$true; Readable=$false
                }
            }
            continue
        }

        if ($line -notmatch '^\s+public\s') { continue }

        # Property block start
        if ($line -match '^\s+public\s+(static\s+)?([\w<>\[\],\.\?\s]+?)\s+(\w+)\s*\{') {
            $ret = $Matches[2].Trim(); $name = $Matches[3]
            if (Should-SkipMember $name $typeName $line) { continue }
            $hasSet = $line -match '\bset\s*;|\bset\s*\{'
            $hasPrivateSet = $line -match 'private\s+set'
            if (-not $hasSet -and ($i + 1) -lt $lines.Count) {
                $block = ($lines[$i..([Math]::Min($i + 8, $lines.Count - 1))] -join ' ')
                $hasSet = $block -match '\bset\s*;|\bset\s*\{'
                $hasPrivateSet = $block -match 'private\s+set'
            }
            $writable = $hasSet -and -not $hasPrivateSet
            $results += [pscustomobject]@{
                Type=$typeName; Name=$name; Kind='property'
                ValueType=$ret; Params=''; Writable=$writable; Readable=$true
            }
            if ($line -notmatch '\}') { $skipPropertyBlock = $true; $braceDepth = 1 }
            continue
        }

        # Method with params on same line
        if ($line -match '^\s+public\s+(static\s+)?([\w<>\[\],\.\?\s]+?)\s+(\w+)\s*\(([^)]*)\)') {
            $ret = $Matches[2].Trim(); $name = $Matches[3]; $params = $Matches[4].Trim()
            if (Should-SkipMember $name $typeName $line) { continue }
            if ($ret -in @('class','struct','enum','interface')) { continue }
            $results += [pscustomobject]@{
                Type=$typeName; Name=$name; Kind='method'
                ValueType=$ret; Params=$params; Writable=$false; Readable=$false
            }
            continue
        }

        # Method no params / field
        if ($line -match '^\s+public\s+(static\s+)?([\w<>\[\],\.\?\s]+?)\s+(\w+)\s*[\(\{;=]') {
            $ret = $Matches[2].Trim(); $name = $Matches[3]
            if (Should-SkipMember $name $typeName $line) { continue }
            if ($ret -in @('class','struct','enum','interface')) { continue }
            if ($line -match '\(') {
                $results += [pscustomobject]@{
                    Type=$typeName; Name=$name; Kind='method'
                    ValueType=$ret; Params=''; Writable=$false; Readable=$false
                }
            } else {
                $readonly = $line -match '\breadonly\b|\bconst\b'
                $results += [pscustomobject]@{
                    Type=$typeName; Name=$name; Kind='field'
                    ValueType=$ret; Params=''; Writable=(-not $readonly); Readable=$true
                }
            }
        }
    }
    return $results
}

$entries = [System.Collections.Generic.List[string]]::new()
$methods = [System.Collections.Generic.List[string]]::new()
$seen = @{}
$stats = @{ files = 0; skipped = 0; read = 0; write = 0; ui = 0; methods = 0 }

function Add-EntryLine($id, $name, $desc, $dir, $cat, $vt, $path) {
    if ($seen.ContainsKey($id)) { return $false }
    $script:seen[$id] = $true
    $d = Escape-Cs $desc
    $n = Escape-Cs $name
    $script:entries.Add("        E(`"$id`", `"$n`", `"$d`", Direction.$dir, Category.$cat, `"$vt`", `"$path`"),")
    return $true
}

function Add-MethodLine($id, $name, $desc, $cat, $path, $ret, $paramList) {
    if ($seen.ContainsKey($id)) { return $false }
    $script:seen[$id] = $true
    $d = Escape-Cs $desc
    $n = Escape-Cs $name
    $hints = Escape-Cs (Format-ParamHints $paramList)
    $script:methods.Add("        M(`"$id`", `"$n`", `"$d`", Category.$cat, `"$path`", `"$ret`", `"$hints`"),")
    return $true
}

Get-ChildItem $dump -Filter '*.cs' -File | Sort-Object Name | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    $declName = Get-TypeDecl $content
    if (-not $declName) {
        $script:stats.skipped++
        return
    }
    $catalogKey = Get-CatalogKey $_.Name
    $typeName = $declName
    $script:stats.files++
    $cat = Get-Category $typeName
    $fileKind = if ($content -match '(?:public|internal)\s+enum\s+$declName\b') { 'enum' }
                elseif ($content -match '(?:public|internal)\s+interface\s+$declName\b') { 'interface' }
                else { 'class' }

    $items = if ($fileKind -eq 'enum') { Parse-EnumFile $_.FullName $catalogKey } else { Parse-ClassFile $_.FullName $catalogKey }

    foreach ($item in $items) {
        $desc = Get-MemberDesc $typeName $item.Name $item.Kind $item.ValueType
        $gp = "$typeName.$($item.Name)"

        if ($item.Kind -eq 'method') {
            if (Add-MethodLine "Method.$($item.Type).$($item.Name)" $item.Name $desc $cat $gp $item.ValueType $item.Params) {
                $script:stats.methods++
            }
            continue
        }

        if ($item.Kind -eq 'uiField') {
            if (Add-EntryLine "UI.$($item.Type).$($item.Name)" $item.Name $desc 'Write' $cat $item.ValueType $gp) {
                $script:stats.ui++
            }
            continue
        }

        if ($item.Kind -eq 'enumValue') {
            if (Add-EntryLine "Enum.$($item.Type).$($item.Name)" $item.Name $desc 'Read' $cat $item.ValueType $gp) {
                $script:stats.read++
            }
            continue
        }

        if ($item.Readable) {
            if (Add-EntryLine "Read.$($item.Type).$($item.Name)" $item.Name $desc 'Read' $cat $item.ValueType $gp) {
                $script:stats.read++
            }
        }

        if ($item.Writable) {
            $writeId = if ($catalogKey -eq 'PlayerSettings') { "Settings.$($item.Name)" } else { "Write.$($item.Type).$($item.Name)" }
            $writeDir = if ($catalogKey -eq 'PlayerSettings') { 'Both' } else { 'Write' }
            if (Add-EntryLine $writeId $item.Name $desc $writeDir $cat $item.ValueType $gp) {
                $script:stats.write++
            }
        }
    }
}

$lines = @(
    '// <auto-generated by tools/Generate-NoGameCatalog.ps1 - full Assembly-CSharp scan>'
    'namespace NuclearOptionSDK.Studio.Services;'
    ''
    'public static partial class NoGameParameterCatalog'
    '{'
    '    private static Entry[] BuildGeneratedEntries() =>'
    '    ['
)
$lines += $entries
$lines += '    ];'
$lines += ''
$lines += '    private static MethodEntry[] BuildGeneratedMethods() =>'
$lines += '    ['
$lines += $methods
$lines += '    ];'
$lines += '}'
[System.IO.File]::WriteAllLines($out, $lines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Scanned $($stats.files) types (skipped $($stats.skipped) non-type files)"
Write-Host "Generated $($entries.Count) entries (read $($stats.read), write $($stats.write), ui $($stats.ui)), $($methods.Count) methods"
