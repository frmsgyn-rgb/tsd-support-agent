# Security Policy

## Supported version

Security fixes are applied to the current development branch and the latest supported release.

## Reporting a vulnerability

Do not publish sensitive vulnerability details in a public issue.

Until a dedicated security contact is configured, use GitHub's private vulnerability reporting feature after the repository is published.

## Design boundaries

TSD Support Agent intentionally avoids:

- arbitrary command execution;
- generic remote shells;
- arbitrary PowerShell execution;
- vulnerability scanning/exploitation;
- credential collection;
- screenshot/input capture;
- hidden or deceptive persistence;
- disabling UAC, Defender, SmartScreen or Windows Firewall.

## Privilege

The Windows service runs as LocalSystem to read protected system health/inventory information. Because this increases impact if compromised, the network/API surface is intentionally narrow and typed.

## Device identity

Endpoint authentication uses a non-exportable ECDSA P-256 machine key. TPM-backed storage is preferred. Requests include timestamp, nonce, body hash and signature to mitigate tampering/replay.

## Update trust

Agent update functionality is not enabled until release-signing, origin-verification and Windows validation gates are complete. A release manifest is verified against a pinned TSD release public key in addition to public Authenticode signing.

## Logs

Secrets, enrollment codes and private key material must never be written to logs.
