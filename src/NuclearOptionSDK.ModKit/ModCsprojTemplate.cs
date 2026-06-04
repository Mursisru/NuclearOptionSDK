using System.Security;
using System.Text;

namespace NuclearOptionSDK.ModKit;

/// <summary>Non-SDK .csproj for BepInEx mods (no NuGet restore / project.assets.json).</summary>
public static class ModCsprojTemplate
{
    public static string Build(
        string assemblyName,
        string nuclearOptionRoot,
        IReadOnlyList<string> compileFileNames,
        IReadOnlyList<GameAssemblyReferenceCollector.GameAssemblyReference>? gameReferences = null)
    {
        var refs = gameReferences ?? GameAssemblyReferenceCollector.Collect(nuclearOptionRoot);
        var gameRoot = string.IsNullOrWhiteSpace(nuclearOptionRoot)
            ? "$(NuclearOptionRoot)"
            : nuclearOptionRoot.TrimEnd('\\', '/');

        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<Project ToolsVersion=""Current"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Release</Configuration>");
        sb.AppendLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
        sb.AppendLine($"    <ProjectGuid>{{{Guid.NewGuid():D}}}</ProjectGuid>");
        sb.AppendLine("    <OutputType>Library</OutputType>");
        sb.AppendLine($"    <RootNamespace>{assemblyName}</RootNamespace>");
        sb.AppendLine($"    <AssemblyName>{assemblyName}</AssemblyName>");
        sb.AppendLine("    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>");
        sb.AppendLine("    <LangVersion>latest</LangVersion>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <Deterministic>true</Deterministic>");
        sb.AppendLine($"    <NuclearOptionRoot>{XmlEscape(gameRoot)}</NuclearOptionRoot>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("  <PropertyGroup Condition=\" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' \">");
        sb.AppendLine("    <OutputPath>bin\\Release\\</OutputPath>");
        sb.AppendLine("    <Optimize>true</Optimize>");
        sb.AppendLine("  </PropertyGroup>");

        if (refs.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var reference in refs)
            {
                sb.AppendLine($@"    <Reference Include=""{XmlEscape(reference.AssemblyName)}"">");
                sb.AppendLine($@"      <HintPath>{XmlEscape(reference.HintPath)}</HintPath>");
                sb.AppendLine("      <Private>False</Private>");
                sb.AppendLine("    </Reference>");
            }

            sb.AppendLine("  </ItemGroup>");
        }
        else
        {
            sb.AppendLine("  <!-- Game references: set Nuclear Option path in Studio Settings, then rebuild. -->");
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine($@"    <Reference Include=""BepInEx"">");
            sb.AppendLine($@"      <HintPath>{XmlEscape(gameRoot)}\BepInEx\core\BepInEx.dll</HintPath>");
            sb.AppendLine("      <Private>False</Private>");
            sb.AppendLine("    </Reference>");
            sb.AppendLine($@"    <Reference Include=""UnityEngine"">");
            sb.AppendLine($@"      <HintPath>{XmlEscape(gameRoot)}\NuclearOption_Data\Managed\UnityEngine.dll</HintPath>");
            sb.AppendLine("      <Private>False</Private>");
            sb.AppendLine("    </Reference>");
            sb.AppendLine("  </ItemGroup>");
        }

        sb.AppendLine("  <ItemGroup>");
        foreach (var file in compileFileNames)
        {
            sb.AppendLine($@"    <Compile Include=""{XmlEscape(file)}"" />");
        }

        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine(@"  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />");
        sb.AppendLine("</Project>");
        return sb.ToString();
    }

    private static string XmlEscape(string value) => SecurityElement.Escape(value) ?? value;
}
