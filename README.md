# TSD Support Agent

TSD Support Agent is a lightweight open-source Windows service for authorized endpoint health monitoring and inventory collection.

It is designed for managed support environments where the person installing or operating the software has permission to monitor the device.

## Scope

The open-source repository contains only:

- the Windows endpoint Agent;
- the Windows installer/updater/uninstaller;
- build and CI definitions;
- privacy, security and code-signing documentation.

The TSD Central server, database, tenant data, credentials, private signing keys and business logic are **not** part of this repository.

## Current status

Version `0.4.0` is an open-source candidate.

Public code signing is not enabled yet. Release binaries must not be represented as trusted/signed until the SignPath Foundation onboarding and signing gates are complete.

## What the Agent does

When communication is enabled by the installer, the Agent can send the configured Central:

- computer name;
- manufacturer, model and serial number;
- CPU model, core and logical processor counts;
- Windows and Agent versions;
- CPU, memory and disk health metrics;
- uptime;
- aggregated antivirus health;
- Windows Firewall profile state;
- System/Application error counts;
- installed software name, version, publisher and architecture;
- the device public key used to authenticate signed requests.

Communication uses HTTPS and signed device requests.

## What the Agent does not collect

The Agent does not collect or transmit:

- file or document contents;
- passwords, cookies or credentials;
- keystrokes;
- screenshots;
- camera or microphone data;
- browser history;
- e-mail or message contents;
- geolocation.

The Agent does not provide a generic remote shell, vulnerability scanner, exploit framework or security-bypass feature.

## Network control

The installer includes an explicit option: **Enable communication with TSD Central (HTTPS)**.

If this option is disabled on a new installation:

- the Agent does not enroll;
- no device identity key is created for network authentication;
- no health or inventory data is sent to the Central;
- the Windows service remains installed but idle.

On an existing enrolled installation, disabling communication stops network transfer while preserving the local device identity for a possible later re-enable.

The option can be changed later by re-running the installer.

Default Central: `https://agent.toservicedesk.com.br`

See [Privacy Policy](docs/PRIVACY.md).

## Security model

- Windows service runs as `LocalSystem` because system health and protected inventory need elevated access.
- Device authentication uses an ECDSA P-256 machine key.
- TPM-backed `Microsoft Platform Crypto Provider` is preferred when available.
- Software KSP is the compatibility fallback.
- Private device keys are non-exportable.
- Network requests are signed by the device key.
- Enrollment codes are temporary and removed after the first authenticated sync.
- Agent releases have an independent TSD release-signing trust chain.
- Release updates remain disabled until all signing and validation gates pass.
- No raw shell or arbitrary PowerShell execution exists in this OSS Agent.

See [Security Policy](SECURITY.md).

## Installation

Run the Setup executable as administrator.

On a new device:

1. Review the privacy policy shown by the installer.
2. Choose whether Central communication is enabled.
3. If communication is enabled, enter the temporary installation code.
4. Click **Install**.

On an existing installation, Setup automatically switches to **Update** and preserves the existing device identity.

## Uninstallation

Re-run Setup and choose **Uninstall**.

Uninstallation removes:

- Windows service registration;
- Agent executable;
- local Agent state/configuration/logs;
- the local device cryptographic key.

Already-received server-side records are not automatically deleted by local uninstall; retention is controlled by the Central operator.

## Building

Requirements:

- .NET SDK `10.0.400` or compatible latest patch;
- Windows x64 target;
- PowerShell for the release build helper.

Build the Agent with `dotnet publish src/TsdSupportAgent/TsdSupportAgent.csproj -c Release -o src/TsdSupportAgent/publish`.

Generate embedded Agent metadata and build Setup with `./build/Build-Release.ps1`.

The CI workflow performs the same process on GitHub-hosted Windows runners.

## Code signing policy

**Free code signing provided by SignPath.io, certificate by SignPath Foundation** — once the project has been accepted and signing is enabled.

Every signed release must originate from the public source repository and trusted CI build. Signing requires manual approval.

See [Code signing policy](docs/CODE_SIGNING_POLICY.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Apache License 2.0. See [LICENSE](LICENSE).

All components included by this repository must remain compatible with the SignPath Foundation OSS conditions. Proprietary binaries must not be added to signed packages.