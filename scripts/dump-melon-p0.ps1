# Dump Melon vs Bep Assembly-CSharp symbols for P0 gate.
# Usage:
#   $env:FUSIONRPG_ML_GAMEDIR = "<Blooms Game Files>"
#   .\scripts\dump-melon-p0.ps1
param(
    [string]$MlGameDir = $env:FUSIONRPG_ML_GAMEDIR,
    [string]$BepGameDir = $(if ($env:FUSIONRPG_GAME_DIR) { $env:FUSIONRPG_GAME_DIR } else { (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path })
)

$ErrorActionPreference = "Stop"
if (-not $MlGameDir -or -not (Test-Path $MlGameDir)) {
    throw "Set FUSIONRPG_ML_GAMEDIR to a MelonLoader game folder (Blooms 3.8.1)."
}

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$tmp = Join-Path $env:TEMP "fusionrpg-p0-dump"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$melonAsm = Join-Path $MlGameDir "MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"
$melonNet6 = Join-Path $MlGameDir "MelonLoader\net6"
$bepAsm = Join-Path $BepGameDir "BepInEx\interop\Assembly-CSharp.dll"
$bepCore = Join-Path $BepGameDir "BepInEx\core"

if (-not (Test-Path $melonAsm)) { throw "Missing $melonAsm" }
if (-not (Test-Path $bepAsm)) { Write-Warning "Bep Assembly-CSharp missing at $bepAsm — Melon-only dump." }

@'
using System; using System.Linq; using System.Reflection; using System.Runtime.Loader;
var path = args[0];
var extraDirs = args.Skip(1).ToArray();
var alc = new AssemblyLoadContext("p0", true);
alc.Resolving += (c, n) => {
  foreach (var dir in extraDirs.Append(Path.GetDirectoryName(path)!)) {
    var cand = Path.Combine(dir!, n.Name + ".dll");
    if (File.Exists(cand)) { try { return c.LoadFromAssemblyPath(cand); } catch {} }
  }
  return null;
};
var a = alc.LoadFromAssemblyPath(path);
Type[] types;
try { types = a.GetTypes(); }
catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
foreach (var n in new[] { "Plant", "Zombie", "Board", "CreateZombie" }) {
  var t = types.FirstOrDefault(x => x.Name == n);
  Console.WriteLine("== " + n + " ==");
  if (t == null) { Console.WriteLine("  (missing)"); continue; }
  Console.WriteLine("  " + t.FullName);
  foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
      .Where(m => m.Name.Contains("TakeDamage", StringComparison.Ordinal) || m.Name.StartsWith("SetZombie", StringComparison.Ordinal))) {
    var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
    Console.WriteLine("  " + m.Name + "(" + m.GetParameters().Length + ") " + ps);
  }
}
var globalCount = types.Count(t => string.IsNullOrEmpty(t.Namespace));
var il2 = types.Count(t => (t.Namespace ?? "").StartsWith("Il2Cpp", StringComparison.Ordinal));
Console.WriteLine($"stats global={globalCount} il2cppNs={il2} total={types.Length}");
'@ | Set-Content (Join-Path $tmp "Program.cs")

@'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>
'@ | Set-Content (Join-Path $tmp "p0.csproj")

Push-Location $tmp
dotnet build -v q | Out-Null
Write-Host "==== MELON ($MlGameDir) ===="
dotnet run -- $melonAsm $melonNet6
if (Test-Path $bepAsm) {
    Write-Host "==== BEP ($BepGameDir) ===="
    dotnet run -- $bepAsm $bepCore
}
Pop-Location
Write-Host "Update docs/research/melonloader-assembly-csharp-p0.md with any deltas."
