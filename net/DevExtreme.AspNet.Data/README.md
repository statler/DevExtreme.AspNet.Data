# DXAutomap.AspNet.Data 2.0.0

Server-side DevExtreme data loading with AutoMapperMIT projections and runtime-context-aware custom accessors. This is the CivilPro fork of DevExtreme.AspNet.Data, based on the upstream 6.0.0 development line.

## Requirements

- .NET 10.
- AutoMapperMIT 14.0.3 (installed as a dependency). Replace references to the original AutoMapper package in consuming projects; the namespace remains `AutoMapper`.

## Install

```shell
dotnet add package DXAutomap.AspNet.Data --version 2.0.0
```

Pass the mapper and per-request projection parameters to the typed loader:

```csharp
var result = await DataSourceLoader.LoadAsync<Entity, EntityDto>(
    query, loadOptions, cancellationToken, mapper, new { UserId, minsOffset });
```

The typed loader resolves mapped selectors using the requested DTO, mapper configuration and request parameters. Custom accessors retain precedence according to compiler registration order. `Register` and `RegisterContext` activate accessor lookup independently of global profile registration.

## Changes from 1.16.0

- Registration is safe under concurrent reads/writes and distinguishes actual .NET types, including same-name classes and closed generic types.
- Runtime mapping parameters and destination-specific expressions are applied consistently to query processing.
- Custom accessors support nested property paths, string conversion/case handling, null processing and group intervals.
- Local selection works after DTO projection, including nested projections and selections above 32 fields.
- Typed loads with different source/destination types require a mapper and report a missing mapper as `ArgumentNullException`.
- `ProjectBeforeFilter` is retired. Its obsolete property is a no-op and omitted from JSON; the parser constant remains for source compatibility.

Nested segment hooks now receive the current nested expression/type. Whole-path hooks retain precedence. Plain loads without a destination/mapper retain legacy global profile lookup; use a typed load when multiple DTOs map the same source member differently.

The assembly remains unsigned. This release replaces the net7.0 target with net10.0 and requires consumer recompilation. See the [release notes](https://github.com/statler/DevExtreme.AspNet.Data/blob/dev/AddRuntimeResolutionContext-dx-6.0.0/docs/civilpro/RELEASE-2.0.0.md) for validation scope and migration details.
