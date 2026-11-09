# DXAutomap.AspNet.Data 2.x releases

Release notes and validation records for the fork's .NET package, newest release first. The release procedure is in the [repository README](../../README.md#releasing-a-version). The JavaScript packages retain their independent upstream versioning.

## 2.0.2

Version set on 2026-11-10.

| Item | Value |
|---|---|
| NuGet package | DXAutomap.AspNet.Data |
| Package version | 2.0.2 |
| Assembly/file version | 2.0.2.0 |
| Target framework | net10.0 |
| Runtime dependency | AutoMapperMIT 14.0.3 |
| License/signing | MIT; unsigned assembly |
| Source | the commit tagged `civilpro-v2.0.2`, which the package records |
| Package validation baseline | 2.0.1 |

### Changes

2.0.2 has the same code as 2.0.1; nothing changes at run time. It exists so the published package can be traced to its source and its nuget.org page describes 2.0.1's behaviour change.

- The package is built from the tagged commit and records it. 2.0.1 records no commit (see below).
- The package README states the typed-load selector rules introduced in 2.0.1 and links to the source tag. The 2.0.1 README describes the change in one sentence and links to the 2.0.0 release notes.
- The assembly version is 2.0.2.0. 2.0.1 kept 2.0.0.0, while its file version is 2.0.1.0.
- Package validation compares with 2.0.1. Because 2.0.1 has the same target framework, the standard validator compares the APIs directly; the explicit cross-target comparison (`build/validate-fork-api.targets`) and the net7.0 suppression (`CompatibilitySuppressions.xml`), which 2.0.0 needed because it dropped net7.0, are removed.
- `release.ps1` refuses to build an upload package unless the working tree has no uncommitted changes and the tag `civilpro-v<version>` is on HEAD, and checks that the package records HEAD. `-Trial` packs `<version>-trial` for checking before the release commit exists. `-SkipTests` is accepted only with `-Trial`.
- `build/set-version.ps1` makes the version changes for a release (step 2 of the procedure): project file, package README, package smoke project and a new section in this file.
- The release procedure is in the repository README. The 2.0.0 release notes moved from `RELEASE-2.0.0.md` into this file; `RELEASE-2.0.0.md` remains as a pointer because the READMEs of the published 2.0.0 and 2.0.1 packages link to it.

### Validation

`release.ps1` builds the package only from the commit tagged `civilpro-v2.0.2`, and only if every check passes: the full suite, the mapping suite against the Release build, package validation against 2.0.1, the archive checks and the smoke application. The code is unchanged from 2.0.1; see its validation below.

## 2.0.1

Published on nuget.org on 2026-11-09 at 21:13 UTC (2026-11-10 08:13 +11:00).

| Item | Value |
|---|---|
| Package version | 2.0.1 |
| Assembly version | 2.0.0.0 |
| File version | 2.0.1.0 |
| Source | tag `civilpro-v2.0.1` on 3d721a0: the code of 6767de4 (pull request #1) with the version, release notes and README committed afterwards |

### How it was built

The package was built from a working tree with uncommitted changes, so it records no commit. Its code is that of 6767de4, the merge of pull request #1. The version, file version, release notes and README edits it was built with were committed afterwards as 3d721a0 ("Update publication info", 2026-11-10 08:18 +11:00, five minutes after publishing).

Checked on 2026-11-10:
- Decompiling the published assembly gives the same code and the same version attributes as a build of 6767de4 with `Version` 2.0.1 and `FileVersion` 2.0.1.0.
- The published release notes are identical to 3d721a0's. The published README is identical to 3d721a0's apart from line endings. So a build of 3d721a0, which the tag `civilpro-v2.0.1` marks, reproduces the package; the tag was moved there from 6767de4 on 2026-11-10.

### Behaviour change

Typed loads (`LoadAsync<TSource, TDto>` with a mapper) accept only selectors that name members of the DTO. This applies to filter, sort, group, select and summary selectors. Each segment of a dotted path must be a member of the DTO, or of the DTO type of the previous segment; otherwise the load throws `ArgumentException` ("'X' is not a member of type 'Dto'"), the message an unknown member gives in an untyped load.

In 2.0.0 such selectors were compiled against the source item, so a request could filter, sort, group or select source members, including navigation paths, that the DTO does not expose. A consumer that relied on that must expose the member on the DTO or register a custom accessor for it.

Untyped loads (no destination type) are unchanged: selectors are dotted paths on the item type, as upstream.

### Changes

- Selectors on typed loads are checked against the DTO type graph, segment by segment. Member names match in any letter case, exact case first, as `Expression.PropertyOrField` matches them. A path made only of `this` is refused.
- The load's own primary key is accepted although the DTO may not declare it, because the loader sorts and pages by it.
- A registered custom accessor on the source type may start a path the DTO does not declare; the rest of the path is checked against the accessor's result. Deeper in a path, members the DTO does not declare are refused.
- DTO members that the map ignores are refused unless a registered accessor supplies them, instead of reading a same-named source member.
- DTO members that the map does not read by their own name (`MapFrom`, flattened, or mapped from a differently named source member) are read through the DTO's map with the request's projection parameters, so a selector sees the value the response shows. Nested DTOs use their own map, built per source/destination pair.
- The mapped-member lookup matches letter case as the DTO check does. In 2.0.0 it matched exact case only, while the fallback to the source item matched any case, so a selector in a different letter case read the same-named source member instead of the DTO's mapped value.
- The public API is unchanged; the changed types (`MappedAccessorResolver`, `DataSourceLoadContext`, `RemoteGroupTransformer`) are internal.

### Validation

No validation record was kept for the published build. The same code was validated on 2026-11-10, on Linux with .NET SDK 10.0.302:

| Suite or check | Result |
|---|---|
| Fork mapping suite, Debug and Release | 100 passed each |
| Main regression suite, Debug | 346 passed, 1 skipped, 1 failed: `DeserializeTestsEx.FilterOperandValueCanBeObject`, which fails only on Linux, because of line endings, as it did before this release |
| CivilPro API integration suite, against a local build of the same code | 5,783 passed, 0 failed, 59 skipped |
| CivilPro API selector, grid and masking tests, against the published 2.0.1 package | 58 passed |

The suites that need Windows and SQL Server LocalDB (EF Core 10, NHibernate and XPO providers) were not run for this code.

## 2.0.0

Release prepared on 2026-10-26 and published on nuget.org the same day. This supersedes the unreleased 1.17.0 working version.

| Item | Value |
|---|---|
| NuGet package | DXAutomap.AspNet.Data |
| Package version | 2.0.0 |
| Assembly/file version | 2.0.0.0 |
| Target framework | net10.0 |
| Runtime dependency | AutoMapperMIT 14.0.3 |
| License/signing | MIT; unsigned assembly |
| Upstream base | 8b05f05, from the upstream 6.0.0 development line |
| Source | tag `civilpro-v2.0.0` (9212db1). The published package records no commit, because it was built before the changes were committed; a build of 9212db1 decompiles to the same code as the published assembly, apart from the informational version. |

### Changes

- Registration works without prior profile registration, installs one finder, preserves callback ordering and supports concurrent registration/read operations without losing accessors or callbacks.
- Registered source types use actual .NET Type identity, preventing collisions between same-name classes or closed generic types.
- Typed loads use the requested DTO and current mapper configuration, with request parameters applied to mapped query expressions. Explicit custom accessors retain precedence.
- Nested property segments use their current object/type; identity accessors and captured outer parameters inside nested lambdas are supported.
- Custom accessors participate in normal string conversion, case handling, null processing and numeric/date group intervals.
- Local selection correctly reads projected DTOs, including nested AutoMapper LetClause projections and the automatic fallback above 32 fields.
- Missing mappers are rejected with ArgumentNullException naming mapper when source and destination types differ, across data/count/summary/select loads. Same-type loads still permit a null mapper.
- ProjectBeforeFilter is retired. Data/count/group/key queries follow the standard mapping-aware path. The obsolete property ignores setters, returns null and is omitted from System.Text.Json/Newtonsoft.Json; the parser constant remains for source compatibility.
- AutoMapper is replaced by AutoMapperMIT 14.0.3. SQL Server test dependencies also override the old transitive System.Security.Cryptography.Pkcs with 10.0.13; that package is not a runtime dependency of DXAutomap.

### Compatibility and consumer migration

This is a major release: .NET 7 support is replaced by .NET 10, the mapping package changes and ProjectBeforeFilter no longer affects queries. Consumers must retarget/recompile and coordinate their AutoMapper package references; the namespace remains AutoMapper. Public loader and registration signatures are retained.

Nested segment compiler hooks now receive the nested expression/type. Whole-path hooks keep their precedence. Consumers with custom hooks should validate this behaviour when upgrading.

Use typed loads with a mapper when DTO-specific mapping is required. Plain loads without a destination type retain legacy global profile lookup, which cannot choose between competing DTO mappings of the same source member.

CivilPro references, contracts/parsers/callers and API/MCP integration validation are deliberately deferred to the application upgrade. Library provider tests do not substitute for that consumer validation.

### Validation

The supported .NET 10 suite is `net/DXAutomap.AspNet.Data.slnx`, invoked by `net/dotnet-test-all.cmd` after building. CI uses the same solution and entry point. Builds and tests run sequentially.

| Suite | Passed | Skipped |
|---|---:|---:|
| Main regression suite, Debug | 347 | 1 |
| Fork mapping suite, Debug (LINQ to Objects/SQLite) | 57 | 0 |
| EF Core 10 / SQL Server LocalDB | 12 | 0 |
| NHibernate / SQL Server LocalDB | 5 | 1 |
| XPO / SQL Server LocalDB | 9 | 1 |
| **Full Debug entry point** | **430** | **3** |
| Fork mapping suite, Release | 57 | 0 |

Existing skips are EmptyLoadResultSerialization and the NHibernate/XPO RemoteGroupingStress scenarios. All former ForkKnownBugsTests assert corrected behaviour. The upstream .NET Framework suites and .NET 11 preview suite are not part of this net10.0-only release's supported matrix. JavaScript runtime code was not changed; its browser matrix was not rerun locally.

Release checks included:

- Package baseline validation against 1.16.0, with one documented suppression for the intentionally removed net7.0 target.
- An explicit Release assembly API comparison across the framework change, since package framework validation alone cannot compare the dropped target. See [Microsoft's baseline validation documentation](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator).
- A negative check substituting an incompatible assembly confirmed that the API comparison rejects missing public types; the real Release assembly passes without API suppressions.
- Archive checks for version, README, XML documentation, net10.0 assembly and AutoMapperMIT as the only runtime dependency.
- A separate smoke application restoring the actual package from the local output feed into a fresh package cache and executing mapped filtering, runtime parameters and local selection.
