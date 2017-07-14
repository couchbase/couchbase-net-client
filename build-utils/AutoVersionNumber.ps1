<#
.SYNOPSIS
Calculates the next version number for this package based on Git tags.

.DESCRIPTION
Gets the previous version number from the most recent tag in Git. If the previous version is a full release (has no suffix) then the last digit of the version number is incremented by 1.  Then, the optional version suffix is applied.

.PARAMETER VersionSuffix
This suffix will be applied to the automatically calculated version number.

.OUTPUTS
string

.EXAMPLE
$version = .\AutoVersionNumber.ps1

.EXAMPLE
$version = .\AutoVersionNumber.ps1 -VersionSuffix beta001

#>

Param(
  [string]
  $VersionSuffix = ""
)

$version = (& git describe --abbrev=0 --tags 2>&1)
$err = $version | ?{$_.GetType().Name -eq "ErrorRecord"}
if ($err) {
  # No tag found in git history
  $version = "0.1.0-pre"
}

if ($version.StartsWith("release-")) {
  # Remove "release-" prefix

  $version = $version.SubString(8)
}

$segments = $version -split "-"

try {
  $version = New-Object System.Version ($segments | select -first 1)
}
catch {
  Write-Error $_
  exit 1
}

if ($segments.Length -eq 1) {
  # Previous tag was a full release, so increment

  if ($version.Revision -gt -1) {
    $version = New-Object System.Version $version.Major, $version.Minor, $version.Build, ($version.Revision  + 1)
  } elseif ($version.Build -gt -1) {
    $version = New-Object System.Version $version.Major, $version.Minor, ($version.Build + 1)
  } else {
    $version = New-Object System.Version $version.Major, ($version.Minor + 1)
  }
}

if ($VersionSuffix) {
  return "$version-$VersionSuffix"
} else {
  return "$version"
}
