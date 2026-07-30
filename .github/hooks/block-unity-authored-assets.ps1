# PreToolUse guard: blocks agent writes to Unity-authored .meta and .asset files under Assets/.
# Reads the hook payload from stdin and returns a deny decision when a write-style
# tool targets a path Unity owns. Any parsing problem falls through to "allow" so a
# broken guard never blocks normal work.

$ErrorActionPreference = 'Stop'

$writeToolPattern = '(?i)(create_file|replace_string|insert_edit|apply_patch|edit_notebook|write_file)'
$blockedPathPattern = '(?i)(^|[\\/])Assets[\\/].*\.(meta|asset)$'

function Approve {
    exit 0
}

try {
    $raw = [Console]::In.ReadToEnd()
}
catch {
    Approve
}

if ([string]::IsNullOrWhiteSpace($raw)) {
    Approve
}

try {
    $payload = $raw | ConvertFrom-Json
}
catch {
    Approve
}

$toolName = [string]$payload.tool_name
if ([string]::IsNullOrWhiteSpace($toolName) -or $toolName -notmatch $writeToolPattern) {
    Approve
}

$toolInput = $payload.tool_input
if ($null -eq $toolInput) {
    Approve
}

$targets = [System.Collections.Generic.List[string]]::new()

foreach ($name in @('filePath', 'path', 'file_path', 'uri')) {
    $property = $toolInput.PSObject.Properties[$name]
    if ($property -and $property.Value -is [string]) {
        $targets.Add($property.Value)
    }
}

$replacements = $toolInput.PSObject.Properties['replacements']
if ($replacements -and $replacements.Value) {
    foreach ($replacement in $replacements.Value) {
        if ($replacement.filePath -is [string]) {
            $targets.Add($replacement.filePath)
        }
    }
}

$blocked = @($targets | Where-Object { $_ -match $blockedPathPattern } | Select-Object -Unique)
if ($blocked.Count -eq 0) {
    Approve
}

$reason = "Blocked by repository policy: $($blocked -join ', '). Unity generates and owns .meta and .asset files under Assets/. Writing them from an agent corrupts GUIDs and serialized references. Provide step-by-step Unity Editor instructions (menu path, fields, values) so the user creates or edits the asset in-editor instead."

$decision = @{
    hookSpecificOutput = @{
        hookEventName            = 'PreToolUse'
        permissionDecision       = 'deny'
        permissionDecisionReason = $reason
    }
}

$decision | ConvertTo-Json -Depth 5 -Compress
exit 0
