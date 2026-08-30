# Release

This document lists repository steps that should be completed before publishing a FoosVision release.

## Version numbers

Before publishing a release, update the central version properties in `Directory.Build.props`.
`FoosVisionVersionMajor`, `FoosVisionVersionMinor`, and `FoosVisionVersionPatch` define the assembly
version and the MAUI `ApplicationDisplayVersion`.

The major number is also the protocol version, so increase it for breaking protocol changes.

`FoosVisionAppVersionCode` defines the MAUI`ApplicationVersion`, which maps to the Android store build
code and must be increased for every published Android app upload.

## Third-party notices

Regenerate app third-party notices after package, framework, or app asset changes:

```powershell
Tools\ThirdPartyNotices\Generate-AppThirdPartyNotices.ps1
```

The generator reads the restored NuGet graphs from:

- `Product/Apps/FoosVision/obj/project.assets.json`

Run `dotnet build FoosVision.slnx` first if these files are missing or stale.

Manual notice inputs, such as package metadata overrides, vendored source, and app assets, are maintained in:

- `Tools/ThirdPartyNotices/AppThirdPartyNoticeInputs.json`
