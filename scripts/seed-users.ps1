#!/usr/bin/env pwsh
# Seeds 3 users (2 user + 1 admin) into GoTrue via the Admin API (T1.2.5).
#
# Public signup is closed, so users are created admin-side. The app role is set
# in app_metadata.role (server-controlled), which the .NET API trusts (§11.1).
# Idempotent: existing users are skipped.
#
# Prereq: the stack is up (docker compose up) so GoTrue is reachable.
# Usage:
#   ./scripts/seed-users.ps1
#   ./scripts/seed-users.ps1 -GoTrueUrl http://localhost:9999 -JwtSecret <secret>

param(
    [string]$GoTrueUrl = $(if ($env:GOTRUE_EXTERNAL_URL) { $env:GOTRUE_EXTERNAL_URL } else { "http://localhost:9999" }),
    [string]$JwtSecret = $(if ($env:SUPABASE_JWT_SECRET) { $env:SUPABASE_JWT_SECRET } else { "super-secret-jwt-token-with-at-least-32-characters-long" }),
    [string]$Password  = "Passw0rd!"
)

$ErrorActionPreference = "Stop"

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Build a short-lived service_role JWT (HS256) accepted by GoTrue admin endpoints.
function New-ServiceRoleToken([string]$secret) {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $header  = '{"alg":"HS256","typ":"JWT"}'
    $payload = @{ role = "service_role"; iss = "seed-script"; iat = $now; exp = $now + 600 } | ConvertTo-Json -Compress

    $enc = [System.Text.Encoding]::UTF8
    $headerB64  = ConvertTo-Base64Url $enc.GetBytes($header)
    $payloadB64 = ConvertTo-Base64Url $enc.GetBytes($payload)
    $signingInput = "$headerB64.$payloadB64"

    $hmac = [System.Security.Cryptography.HMACSHA256]::new($enc.GetBytes($secret))
    try {
        $sig = $hmac.ComputeHash($enc.GetBytes($signingInput))
    } finally {
        $hmac.Dispose()
    }
    "$signingInput.$(ConvertTo-Base64Url $sig)"
}

$token = New-ServiceRoleToken $JwtSecret
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
# Standalone GoTrue serves its API at the root (no /auth/v1 prefix — that is
# added by the Supabase API gateway, which we do not run).
$endpoint = "$($GoTrueUrl.TrimEnd('/'))/admin/users"

$users = @(
    @{ email = "user1@finance.local"; role = "user" },
    @{ email = "user2@finance.local"; role = "user" },
    @{ email = "admin@finance.local"; role = "admin" }
)

foreach ($u in $users) {
    $body = @{
        email         = $u.email
        password      = $Password
        email_confirm = $true
        app_metadata  = @{ role = $u.role }
    } | ConvertTo-Json -Compress

    try {
        $resp = Invoke-RestMethod -Uri $endpoint -Method Post -Headers $headers -Body $body
        Write-Host "Created $($u.email) (role=$($u.role), id=$($resp.id))"
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -in 409, 422) {
            Write-Host "Skipped $($u.email) — already exists"
        }
        else {
            Write-Warning "Failed to create $($u.email): HTTP $status"
            throw
        }
    }
}

Write-Host "Done. Default password for all seeded users: $Password"
