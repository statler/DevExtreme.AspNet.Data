# DXAutomap mapping regression tests

Run from the repository root:

```powershell
dotnet build net/DXAutomap.AspNet.Data.slnx -c Debug -m:1 -nodeReuse:false -p:UseSharedCompilation=false
./net/dotnet-test-all.cmd
```

This is the supported full suite entry point, also used by CI. It includes these mapping tests, the main regression tests, and EF Core 10/NHibernate/XPO provider tests using Windows SQL Server LocalDB. It leaves the shared LocalDB instance running. The inherited upstream solution still contains .NET Framework/.NET 11 projects outside this .NET 10 fork's supported matrix.

To run only the suites that do not require SQL Server:

```powershell
dotnet test net/DXAutomap.AspNet.Data.Tests/DXAutomap.AspNet.Data.Tests.csproj
dotnet test net/DevExtreme.AspNet.Data.Tests/DevExtreme.AspNet.Data.Tests.csproj
```

The mapping scenarios run against LINQ to Objects and EF Core SQLite with a separate in-memory database per test. No server or connection string is required. SQLite tests execute queries, rather than only checking expression construction. The native SQLite bundle is pinned separately to avoid EF Core's older transitive bundle.

For the complete build, test and package verification procedure, use `./build/release.ps1`; see [2.0.0 release notes](../../docs/civilpro/RELEASE-2.0.0.md). The main regression suite needs Debug because upstream test hooks and friend-assembly access are enabled only in that configuration; the separate mapping suite also runs against Release.

Coverage includes request parameters (anonymous objects and dictionaries), separate destination DTOs and mapper configurations, filtering, sorting, local and remote selection (including automatic fallback above 32 fields), counts, grouped summaries, group counts, key paging, empty sums, multi-stage AutoMapper projections, concurrent requests, string conversion/case handling, numeric/nullable-date group intervals, and nested accessors with context and null parents.

The main test project retains the original defect reproductions, now asserting corrected behaviour, and the registration tests. Additional tests cover type-identity collisions and null-mapper argument validation across typed loader overloads. `ProjectBeforeFilter` is retired: obsolete compatibility members remain, but the property is ignored and excluded from JSON. Tests verify that legacy callers use the standard query path. SQLite coverage does not replace CivilPro's SQL Server integration tests or MCP restricted-column tests.
