[Reading 25 lines from start (total: 25 lines, 0 remaining)]

# Contributing

Contributions are welcome if they preserve the project's narrow support/monitoring scope and the SignPath Foundation OSS conditions.

## Requirements

- All submitted code must be compatible with the Apache-2.0 license.
- Do not add proprietary components or binaries.
- Do not add features for exploitation, security bypass, stealth, credential extraction or generic remote shells.
- Do not add telemetry/data collection without updating the privacy policy and installer disclosure.
- Do not commit secrets, credentials, certificates, private keys or real enrollment codes.

## Development security

Maintainers/committers/reviewers/approvers must use multi-factor authentication for source repository and SignPath access.

External pull requests require review by a project maintainer before merge.

Build scripts, CI workflows and signing configuration are security-sensitive code and require the same review as application code.

## Build

Use the SDK version pinned in `global.json` and run `./build/Build-Release.ps1` on Windows.

Pull requests must pass the GitHub Actions build and secret-pattern checks.
