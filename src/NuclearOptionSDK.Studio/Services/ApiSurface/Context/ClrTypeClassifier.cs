using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Context;

public static class ClrTypeClassifier
{
    private static readonly HashSet<string> Primitives = new(StringComparer.OrdinalIgnoreCase)
    {
        "Int32", "Int64", "Int16", "UInt32", "UInt64", "UInt16",
        "Single", "Double", "Boolean", "Byte", "SByte", "Char", "Decimal"
    };

    private static readonly HashSet<string> GameObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Player", "Aircraft", "Unit", "WeaponStation", "Missile", "Bomb",
        "CombatHUD", "FlightHud", "GameplayUI", "WeaponManager", "ControlInputs"
    };

    public static ClrTypeKind Classify(string clrTypeName)
    {
        if (string.IsNullOrWhiteSpace(clrTypeName))
        {
            return ClrTypeKind.Other;
        }

        if (clrTypeName.Equals("Void", StringComparison.OrdinalIgnoreCase))
        {
            return ClrTypeKind.Void;
        }

        if (clrTypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
        {
            return ClrTypeKind.String;
        }

        if (Primitives.Contains(clrTypeName))
        {
            return ClrTypeKind.Primitive;
        }

        if (clrTypeName.StartsWith("UnityEngine.", StringComparison.Ordinal)
            || clrTypeName is "Vector2" or "Vector3" or "Quaternion" or "Transform" or "GameObject")
        {
            return ClrTypeKind.UnityEngine;
        }

        if (GameObjectTypes.Contains(clrTypeName) || LooksLikeGameType(clrTypeName))
        {
            return ClrTypeKind.GameObject;
        }

        return ClrTypeKind.Other;
    }

    private static bool LooksLikeGameType(string name)
    {
        if (name.EndsWith("UI", StringComparison.Ordinal) && name.Length > 2)
        {
            return true;
        }

        return name.Contains("Manager", StringComparison.Ordinal)
               || name.Contains("Controller", StringComparison.Ordinal)
               || name.Contains("Station", StringComparison.Ordinal)
               || name.Contains("Display", StringComparison.Ordinal)
               || name.Contains("Hud", StringComparison.OrdinalIgnoreCase);
    }
}
