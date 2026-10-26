param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$library = Join-Path $repoRoot 'net/DevExtreme.AspNet.Data/DevExtreme.AspNet.Data.csproj'
$releaseDirectory = Join-Path $repoRoot 'artifacts/release'
$resultsDirectory = Join-Path $repoRoot 'artifacts/test-results'
[xml]$project = Get-Content -Raw $library
$version = [string]($project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)

function Invoke-DotNet {
    & dotnet @args
    if($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}

Push-Location $repoRoot
try {
    if(-not $SkipTests) {
        if(-not (Get-Command SqlLocalDB -ErrorAction SilentlyContinue)) {
            throw 'Full release tests require Windows and SQL Server LocalDB. Use -SkipTests only after recording a successful full suite run.'
        }
        Invoke-DotNet build net/DXAutomap.AspNet.Data.slnx -c Debug '-m:1' '-nodeReuse:false' '-p:UseSharedCompilation=false'
        & ./net/dotnet-test-all.cmd --logger trx --results-directory $resultsDirectory
        if($LASTEXITCODE -ne 0) { throw 'The full test suite failed.' }
    }

    # Exercise the public mapping surface against the Release library too.
    Invoke-DotNet test net/DXAutomap.AspNet.Data.Tests/DXAutomap.AspNet.Data.Tests.csproj -c Release '-m:1' '-nodeReuse:false' '-p:UseSharedCompilation=false' --logger 'trx;LogFileName=mapping-release.trx' --results-directory $resultsDirectory

    # An uncommitted build must not advertise HEAD as if it contains the packaged changes.
    $gitStatus = & git -c "safe.directory=$($repoRoot.Replace('\', '/'))" status --porcelain
    if($LASTEXITCODE -ne 0) { throw 'Cannot determine source provenance.' }
    $provenance = @()
    if($gitStatus) {
        $provenance = @('-p:EnableSourceControlManagerQueries=false', '-p:RepositoryCommit=', '-p:SourceRevisionId=', '-p:IncludeSourceRevisionInInformationalVersion=false')
        Write-Host 'Packaging working-tree changes; omitting the repository commit from package metadata.'
    }
    Invoke-DotNet pack $library -c Release '-m:1' '-nodeReuse:false' '-p:UseSharedCompilation=false' --output $releaseDirectory @provenance

    $packagePath = Join-Path $releaseDirectory "DXAutomap.AspNet.Data.$version.nupkg"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        foreach($entry in @('README.md', 'lib/net10.0/DXAutomap.AspNet.Data.dll', 'lib/net10.0/DXAutomap.AspNet.Data.xml')) {
            if(-not $archive.GetEntry($entry)) { throw "Package is missing $entry." }
        }
        $reader = [System.IO.StreamReader]::new($archive.GetEntry('DXAutomap.AspNet.Data.nuspec').Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if($nuspec.package.metadata.version -ne $version) { throw 'Package version mismatch.' }
        if($gitStatus -and $nuspec.package.metadata.repository.commit) { throw 'A working-tree package must not advertise a repository commit.' }
        $dependencies = @($nuspec.package.metadata.dependencies.group.dependency)
        if($dependencies.Count -ne 1 -or $dependencies[0].id -ne 'AutoMapperMIT' -or $dependencies[0].version -ne '14.0.3') {
            throw 'Unexpected runtime package dependencies.'
        }
    } finally { $archive.Dispose() }

    # A fresh package cache prevents a previous local package with this version from masking packaging errors.
    $smokePackages = Join-Path $repoRoot ("artifacts/smoke-packages/" + [guid]::NewGuid().ToString('N'))
    Invoke-DotNet restore build/package-smoke/PackageSmoke.csproj --configfile build/package-smoke/NuGet.Config --packages $smokePackages "-p:PackageVersionUnderTest=$version"
    Invoke-DotNet run --project build/package-smoke/PackageSmoke.csproj -c Release --no-restore "-p:PackageVersionUnderTest=$version" '-p:UseSharedCompilation=false'

    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($packagePath))" | Set-Content -LiteralPath "$packagePath.sha256" -Encoding ascii
    Write-Host "Upload package: $packagePath"
} finally {
    Pop-Location
}
