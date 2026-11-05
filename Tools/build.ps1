# Builds 1.7\Assemblies\Harmony3StrictCompat.dll from Source\*.cs against the game's own runtime DLLs.
$ErrorActionPreference = 'Stop'
$mod = Split-Path $PSScriptRoot
$game = 'M:\Steam\steamapps\common\RimWorld'
$csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk\8.*\Roslyn\bincore\csc.dll' | Sort-Object Name | Select-Object -Last 1).FullName
$refs = @('System.Private.CoreLib','System.Runtime','System.Collections','System.Collections.Concurrent','System.Linq','System.Private.Xml','System.Xml.ReaderWriter','System.Xml.XmlDocument','System.Reflection.Primitives','System.Reflection.Emit.ILGeneration','netstandard','mscorlib','Assembly-CSharp','UnityStubs') |
    ForEach-Object { "-r:$game\$_.dll" }
$refs += "-r:M:\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll"
& dotnet $csc -nologo -nostdlib -noconfig -target:library -optimize+ -langversion:latest -nowarn:CS1701,CS1702 `
    -out:"$mod\1.7\Assemblies\Harmony3StrictCompat.dll" @refs (Get-ChildItem "$mod\Source\*.cs").FullName
