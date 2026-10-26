# DXAutomap.AspNet.Data release status

Updated 2026-10-26. Release version: **2.0.0**. The former 1.17.0 working version was not released; 2.0.0 reflects the .NET target and dependency changes and the retirement of ProjectBeforeFilter.

The authoritative release notes, validation record and packaging procedure are in [RELEASE-2.0.0.md](RELEASE-2.0.0.md). The [original fork review](fork-review-2026-10-23.md) is retained as historical evidence, not a current implementation checklist.

## Source and package

- Package: `DXAutomap.AspNet.Data` 2.0.0; assembly/file version 2.0.0.0; unsigned.
- Target: `net10.0`, replacing 1.16.0's `net7.0`.
- Runtime dependency: `AutoMapperMIT` 14.0.3. The `AutoMapper` namespace is unchanged.
- Branch: `dev/AddRuntimeResolutionContext-dx-6.0.0`, based on upstream `8b05f05` from the 6.0.0 development line. This does not imply an upstream 6.0.0 final release.
- Fork source of truth: `d04cfd8` on `dev/AddRuntimeResolutionContext`; its nested ConcurrentDictionary change was retained.
- No application repository or package reference in CivilPro has been changed by this work.

## Completed fixes

| Area | Current behaviour |
|---|---|
| Registration lifecycle | Register/RegisterContext activate lookup independently; the finder is installed once and preserves compiler ordering. |
| Registration concurrency | First registration uses GetOrAdd; readers use stable callback snapshots and captured accessor lookups. |
| Type identity | Generic/context registrations and legacy mapped registrations use actual Type keys, including closed generic types. Explicitly named string-keyed AccessorLibrary APIs remain available. |
| Runtime parameters | Typed loads prepare mapping expressions using the current request parameters and mapper configuration. |
| Destination isolation | Typed loads resolve the requested source/DTO pair instead of a globally overwritten mapping. |
| Nested paths | Segments resolve against the current nested expression/type; substitution supports member expressions, identity accessors and nested lambdas. Whole-path hooks still run first. |
| String/group processing | Custom accessors participate in string conversion, StringToLower, null guards and numeric/date group intervals. |
| Local selection | Projected queries are read as DTOs before extracting selected fields, including nested LetClause projections and selections above 32 fields. |
| Mapper diagnostics | Different source/destination types require a mapper; all typed overloads report ArgumentNullException naming mapper before query construction. Same-type loads allow null. |
| ProjectBeforeFilter | Retired. Query branches are removed; the obsolete property ignores setters, returns null and is excluded from JSON. The parser constant remains obsolete for source compatibility. |

All original ForkKnownBugsTests now assert corrected behaviour. No FIXME defects remain in that file.

## Tests and release checks

The fork test solution is `net/DXAutomap.AspNet.Data.slnx`. It contains the main tests, the new mapping tests and the EF Core 10, NHibernate and XPO provider suites. The existing `net/dotnet-test-all.cmd` entry point and CI build action use this solution. The upstream solution is retained for historical/upstream maintenance; its .NET Framework and .NET 11 projects are outside this .NET 10 release's supported matrix.

The full Debug entry point passed 430 tests with 3 existing skips. The Release mapping suite covers the public library surface with LINQ to Objects and EF Core SQLite. See the release notes for the latest validation record.

Release packaging includes a README, XML documentation and explicit release metadata. Baseline package validation against 1.16.0 allows only the intentional net7.0 target removal. A separate assembly API comparison checks the Release public API against the baseline across the framework change. A package smoke application installs the actual nupkg from a local feed and exercises mapped filtering, request parameters and local DTO selection.

The supported release procedure is `./build/release.ps1` on Windows with .NET 10 and SQL Server LocalDB. It builds/tests sequentially, packs, inspects the archive, runs the package smoke check and writes a SHA-256 checksum. It does not publish. Dirty working-tree packages omit the repository commit so they do not falsely identify HEAD as the source of uncommitted changes.

## Consumer work scheduled separately

The user has deferred CivilPro application updates until after this library release preparation:

- Coordinate the DXAutomap and AutoMapperMIT references across consuming API/shared/desktop projects and regenerate affected lock files.
- Remove ProjectBeforeFilter from request DTOs, parsers, generated clients and callers.
- Rebuild CivilPro develop and MCP consumers and run their own integration suites, including restricted-column hooks and runtime UserId/minsOffset mappings.
- Validate affected DTO mappings and nested hooks in CivilPro before deploying the updated application.

Plain loads without a destination type/mapper retain legacy global profile lookup. That API cannot distinguish multiple destination DTO mappings for one source member; use the typed loader with its mapper for destination-specific behaviour.

## Publication

Commit/tag the reviewed source when ready and build from that state to include accurate source provenance. The generated nupkg can then be uploaded to NuGet by the package owner. Uploading/publishing is not performed by the release script. After 2.0.0 is published, future releases should use it as the compatibility baseline and retire the 1.16.0 framework-transition exception/check.
