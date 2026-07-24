param(
    [string]$SourcePath,
    [string]$IconDirectory,
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $repositoryRoot 'ModifierIconSource.cs'
}

if ([string]::IsNullOrWhiteSpace($IconDirectory)) {
    $IconDirectory = Join-Path $repositoryRoot 'docs\modifier-icons'
}

$SourcePath = [System.IO.Path]::GetFullPath($SourcePath)
$IconDirectory = [System.IO.Path]::GetFullPath($IconDirectory)
Add-Type -AssemblyName System.Drawing

$iconPattern = [regex]::new(
    'Icon\("(?<key>[^"]+)", P\((?<palette>C\(\d+, \d+, \d+\)(?:, C\(\d+, \d+, \d+\)){0,2})\), "(?<payload>[A-Za-z0-9+/=]+)"\)')
$colorPattern = [regex]::new('C\((?<red>\d+), (?<green>\d+), (?<blue>\d+)\)')
$source = [System.IO.File]::ReadAllText($SourcePath)
$matches = $iconPattern.Matches($source)
if ($matches.Count -eq 0) {
    throw "No modifier icon entries were found in '$SourcePath'."
}

$staleKeys = [System.Collections.Generic.List[string]]::new()
$updated = $iconPattern.Replace(
    $source,
    [System.Text.RegularExpressions.MatchEvaluator]{
        param([System.Text.RegularExpressions.Match]$match)

        $key = $match.Groups['key'].Value
        $palette = [System.Collections.Generic.Dictionary[string, byte]]::new(
            [System.StringComparer]::Ordinal)
        $tone = 1
        foreach ($colorMatch in $colorPattern.Matches($match.Groups['palette'].Value)) {
            $colorKey = '{0},{1},{2}' -f `
                $colorMatch.Groups['red'].Value, `
                $colorMatch.Groups['green'].Value, `
                $colorMatch.Groups['blue'].Value
            if ($palette.ContainsKey($colorKey)) {
                throw "Icon '$key' has a duplicate palette color ($colorKey)."
            }

            $palette.Add($colorKey, [byte]$tone)
            $tone++
        }

        $iconPath = Join-Path $IconDirectory ($key + '.png')
        if (-not [System.IO.File]::Exists($iconPath)) {
            throw "Missing modifier icon '$iconPath'."
        }

        $bitmap = [System.Drawing.Bitmap]::new($iconPath)
        try {
            if ($bitmap.Width -ne 64 -or $bitmap.Height -ne 64) {
                throw "Icon '$key' must be exactly 64x64 pixels."
            }

            $tones = [System.Collections.Generic.List[byte]]::new(4096)
            for ($y = 0; $y -lt 64; $y++) {
                for ($x = 0; $x -lt 64; $x++) {
                    $color = $bitmap.GetPixel($x, 63 - $y)
                    if ($color.A -eq 0) {
                        [void]$tones.Add(0)
                        continue
                    }

                    if ($color.A -ne 255) {
                        throw "Icon '$key' contains partial alpha at ($x,$(63 - $y))."
                    }

                    $colorKey = '{0},{1},{2}' -f $color.R, $color.G, $color.B
                    [byte]$pixelTone = 0
                    if (-not $palette.TryGetValue($colorKey, [ref]$pixelTone)) {
                        throw "Icon '$key' uses color ($colorKey) outside its source palette."
                    }

                    [void]$tones.Add($pixelTone)
                }
            }
        }
        finally {
            $bitmap.Dispose()
        }

        $runs = [System.Collections.Generic.List[byte]]::new()
        for ($index = 0; $index -lt $tones.Count;) {
            [byte]$pixelTone = $tones[$index]
            $count = 1
            while (
                $index + $count -lt $tones.Count -and
                $tones[$index + $count] -eq $pixelTone -and
                $count -lt 64
            ) {
                $count++
            }

            [void]$runs.Add(
                [byte](($pixelTone -shl 6) -bor ($count - 1)))
            $index += $count
        }

        $payload = [System.Convert]::ToBase64String($runs.ToArray())
        if ($payload -ne $match.Groups['payload'].Value) {
            [void]$staleKeys.Add($key)
        }

        $relativeStart = $match.Groups['payload'].Index - $match.Index
        return $match.Value.Substring(0, $relativeStart) +
            $payload +
            $match.Value.Substring(
                $relativeStart + $match.Groups['payload'].Length)
    })

if ($Check) {
    if ($staleKeys.Count -gt 0) {
        throw "Stale modifier icon tone maps: $($staleKeys -join ', ')."
    }

    Write-Output "Modifier icon tone maps are current ($($matches.Count) icons)."
    exit 0
}

if ($staleKeys.Count -eq 0) {
    Write-Output "Modifier icon tone maps are already current ($($matches.Count) icons)."
    exit 0
}

[System.IO.File]::WriteAllText(
    $SourcePath,
    $updated,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "Updated modifier icon tone maps: $($staleKeys -join ', ')."
