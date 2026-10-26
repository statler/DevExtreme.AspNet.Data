# CivilPro fork of DevExtreme.AspNet.Data: rebase and test plan

Written 2026-10-23. Status: in progress. The rebased 6.0.0 branch exists locally and is not pushed. The test project (section 5) is planned but
not applied to the branch.

Updated 2026-10-26: the fork follows the latest upstream, so only the 6.0.0 branch is kept. The local 5.2.0 branch was deleted (it was at
`79820d4`; `git branch dev/AddRuntimeResolutionContext-dx-5.2.0 79820d4` restores it while the commit is still in the object store).

The full fork review is in `fork-review-2026-10-23.md` in this folder. Paths in it that start with `/tmp/claude-1000/...` point to a temporary
working folder that may no longer exist.

## 1. Background

- **What the API ships.** The CivilPro API references the NuGet package `DXAutomap.AspNet.Data` 1.16.0 (net7.0) in `cpDataASP/cpDataASP.csproj:52`.
  It does not build the `DevExtreme.AspNet.Data` submodule in civilpro-api2. That submodule was reverted to the package reference in API commit
  `8a1f3da2c` (2024-07-12), and only a comment is left at `cpDataASP.csproj:82`.
- **Source of truth for the fork:** commit `d04cfd8` on `dev/AddRuntimeResolutionContext` (confirmed). It is the closest committed source of 1.16.0.
  - The package records source commit `3f466bec`, which is on neither GitHub nor this repo.
  - The submodule pin `029326c` is on the side branch `dev/aspnetlibnet9` and does not contain `d04cfd8`'s nested `ConcurrentDictionary` fix.
- **Fork base.** The fork branched from upstream 4.0.0 exactly (`c94a149`). Against upstream `master` it is 27 commits ahead (23 without merges)
  and 103 behind.
- **Upstream will not take the fork's features.** PR #589 was closed unmerged on 2026-02-27, and issue #588 is still open. So the fork has to be
  kept and rebased onto each upstream release.
- **Remotes in this repo.** `origin` is github.com/statler/DevExtreme.AspNet.Data. `upstream` is github.com/DevExpress/DevExtreme.AspNet.Data.
  All branches and tags are fetched.
- **Upstream versions:**
  - 5.1.0 (2025-04-24) is the newest stable release on NuGet and npm.
  - 5.2.0 (2026-04-23, `182e124`) is a GitHub release only, with no packages.
  - 6.0.0-beta (`9a5a549`, NuGet 2026-10-19) has the same `net/` code as `master` `8b05f05`.
  - As of 2026-10-26, upstream `master` is `fcdc6c3`. Its one commit after `8b05f05` changes only `package-lock.json` files, not `net/`.

## 2. Rebased branch (local, not pushed)

Each fork change is one commit. Each commit body records its purpose, the fork commits it came from, its upstream status and where the API uses
it. These commits are the durable patch series: `git format-patch <base>..<branch>` regenerates the patches.

### `dev/AddRuntimeResolutionContext-dx-6.0.0`

Head `98a0538`, on upstream `master` `8b05f05`.

| Commit | Change |
|---|---|
| `ab3cd1d` | Package as DXAutomap.AspNet.Data 1.17.0 for net10.0 |
| `b196f84` | Pass a runtime resolution context to every expression compiler |
| `32d836a` | Add context-aware custom binary filters |
| `354ae1a` | Add custom accessor library with context-aware registration |
| `af9c5c5` | Make the inner accessor dictionary concurrent (the `d04cfd8` fix) |
| `28e99cb` | Register AutoMapper MapFrom expressions as custom accessors |
| `193673c` | Add AutoMapper projection overloads to DataSourceLoader |
| `68bdc02` | Support multi-stage (LetClause) AutoMapper projections |
| `4933677` | Add ProjectBeforeFilter load option. Also restores the public `KEY_PROJECTBEFOREFILTER`, which the MCP branch uses |
| `f8c1e47` | Add unit tests for the fork features (6 tests). **To be replaced by section 5** |
| `98a0538` | Bump test-only System.Security.Cryptography.Xml to 10.0.13 (three high advisories) |

Verified on the branch:
- The library builds. The only warnings are 2 NU1903 warnings for AutoMapper 13.0.1.
- Unit tests: 294 passed, 1 skipped, 1 failed. The failure is `DeserializeTestsEx.FilterOperandValueCanBeObject`, which also fails on unmodified
  upstream on Linux because it expects `\r\n`.

Verified on the same tree earlier:
- A Release `dotnet pack` gives `lib/net10.0` with AutoMapper 13.0.1 as the only dependency. The assembly is not strong-named, as in 1.16.0.
- A copy of `cpDataASP` (develop) compiled against the library with 0 errors.

### Left out of the branch

- The fork's change to the library's own load-options parser. The API uses its own parser, and the fork's code assigned `isCountQuery` to
  `ProjectBeforeFilter`. The public constant is kept.
- The deletion of `release.snk.enc`. The file is kept but not referenced.
- Build-only fork commits: solution and test project edits, `Tests.EFCore7`, the XPO and .NET 9 bumps, and the MongoDB rename and revert, which
  cancel each other out.
- The fork's explicit `System.Text.Json` 7.0.3 and `Microsoft.CSharp` 4.7.0 references, which the shared framework now provides.

## 3. How the API uses the fork

Checked against API develop `d89ad3c4a` and the MCP branch `origin/dev/CP2-3037--MCP-server2` (`a45caef7c`).

| Fork feature (commit) | API use |
|---|---|
| Runtime resolution context (02) | `NotificationsController.cs:50,396` (`new { UserId }`), `ApprovalsController.cs:120,140` (`new { minsOffset }`). Read in `Helpers/CustomFilters.cs:35` and `Helpers/CustomAccessors.cs:12-41`. On MCP, also `Devex/RestrictedColumns.cs:26` |
| Context-aware filters, public `FilterExpressionCompiler`, `CompileNonCustomBinary` (03) | `CustomFilters.cs:29,43` |
| Accessor library: `Register<T,U>`, `RegisterContext`, `RegisterWithContext` (04) | `Interfaces/ICustomRegister`1.cs:50,62`, on every request; `CustomAccessors.cs:12,22,32`. On MCP, also `RegisterWithContext` and `CompilerFuncWithContext` (`RestrictedColumns.cs:50,65`), which rely on compilers being tried in registration order |
| `RegisterAutomapperProfiles` (06) | `Startup.cs:433`; on MCP `Startup.cs:497-498` through `Vendor.Registration.Once` |
| `Load` / `LoadAsync<T,TDto>` with AutoMapper projection (07) | `Devex/DataSourceLoader.cs:120`: 37 `LoadDtoAsync` sites and about 225 BaseController helper calls. On MCP, the `CancellationToken` overload (`:124`) |
| LetClause two-stage Select (08) | Every projection goes through it. Whether any CivilPro map produces a LetClause is not known |
| `ProjectBeforeFilter` (09) | The API's parser (`DataSourceLoadOptions.cs:185-186`) and `DummyController.cs:80`. On MCP the parser also uses `KEY_PROJECTBEFOREFILTER` (`DataSourceLoadOptions.cs:57`) |

## 4. Retarget of the 5.2.0 branch (dropped)

Not needed: the 5.2.0 branch was dropped on 2026-10-26 (see the top of this file). The section number is kept so that references to later
sections stay valid.

## 5. Pending step: test project for the fork's additions (the test plan)

### What upstream already has

- **Main test project.** `DevExtreme.AspNet.Data.Tests` runs 241 test methods (290 cases) against LINQ to Objects.
- **Provider suites.** The same scenarios run in `Tests.EFCore*`, EF6, L2S, NH and Xpo, from shared helpers in `Tests.Common`. These use SQL
  Server LocalDB (`Tests.Common/SqlServerTestDbHelper.cs:8`), so they run on Windows only.
- **JavaScript.** `js-test` holds karma tests for the client library.
- None of these cover the fork's additions.

### Layout

- A new project, `net/DXAutomap.AspNet.Data.Tests/`: xunit, net10.0, with package versions in `net/Directory.Packages.props`.
- Do not add it to upstream's `.sln`, and do not change upstream test projects, so future rebases touch only our files. Commit 10's 6 tests move
  into it, and upstream's test project goes back to its original state.
- The central EF Core pin on the 6.0.0 branch is `Microsoft.EntityFrameworkCore.SqlServer` 10.0.10. If a later upstream base pins an older
  version, use `VersionOverride`, as `Tests.EFCore10` does.
- Add `Microsoft.EntityFrameworkCore.Sqlite` at the same EF Core version.

### One set of scenarios, three runs

Write each scenario once, against an abstraction that provides `IQueryable` sources for a small entity model, plus DTOs and an AutoMapper
configuration. Run every scenario three ways:

1. **In memory:** LINQ to Objects, with `AllowAsyncOverSync` where needed.
2. **EF Core on SQLite in memory:** always runs and needs no server. Keep one open connection per test class, and call `EnsureCreated`.
3. **EF Core on SQL Server:** only when the environment variable `DXAUTOMAP_TESTS_SQLSERVER` holds a connection string.
   - When the variable is unset, every SQL Server test is reported as skipped, with a message that names the variable. Use a
     `FactAttribute`/`TheoryAttribute` subclass that sets `Skip`. No connection is attempted.
   - When it is set, the tests create their own database with a unique name derived from the connection string
     (`<catalog or DXAutomapTests>_<random>`), run against it, and drop it at the end.
   - The login needs CREATE DATABASE and DROP DATABASE permission.
   - Never write the connection string to disk.

Where a scenario cannot be translated on SQLite but should be on SQL Server, mark it with the reason. Do not remove it.

### Scenarios

- **Runtime resolution context**, read by:
  - a context accessor;
  - a context-aware filter (`RegisterBinaryExpressionCompilerWithContext` plus `CompileNonCustomBinary`).
- **Registration paths:**
  - `Register<T,U>`, the per-request custom-register path, and `RegisterContext`;
  - dotted-path accessor lookup.
- **`RegisterAutomapperProfiles`:** filter, sort, group and select on `MapFrom` DTO member names.
- **Projection overloads:** `LoadAsync<T,TDto>` with and without `CancellationToken`, and sync `Load<T,TDto>`, including group and summary
  results.
- **LetClause two-stage projection (commit 08).** Find an AutoMapper 13.0.1 map for which `ProjectionBuilder.GetProjection` returns a LetClause,
  and show that the test reaches that branch.
- **`ProjectBeforeFilter`** on a plain load and on remote grouping.
- **Context use** in `DefaultAccessor`, `SumFix` and remote grouping.
- **Concurrency (commit 05):** many threads register and read accessors at once. The test must fail with a plain `Dictionary`.
- **Known fork bugs 1 to 4 and 7 (section 6):** one test each, of the correct behaviour, marked `Skip = "Known fork defect: ..."`. Fixing the bug
  means removing the `Skip`.

### Package validation

- In the library csproj, set `EnablePackageValidation` with `PackageValidationBaselineVersion` 1.16.0. The baseline package id is
  `DXAutomap.AspNet.Data`.
- Put the expected differences in `CompatibilitySuppressions.xml`, each with a one-line reason: net7.0 becoming net10.0, and the now-optional
  `ExpressionCompiler` constructor parameter.
- After that, any new public API change makes `dotnet pack` fail.

### README

Add `net/DXAutomap.AspNet.Data.Tests/README.md`. It covers what the project tests, how to run it, the SQL Server option and the permission it needs.

### How to verify

1. Build the test project and run it with the variable unset: the in-memory and SQLite runs pass, and the SQL Server tests are skipped.
2. Run upstream's main test project and confirm it gives upstream's result again.
3. Run a Release pack.
4. Check that the tests catch real faults. In a copy, break the code each test claims to cover and confirm the test fails, for example:
   - a plain `Dictionary` for commit 05;
   - removing the two-stage Select for commit 08;
   - not passing the runtime context.
5. Check that package validation fails when a public signature changes.

### How to apply to the branch

- Commits 01 to 09, then the tests commit in place of `f8c1e47`, then `98a0538`, the System.Security.Cryptography.Xml bump.
- The commit message gets a short subject and a body: what is covered, how to run it, and the SQL Server option.

## 6. Known fork bugs and whether fixing them changes default behaviour

| # | Bug | Effect today | Does a fix change default behaviour? |
|---|---|---|---|
| 1 | Dotted paths are looked up against the root type. `ExpressionCompiler.cs` passes `target`, not `currentTarget`, to `TryCompile` | For `A.B`, segment `B` is matched against the root type's accessors. Accessors registered for `A`'s type are never used for `B`. The MCP `RestrictedColumns` hook gets the same calls; reading the code, the result is extra redaction, not a leak | **Yes.** Nested segments start using their own type's accessors, and stop picking up root-type accessors with the same name. The fix also needs `ParameterVisitor` to accept a non-parameter target |
| 2 | Concurrent first registration can lose an accessor: `ContainsKey` then `TryAdd` in `AccessorLibrary.Add` | Rarely, one entry is dropped when two requests register the first property of a type at once. The next request registers it again, but the affected request can fail to compile `CustomRegister_<id>`, which is not a real property | **No.** Use `GetOrAdd` |
| 3 | Accessors are keyed by short type name | Registered types with the same class name in different namespaces would share accessors | **Only if such duplicates exist.** Scan the registered entity types first |
| 4 | `MapFrom` entries are keyed by (entity, member), without the DTO type; `Add` overwrites, so the last one wins | Two DTOs from one entity share an entry. Candidates: `Approval.Status`, `IsCompleted`, `DateLastStep`; `Lot.TRCount`; `User.FirstLetterLastName`; `Notification.Status` (an enum in one DTO, a string in the other); `PurchaseOrder.ProjectName`; `TestRequest.LatestUploadDate`; `TransmittalItem.RevisionDate`. Which DTO wins depends on the order of `GetAllTypeMaps`, and has not been checked at run time | **Yes.** Results change, and become correct, for the DTO that currently loses. It is a design change: the accessor lookup has to know `TDto` |
| 5 | `ProjectBeforeFilter` leaves the count, group-count and key queries unprojected, and any request can set `projectbeforefilter` | Only `DummyController` sets it. If a client sends it, totals can disagree with rows, or the count can fail on DTO-only members | **Not by default**, because the option is off. Either project those queries too, or have the API stop accepting the option from requests |
| 6 | `LoadAsync<T,TDto>` with a null mapper and `TDto != T` throws `NullReferenceException` | Only on a programming mistake | **No.** Only the exception type changes |
| 7 | Each `RegisterAutomapperProfiles` call adds another accessor finder | develop calls it once at startup, but tests call it again (`CustomRegisterDetailPrivacyTests.cs:125`). MCP guards it with `Registration.Once`. The duplicates return the same results | **No.** Register the finder once |

Checked and not a bug: the per-request `Register` calls overwrite entries in the shared static library. Query accessors are named
`CustomRegister_<id>` and value accessors `CustomRegisterValue_<id>`, and each expression depends only on the entity type and the register id. So
an overwrite writes the same expression.

Suggested order:
- Bugs 2, 6 and 7 now, as their own commits on the branch.
- Bug 3 after the scan.
- Bugs 1 and 4 only after section 5's tests have recorded today's results for the affected paths and members.
- Bug 5 after the API decides whether requests may set the option.

### Behaviour changes that the upgrade itself brings (not fork bugs)

- Guid `<`, `>`, `<=` and `>=` compile to `CompareTo`. This is upstream #634, in 5.0.0. On .NET 7 and later, 1.16.0 used Guid's own operators.
- DateTimeOffset string values are parsed as DateTimeOffset (#736).
- Group counts are correct with group paging and `groupInterval` (#737). The client can trigger this: `app.component.ts:124` turns on
  `groupPaging` for all grids, and `daycosts-grid.component.ts:2973` sets `groupInterval: 'day'`.

## 7. Advisories

Details are in `fork-review-2026-10-23.md`, section 5.

### API

- **Cleared by the rebase:**
  - System.Text.Json 7.0.3 (GHSA-hh2w-p6rv-4g7w);
  - System.Linq.Dynamic.Core 1.3.3, which the fork used in tests only (GHSA-4cv2-4hjh-77rx).
- **Not cleared: AutoMapper 13.0.1 (GHSA-rvv3-g6hj-g44x, high).** It is fixed only in 15.1.1+ and 16.1.1+, which need an RPL-1.5 or commercial
  licence.
  - It is referenced directly in cpDataASP, cpDataORM, cpDataServices and cpModel.
  - 4 desktop projects pin 13.0.1. A higher version in the shared projects would give NU1605, which the SDK treats as an error. This is inferred,
    not built.
  - So it needs both a licence decision and a decision about the desktop projects.
- **Fixable in the API alone:**
  - System.Security.Cryptography.Pkcs and Xml, through 10.0.13 references (Xml also through the hosting runtime);
  - SixLabors.ImageSharp 3.1.12, which needs 4.1.2+, a major version;
  - OpenMcdf 3.1.0, through MsgReader or a direct reference.

### Client (yarn.lock)

- **Angular 21.2.17:** a high-severity i18n XSS advisory. Bump all `@angular/*` to 21.2.25, including the exact peer dependencies in cp-lib.
- **vite:** its dev-server advisories can't be fixed yet, because the vite pin follows `@angular/build`'s version.
- **Most of the rest:** clears with a lockfile refresh.
- **`@grpc/grpc-js`:** comes through firebase, and only a resolutions override would clear it.
- **`pnpm-lock.yaml`:** has drifted from `yarn.lock`. Check which one CI installs from.
- **devextreme and devextreme-aspnet-data-nojquery:** no advisories.

## 8. Open decisions

1. **Which branch to ship: decided 2026-10-26.** The 6.0.0 branch, which follows the latest upstream. Its base has the same `net/` code as the
   6.0.0-beta prerelease; there is no 6.0.0 final release yet.
2. **Publishing.** The package owner on nuget.org (`Devexpress_Statler`) pushes the chosen branch to github.com/statler/DevExtreme.AspNet.Data, packs
   Release from that pushed commit so the nuspec records a real commit, and publishes. The alternative is for cpDataASP to switch to a
   ProjectReference.
3. **AutoMapper:** the licence question, and the desktop pins.
4. **`ProjectBeforeFilter`:** whether the API should stop accepting it from requests. If so, it could be treated as an 11th unread load option in
   the client-contract plan.
5. **`.gitmodules`:** whether to reconcile it, and the civilpro-api2 submodule pin, with the chosen branch.
6. **SQL Server run:** whether to run the SQL Server variant against `cp_testing_integration_mssql`. It needs a login that can create and drop a
   database.

## 9. Before shipping

1. **Compile:** build cpDataASP from develop and from MCP-server2 against the nupkg, using a local feed.
2. **Library tests:** run the library and DXAutomap tests.
3. **SwaggerTest:** run it and confirm the models hash is unchanged.
4. **Lock files:** regenerate them in cpDataASP, cpApiTestData and cpApi_IntegrationTests, because CI restores in locked mode.
5. **Integration tests:** run cpApi_IntegrationTests on Linux against `cp_testing_integration_mssql`. On MCP these include
   VendorRegistrationTests, RestrictedColumnsLoaderTests and QueryToolsTests.
6. **In the running API, check:**
   - grids with custom-register columns;
   - Notifications (UserId context) and Approvals (minsOffset);
   - a Guid range filter;
   - a grouped grid with `groupInterval` and group paging;
   - a filter on `Status` in the approvals list (bug 4).

## 10. How to resume

### Resource rules

Do not run builds in parallel. An earlier run of many builds at once overloaded the machine, and other Claude sessions run here too. Run every
build, test or pack through a wrapper like this one, which allows one heavy command at a time, 16 GB with no swap, 4 CPUs, low priority and a
30-minute limit:

```bash
#!/usr/bin/env bash
# heavy.sh: one heavy command at a time across all callers, in a capped cgroup.
LOCK="$(dirname "$(readlink -f "$0")")/heavy.lock"
exec flock "$LOCK" systemd-run --user --scope -q -p MemoryMax=16G -p MemorySwapMax=0 -p CPUQuota=400% \
  nice -n 15 timeout 1800 env DOTNET_CLI_TELEMETRY_OPTOUT=1 MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 \
  UseSharedCompilation=false NODE_OPTIONS=--max-old-space-size=8192 "$@"
```

Build and test single projects only:

```bash
heavy.sh ~/.dotnet/dotnet build net/DevExtreme.AspNet.Data/DevExtreme.AspNet.Data.csproj -c Debug -m:1 -nodeReuse:false -p:UseSharedCompilation=false
heavy.sh ~/.dotnet/dotnet test net/DevExtreme.AspNet.Data.Tests/DevExtreme.AspNet.Data.Tests.csproj -c Debug -m:1 -nodeReuse:false -p:UseSharedCompilation=false
heavy.sh ~/.dotnet/dotnet build-server shutdown
```

### Working method used so far

- Never change the checked-out branch (`dev/AddRuntimeResolutionContext` at `d04cfd8`). Work in a temporary `git worktree` outside the repo,
  rebuild the branch there, and remove the worktree afterwards.
- Commit authors use the repo's configured identity. Messages have a short subject, a plain body and no AI attribution.
- Nothing is pushed.

### Next steps

1. Section 5: write the test project on the 6.0.0 tree without commit 10, and verify it.
2. Rebuild the branch with the tests commit in place of commit 10.
3. Fix bugs 2, 6 and 7 as separate commits on the branch, with tests.
4. Settle the decisions in section 8, then do section 9.

### Draft artifacts in a temporary folder (may be gone)

- **Last scratchpad:** `/tmp/claude-1000/-home-mike-civilpro-civilpro-wt-angular-liquid--models-update-CivilPro-ClientApp/e50612dd-0162-4456-a182-cfdd67691b9a/scratchpad/`.
- **Test project (section 5):** a run was in progress, writing to `fork/tests/tests/` and `fork/tests/port/`. Treat anything found there as an
  unreviewed draft.
- **Earlier work:**
  - patch series: `fork/rebase/` (6.0.0) and `fork/final/` (review corrections);
  - the API copy used for compile checks: `fork/advisories/api-copy/`;
  - the wrapper script: `heavy.sh`.

### Related plan in the client repo

The generated API client contract plan is in `civilpro-wt-angular-liquid--models-update/CivilPro/ClientApp/docs/generated-client-contract/`
(`OVERVIEW.md`, `DESIGN.md`). It depends on this fork only through `ProjectBeforeFilter` (decision 4) and the copied
`devextreme-aspnet-data-nojquery` encoder. That package is still 4.0.2, whose `index.js` is identical to 5.1.0 and 6.0.0-beta apart from the
version comment.
