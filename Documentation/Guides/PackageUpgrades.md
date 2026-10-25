# Package Upgrades Guide

Use this guide when asked to upgrade a NuGet package or change its version, including
requests such as "Increase the version of MessagePack to 3.1.12" or
"Upgrade package MessagePack to 3.1.12". Match package IDs case-insensitively and use
the requested version rather than substituting the latest version.

## Scope and Compatibility

- Inspect the working tree and index before editing; preserve user-managed staging.
- Find the package declaration and its consumers. Product package versions are managed
  centrally in `Directory.Packages.props`; `Tests/Directory.Packages.props` and
  `Tools/Directory.Packages.props` also exist, so check the applicable scope.
- Verify the target version exists using official package metadata. Review upstream
  release notes and dependency requirements for compatibility, security fixes, and
  license or attribution changes relevant to the upgrade.
- Check related packages that must remain compatible. For MessagePack, inspect both
  `MessagePack` and `MessagePack.Annotations`, including their explicit central versions
  and the target package's dependency requirements. Update companion versions when
  needed for a compatible upgrade, and explain those changes in the final response.
- Keep unrelated dependencies unchanged. Review transitive changes caused by the upgrade;
  do not turn the request into a general dependency refresh.
- Make necessary compatibility fixes in the owning layer and update existing tests when
  behavior changes. Existing permission requirements for new cross-project dependencies
  and new test projects still apply.
- For serialization or networking packages, check for changes to wire formats, serializer
  options, generated code, and recorder-viewer compatibility. Follow
  `Documentation/Guides/ProtocolAndConnectivity.md` when protocol behavior is affected.
  A package version increase alone does not imply a FoosVision protocol version increase.

## Package Versions and Notices

1. Update the applicable central package versions. For a product package upgrade, change
   `Directory.Packages.props` without adding redundant versions to individual projects.
2. Update affected entries in `THIRD-PARTY-NOTICES.md`, including versions and any changed
   license or attribution information. Keep the edit scoped; do not change the inventory's
   overall review date unless the whole inventory was actually reviewed.
3. Restore the solution from the repository root so the app's
   `Product/Apps/FoosVision/obj/project.assets.json` reflects the new dependency graph:

   ```powershell
   dotnet restore FoosVision.slnx
   ```

4. Regenerate `Product/Apps/FoosVision/Resources/Raw/FoosVisionThirdPartyNotices.txt`
   using the existing generator:

   ```powershell
   Tools\ThirdPartyNotices\Generate-AppThirdPartyNotices.ps1
   ```

   See `Documentation/Release.md` for the generator's inputs. Review changes to direct
   and transitive runtime packages and their license texts. Maintain
   `Tools/ThirdPartyNotices/AppThirdPartyNoticeInputs.json` only if metadata overrides
   actually need adjustment. Do not hand-edit the generated notice file or generate it
   from a stale restore graph.

For a package used only by tests or tools, check whether product notices are affected;
do not invent product notice entries for dependencies that are not shipped. Explain
when either notice file needs no change.

## Verification

After updating versions and regenerating notices, run these commands from the repository
root, one at a time, and inspect each result:

```powershell
dotnet build FoosVision.slnx
```

```powershell
dotnet test --settings Tests/test.runsettings
```

Run the full test suite, not only tests directly associated with the package. Build after
notice generation so the app includes the updated resource. Fix failures caused by the
upgrade and rerun the affected verification. Report unrelated failures or environmental
blockers accurately; do not describe skipped or blocked checks as passing.

Review the final diff for unexpected dependency changes, missing notice updates, and
formatting churn. Preserve encoding, LF line endings, and the user's Git index.
Do not launch apps, tool projects, benchmarks, or Android deployment/device workflows.

## Release Notes and Final Response

Do not add an entry to `Documentation/FoosVision Changes.md` for a package upgrade.
If the upgrade requires a separate user-visible behavior change, flag it explicitly
rather than silently adding a release-note entry as part of this workflow.

In the final response, state:

- the package's old and new versions, plus any companion package updates;
- which notices were updated or why no notice change was needed;
- any changes beyond package versions and notices, such as compatibility fixes,
  test changes, or configuration adjustments, and why they were necessary;
- the build and full-test results, including failures or checks that could not run;
- any material compatibility or protocol impact;
- explicitly that no entry was added to `Documentation/FoosVision Changes.md`.
