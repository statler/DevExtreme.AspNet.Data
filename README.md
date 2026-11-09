# DXAutomap.AspNet.Data

This repository contains the CivilPro fork of DevExtreme.AspNet.Data, published on NuGet as **DXAutomap.AspNet.Data**. The 2.x releases target **.NET 10** with **AutoMapperMIT 14.0.3**; the version in this branch's project file is **2.0.2**. Each release is built from a commit tagged `civilpro-v<version>`; see [Releasing a version](#releasing-a-version).

In typed loads (`LoadAsync<TSource, TDto>`), request selectors can name only members of the DTO, unlike upstream, where selectors are paths on the queried item. See the package README for the rules.

- [Package usage and compatibility](net/DevExtreme.AspNet.Data/README.md)
- [2.x release notes and validation records](docs/civilpro/RELEASE-2.x.x.md)
- [Fork regression tests](net/DXAutomap.AspNet.Data.Tests/README.md)

## Releasing a version

Every published version is built from a commit that is on GitHub and tagged, so anyone can find the exact source of a package. Earlier versions were built from uncommitted changes, which were committed afterwards or not at all: 1.0.0 to 1.14.0, 2.0.0 and 2.0.1 record no commit, and the commit that 1.16.0 records was never pushed.

1. **Make the changes.** Commit them on a branch, merge them into `dev/AddRuntimeResolutionContext-dx-6.0.0` and push.
2. **Set the version in its own commit.** In `net/DevExtreme.AspNet.Data/DevExtreme.AspNet.Data.csproj` set `Version`, `AssemblyVersion`, `FileVersion` and `PackageReleaseNotes`, and set `PackageValidationBaselineVersion` to the last published version. Update the version and the source tag in `net/DevExtreme.AspNet.Data/README.md` (it is the package's nuget.org page) and the default in `build/package-smoke/PackageSmoke.csproj`, and add the release's section to [RELEASE-2.x.x.md](docs/civilpro/RELEASE-2.x.x.md). To check the packaging before committing, run `./build/release.ps1 -SkipTests -Trial`; it packs `<version>-trial`, which is not for upload.
3. **Commit, push and tag.** Push the commit, then tag it and push the tag:

   ```shell
   git tag -a civilpro-v2.0.2 -m "DXAutomap.AspNet.Data 2.0.2"
   git push origin civilpro-v2.0.2
   ```

4. **Build from the clean tagged commit.** On Windows with the .NET 10 SDK and SQL Server LocalDB, with `git status` showing no changes, run `./build/release.ps1` from the repository root. It refuses to run if anything is uncommitted or if the tag `civilpro-v<version>` is not on HEAD.
5. **Upload** `artifacts/release/DXAutomap.AspNet.Data.<version>.nupkg` to nuget.org. Upload only a package that step 4 built.

If something must change after tagging, commit the fix, move the tag to the new commit and build again. A version cannot be replaced on nuget.org once it is uploaded, so a fix after upload needs a new version.

`release.ps1` builds the solution and runs the full suite (`net/dotnet-test-all.cmd`), runs the mapping suite against the Release build and packs the library; package validation compares the public API with `PackageValidationBaselineVersion`. It then checks the archive (version, README, XML documentation, net10.0 assembly, AutoMapperMIT 14.0.3 as the only runtime dependency, and that the package records the HEAD commit), restores the package into a fresh cache and runs the smoke application in `build/package-smoke`, and writes a SHA-256 checksum beside the package. `-SkipTests` skips the Debug build and the full suite, for use only after a recorded full-suite pass on the same commit.

The package records the repository URL and the commit in its nuspec, where tools such as NuGet Package Explorer show them. The nuget.org page shows only the repository link and the README, so the README's source tag is the link to the source that nuget.org displays.

The sections below describe the upstream library. Their package badges and upstream release links are not the DXAutomap fork release channel.

## Upstream DevExtreme ASP.NET Data

[![CI](https://github.com/DevExpress/DevExtreme.AspNet.Data/actions/workflows/ci.yml/badge.svg?branch=master&event=push)](https://github.com/DevExpress/DevExtreme.AspNet.Data/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/DevExpress/DevExtreme.AspNet.Data/branch/master/graph/badge.svg)](https://codecov.io/gh/DevExpress/DevExtreme.AspNet.Data)
[![NuGet](https://img.shields.io/nuget/v/DevExtreme.AspNet.Data.svg?maxAge=43200)](https://www.nuget.org/packages/DevExtreme.AspNet.Data)
[![npm](https://img.shields.io/npm/v/devextreme-aspnet-data.svg?maxAge=43200)](https://www.npmjs.com/package/devextreme-aspnet-data)
[![npm nojquery](https://img.shields.io/npm/v/devextreme-aspnet-data-nojquery.svg?maxAge=43200&label=npm+nojquery)](https://www.npmjs.com/package/devextreme-aspnet-data-nojquery)

This library enables [DevExtreme client-side widgets](https://js.devexpress.com) to perform CRUD operations via ASP.NET controllers and handlers and allows you to delegate all data-intensive operations to the server:

* a widget sends data loading options (filtering, grouping, sorting, etc.) to the server;
* the server processes data according to these options;
* the processed data is sent back to the widget.

Can be used with:

* [DevExtreme client-side widgets](https://js.devexpress.com)
* [DevExtreme-based ASP.NET Core controls](https://docs.devexpress.com/AspNetCore/400263)
* [DevExtreme ASP.NET MVC 5 controls](https://docs.devexpress.com/DevExtremeAspNetMvc/400943/)

## Installation and Configuration

`DevExtreme.AspNet.Data` consists of server-side and client-side parts. The following topics explain how to install and configure these parts:

- [Server Side Configuration](docs/server-side-configuration.md)
- [Client Side with jQuery](docs/client-side-with-jquery.md)
- [Client Side without jQuery (Angular, etc.)](docs/client-side-without-jquery.md)

## CI Builds

We recommend that you use [release builds](https://github.com/DevExpress/DevExtreme.AspNet.Data/releases). However, you can also use [CI builds](docs/using-ci-builds.md) to get urgent bug fixes or to test unreleased functionality.
