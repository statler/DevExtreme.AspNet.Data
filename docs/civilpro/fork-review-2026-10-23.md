# DevExtreme.AspNet.Data fork (DXAutomap.AspNet.Data): rebase, advisories and upgrade order

## 1. Summary

- **What the API actually uses.** The API does not build the submodule. `cpDataASP/cpDataASP.csproj:52` references the NuGet package DXAutomap.AspNet.Data 1.16.0 (net7.0). Its closest committed source is fork commit d04cfd8, which the user confirmed as the source of truth. The submodule pin 029326c is not that source: it lacks d04cfd8's nested ConcurrentDictionary fix.
- **What I built.** The fork's changes were rebased onto upstream 6.0.0-beta (tag 9a5a549, which has the same .NET code as master 8b05f05). The result is patches 01–10 from the rebase step, plus patch 11 from this review.
- **What was checked:**
  - The library builds.
  - Upstream's tests pass (294 of 296). The one failure also fails on unmodified upstream.
  - cpDataASP on develop compiles against the rebased library with 0 errors.
  - A Release `dotnet pack` of the corrected tree succeeds.
- **Correction found in this review.** The MCP branch `origin/dev/CP2-3037--MCP-server2` (a45caef7c), which the contract plan merges first, references `DataSourceLoadOptionsParser.KEY_PROJECTBEFOREFILTER`. The rebase had removed that constant. Patch 11 adds it back.
- **Upstream will not take the fork's features.** Upstream PR #589 was closed unmerged on 2026-02-27, so the fork has to be kept and rebased onto each upstream release.
- **What changes in the API, and what does not.** The rebase clears the fork's System.Text.Json 7.0.3 and System.Linq.Dynamic.Core 1.3.3 advisories. It does not clear AutoMapper 13.0.1 (GHSA-rvv3-g6hj-g44x). That needs a licence decision and a desktop decision.
- **Nothing reaches the API until one of these happens:**
  - a new DXAutomap version is published and `cpDataASP.csproj:52` is bumped to it; or
  - cpDataASP switches to a ProjectReference.

  Publishing is the package owner's decision.

## 2. Fork change catalogue (source d04cfd8, base c94a149 = upstream 4.0.0)

None of the fork's own changes went upstream. "develop" means API worktree HEAD d89ad3c4a. "MCP" means `origin/dev/CP2-3037--MCP-server2` a45caef7c.

| Feature (fork commits) | Upstream 6.0.0-beta | API use: develop | API use: MCP | Rebase decision |
|---|---|---|---|---|
| A. Runtime resolution context passed to every expression compiler (344745b, 4f334d2) | No | `NotificationsController.cs:50,396` (`new { UserId }`); `ApprovalsController.cs:120,140` (`new { minsOffset }`); read in `Helpers/CustomFilters.cs:35` and `Helpers/CustomAccessors.cs:12-41` | Same, plus `RestrictedColumnContext` (`Devex/RestrictedColumns.cs:26`) | Keep (patch 02). New internal parameters default to null. |
| B. `RegisterBinaryExpressionCompilerWithContext`, public `FilterExpressionCompiler`, `CompileNonCustomBinary` (344745b, 1980a7b) | No; still internal | `CustomFilters.cs:29,43` | Same | Keep (patch 03). The unused `CompileNonCustomBinary(IList)` and public `CreateItemParam` are kept because 1.16.0 exposes them. |
| C. Static accessor library: `Register<T,U>`, `RegisterContext`, `RegisterWithContext`, `CompilerFuncWithContext`, `AccessorLibrary` (93f4afb, 4f334d2, 170fa8e) | No | `Interfaces/ICustomRegister`1.cs:50,62` (on every request); `CustomAccessors.cs:12,22,32`. `RegisterWithContext` is not used. | Adds `RegisterWithContext(Hook)` and `CompilerFuncWithContext` (`RestrictedColumns.cs:50,65`; `VendorRegistrationTests.cs:70`). That hook depends on the compilers being tried in registration order. | Keep (patch 04). The rebase keeps the order: `TryCompile` returns the first non-null result. |
| D. `RegisterAutomapperProfiles`: every MapFrom becomes an accessor (93f4afb) | No | `Startup.cs:433`; `CustomRegisterDetailPrivacyTests.cs:125` | `Startup.cs:497-498`, called through `Vendor.Registration.Once` | Keep (patch 06) |
| E. `Load/LoadAsync<T,TDto>` with AutoMapper projection (53a5f08, 77a67c8) | No; upstream's ProjectTo workaround is in #367 | `Devex/DataSourceLoader.cs:120`: 37 `LoadDtoAsync` sites and 225 BaseController helper calls | `Devex/DataSourceLoader.cs:124` uses the **CancellationToken** overload | Keep (patch 07), all four overloads |
| F. LetClause two-stage Select (eda620a) | No | Every projection passes through it. Whether any map produces a LetClause is unknown. | Same | Keep (patch 08) |
| G. `ProjectBeforeFilter` option (53a5f08, 4f334d2) | No | Parser `DataSourceLoadOptions.cs:185-186`; `DummyController.cs:80` | Parser `DataSourceLoadOptions.cs:147`; dummy `:62` | Keep (patch 09) |
| H. Load-options `RuntimeResolutionContext` property (344745b, removed in 53a5f08) | n/a | Commented-out code only | No | Drop. Not in d04cfd8 or 1.16.0. |
| I. Custom accessor lookup on each path segment (4f334d2) | No | Implicit, for every dotted path | Implicit; the MCP hook also receives these per-segment calls | Keep as is. The defect is listed in section 7. |
| J. Nested ConcurrentDictionary (170fa8e, d04cfd8) | No | Needed by the per-request `Register<T,U>` calls | Same | Keep (patch 05). Not in pin 029326c. |
| K. Library parser `KEY_PROJECTBEFOREFILTER` (fork) | No | Not used. develop defines its own constant (`DataSourceLoadOptions.cs:85`). | **Used**: `DataSourceLoadOptions.cs:57` `KEY_PROJECTBEFOREFILTER = DxParser.KEY_PROJECTBEFOREFILTER` | **Changed by this review.** The public constant is added back (patch 11). The fork's read of the value stays dropped, because it assigned `Convert.ToBoolean(isCountQuery)` and nothing calls it. |
| L. Context parameter on internal classes (Sort/Select/RemoteGroup compilers, DefaultAccessor, SumFix, AggregateCalculator, SelectHelper) | No | Implicit | Implicit | Keep (patch 02), with `= null` defaults so that upstream's tests compile unchanged |
| M. Packaging: id `DXAutomap.AspNet.Data`, unsigned, AutoMapper dependency | No | Consumed as the 1.16.0 package | Same | Keep, changed (patches 01 and 11): net10.0 only; central package versions; System.Text.Json and Microsoft.CSharp references dropped; AssemblyKeyFile removed; CLSCompliant kept |
| Test, sample and sln edits; Tests.EFCore7; XPO bumps; IsMongoDB rename and revert; CodeRush file (9f43fc0, 671681d, 029326c, 00220d7, 4d6c4f1, eda620a) | Replaced by upstream's own project files | No | No | Drop |

Upstream changes the rebase brings in:
- #634: Guid `<`, `>`, `<=` and `>=` now compile to `CompareTo`.
- #631 and #663: System.Text.Json parser and converter fixes. The API does not call that parser.
- #636: TryGetValue changes.
- #683: DynamicClassBridge indexer.
- #736: a DateTimeOffset string value is parsed as a DateTimeOffset.
- #737: correct groupCount when GroupInterval is set.
- #733: net10.0 target.

## 3. Rebase base and patch series

**Base:** upstream 6.0.0-beta (9a5a549). master (8b05f05) has no `net/` changes after it.
- Why: it targets net10.0, the same as the API (`cpDataASP.csproj:3`). It also contains the #737 groupCount fix, which the client can trigger: `app.component.ts:124` turns on groupPaging for all grids, and `daycosts-grid.component.ts:2973` sets `groupInterval: 'day'`.
- If a non-prerelease tag is required, use 5.2.0 (182e124), not 5.1.0.

**How the fork side was checked:** the fork files were three-way merged from c94a149 to d04cfd8 onto 6.0.0-beta. There were two conflicts:
- the csproj;
- one line in `AddRemoteGrouping`, where #737 renamed `suppressGroups` to `suppressGroupSummary`. Both changes were kept.

**How to apply:** `patch -p1` from the folder that holds `net/`, in the order in `/tmp/claude-1000/-home-mike-civilpro-civilpro-wt-angular-liquid--models-update-CivilPro-ClientApp/e50612dd-0162-4456-a182-cfdd67691b9a/scratchpad/fork/final/series`.

| # | Patch | What it does | Differences from d04cfd8 |
|---|---|---|---|
| 01 | package-identity-net10 | PackageId and AssemblyName DXAutomap.AspNet.Data, version 1.17.0 (placeholder), net10.0 only, AutoMapper 13.0.1 in `net/Directory.Packages.props`, `AssemblyKeyFile` removed, Tests.Common moved to net10.0 | System.Text.Json and Microsoft.CSharp references dropped; CLSCompliant kept |
| 02 | runtime-resolution-context | Context field on ExpressionCompiler, passed through every compiler; `CompilerFuncWithContext`; `RegisterWithContext`; `AutomapperProjectionParameters` | New parameters default to null |
| 03 | context-custom-filters | `RegisterBinaryExpressionCompilerWithContext`; public FilterExpressionCompiler and ExpressionCompiler; `CompileNonCustomBinary` (both overloads); public `CreateItemParam` | The internal list keeps upstream's name `CompilerFuncs`; the duplicate assignment in the constructor is dropped |
| 04 | custom-accessor-library | AccessorLibrary, `Register<T,U>`, `RegisterContext` (both forms), Accessor, ParameterVisitor, per-segment lookup | None (known defects kept) |
| 05 | nested-concurrent-dictionary | Inner dictionary becomes a ConcurrentDictionary | None |
| 06 | register-automapper-profiles | `RegisterAutomapperProfiles(IMapper)` | None |
| 07 | automapper-projection-loader | The four `Load/LoadAsync<T,TDto>` overloads, projection in AddSelect | None |
| 08 | multistage-projection-letclause | Two Selects when there is a LetClause | Unused local `srcType` dropped |
| 09 | project-before-filter | Option and query ordering; upstream's SerializeEmptyOptions expected strings updated | None |
| 10 | fork-feature-tests | New: 6 in-memory xunit tests (`ForkAutoMapperTests.cs`) | New |
| **11** | **review-corrections** (this review) | (a) Adds the public const `KEY_PROJECTBEFOREFILTER` back, so MCP compiles; the parser still does not read it. (b) Rewords the csproj comment, which was false for AutoMapper 15.1.1 and 16.1.1. (c) Raises the test-only System.Security.Cryptography.Xml pin from 10.0.10 to 10.0.13. | New |

**Dropped:**
- the library parser's read of `projectbeforefilter`;
- the load-options `RuntimeResolutionContext` property;
- the IsMongoDB rename and its revert;
- the test, sample and sln edits;
- the System.Text.Json and Microsoft.CSharp references;
- the CLSCompliant removal and the release.snk.enc deletion (the file is left in place, unreferenced);
- the CodeRush file.

**Public surface compared with 1.16.0:** after patch 11, the only difference is that the `ExpressionCompiler` constructor parameter `runtimeResolutionContext` is optional. That change does not break any caller's source code.

**Files** (all under `/tmp/claude-1000/-home-mike-civilpro-civilpro-wt-angular-liquid--models-update-CivilPro-ClientApp/e50612dd-0162-4456-a182-cfdd67691b9a/scratchpad/fork/`):
- `rebase/patches/01..10`
- `final/11-review-corrections.patch`
- `final/series`
- `final/tree`: 6.0.0-beta with patches 01–11 applied
- `final/pkg/DXAutomap.AspNet.Data.1.17.0.nupkg`

## 4. Build and test results

Every command went through heavy.sh, one at a time.

| Check | Result | Evidence |
|---|---|---|
| Library Debug build, patches 01–10 | 0 errors; 2 × NU1903 (AutoMapper 13.0.1) | `rebase/logs/01-library-build.log`, `03-library-build.log` |
| Upstream tests plus 6 fork tests, Debug | Failed 1, Passed 294, Skipped 1, Total 296. All 6 fork tests pass. | `rebase/logs/04-tests.log` |
| Control: unmodified 6.0.0-beta tests | Failed 1, Passed 288, Skipped 1, Total 290. The same test fails in both runs: `DeserializeTestsEx.FilterOperandValueCanBeObject`, which expects `\r\n` line endings on Linux. | `rebase/logs/05-pristine-upstream-tests.log` |
| Series applied to fresh 6.0.0-beta gives the same tree (patches 01–10) | `diff -r` empty | `rebase/work/applycheck` |
| cpDataASP (develop d89ad3c4a copy) with a ProjectReference to the rebased library | Build succeeded, 0 errors, 415 warnings. None of the warnings comes from library code apart from NU1903. `project.assets.json` shows `DXAutomap.AspNet.Data/1.17.0` with `"type": "project"` and the path of the rebased tree. | `rebase/logs/06-cpDataASP-build.log`; `rebase/api-copy/cpDataASP/obj/project.assets.json:4753,19327` |
| **Release build and `dotnet pack` of `final/tree`, patches 01–11 (this review)** | exit 0, 0 errors. Warnings: NU1903 (AutoMapper) and NU5048 (`PackageIconUrl` deprecated). See the next row for the package contents. | `final/logs/01-release-pack.log`; `final/logs/02-build-server-shutdown.log` |
| Contents of the 1.17.0 package | `lib/net10.0/DXAutomap.AspNet.Data.dll` and `DevExtreme.AspNet.Data.xml`, the same layout as 1.16.0. The nuspec net10.0 group lists only `AutoMapper 13.0.1`. The DLL is not strong-named (CLI flags 0x1, no signature) and has no InternalsVisibleTo. It contains the `projectbeforefilter` constant. The DLL is byte-identical to the Release build output. sha256 9f0d0ff9e88b163dfb2df28859cf25b9bbfc083c6d25b25dd189f44023a1bc68. | `final/pkg/x/DXAutomap.AspNet.Data.nuspec` |

**Not run or not built:**
- Debug tests after patch 11. Patch 11 adds one constant, changes one comment and changes one test-only package version.
- Tests.Xpo, the .NET Framework test projects (which cannot reference a net10.0-only library), the database test projects and Sample.
- cpDataASP from the MCP branch. Instead I checked by grep that every library member the MCP branch uses is public in the rebased build: `RegisterWithContext`, `CompilerFuncWithContext`, `LoadAsync<T,TDto>(…, CancellationToken, IMapper, object)`, the `DxParser.KEY_*` constants, `RegisterContext<T,U>`, `CompileNonCustomBinary`, `RegisterAutomapperProfiles` and `Register<T,U>`.
- cpApi_IntegrationTests.
- SwaggerTest.
- Any runtime check.

The earlier claim that the API copy's DLL is "byte-identical" to the rebased build output cannot be repeated, because `rebase/src` has no `bin/` folder any more. The assets.json evidence above shows that the project reference was used.

## 5. Advisories

### API (NuGet)

| Package@version | Advisory | Where | Reachable in the API | What clears it |
|---|---|---|---|---|
| AutoMapper 13.0.1 | GHSA-rvv3-g6hj-g44x (CVE-2026-32933), high | Direct in cpDataASP, cpDataORM, cpDataServices and cpModel. Transitive in cpDataReports and cpReportDefinitions. That is 6 of the 7 listed projects; cpShared is clean. Also a direct pin in 4 desktop projects (`CivilPro.UI.WinForms.csproj:695`, `cpServices.csproj:91`, `cpReporting.csproj:100`, `CivilPro.Reporting.csproj:99`). The DXAutomap package also depends on it. | Low. Request bodies are limited to Newtonsoft MaxDepth 32, and ProjectTo builds expressions rather than walking objects. | Bump to 15.1.1+ or 16.1.1+, which needs an RPL-1.5 or commercial licence. **This cannot be done in the API alone:** the desktop direct pins against a shared-project requirement of ≥15.1.1 give NU1605, which the SDK treats as an error (`Microsoft.NET.Sdk.CSharp.props:18`; inferred, not built). Code that must change: `Startup.cs:168`, `cpDataORM/Helpers/AutomapperConfig.cs:9,15`, `cpApiTestData/Database/BootstrapDi.cs:60`, 38 live `new MapperConfiguration(` lines in cpApi_IntegrationTests (2 more are commented out), and on the desktop side `mdimProject.cs:203` and `CustomRegisterService.cs:335,389`. DXAutomap has to be rebuilt against the new AutoMapper. A mitigation that keeps the scanner flag is `.MaxDepth(n)` on self-referential maps. The fork rebase does not clear this advisory. |
| System.Text.Json 7.0.3 | GHSA-hh2w-p6rv-4g7w, high | Fork csproj only. It is pruned from the API graph on net10.0. | No | Fork rebase (patch 01). Verified: the 1.17.0 nuspec has no System.Text.Json dependency. |
| System.Linq.Dynamic.Core 1.3.3 | GHSA-4cv2-4hjh-77rx, high | Fork test projects only | No | Fork rebase (upstream tests use 1.6.8) |
| System.Security.Cryptography.Xml 10.0.10 | GHSA-c399-9853-6jmj, GHSA-7hqp-7c5m-4vq6, GHSA-r955-8q5j-3h83, all high | Rebased fork tree, Tests.Xpo only. It came from upstream 6.0.0-beta. | No; it is not in the package | Patch 11 sets 10.0.13 (published on nuget.org). Not built. |
| System.Security.Cryptography.Xml 8.0.2 | 12 advisories, high | Transitive in 6 libraries. Pruned in cpDataASP, which uses the ASP.NET Core shared-framework copy. | Through the shared framework, if the deployment is framework-dependent. That is not confirmed: the only `SelfContained=false` profile is a stale net6.0 pubxml, and all 4 profiles target net472 or net6.0. | Host runtime ASP.NET Core 10.0.13+. For the libraries, a direct 10.0.13 reference in cpShared. No desktop pin conflicts with it. |
| System.Security.Cryptography.Pkcs 9.0.4 (API) / 8.0.1 (cpShared) | GHSA-846f-f764-76mx (high), GHSA-2fmv-rv2c-59x7 (moderate) | Ships with the API | Yes (DoS) | Direct 10.0.13 reference in cpShared. No desktop pin conflicts with it. |
| SixLabors.ImageSharp 3.1.12 | 3 high, 2 moderate | Direct in cpDataServices (PlanService, ImageLayerService) | Yes | Bump to 4.1.2+, a major version. API breaks were not checked. The desktop exe would ship it through cpDataServices; desktop code does not use it directly. |
| OpenMcdf 3.1.0 | 2 moderate | Via MsgReader 6.0.6 | Yes | Direct 3.3.0 reference in cpDataServices, or MsgReader 6.2.0. Desktop `cpServices.csproj:113` pins MsgReader 6.0.6 but does not reference cpDataServices, so there is no downgrade conflict. |
| DXAutomap.AspNet.Data / DevExtreme.AspNet.Data | none in the GitHub advisory database | | | |

### Client (yarn.lock; yarn 1.22.22)

The audit was run twice:
- With the task's `--groups dependencies devDependencies`, yarn audited only `dependencies`: 47 advisories in 12 packages.
- With no `--groups`: 151 advisories in 41 packages. The 5 criticals are all in dev tooling.

| Package@version | Advisory | Reachable | What clears it |
|---|---|---|---|
| @angular/* 21.2.17 | GHSA-jj27-h5hq-8x99 (high, i18n XSS), GHSA-hh8m-fm6v-7cvg (moderate). The SSR advisories do not apply because there is no SSR config. | Browser build | Bump all @angular/* to 21.2.25, including the exact peerDependencies in cp-lib |
| vite 7.3.6 | 5 dev-server advisories, fixed in 7.3.7 | Dev server only | No fix yet: the CLAUDE.md pin rule ties vite to the version @angular/build 21.2.25 uses, which is 7.3.6 |
| fast-uri 3.1.3, browserslist 4.28.5, baseline-browser-mapping 2.10.42 | high / high / moderate | Build time | Lockfile refresh |
| brace-expansion 1.1.15 and 2.1.1, undici 6.27.0 | high / moderate | Runtime group | Lockfile refresh |
| @grpc/grpc-js 1.9.16 (via firebase) | GHSA-m9gg-hp2v-232j (high) | Probably not in the browser bundle (not verified) | No firebase release clears it (all pin ~1.9.0); only a resolutions override would |
| uuid 8.3.2 (via exceljs) | GHSA-w5hq-g745-h8pq (moderate) | Narrow | Only a major-version override |
| Dev tooling (handlebars, piscina, proxy-addr, storybook and others) | 5 critical, many high | Developer machines and CI | Mostly a lockfile refresh or tool upgrades |
| devextreme 25.2.6, devextreme-aspnet-data-nojquery 4.0.2 | none | | No action |
| pnpm-lock.yaml (drifted from yarn.lock) | 258 advisories (8 critical) | Depends on which lock CI uses | Refresh it separately |

## 6. Upgrade order relative to the `_Named` / positional `get*(loadOptions)` removal work

Step names refer to `CivilPro/ClientApp/docs/generated-client-contract/OVERVIEW.md` section 6. "L1" is the library PR: the rebased DXAutomap version, plus a change to `cpDataASP.csproj:52` and regenerated `packages.lock.json` in cpDataASP, cpApiTestData and cpApi_IntegrationTests.

1. **L1 has no swagger effect, and must show it.**
   - `DataSourceLoadOptionsDummy` uses the library types SortingInfo, GroupingInfo and SummaryInfo. Those files, and ResponseModel, are byte-identical between upstream 4.0.0 and the rebased tree.
   - Even so, L1 must run SwaggerTest and show that the models hash is unchanged, because API-1 relies on that hash. Keep L1 a separate PR from API-1.
2. **Land L1 after the MCP branches** (decision 1) **and before API-3**.
   - MCP-server2 adds its own uses of the library: the `RestrictedColumns` hook, the CancellationToken overload and `DxParser.KEY_PROJECTBEFOREFILTER`. Patch 11 is required for it to compile.
   - Testing L1 on top of MCP covers both sets of callers.
   - If L1 lands first, the MCP branch has to be built against 1.17.0 before it merges. Whichever lands second regenerates the lock files, because MCP-server2 also pins 1.16.0.
   - API-3 changes which values reach the library: Group as JSON, two-level sorts, and summaries passed positionally. With L1 already in, a failure after API-3 can be traced to one PR. API-5's planned multi-level sort test through `LoadDtoAsync` then also runs against the library that will ship.
3. **Land L1 before B2.** In B2, header-filter loads (group, with groupInterval on date columns) move to `_Named` "raw". #737's groupCount fix is in the rebased library.
4. **Decide ProjectBeforeFilter before API-4.**
   - The contract plan treats `projectBeforeFilter` as an option the API reads, so API-4 would add it to the 1151 `I*Args` interfaces (DESIGN.md:325,331).
   - If the API should stop accepting it from requests (the count, group-count and key queries are not projected, and any caller can set it), handle it as an 11th unread option:
     - tag it in API-1;
     - stop sending it and refuse it in API-4;
     - remove it from the dummy and stop parsing it (develop `DataSourceLoadOptions.cs:128,186`; MCP `:89,147`) in API-7, where `manageLoadOptions` and `dataSourceUtil.ts:105` go away.
   - The library property stays either way, because `DummyController.cs:80` and the fork tests set it in code.
5. **API-7 needs no library change.** The 10 unread options are removed only from `DataSourceLoadOptionsDummy` (API code). `DataSourceLoadOptionsBase` keeps them.
6. **devextreme-aspnet-data-nojquery: no bump.**
   - The published `index.js` of 4.0.2, 5.1.0 and 6.0.0-beta differs only in the version comment, and the `js/` source is identical from 4.0.0 to 6.0.0-beta. So the client and server versions do not need to match, and DESIGN.md:273's citation of 4.0.2 `index.js:221-228` stays valid.
   - Its only importer is `src/core/_services/cp-api-services/user.service.ts:3`. B1 moves that service's three stores to `createApiStore`, so remove the package in B1 or right after it.
7. **AutoMapper bump (L2): a separate track after L1.**
   - It depends on the licence decision and the desktop decision.
   - It needs a DXAutomap version built against AutoMapper ≥15.1.1, because the fork calls `AutoMapper.Internal` APIs. Those compiled against 15.1.1 from 029326c source, which has the same AutoMapper call sites as d04cfd8.
   - Before L2, any new integration tests (for example API-5's) should create `MapperConfiguration` through one shared test helper, so that L2 changes one place.
8. **Upstream 6.0.0 final:** re-apply the series when it is released. Master after the beta has no `net/` changes.

## 7. Risks and next steps

**Behaviour changes from upstream** (not fork regressions; none run against SQL Server):
- #634: Guid `<`, `>`, `<=` and `>=` now go through `CompareTo` (`FilterExpressionCompiler.cs:185-188`). 1.16.0 used Guid's operators on .NET 7+.
- #736: a DateTimeOffset string value is parsed as a DateTimeOffset.
- #737: groupCount with GroupInterval.

**Fork defects kept unchanged:**
1. The per-segment accessor lookup passes the root `target`, not `currentTarget` (`ExpressionCompiler.cs:49-55`). For `A.B`, segment `B` resolves against the root type's accessors. The MCP `RestrictedColumns` hook receives the same calls. Reading the code, the result there is extra redaction, not a leak, because the whole-path call runs first.
2. `AccessorLibrary.Add` has a check-then-add race. MCP's `Vendor.Registration.Once` guards the startup registration, but not the per-request `Register<T,U>` calls in `ICustomRegister`1.cs`.
3. Accessors are keyed by the short type name.
4. `RegisterAutomapperProfiles` keys entries by (entity short name, member) and leaves out the DTO type, so two DTOs mapped from one entity share one entry. A text scan (`final/scripts/mapfrom_collisions.py`, output in `final/evidence/mapfrom_collisions.txt`) lists 38 candidate keys. Many differ only in null guards. The scan ignores IncludeAllDerived/IncludeBase, and some of its lines are scanner errors. Pairs whose meaning clearly differs:
   - `Approval.Status`, `IsCompleted` and `DateLastStep`: ApprovalListStatuserDto `AutoMapper_base.cs:30` against ApprovalLotItpDetailReportDto `:1846`.
   - `Lot.TRCount`: `:1904` against `AutoMapperProfileORM.cs:195`.
   - `User.FirstLetterLastName`: `:1664` against `:1703`.
   - `Notification.Status`: an enum in one DTO, a string in the other.
   - `PurchaseOrder.ProjectName`, `TestRequest.LatestUploadDate`, `TransmittalItem.RevisionDate`.

   Which entry wins depends on the order of `GetAllTypeMaps`. Not checked at runtime.
5. With ProjectBeforeFilter set, the count, group-count (including #737's path) and key queries are not projected, and any request can set it.
6. `LoadAsync<T,TDto>` with a null mapper and `TDto != T` throws NullReferenceException.

**Tests that do not exist yet:** patches 05 and 08; the per-segment dotted path; `Register<T,U>`; ProjectBeforeFilter on the grouping path; the context in DefaultAccessor, SumFix and remote grouping.

**Next steps, in order:**
1. Package owner:
   - Apply 01–11 to a branch of github.com/statler/DevExtreme.AspNet.Data from 6.0.0-beta or master, and push it.
   - Pack Release from that pushed commit, so the nuspec records a commit that exists on GitHub. My pack has `<repository type="git" />` with no commit, because the tree was not a git checkout. The 1.16.0 package recorded 3f466bec, which cannot be found.
   - Publish 1.17.0, or have cpDataASP switch to a ProjectReference.
2. Before publishing:
   - Build cpDataASP from develop and from MCP-server2 against the nupkg from a local feed.
   - Run the Debug tests on 01–11.
   - Run SwaggerTest and confirm the models hash is unchanged.
   - Regenerate the 3 lock files.
   - Run cpApi_IntegrationTests on Linux against cp_testing_integration_mssql (including MCP's VendorRegistrationTests, RestrictedColumnsLoaderTests and QueryToolsTests).
3. In the running API, check:
   - grids with custom-register columns;
   - Notifications (UserId context) and Approvals (minsOffset);
   - a Guid range filter;
   - a grouped grid with groupInterval and group paging;
   - a filter on `Status` in the approvals list (risk 4).
4. Decide ProjectBeforeFilter before API-4, and the AutoMapper licence and desktop question before L2.
5. Later fork fixes: segment target, the Add race, keying by (source, destination), and the null-mapper exception.
