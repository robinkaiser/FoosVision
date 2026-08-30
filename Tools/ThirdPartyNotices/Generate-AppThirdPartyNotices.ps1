# SPDX-License-Identifier: GPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 Robin Kaiser

[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$NuGetPackageRoot = if ($env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES
}
else {
    Join-Path $env:USERPROFILE ".nuget\packages"
}

$InputDataPath = Join-Path $PSScriptRoot "AppThirdPartyNoticeInputs.json"
$InputData = Get-Content $InputDataPath -Raw | ConvertFrom-Json

$Apps = @(
    @{
        Name = "FoosVision"
        AssetsPath = "Product/Apps/FoosVision/obj/project.assets.json"
        OutputPath = "Product/Apps/FoosVision/Resources/Raw/FoosVisionThirdPartyNotices.txt"
    }
)

function Join-RepoPath([string] $Path) {
    return Join-Path $RepositoryRoot ($Path -replace '/', [System.IO.Path]::DirectorySeparatorChar)
}

function Normalize-Text([string] $Text) {
    return ($Text -replace "`r`n", "`n" -replace "`r", "`n").Trim()
}

function Get-TextHash([string] $Text) {
    $bytes = $Utf8NoBom.GetBytes((Normalize-Text $Text))
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash)
}

function Resolve-PropertyReferences([string] $Value, $Properties) {
    $resolved = $Value
    for ($i = 0; $i -lt 20; $i++) {
        $next = [regex]::Replace(
            $resolved,
            '\$\(([^)]+)\)',
            {
                param($match)

                $propertyName = $match.Groups[1].Value
                if ($Properties.ContainsKey($propertyName)) {
                    return $Properties[$propertyName]
                }

                return $match.Value
            })

        if ($next -eq $resolved) {
            return $next
        }

        $resolved = $next
    }

    throw "Unable to resolve MSBuild property references in '$Value'."
}

function Get-RepositoryVersion {
    $propsPath = Join-RepoPath "Directory.Build.props"
    [xml] $props = Get-Content $propsPath -Raw
    $properties = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($propertyGroup in $props.Project.PropertyGroup) {
        foreach ($property in $propertyGroup.ChildNodes) {
            if ($property.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                continue
            }

            if (-not [string]::IsNullOrWhiteSpace($property.InnerText)) {
                $properties[$property.LocalName] = $property.InnerText.Trim()
            }
        }
    }

    foreach ($propertyName in @("InformationalVersion", "AssemblyVersion")) {
        if ($properties.ContainsKey($propertyName)) {
            return Resolve-PropertyReferences $properties[$propertyName] $properties
        }
    }

    throw "Directory.Build.props does not define InformationalVersion or AssemblyVersion."
}

function Get-PackageIdAndVersion([string] $LibraryName) {
    if ($LibraryName -notmatch '^(.+)/([^/]+)$') {
        return $null
    }

    return @{
        Id = $Matches[1]
        Version = $Matches[2]
    }
}

function Get-PackageDirectory([string] $PackageId, [string] $Version) {
    return Join-Path $NuGetPackageRoot ((Join-Path $PackageId.ToLowerInvariant() $Version))
}

function Get-NuspecMetadata([string] $PackageId, [string] $Version) {
    $packageDirectory = Get-PackageDirectory $PackageId $Version
    $nuspecPath = Join-Path $packageDirectory "$($PackageId.ToLowerInvariant()).nuspec"

    if (-not (Test-Path $nuspecPath)) {
        $nuspecPath = Get-ChildItem $packageDirectory -Filter "*.nuspec" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }

    $metadata = @{
        License = "UNKNOWN"
        LicenseFile = $null
        ProjectUrl = $null
    }

    if (-not $nuspecPath) {
        return $metadata
    }

    [xml] $nuspec = Get-Content $nuspecPath -Raw
    $metadataNode = $nuspec.package.metadata
    $licenseNode = $metadataNode.ChildNodes |
        Where-Object { $_.LocalName -eq "license" } |
        Select-Object -First 1

    if ($licenseNode) {
        $licenseType = $licenseNode.Attributes["type"]?.Value
        if ($licenseType -eq "file") {
            $metadata.License = "See package license file"
            $metadata.LicenseFile = Join-Path $packageDirectory $licenseNode.InnerText
        }
        elseif (-not [string]::IsNullOrWhiteSpace($licenseNode.InnerText)) {
            $metadata.License = $licenseNode.InnerText.Trim()
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($metadataNode.licenseUrl)) {
        $metadata.License = $metadataNode.licenseUrl
    }

    if (-not [string]::IsNullOrWhiteSpace($metadataNode.projectUrl)) {
        $metadata.ProjectUrl = $metadataNode.projectUrl.Trim()
    }

    return $metadata
}

function Find-RootPackageFile([string] $PackageId, [string] $Version, [string[]] $Patterns) {
    $packageDirectory = Get-PackageDirectory $PackageId $Version
    if (-not (Test-Path $packageDirectory)) {
        return $null
    }

    foreach ($pattern in $Patterns) {
        $candidate = Get-ChildItem $packageDirectory -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like $pattern } |
            Sort-Object Name |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    return $null
}

function Has-RuntimeRelevantAssets($TargetLibrary) {
    foreach ($propertyName in @("runtime", "native", "runtimeTargets", "resource", "contentFiles")) {
        if ($TargetLibrary.PSObject.Properties.Name -contains $propertyName) {
            return $true
        }
    }

    if ($TargetLibrary.PSObject.Properties.Name -contains "compile") {
        return $true
    }

    return $false
}

function Get-DirectPackageDependencies($ProjectAssets) {
    $dependencies = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($framework in $ProjectAssets.project.frameworks.PSObject.Properties) {
        $dependencyNode = $framework.Value.dependencies
        if (-not $dependencyNode) {
            continue
        }

        foreach ($dependency in $dependencyNode.PSObject.Properties) {
            if ($dependency.Value.target -ne "Package") {
                continue
            }

            if ($dependency.Value.autoReferenced -eq $true -or $dependency.Value.suppressParent -eq "All") {
                continue
            }

            [void] $dependencies.Add($dependency.Name)
        }
    }

    return $dependencies
}

function Get-AppPackages([string] $AssetsPath) {
    $projectAssetsPath = Join-RepoPath $AssetsPath
    if (-not (Test-Path $projectAssetsPath)) {
        throw "Missing NuGet restore graph: $projectAssetsPath. Run dotnet restore or dotnet build first."
    }

    $projectAssets = Get-Content $projectAssetsPath -Raw | ConvertFrom-Json
    $directDependencies = Get-DirectPackageDependencies $projectAssets
    $packages = @{}

    foreach ($target in $projectAssets.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            $idAndVersion = Get-PackageIdAndVersion $library.Name
            if (-not $idAndVersion) {
                continue
            }

            $id = $idAndVersion.Id
            $version = $idAndVersion.Version

            if ($library.Value.type -ne "package") {
                continue
            }

            $isDirectDependency = $directDependencies.Contains($id)
            if (-not $isDirectDependency -and -not (Has-RuntimeRelevantAssets $library.Value)) {
                continue
            }

            if ($id -like "FoosVision.*") {
                continue
            }

            $key = $id.ToLowerInvariant()
            if (-not $packages.ContainsKey($key)) {
                $metadata = Get-NuspecMetadata $id $version
                $override = $InputData.packageOverrides.PSObject.Properties |
                    Where-Object { $_.Name -ieq $id } |
                    Select-Object -First 1

                if ($override) {
                    foreach ($overrideProperty in $override.Value.PSObject.Properties) {
                        $metadata[$overrideProperty.Name] = $overrideProperty.Value
                    }
                }

                $packages[$key] = [ordered]@{
                    Id = $id
                    Version = $version
                    License = $metadata.License
                    ProjectUrl = $metadata.ProjectUrl
                    LicenseFile = $metadata.LicenseFile
                    Notice = $metadata.Notice
                }
            }
        }
    }

    return $packages.Values | Sort-Object { $_["Id"] }
}

function Add-UniqueTextBlock($Blocks, [string] $Title, [string] $UsedBy, [string] $Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return
    }

    $normalizedText = Normalize-Text $Text
    $hash = Get-TextHash $normalizedText
    if (-not $Blocks.ContainsKey($hash)) {
        $Blocks[$hash] = [ordered]@{
            Title = $Title
            UsedBy = [System.Collections.Generic.List[string]]::new()
            Text = $normalizedText
        }
    }

    if (-not $Blocks[$hash].UsedBy.Contains($UsedBy)) {
        $Blocks[$hash].UsedBy.Add($UsedBy)
    }
}

function Get-AppNoticeText($App) {
    $packages = Get-AppPackages $App.AssetsPath
    $appVersion = Get-RepositoryVersion
    $textBlocks = @{}
    $lines = [System.Collections.Generic.List[string]]::new()

    $lines.Add("$($App.Name) $appVersion")
    $lines.Add("License: GPL-3.0-or-later")
    $lines.Add("Source license file: LICENSE.md")
    $lines.Add("")
    $lines.Add("Third-party notices")
    $lines.Add("")
    $lines.Add("Runtime NuGet packages")

    foreach ($package in $packages) {
        $projectSuffix = if ($package.ProjectUrl) { " - $($package.ProjectUrl)" } else { "" }
        $lines.Add("- $($package.Id) $($package.Version) - $($package.License)$projectSuffix")

        $usedBy = "$($package.Id) $($package.Version)"
        if ($package.LicenseFile -and (Test-Path $package.LicenseFile)) {
            Add-UniqueTextBlock $textBlocks "Package license file" $usedBy (Get-Content $package.LicenseFile -Raw)
        }
        else {
            $licenseFile = Find-RootPackageFile $package.Id $package.Version @("LICENSE*", "COPYING*")
            if ($licenseFile) {
                Add-UniqueTextBlock $textBlocks "Package license file" $usedBy (Get-Content $licenseFile -Raw)
            }
        }

        $noticeFile = Find-RootPackageFile $package.Id $package.Version @("THIRD-PARTY-NOTICES*", "THIRD_PARTY_NOTICES*", "NOTICE*")
        if ($noticeFile) {
            Add-UniqueTextBlock $textBlocks "Package notice file" $usedBy (Get-Content $noticeFile -Raw)
        }

        if (-not [string]::IsNullOrWhiteSpace($package.Notice)) {
            Add-UniqueTextBlock $textBlocks "Package notice" $usedBy $package.Notice
        }
    }

    $lines.Add("")
    $lines.Add("Vendored third-party source")
    foreach ($component in $InputData.vendoredComponents) {
        $lines.Add("- $($component.name) $($component.version) - $($component.license) - $($component.projectUrl)")
        $lines.Add("  Source path: $($component.sourcePath)")

        if ($component.licenseFile) {
            $licensePath = Join-RepoPath $component.licenseFile
            if (Test-Path $licensePath) {
                Add-UniqueTextBlock $textBlocks "Vendored source license file" $component.name (Get-Content $licensePath -Raw)
            }
        }

        if ($component.notice) {
            Add-UniqueTextBlock $textBlocks "Vendored source notice" $component.name $component.notice
        }
    }

    $appAssets = @($InputData.appAssets.PSObject.Properties |
        Where-Object { $_.Name -eq $App.Name } |
        Select-Object -ExpandProperty Value)

    if ($appAssets.Count -gt 0) {
        $lines.Add("")
        $lines.Add("Third-party app assets")
        foreach ($asset in $appAssets) {
            $lines.Add("- $($asset.name) $($asset.version) - $($asset.license) - $($asset.projectUrl)")
            foreach ($sourcePath in $asset.sourcePaths) {
                $lines.Add("  Source path: $sourcePath")
            }

            if ($asset.notice) {
                Add-UniqueTextBlock $textBlocks "Asset notice" $asset.name $asset.notice
            }
        }
    }

    $lines.Add("")
    $lines.Add("License and notice texts")
    $lines.Add("")

    foreach ($block in ($textBlocks.Values | Sort-Object { $_["Title"] }, { $_["Text"] })) {
        $lines.Add("----- $($block.Title) -----")
        $lines.Add("Used by: $([string]::Join(', ', ($block.UsedBy | Sort-Object)))")
        $lines.Add("")
        $lines.Add($block.Text)
        $lines.Add("")
    }

    return [string]::Join("`n", $lines) + "`n"
}

function Write-TextIfChanged([string] $Path, [string] $Text) {
    if ((Test-Path $Path) -and ((Get-Content $Path -Raw) -eq $Text)) {
        return $false
    }

    [System.IO.File]::WriteAllText($Path, $Text, $Utf8NoBom)
    return $true
}

foreach ($app in $Apps) {
    $outputPath = Join-RepoPath $app.OutputPath
    $outputDirectory = Split-Path $outputPath -Parent
    if (-not (Test-Path $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $noticeText = Get-AppNoticeText $app
    if (Write-TextIfChanged $outputPath $noticeText) {
        Write-Host "Generated $($app.OutputPath)"
    }
    else {
        Write-Host "Unchanged $($app.OutputPath)"
    }
}
