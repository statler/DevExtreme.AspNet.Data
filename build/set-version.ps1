# Sets a new release version: step 2 of "Releasing a version" in the repository README.
# Edits the project file, the package README, the package smoke project and docs/civilpro/RELEASE-2.x.x.md.
# Does not commit, tag or publish.
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ReleaseNotes
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$library = Join-Path $repoRoot 'net/DevExtreme.AspNet.Data/DevExtreme.AspNet.Data.csproj'
$packageReadme = Join-Path $repoRoot 'net/DevExtreme.AspNet.Data/README.md'
$smokeProject = Join-Path $repoRoot 'build/package-smoke/PackageSmoke.csproj'
$packageVersions = Join-Path $repoRoot 'net/Directory.Packages.props'
$releaseNotesFile = Join-Path $repoRoot 'docs/civilpro/RELEASE-2.x.x.md'

function Invoke-Git {
    $output = & git -c "safe.directory=$($repoRoot.Replace('\', '/'))" @args
    if($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed with exit code $LASTEXITCODE." }
    return $output
}

# Files are rewritten with the encoding (byte order mark or not) and line endings they already have.
function Read-Text($path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.Text.UTF8Encoding]::new($false).GetString($bytes, $(if($bom) { 3 } else { 0 }), $bytes.Length - $(if($bom) { 3 } else { 0 }))
    [pscustomobject]@{ Path = $path; Text = $text; Bom = $bom; NewLine = $(if($text.Contains("`r`n")) { "`r`n" } else { "`n" }) }
}

function Write-Text($file) {
    [System.IO.File]::WriteAllText($file.Path, $file.Text, [System.Text.UTF8Encoding]::new($file.Bom))
}

# Replaces a pattern that must occur exactly once (at most once with -Optional), so a changed file layout stops the
# script instead of being skipped. The replacement is literal text.
function Set-Once($file, [string]$pattern, [string]$replacement, [switch]$Optional) {
    $found = [regex]::Matches($file.Text, $pattern)
    if($found.Count -gt 1 -or ($found.Count -eq 0 -and -not $Optional)) { throw "Expected one match of '$pattern' in $($file.Path); found $($found.Count)." }
    $file.Text = [regex]::Replace($file.Text, $pattern, $replacement.Replace('$', '$$'))
}

function Get-Element($file, [string]$name) {
    $found = [regex]::Matches($file.Text, "<$name>([^<]*)</$name>")
    if($found.Count -ne 1) { throw "Expected one <$name> in $($file.Path); found $($found.Count)." }
    return $found[0].Groups[1].Value
}

Push-Location $repoRoot
try {
    if($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be major.minor.patch, for example 2.0.3; got '$Version'." }
    if(Invoke-Git status --porcelain) { throw 'The working tree has uncommitted changes. The version change goes in a commit of its own; commit or discard the other changes first.' }

    $project = Read-Text $library
    $previous = Get-Element $project 'Version'
    if([version]$Version -le [version]$previous) { throw "Version $Version is not later than the current version $previous." }

    $tag = "civilpro-v$Version"
    if(& git -c "safe.directory=$($repoRoot.Replace('\', '/'))" rev-parse --verify --quiet "refs/tags/$tag") { throw "Tag $tag already exists." }
    $published = $null
    try { $published = (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/dxautomap.aspnet.data/index.json').versions }
    catch { Write-Warning "Could not check nuget.org: $($_.Exception.Message)" }
    if($published -contains $Version) { throw "Version $Version is already on nuget.org." }
    if($published -and $published -notcontains $previous) { Write-Warning "The current version $previous is not on nuget.org; package validation will fail until it is, because $previous becomes the baseline." }

    # Project file: the previous version becomes the package validation baseline.
    $escapedNotes = [System.Security.SecurityElement]::Escape($ReleaseNotes)
    Set-Once $project '<Version>[^<]*</Version>' "<Version>$Version</Version>"
    Set-Once $project '<AssemblyVersion>[^<]*</AssemblyVersion>' "<AssemblyVersion>$Version.0</AssemblyVersion>"
    Set-Once $project '<FileVersion>[^<]*</FileVersion>' "<FileVersion>$Version.0</FileVersion>"
    Set-Once $project '<PackageReleaseNotes>[^<]*</PackageReleaseNotes>' "<PackageReleaseNotes>$escapedNotes</PackageReleaseNotes>"
    Set-Once $project '<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>' "<PackageValidationBaselineVersion>$previous</PackageValidationBaselineVersion>"
    $targetFramework = Get-Element $project 'TargetFrameworks'

    # Package README (the nuget.org page): version, install line, exact-version example and source tag links.
    $readme = Read-Text $packageReadme
    $old = [regex]::Escape($previous)
    $nl = $readme.NewLine
    Set-Once $readme "(?m)^# DXAutomap\.AspNet\.Data $old(?=\r?$)" "# DXAutomap.AspNet.Data $Version"
    Set-Once $readme "--version $old\b" "--version $Version"
    Set-Once $readme "Version=`"\[$old\]`"" "Version=`"[$Version]`"" -Optional
    if(-not [regex]::IsMatch($readme.Text, "civilpro-v$old\b")) { throw "No link to tag civilpro-v$previous in $packageReadme." }
    $readme.Text = [regex]::Replace($readme.Text, "civilpro-v$old\b", "civilpro-v$Version")
    Set-Once $readme "(?m)^## Changes in $old(?=\r?$)" "## Changes in $Version$nl$nl- $ReleaseNotes$nl$nl## Changes in $previous"

    $smoke = Read-Text $smokeProject
    Set-Once $smoke "(?<=<PackageVersionUnderTest [^>]*>)$old(?=</PackageVersionUnderTest>)" $Version

    $versions = Read-Text $packageVersions
    $autoMapper = [regex]::Match($versions.Text, '<PackageVersion Include="AutoMapperMIT" Version="([^"]+)"')
    if(-not $autoMapper.Success) { throw "No AutoMapperMIT version in $packageVersions." }

    # Release notes file: a section for the new version above the previous one.
    $notes = Read-Text $releaseNotesFile
    $nl = $notes.NewLine
    $section = @(
        "## $Version", '',
        "Version set on $((Get-Date).ToString('yyyy-MM-dd')).", '',
        '| Item | Value |', '|---|---|',
        '| NuGet package | DXAutomap.AspNet.Data |',
        "| Package version | $Version |",
        "| Assembly/file version | $Version.0 |",
        "| Target framework | $targetFramework |",
        "| Runtime dependency | AutoMapperMIT $($autoMapper.Groups[1].Value) |",
        '| License/signing | MIT; unsigned assembly |',
        "| Source | the commit tagged ``$tag``, which the package records |",
        "| Package validation baseline | $previous |", '',
        '### Changes', '',
        "- $ReleaseNotes", '',
        '### Validation', '',
        "``release.ps1`` builds the package only from the commit tagged ``$tag``, and only if every check passes: the full suite, the mapping suite against the Release build, package validation against $previous, the archive checks and the smoke application.", ''
    ) -join $nl
    Set-Once $notes "(?m)^## $old(?=\r?$)" "$section$nl## $previous"

    foreach($file in @($project, $readme, $smoke, $notes)) { Write-Text $file }

    Write-Host "Set version $Version (was $previous). Changed:"
    Invoke-Git status --short | ForEach-Object { Write-Host "  $_" }
    Write-Host @"

Next: expand the changes in docs/civilpro/RELEASE-2.x.x.md and in the package README if needed, check with
./build/release.ps1 -SkipTests -Trial, then commit, push and tag:
  git commit -am "DXAutomap.AspNet.Data $Version"
  git push
  git tag -a $tag -m "DXAutomap.AspNet.Data $Version"
  git push origin $tag
"@
} finally {
    Pop-Location
}
