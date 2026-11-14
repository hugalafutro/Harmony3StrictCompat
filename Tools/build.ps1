# Builds 1.7\Assemblies\Harmony3StrictCompat.dll from Source\*.cs against the game's own runtime DLLs.
$ErrorActionPreference = 'Stop'
$mod = Split-Path $PSScriptRoot
$game = 'M:\Steam\steamapps\common\RimWorld'
$csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk\8.*\Roslyn\bincore\csc.dll' | Sort-Object Name | Select-Object -Last 1).FullName
$refs = @('System.Private.CoreLib','System.Runtime','System.Collections','System.Collections.Concurrent','System.Linq','System.Private.Xml','System.Xml.ReaderWriter','System.Xml.XmlDocument','System.Reflection.Primitives','System.Reflection.Emit.ILGeneration','System.Text.RegularExpressions','netstandard','mscorlib','Assembly-CSharp','UnityStubs') |
    ForEach-Object { "-r:$game\$_.dll" }
$refs += "-r:M:\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll"
& dotnet $csc -nologo -nostdlib -noconfig -target:library -optimize+ -langversion:latest -nowarn:CS1701,CS1702 `
    -out:"$mod\1.7\Assemblies\Harmony3StrictCompat.dll" @refs (Get-ChildItem "$mod\Source\*.cs").FullName

# Useful Marks add-on: derives from its types, so it is its own DLL, outside Assemblies; the main DLL loads it at startup.
$um = 'M:\Steam\steamapps\workshop\content\294100\3506573327\1.7\Assemblies\UsefulColonistBar.dll'
if (Test-Path $um) {
    New-Item -ItemType Directory -Force "$mod\Compat" | Out-Null
    & dotnet $csc -nologo -nostdlib -noconfig -target:library -optimize+ -langversion:latest -nowarn:CS1701,CS1702 `
        -out:"$mod\Compat\Harmony3StrictCompat.UsefulMarks.dll" @refs "-r:$um" (Get-ChildItem "$mod\Source\UsefulMarks\*.cs").FullName
}

# Vehicle Framework add-on (Too Many Mods' Merchant Guild landing rules): own DLL for the same reason.
$ws = 'M:\Steam\steamapps\workshop\content\294100'
$vf = @("$ws\3014915404\1.7\Assemblies\Vehicles.dll", "$ws\3014915404\1.7\Assemblies\SmashTools.dll", "$ws\2023507013\1.7\Assemblies\VEF.dll", "$ws\3444347874\1.6\Assemblies\VFEMedieval.dll")
if (-not ($vf | Where-Object { -not (Test-Path $_) })) {
    & dotnet $csc -nologo -nostdlib -noconfig -target:library -optimize+ -langversion:latest -nowarn:CS1701,CS1702 `
        -out:"$mod\Compat\Harmony3StrictCompat.Vehicles.dll" @refs ($vf | ForEach-Object { "-r:$_" }) (Get-ChildItem "$mod\Source\Vehicles\*.cs").FullName
}
