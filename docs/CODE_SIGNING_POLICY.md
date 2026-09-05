# Code signing policy

**Free code signing provided by SignPath.io, certificate by SignPath Foundation** — after project acceptance and signing activation.

## Source and build origin

Only artifacts built from this public repository may be submitted for signing.

Release-signing workflows must run on GitHub-hosted runners and use the SignPath GitHub trusted build integration/origin verification.

Signed artifacts must be reproducible from the source revision and build scripts recorded by the CI workflow. The source repository, CI workflow and build helper are part of the reviewed source.

## Project roles

Public repository: https://github.com/frmsgyn-rgb/tsd-support-agent

- Owner / maintainer: [@frmsgyn-rgb](https://github.com/frmsgyn-rgb)
- Author / committer: [@frmsgyn-rgb](https://github.com/frmsgyn-rgb)
- Reviewer: [@frmsgyn-rgb](https://github.com/frmsgyn-rgb)
- SignPath approver candidate: [@frmsgyn-rgb](https://github.com/frmsgyn-rgb)

External pull requests require review by the repository maintainer before merge. Signing approval is a separate manual action in SignPath and must never be inferred from a Git merge alone.

## Signing approval

Every production signing request requires manual approval. Automatic signing of unreviewed commits is not permitted.

## Artifact scope

The project signs only artifacts built from source maintained in this repository:

- `TsdSupportAgent.exe`
- `TSD-Support-Setup.exe`

Proprietary code or proprietary binaries must not be added to signed packages.

## Security restrictions

The signed project does not provide a generic remote shell, vulnerability scanner, exploit framework, credential extraction, security bypass or stealth persistence.

System changes are announced by the installer. Network communication is optional and can be disabled. An uninstaller is provided.

## Release sequence

1. Build Agent on GitHub-hosted runner.
2. Submit Agent for SignPath signing with manual approval.
3. Download the signed Agent.
4. Generate its SHA-256 metadata.
5. Embed that exact signed Agent into Setup.
6. Build Setup on GitHub-hosted runner.
7. Submit Setup for SignPath signing with manual approval.
8. Publish only signed artifacts that pass Defender and Windows validation.

## Key handling

No code-signing private key is stored in this repository, CI variables or endpoint. SignPath Foundation/SignPath.io manages the signing certificate key in its signing infrastructure.
