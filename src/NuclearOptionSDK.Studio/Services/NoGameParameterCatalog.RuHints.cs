namespace NuclearOptionSDK.Studio.Services;

public static partial class NoGameParameterCatalog
{
    private readonly record struct RuHint(string Title, string Description);

    private static readonly Dictionary<string, RuHint> RuByGamePath =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Aircraft.speed"] = new("Speed", "Aircraft speed, m/s."),
            ["Unit.speed"] = new("Unit speed", "Object speed, m/s."),
            ["Unit.radarAlt"] = new("Altitude AGL", "Radar altitude above ground, m."),
            ["Aircraft.fuelLevel"] = new("Fuel", "Remaining fuel 0–1."),
            ["Aircraft.gForce"] = new("G-load", "Current g-load."),
            ["ControlInputs.throttle"] = new("Throttle", "Throttle 0–1."),
            ["ControlInputs.pitch"] = new("Pitch", "Pitch −1…1."),
            ["ControlInputs.roll"] = new("Roll", "Roll −1…1."),
            ["ControlInputs.yaw"] = new("Yaw", "Yaw −1…1."),
            ["ControlInputs.brake"] = new("Brake", "Wheel brake 0–1."),
            ["Aircraft.gearDeployed"] = new("Gear", "Gear down (true/false)."),
            ["Aircraft.flightAssist"] = new("Flight Assist", "FA mode on/off."),
            ["Aircraft.Ignition"] = new("Ignition", "Engine running."),
            ["Aircraft.ecmIntensity"] = new("ECM", "ECM intensity 0–1."),
            ["Unit.RCS"] = new("RCS", "Radar cross section."),
            ["Unit.disabled"] = new("Destroyed", "Unit disabled."),
            ["WeaponStation.Ammo"] = new("Ammo", "Current station ammo."),
            ["WeaponStation.FullAmmo"] = new("Max ammo", "Station FullAmmo."),
            ["CombatHUD.HasTargets"] = new("Has targets", "CombatHUD.HasTargets."),
            ["CombatHUD.jamAccumulation"] = new("HUD jam", "Jam level on HUD."),
            ["CombatHUD.landingMode"] = new("Landing mode", "Landing mode on CombatHUD."),
            ["CombatHUD.turretAutoControl"] = new("Auto turret", "Turret auto control."),
            ["DynamicMap.mapMaximized"] = new("Map maximized", "Map maximized."),
            ["DynamicMap.mapDisplayFactor"] = new("Map scale", "Map display factor."),
            ["PlayerSettings.hudTextSize"] = new("HUD size", "HUD text size."),
            ["PlayerSettings.lagPip"] = new("Lag pip", "Show lag pip."),
            ["PlayerSettings.rangeCircle"] = new("Range circle", "Range circle on HUD."),
            ["PlayerSettings.gauges"] = new("Gauges", "Show gauges."),
            ["HUDMissileState.minRange"] = new("Missile min range", "Launch min range."),
            ["HUDMissileState.maxRange"] = new("Missile max range", "Launch max range."),
            ["HUDMissileState.noEscapeRange"] = new("NEZ", "No-escape zone."),
            ["HUDMissileState.allRequirementsMet"] = new("Launch requirements", "All requirements met."),
            ["Missile.seekerMode"] = new("Seeker mode", "Missile seeker mode."),
            ["CountermeasureManager.activeIndex"] = new("Active CM", "Flare/jammer index."),
            ["GameManager.gameState"] = new("Game state", "GameState enum."),
            ["GameplayUI.GameIsPaused"] = new("Paused", "Game paused."),
            ["Aircraft.GetInputs"] = new("Get inputs", "ControlInputs: pitch/roll/yaw/throttle."),
            ["Aircraft.GetFuelLevel"] = new("Get fuel", "Fuel 0–1."),
            ["FlightHud.EnableCanvas"] = new("Enable HUD", "Show/hide FlightHud canvas."),
            ["FlightHud.GetHUDCenter"] = new("HUD center", "Reticle center Transform."),
            ["FlightHud.RefreshSettings"] = new("Refresh HUD", "Re-read PlayerSettings."),
            ["CombatHUD.GetTargetList"] = new("Target list", "List<Unit> selected targets."),
            ["CombatHUD.DeselectAll"] = new("Deselect targets", "Deselect all targets."),
            ["WeaponManager.SetActiveStation"] = new("Switch station", "Active weapon station index."),
            ["WeaponManager.AddTargetList"] = new("Add target", "Add target to list."),
            ["Pilot.Fire"] = new("Fire", "Fire — Harmony gate for blocking."),
            ["PlayerSettings.ApplyPrefs"] = new("Apply settings", "Apply player HUD prefs."),
            ["MusicManager.CrossFadeMusic"] = new("Change music", "CrossFade music clip."),
            ["DynamicMap.UpdateIcons"] = new("Refresh map icons", "Refresh map icons."),
            ["GameManager.SetGameState"] = new("Set GameState", "Set game state."),
            ["SoundManager.PlayInterfaceOneShot"] = new("UI sound", "Play interface one-shot."),
            ["CameraCockpitState.UpdateState"] = new("Update camera", "FOV + position cockpit cam."),
            ["NightVision.UpdateGain"] = new("NVG gain", "Night vision gain."),
            ["AeroPart.GetWingArea"] = new("Wing area", "Wing area aero part."),
            ["AeroPart.GetAltitude"] = new("Altitude (aero)", "Altitude aero part."),
            ["AeroPart.Repair"] = new("Repair part", "Repair aero part."),
            ["AeroPart.ModifyDrag"] = new("Modify drag", "Modify drag coefficient."),
            ["AeroPart.ModifyWingArea"] = new("Modify wing area", "Modify wing area."),
        };

    private static readonly Dictionary<string, RuHint> RuByMember =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["speed"] = new("Speed", "Speed, m/s."),
            ["radarAlt"] = new("Altitude AGL", "Radar altitude."),
            ["fuelLevel"] = new("Fuel", "Fuel level 0–1."),
            ["gForce"] = new("G-load", "G-load."),
            ["throttle"] = new("Throttle", "Throttle 0–1."),
            ["pitch"] = new("Pitch", "Pitch input."),
            ["roll"] = new("Roll", "Roll input."),
            ["yaw"] = new("Yaw", "Yaw input."),
            ["brake"] = new("Brake", "Wheel brake."),
            ["ammo"] = new("Ammo", "Ammo count."),
            ["Ammo"] = new("Ammo", "Ammo count."),
            ["FullAmmo"] = new("Max ammo", "Maximum ammo."),
            ["gearDeployed"] = new("Gear down", "Gear down state."),
            ["flightAssist"] = new("Flight Assist", "Flight assist mode."),
            ["Ignition"] = new("Ignition", "Engine ignition."),
            ["disabled"] = new("Destroyed", "Unit disabled."),
            ["RCS"] = new("RCS", "Radar cross section."),
            ["ecmIntensity"] = new("ECM", "ECM intensity."),
            ["jamAccumulation"] = new("HUD jam", "Jam accumulation."),
            ["HasTargets"] = new("Has targets", "Has selected targets."),
            ["landingMode"] = new("Landing mode", "Landing mode."),
            ["turretAutoControl"] = new("Auto turret", "Turret auto."),
            ["mapMaximized"] = new("Map maximized", "Map maximized."),
            ["mapDisplayFactor"] = new("Map scale", "Map scale factor."),
            ["hudTextSize"] = new("HUD text size", "HUD font size."),
            ["hudColorR"] = new("HUD color (R)", "HUD color red channel."),
            ["hudColorG"] = new("HUD color (G)", "HUD color green channel."),
            ["hudColorB"] = new("HUD color (B)", "HUD color blue channel."),
            ["lagPip"] = new("Lag pip", "Lag pip toggle."),
            ["rangeCircle"] = new("Range circle", "Range circle HUD."),
            ["gauges"] = new("Gauges", "Show gauges."),
            ["minRange"] = new("Min range", "Minimum range."),
            ["maxRange"] = new("Max range", "Maximum range."),
            ["noEscapeRange"] = new("NEZ", "No-escape zone range."),
            ["seekerMode"] = new("Seeker mode", "Missile seeker mode."),
            ["activeIndex"] = new("Active index", "Active selection index."),
            ["targetList"] = new("Target list", "Target list."),
            ["targetText"] = new("Target text", "Target info text."),
            ["targetDesignator"] = new("Target designator", "Target designator."),
            ["airspeedDisplay"] = new("Airspeed", "Airspeed display text."),
            ["AoAText"] = new("AoA text", "Angle of attack text."),
            ["fuelReading"] = new("Fuel reading", "Fuel gauge reading."),
            ["compass"] = new("Compass", "Heading compass."),
            ["velocityVector"] = new("Velocity vector", "Velocity vector HUD."),
            ["waterline"] = new("Waterline", "Waterline indicator."),
            ["fieldOfView"] = new("FOV", "Camera field of view."),
            ["defaultFoV"] = new("Default FOV", "Default camera FOV."),
            ["missionTime"] = new("Mission time", "Mission time display."),
            ["sortieScore"] = new("Sortie score", "Sortie score."),
            ["weaponManager"] = new("Weapon manager", "Weapon manager ref."),
            ["countermeasureManager"] = new("CM manager", "Countermeasure manager."),
            ["cockpit"] = new("Cockpit", "Cockpit part."),
            ["loadout"] = new("Loadout", "Aircraft loadout."),
            ["playerRef"] = new("Player (ref)", "Player reference."),
            ["GameIsPaused"] = new("Paused", "Game paused."),
            ["gameState"] = new("Game state", "Game state."),
            ["allRequirementsMet"] = new("Requirements met", "Launch requirements met."),
            ["impactDamage"] = new("Impact damage", "Impact damage."),
            ["mass"] = new("Mass", "Mass value."),
            ["thrust"] = new("Thrust", "Thrust force."),
            ["burnTime"] = new("Burn time", "Burn time."),
            ["topSpeed"] = new("Max speed", "Top speed."),
            ["Armed"] = new("Armed", "Armed state."),
            ["Safety"] = new("Safety", "Weapon safety."),
            ["Rearmable"] = new("Rearmable", "Rearmable flag."),
            ["owner"] = new("Owner", "Owner unit."),
            ["timeSinceSpawn"] = new("Time since spawn", "Seconds since spawn."),
            ["IsJammed"] = new("Radar jammed", "Radar is jammed."),
            ["GetInputs"] = new("Get inputs", "Get pilot control inputs."),
            ["GetFuelLevel"] = new("Get fuel", "Get fuel level 0–1."),
            ["GetTargetList"] = new("Target list", "Get target list."),
            ["SetActiveStation"] = new("Switch station", "Set active weapon station."),
            ["EnableCanvas"] = new("Enable canvas", "Enable/disable HUD canvas."),
            ["RefreshSettings"] = new("Refresh settings", "Refresh HUD from settings."),
            ["ApplyPrefs"] = new("Apply prefs", "Apply player preferences."),
            ["Fire"] = new("Fire", "Fire weapon."),
            ["UpdateState"] = new("Update state", "Update component state."),
            ["UpdateIcons"] = new("Update icons", "Update map icons."),
            ["CrossFadeMusic"] = new("Change music", "Cross-fade music."),
            ["PlayInterfaceOneShot"] = new("UI sound", "Play UI sound."),
            ["DeselectAll"] = new("Deselect all", "Deselect all."),
            ["AddTargetList"] = new("Add target", "Add to target list."),
            ["SetGameState"] = new("Set state", "Set game state."),
            ["GetHUDCenter"] = new("HUD center", "Get HUD center transform."),
            ["UpdateGain"] = new("Update gain", "Update NVG gain."),
            ["Repair"] = new("Repair", "Repair part."),
            ["Awake"] = new("Awake", "Unity Awake lifecycle."),
            ["Update"] = new("Update", "Unity Update tick."),
            ["Start"] = new("Start", "Unity Start lifecycle."),
            ["enabled"] = new("Enabled / active", "Component or object enabled."),
            ["isActive"] = new("Active", "GameObject active in scene."),
            ["IsActive"] = new("Active", "GameObject active in scene."),
            ["active"] = new("Active flag", "Active flag."),
            ["time"] = new("Time", "Time (sec or game)."),
            ["source"] = new("Source", "AudioSource or data source."),
            ["target"] = new("Target", "Target reference."),
            ["value"] = new("Value", "Number or flag."),
            ["duration"] = new("Duration", "Duration, sec."),
            ["delay"] = new("Delay", "Delay, sec."),
            ["interval"] = new("Interval", "Repeat interval."),
            ["radius"] = new("Radius", "Radius, m."),
            ["height"] = new("Altitude", "Altitude, m."),
            ["visible"] = new("Visibility", "Shown or hidden."),
            ["locked"] = new("Lock-on", "Lock-on."),
            ["maxSpeed"] = new("Max speed", "Speed limit."),
            ["stallSpeed"] = new("Stall", "Stall speed."),
            ["collective"] = new("Collective", "Rotor pitch."),
            ["heading"] = new("Heading", "Heading, °."),
            ["rpm"] = new("RPM", "RPM."),
        };

    private static readonly Dictionary<string, string> RuTypeLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Aircraft"] = "Aircraft",
            ["Unit"] = "Unit",
            ["Weapon"] = "Weapon",
            ["WeaponStation"] = "Station",
            ["WeaponManager"] = "Weapons",
            ["Missile"] = "Missile",
            ["Pilot"] = "Pilot",
            ["ControlInputs"] = "Input",
            ["FlightHud"] = "FlightHud",
            ["CombatHUD"] = "Combat HUD",
            ["DynamicMap"] = "Map",
            ["PlayerSettings"] = "Settings",
            ["GameManager"] = "Game",
            ["CountermeasureManager"] = "Countermeasures",
            ["Radar"] = "Radar",
            ["AeroPart"] = "Aero part",
            ["UnitPart"] = "Unit part",
            ["HUDMissileState"] = "HUD missile",
            ["HUDBombingState"] = "HUD bombs",
            ["VirtualMFD"] = "MFD",
            ["HeadMountedDisplay"] = "HMD",
            ["AoADisplay"] = "AoA display",
            ["SpeedGauge"] = "Speed gauge",
            ["FuelGauge"] = "Fuel HUD",
            ["Altitude"] = "Altitude HUD",
            ["GIndicators"] = "G indicator",
            ["MusicManager"] = "Music",
            ["SoundManager"] = "Sound",
            ["CameraCockpitState"] = "Camera",
            ["NightVision"] = "NVG",
            ["FlareEjector"] = "Flare",
            ["RadarJammer"] = "Jammer",
            ["TargetDetector"] = "Detector",
            ["MissileWarning"] = "MWS",
            ["GameplayUI"] = "Game UI",
            ["Airbase"] = "Airbase",
            ["Hangar"] = "Hangar",
            ["BallisticMissileGuidance"] = "Ballistic guidance",
            ["TargetCam"] = "Target cam",
            ["ExplosionAudio"] = "Explosion sound",
            ["RandomSound"] = "Random sound",
            ["PowerSupply"] = "Power supply",
            ["PropFan"] = "Prop fan",
            ["JetNozzle"] = "Nozzle",
            ["TurbineFireSound"] = "Combustion sound",
            ["ControlSurface"] = "Control surface",
            ["FlightModel"] = "Flight model",
            ["Cockpit"] = "Cockpit",
            ["Loadout"] = "Loadout",
            ["TrackingInfo"] = "Tracking",
            ["GlobalPosition"] = "World position",
            ["Capture"] = "Base capture",
            ["Building"] = "Building",
            ["Runway"] = "Runway",
            ["WarheadStorage"] = "Warhead storage",
            ["SavedAirbase"] = "Saved airbase",
            ["Session"] = "Session",
            ["General"] = "General",
        };

    private static readonly Dictionary<string, string> RuWordParts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["speed"] = "speed",
            ["alt"] = "altitude",
            ["altitude"] = "altitude",
            ["fuel"] = "fuel",
            ["gear"] = "gear",
            ["weapon"] = "weapon",
            ["target"] = "target",
            ["radar"] = "radar",
            ["missile"] = "missile",
            ["jam"] = "jam",
            ["throttle"] = "throttle",
            ["pitch"] = "pitch",
            ["roll"] = "roll",
            ["yaw"] = "yaw",
            ["damage"] = "damage",
            ["count"] = "count",
            ["level"] = "level",
            ["time"] = "time",
            ["max"] = "max",
            ["min"] = "min",
            ["active"] = "active",
            ["disabled"] = "disabled",
            ["enabled"] = "enabled",
            ["color"] = "color",
            ["text"] = "text",
            ["display"] = "display",
            ["manager"] = "manager",
            ["state"] = "state",
            ["range"] = "range",
            ["ammo"] = "ammo",
            ["index"] = "index",
            ["mass"] = "mass",
            ["force"] = "force",
            ["position"] = "position",
            ["rotation"] = "rotation",
            ["scale"] = "scale",
            ["visible"] = "visibility",
            ["volume"] = "volume",
            ["map"] = "map",
            ["icon"] = "icon",
            ["marker"] = "marker",
            ["warning"] = "warning",
            ["hud"] = "HUD",
            ["owner"] = "owner",
            ["source"] = "source",
            ["guidance"] = "guidance",
            ["ballistic"] = "ballistic",
            ["turbine"] = "turbine",
            ["nozzle"] = "nozzle",
            ["fan"] = "fan",
            ["supply"] = "power",
            ["explosion"] = "explosion",
            ["random"] = "random",
            ["fire"] = "fire",
            ["cam"] = "camera",
            ["track"] = "track",
            ["spawn"] = "spawn",
            ["pause"] = "pause",
            ["lock"] = "lock",
            ["flap"] = "flap",
            ["slat"] = "slat",
            ["aileron"] = "aileron",
            ["rudder"] = "rudder",
            ["elevator"] = "elevator",
            ["stall"] = "stall",
            ["gforce"] = "g-load",
            ["altimeter"] = "altimeter",
            ["compass"] = "compass",
            ["autopilot"] = "autopilot",
            ["countermeasure"] = "countermeasure",
            ["flare"] = "flare",
            ["chaff"] = "chaff",
            ["jammer"] = "jammer",
            ["designator"] = "designator",
            ["ripple"] = "ripple",
            ["station"] = "station",
            ["hardpoint"] = "hardpoint",
            ["occupied"] = "occupied",
            ["capture"] = "capture",
            ["runway"] = "runway",
            ["hangar"] = "hangar",
            ["airbase"] = "airbase",
        };

    private static bool TryResolveRuEntry(Entry e, out string title, out string description)
    {
        if (RuByGamePath.TryGetValue(e.GamePath, out var pathHint))
        {
            title = PrefixDirectionTitle(e, pathHint.Title);
            description = PrefixDirectionDesc(e, pathHint.Description, e.GamePath);
            return true;
        }

        var member = MemberName(e.GamePath, e.Title);
        if (RuByMember.TryGetValue(member, out var memberHint))
        {
            var typeLabel = ResolveTypeLabelRu(TypeName(e.GamePath));
            title = ComposeFieldTitle(typeLabel, memberHint.Title);
            description = PrefixDirectionDesc(e, memberHint.Description, e.GamePath);
            return true;
        }

        var typeName = TypeName(e.GamePath);
        var typeLabel2 = ResolveTypeLabelRu(typeName);
        var memberRu = ResolveMemberLabelRu(member);
        title = ComposeFieldTitle(typeLabel2, memberRu);
        description = BuildFallbackDesc(e, typeName, member);
        return true;
    }

    private static bool TryResolveRuMethod(MethodEntry m, out string title, out string description)
    {
        if (RuByGamePath.TryGetValue(m.GamePath, out var hint))
        {
            title = hint.Title;
            description = BuildMethodDesc(m, hint.Description);
            return true;
        }

        var member = MemberName(m.GamePath, m.Title);
        if (RuByMember.TryGetValue(member, out var memberHint))
        {
            var typeLabel = ResolveTypeLabelRu(TypeName(m.GamePath));
            title = string.IsNullOrEmpty(typeLabel)
                ? memberHint.Title
                : ComposeFieldTitle(typeLabel, memberHint.Title);
            description = BuildMethodDesc(m, memberHint.Description);
            return true;
        }

        var verbTitle = MethodVerbTitleRu(member);
        if (verbTitle != null)
        {
            title = verbTitle;
            description = BuildMethodDesc(m, $"Call {m.GamePath}.");
            return true;
        }

        var typeLabel2 = ResolveTypeLabelRu(TypeName(m.GamePath));
        title = ComposeFieldTitle(typeLabel2, ResolveMemberLabelRu(member));
        description = BuildMethodDesc(m, $"Game method {m.GamePath}.");
        return true;
    }

    private static string ComposeFieldTitle(string typeLabel, string memberLabel)
    {
        if (string.IsNullOrWhiteSpace(memberLabel))
        {
            return typeLabel;
        }

        if (string.IsNullOrWhiteSpace(typeLabel))
        {
            return memberLabel;
        }

        return $"{typeLabel} · {memberLabel}";
    }

    private static string ResolveMemberLabelRu(string member)
    {
        if (RuByMember.TryGetValue(member, out var hint))
        {
            return hint.Title;
        }

        return HumanizeRu(member);
    }

    private static readonly (string Suffix, string LabelRu)[] RuTypeSuffixes =
    [
        ("Manager", "manager"),
        ("Controller", "controller"),
        ("Display", "display"),
        ("HUD", "HUD"),
        ("Hud", "HUD"),
        ("Audio", "audio"),
        ("Sound", "sound"),
        ("Settings", "settings"),
        ("State", "state"),
        ("System", "system"),
        ("Module", "module"),
        ("Handler", "handler"),
        ("Provider", "provider"),
        ("Tracker", "tracker"),
        ("Guidance", "guidance"),
        ("Weapon", "weapon"),
        ("Missile", "missile"),
        ("Aircraft", "AC"),
        ("Unit", "unit"),
        ("Panel", "panel"),
        ("Widget", "widget"),
        ("Indicator", "indicator"),
        ("Gauge", "gauge"),
        ("Camera", "camera"),
        ("Effect", "effect"),
        ("Storage", "storage"),
    ];

    private static string ResolveTypeLabelRu(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        if (RuTypeLabels.TryGetValue(typeName, out var exact))
        {
            return exact;
        }

        foreach (var (suffix, suffixRu) in RuTypeSuffixes)
        {
            if (!typeName.EndsWith(suffix, StringComparison.Ordinal) || typeName.Length <= suffix.Length)
            {
                continue;
            }

            var stem = typeName[..^suffix.Length];
            if (stem.Length == 0)
            {
                return suffixRu;
            }

            var stemRu = ResolveTypeLabelRu(stem);
            if (!string.IsNullOrEmpty(stemRu) && !string.Equals(stemRu, Humanize(stem), StringComparison.OrdinalIgnoreCase))
            {
                return $"{stemRu} · {suffixRu}";
            }

            if (!string.IsNullOrEmpty(stemRu))
            {
                return $"{stemRu} · {suffixRu}";
            }
        }

        var spaced = Humanize(typeName);
        var words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var translated = false;
        for (var i = 0; i < words.Length; i++)
        {
            if (RuWordParts.TryGetValue(words[i], out var ru)
                || RuWordParts.TryGetValue(words[i].ToLowerInvariant(), out ru))
            {
                words[i] = char.ToUpperInvariant(ru[0]) + ru[1..];
                translated = true;
            }
        }

        if (translated)
        {
            return string.Join(' ', words);
        }

        return HumanizeRu(typeName);
    }

    private static string PrefixDirectionTitle(Entry e, string baseTitle) => e.Direction switch
    {
        Direction.Write when e.Id.StartsWith("UI.", StringComparison.Ordinal) => $"HUD UI: {baseTitle}",
        Direction.Write => $"Write: {baseTitle}",
        Direction.Both => baseTitle,
        _ => baseTitle
    };

    private static string PrefixDirectionDesc(Entry e, string baseDesc, string gamePath)
    {
        var core = string.IsNullOrWhiteSpace(baseDesc) ? gamePath : baseDesc;
        return e.Direction switch
        {
            Direction.Read => $"Read {gamePath}. {core}",
            Direction.Write when e.Id.StartsWith("UI.", StringComparison.Ordinal) =>
                $"SerializeField / HUD UI: {gamePath}. {core}",
            Direction.Write => $"Write {gamePath}. {core}",
            Direction.Both => $"Read/write {gamePath}. {core}",
            _ => core
        };
    }

    private static string BuildFallbackDesc(Entry e, string typeName, string member)
    {
        var typeLabel = ResolveTypeLabelRu(typeName);
        var who = string.IsNullOrEmpty(typeLabel) ? typeName : typeLabel;
        return e.Direction switch
        {
            Direction.Read => $"Read field {who}.{member} ({e.ValueType}).",
            Direction.Write when e.Id.StartsWith("UI.", StringComparison.Ordinal) =>
                $"HUD UI field {gamePathShort(typeName, member)} ({e.ValueType}) — color, text, visibility.",
            Direction.Write => $"Write {who}.{member} ({e.ValueType}).",
            Direction.Both => $"Read/write {who}.{member} ({e.ValueType}).",
            _ when e.Id.StartsWith("Enum.", StringComparison.Ordinal) =>
                $"Enum value {typeName}.{member}.",
            _ => $"{who}.{member} ({e.ValueType})."
        };
    }

    private static string gamePathShort(string typeName, string member) => $"{typeName}.{member}";

    private static string BuildMethodDesc(MethodEntry m, string core)
    {
        if (m.ParameterHints.Length > 0 && m.ParameterHints[0] is not ("none" or "—" or "-"))
        {
            return $"{core} Params: {string.Join(", ", m.ParameterHints)}. Returns: {m.ReturnType}.";
        }

        return $"{core} Returns: {m.ReturnType}.";
    }

    private static string? MethodVerbTitleRu(string name)
    {
        if (name.StartsWith("Get", StringComparison.Ordinal) && name.Length > 3)
            return $"Get {HumanizeRu(name[3..]).ToLowerInvariant()}";
        if (name.StartsWith("Set", StringComparison.Ordinal) && name.Length > 3)
            return $"Set {HumanizeRu(name[3..]).ToLowerInvariant()}";
        if (name.StartsWith("Is", StringComparison.Ordinal) && name.Length > 2)
            return $"Check: {HumanizeRu(name[2..]).ToLowerInvariant()}";
        if (name.StartsWith("Has", StringComparison.Ordinal) && name.Length > 3)
            return $"Has {HumanizeRu(name[3..]).ToLowerInvariant()}";
        if (name.StartsWith("Add", StringComparison.Ordinal) && name.Length > 3)
            return $"Add {HumanizeRu(name[3..]).ToLowerInvariant()}";
        if (name.StartsWith("Remove", StringComparison.Ordinal) && name.Length > 6)
            return $"Remove {HumanizeRu(name[6..]).ToLowerInvariant()}";
        if (name.StartsWith("Update", StringComparison.Ordinal) && name.Length > 6)
            return $"Update {HumanizeRu(name[6..]).ToLowerInvariant()}";
        if (name.StartsWith("Enable", StringComparison.Ordinal) && name.Length > 6)
            return $"Enable {HumanizeRu(name[6..]).ToLowerInvariant()}";
        if (name.StartsWith("Disable", StringComparison.Ordinal) && name.Length > 7)
            return $"Disable {HumanizeRu(name[7..]).ToLowerInvariant()}";
        if (name.StartsWith("Apply", StringComparison.Ordinal) && name.Length > 5)
            return $"Apply {HumanizeRu(name[5..]).ToLowerInvariant()}";
        if (name.StartsWith("Refresh", StringComparison.Ordinal) && name.Length > 7)
            return $"Refresh {HumanizeRu(name[7..]).ToLowerInvariant()}";
        if (name.StartsWith("CrossFade", StringComparison.Ordinal))
            return "Cross-fade";
        if (name.Equals("Fire", StringComparison.OrdinalIgnoreCase))
            return "Fire";
        return null;
    }

    private static string HumanizeRu(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (RuByMember.TryGetValue(name, out var hint))
        {
            return hint.Title;
        }

        var spaced = Humanize(name);
        var words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (RuWordParts.TryGetValue(words[i], out var ru))
            {
                words[i] = ru;
            }
            else if (RuWordParts.TryGetValue(words[i].ToLowerInvariant(), out var ruLower))
            {
                words[i] = ruLower;
            }
        }

        return string.Join(' ', words);
    }
}
