function Convert-PitoNoteRainmeterConfig {
    param([Parameter(Mandatory)][string]$Text)
    $lines = [Collections.Generic.List[string]]::new()
    $skip = $false
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line -match '^\[([^\]]+)\]\s*$') {
            $name = $Matches[1]
            $skip = $name -match '^NotesPlus-Studio\\(?:Board|Launcher)(?:\\|$)'
            if ($name -match '^NotesPlus-Studio(?:\\|$)' -and -not $skip) {
                $line = '[' + ($name -replace '^NotesPlus-Studio', 'PitoNote') + ']'
            }
        }
        if (-not $skip) { $lines.Add($line) }
    }
    return ($lines -join "`r`n")
}

function Enable-PitoNoteToolbar {
    param([Parameter(Mandatory)][string]$Text)
    $pattern='(?ms)^(\[PitoNote\\Control\]\r?\n)(.*?)(?=^\[|\z)'
    if($Text -match $pattern){
        return [regex]::Replace($Text,$pattern,{
            param($match)
            $body=$match.Groups[2].Value
            if($body -match '(?m)^Active=\d+\r?$'){$body=[regex]::Replace($body,'(?m)^Active=\d+(?=\r?$)','Active=1')}
            else{$body="Active=1`r`n"+$body}
            return $match.Groups[1].Value+$body
        })
    }
    return $Text.TrimEnd()+"`r`n`r`n[PitoNote\Control]`r`nActive=1`r`n"
}
