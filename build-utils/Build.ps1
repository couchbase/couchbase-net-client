# is this a tagged build?
if ($env:APPVEYOR_REPO_TAG -eq "true") {
    # use tag as version
    $versionNumber = "$env:APPVEYOR_REPO_TAG_NAME"
} else {
    # create pre-release build number based on AppVeyor build number
    $buildCounter = "$env:APPVEYOR_BUILD_NUMBER".PadLeft(6, "0")
    $versionNumber = .\build-utils\AutoVersionNumber.ps1 -VersionSuffix "alpha-$buildCounter"
}

# replace AssemblyInfo with version that doesn't include IntervalsVisibleTo attributes
Copy-Item .\build-utils\AssemblyInfo.cs .\Src\Couchbase\Properties\AssemblyInfo.cs -Force

# decrypt snk for signing the assembly
nuget install secure-file -ExcludeVersion
.\secure-file\tools\secure-file.exe -decrypt .\build-utils\Couchbase.snk.enc -secret $env:SnkSecret -out .\Src\Couchbase\Couchbase.snk

# clean then build with snk & version number creating nuget package
msbuild Src\Couchbase\Couchbase.csproj /t:Clean /p:Configuration=Release
msbuild Src\Couchbase\Couchbase.csproj /t:Restore,Pack /p:Configuration=Release /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=Couchbase.snk /p:version=$versionNumber /p:PackageOutputPath=..\..\

# create zip from release folder
Compress-Archive -Path .\Src\Couchbase\bin\Release\* -CompressionLevel Optimal -DestinationPath .\Couchbase-Net-Client-$versionNumber.zip