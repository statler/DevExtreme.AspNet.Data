# DXAutomap.AspNet.Data 2.0.2

Server-side DevExtreme data loading with AutoMapperMIT projections and runtime-context-aware custom accessors. This is the CivilPro fork of DevExtreme.AspNet.Data, based on the upstream 6.0.0 development line.

Source: tag [`civilpro-v2.0.2`](https://github.com/statler/DevExtreme.AspNet.Data/tree/civilpro-v2.0.2) in github.com/statler/DevExtreme.AspNet.Data. The package also records the commit it was built from.

## Requirements

- .NET 10.
- AutoMapperMIT 14.0.3 (installed as a dependency). Replace references to the original AutoMapper package in consuming projects; the namespace remains `AutoMapper`.

## Install

```shell
dotnet add package DXAutomap.AspNet.Data --version 2.0.2
```

nuget.org also holds an old unlisted 999.0.0 build. A `*` wildcard selects it, and so does a reference to a version that is not on the feed, because NuGet then takes the next higher version. To accept only one version, write it in brackets: `Version="[2.0.2]"`.

## Typed loads

Pass the mapper and per-request projection parameters to the typed loader:

```csharp
var result = await DataSourceLoader.LoadAsync<Entity, EntityDto>(
    query, loadOptions, cancellationToken, mapper, new { UserId, minsOffset });
```

A typed load queries the source item and returns the DTO. The request's selectors (filter, sort, group, select and summaries) can name only what the DTO exposes:

- Each segment of a dotted path must be a member of the DTO, or of the DTO type of the previous segment. Otherwise the load throws `ArgumentException` ("'X' is not a member of type 'Dto'"), as an unknown member does in an untyped load. Member names match in any letter case, exact case first.
- DTO members that the map does not read by their own name (`MapFrom`, flattened, or mapped from a differently named member) are read through the DTO's map with the request's projection parameters, at the root and in nested DTOs, so a selector sees the value the response shows. Members the map ignores are refused.
- The load's own primary key is always accepted, because the loader sorts and pages by it.
- Custom accessors take precedence according to compiler registration order. A registered accessor on the source type may start a path even when the DTO has no such member; deeper in a path, members the DTO does not declare are refused. `Register` and `RegisterContext` activate accessor lookup independently of global profile registration.

Untyped loads (no destination type) behave as upstream: selectors are dotted paths on the item type. Plain loads without a destination/mapper retain legacy global profile lookup; use a typed load when multiple DTOs map the same source member differently.

## Changes in 2.0.2

- The same code as 2.0.1. The package is built from a tagged commit and records it, and this README documents the typed-load selector rules above.
- The assembly version is 2.0.2.0; 2.0.1 kept 2.0.0.0.

## Changes in 2.0.1

- Typed loads accept only selectors that name members of the requested DTO, as described above. Previously selectors were compiled against the source item, so a request could filter, sort, group or select source members that the DTO does not expose. Selectors that name only source members now throw `ArgumentException`.
- Mapped DTO members are read through the DTO's map in nested DTOs as well as at the root, and in any letter case. Previously a selector in a different letter case could read the same-named source member instead.
- Untyped loads and the public API are unchanged.

## Changes in 2.0.0 (from 1.16.0)

- Registration is safe under concurrent reads/writes and distinguishes actual .NET types, including same-name classes and closed generic types.
- Runtime mapping parameters and destination-specific expressions are applied consistently to query processing.
- Custom accessors support nested property paths, string conversion/case handling, null processing and group intervals.
- Local selection works after DTO projection, including nested projections and selections above 32 fields.
- Typed loads with different source/destination types require a mapper and report a missing mapper as `ArgumentNullException`.
- `ProjectBeforeFilter` is retired. Its obsolete property is a no-op and omitted from JSON; the parser constant remains for source compatibility.
- Nested segment hooks receive the current nested expression/type. Whole-path hooks retain precedence.
- The net7.0 target is replaced with net10.0, which requires consumer recompilation.

The assembly remains unsigned. See the [2.x release notes](https://github.com/statler/DevExtreme.AspNet.Data/blob/civilpro-v2.0.2/docs/civilpro/RELEASE-2.x.x.md) for validation scope and migration details.
