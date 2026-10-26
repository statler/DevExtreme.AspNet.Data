# DXAutomap.AspNet.Data 2.0.0

Release prepared on 2026-10-26; publication is a separate step. This supersedes the unreleased 1.17.0 working version. CivilPro application migration is scheduled separately.

## Package identity

| Item | Value |
|---|---|
| NuGet package | DXAutomap.AspNet.Data |
| Package version | 2.0.0 |
| Assembly/file version | 2.0.0.0 |
| Target framework | net10.0 |
| Runtime dependency | AutoMapperMIT 14.0.3 |
| License/signing | MIT; unsigned assembly |
| Upstream base | 8b05f05, from the upstream 6.0.0 development line |

The JavaScript packages retain their independent upstream versioning. This release is for the fork's .NET package.

## Changes

- Registration works without prior profile registration, installs one finder, preserves callback ordering and supports concurrent registration/read operations without losing accessors or callbacks.
- Registered source types use actual .NET Type identity, preventing collisions between same-name classes or closed generic types.
- Typed loads use the requested DTO and current mapper configuration, with request parameters applied to mapped query expressions. Explicit custom accessors retain precedence.
- Nested property segments use their current object/type; identity accessors and captured outer parameters inside nested lambdas are supported.
- Custom accessors participate in normal string conversion, case handling, null processing and numeric/date group intervals.
- Local selection correctly reads projected DTOs, including nested AutoMapper LetClause projections and the automatic fallback above 32 fields.
- Missing mappers are rejected with ArgumentNullException naming mapper when source and destination types differ, across data/count/summary/select loads. Same-type loads still permit a null mapper.
- ProjectBeforeFilter is retired. Data/count/group/key queries follow the standard mapping-aware path. The obsolete property ignores setters, returns null and is omitted from System.Text.Json/Newtonsoft.Json; the parser constant remains for source compatibility.
- AutoMapper is replaced by AutoMapperMIT 14.0.3. SQL Server test dependencies also override the old transitive System.Security.Cryptography.Pkcs with 10.0.13; that package is not a runtime dependency of DXAutomap.

## Compatibility and later consumer migration

This is a major release: .NET 7 support is replaced by .NET 10, the mapping package changes and ProjectBeforeFilter no longer affects queries. Consumers must retarget/recompile and coordinate their AutoMapper package references; the namespace remains AutoMapper. Public loader and registration signatures are retained.

Nested segment compiler hooks now receive the nested expression/type. Whole-path hooks keep their precedence. Consumers with custom hooks should validate this behaviour when upgrading.

Use typed loads with a mapper when DTO-specific mapping is required. Plain loads without a destination type retain legacy global profile lookup, which cannot choose between competing DTO mappings of the same source member.

CivilPro references, contracts/parsers/callers and API/MCP integration validation are deliberately deferred to the application upgrade. Library provider tests do not substitute for that consumer validation.

## Validation

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

Release checks include:

- Package baseline validation against 1.16.0, with one documented suppression for the intentionally removed net7.0 target.
- An explicit Release assembly API comparison across the framework change, since package framework validation alone cannot compare the dropped target. See [Microsoft's baseline validation documentation](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/baseline-version-validator).
- A negative check substituting an incompatible assembly confirmed that the API comparison rejects missing public types; the real Release assembly passes without API suppressions.
- Archive checks for version, README, XML documentation, net10.0 assembly and AutoMapperMIT as the only runtime dependency.
- A separate smoke application restoring the actual package from the local output feed into a fresh package cache and executing mapped filtering, runtime parameters and local selection.

## Build the upload package

Prerequisites: Windows, .NET 10 SDK, SQL Server LocalDB, and access to configured NuGet feeds for the provider test dependencies.

From the repository root:

```powershell
./build/release.ps1
```

This builds the fork solution, runs the full suite entry point, runs the mapping suite in Release, builds/validates the nupkg, runs the package smoke application and writes a SHA-256 checksum. After a recorded successful full suite run, `./build/release.ps1 -SkipTests` skips only the Debug solution build/full test entry point; Release mapping tests and all package checks still run.

Artifacts:

- `artifacts/release/DXAutomap.AspNet.Data.2.0.0.nupkg` — upload this file to NuGet.
- `artifacts/release/DXAutomap.AspNet.Data.2.0.0.nupkg.sha256` — checksum.
- `artifacts/test-results/` — test result files.

The package remains unsigned, consistent with 1.16.0. Release builds include XML documentation in the correct output folder and a package README. The inherited metadata replacement script preserves the fork's NuGet version; CI packages Release only after the .NET suite succeeds, without requiring the upstream signing secret.

The script does not commit, tag or publish. A dirty working-tree build omits its repository commit rather than claiming the changes are present in HEAD. For a traceable published source tag, commit/tag the reviewed changes and rerun the script from that clean state. Once 2.0.0 is published, move compatibility validation to the 2.0.0 baseline for subsequent releases.
