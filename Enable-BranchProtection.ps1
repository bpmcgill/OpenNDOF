# Enable-BranchProtection.ps1
#
# Protects the default branch (master) so contributors must open pull requests
# and pass CI before merging. The repository owner (an admin) can still bypass
# the rules, because enforce_admins is disabled.
#
# Usage:
#   .\Enable-BranchProtection.ps1                        # uses $env:GH_TOKEN
#   .\Enable-BranchProtection.ps1 -Token <PAT>
#   .\Enable-BranchProtection.ps1 -Repo owner/repo
#
# Token needs "Administration: write" (classic PAT) or the repository-admin
# permission on the GitHub App / fine-grained token.
#
# Requirements: .\Register-ComServer.ps1-style conventions — run from repo root.

param(
    [string]$Token = $env:GH_TOKEN,
    [string]$Repo,
    [string]$Branch = "master",
    [int]$RequiredReviewers = 1,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if (-not $Token) {
    throw "No GitHub token. Set GH_TOKEN or pass -Token (Admin:write scope)."
}

if (-not $Repo) {
    $remote = git config --get remote.origin.url
    if (-not $remote) { throw "Cannot determine repo. Pass -Repo owner/repo." }
    $Repo = ($remote -replace "^https://github.com/", "" -replace "^git@github.com:", "" -replace "\.git$", "").TrimEnd("/")
}
if ($Repo -notmatch "^[^/]+/[^/]+$") {
    throw "Invalid repo '$Repo'. Expected owner/repo."
}

$headers  = @{ Authorization = "Bearer $Token"; "X-GitHub-Api-Version" = "2022-11-28" }
$base     = "https://api.github.com"

# Verify the token and report who we are acting as.
$me = Invoke-RestMethod -Headers $headers -Uri "$base/user"
Write-Host "Authenticated as: $($me.login) (admin: $($me.site_admin))"

$payload = @{
    required_status_checks = @{
        strict   = $true
        contexts = @("build-test")
    }
    enforce_admins                 = $false  # admins (repo owner) can bypass
    required_pull_request_reviews  = @{
        required_approving_review_count = $RequiredReviewers
        dismiss_stale_reviews           = $true
        require_code_owner_reviews      = $false
    }
    restrictions                    = $null  # no branch restrictions (outside collaborators)
    allow_force_pushes              = $false
    allow_deletions                 = $false
    required_linear_history         = $false
    require_conversation_resolution = $false
    allow_auto_merge                = $false
    delete_branch_on_merge          = $true
    required_signatures             = $false
} | ConvertTo-Json -Depth 6

$uri = "$base/repos/$Repo/branches/$Branch/protection"

if ($DryRun) {
    Write-Host "[DryRun] Would PUT $uri"
    Write-Host $payload
    exit 0
}

Write-Host "Applying branch protection to $Repo/$Branch ..."
$result = Invoke-RestMethod -Method Put -Headers $headers -ContentType "application/json" -Uri $uri -Body $payload
Write-Host "Done."
Write-Host ("  enforce_admins            : {0}  (owner can bypass)" -f $result.enforce_admins)
Write-Host ("  required reviews          : {0}" -f $result.required_pull_request_reviews.required_approving_review_count)
Write-Host ("  required status checks    : {0}" -f ($result.required_status_checks.contexts -join ", "))
Write-Host ("  delete branch on merge    : {0}" -f $result.delete_branch_on_merge)