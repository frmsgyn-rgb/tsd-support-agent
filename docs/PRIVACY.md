[Reading 55 lines from start (total: 55 lines, 0 remaining)]

# Privacy Policy

Policy version: `1.0`

## Purpose

TSD Support Agent provides authorized endpoint health monitoring and inventory reporting to a Central selected by the person installing or operating the software.

## Data transmitted when communication is enabled

- computer name;
- manufacturer, model and serial number;
- processor model, core count and logical processor count;
- Windows version/build and Agent version;
- CPU, memory, disk and uptime metrics;
- Agent process working-set metric;
- aggregated antivirus health reported by Windows;
- Windows Firewall profile state;
- counts of error-level System and Application events;
- installed software name, version, publisher and architecture;
- the device public key and protocol/capability metadata required to authenticate the endpoint.

Default destination: `https://agent.toservicedesk.com.br`.

## Data not collected

The Agent does not collect file contents, passwords, cookies, browser history, keystrokes, screenshots, camera/microphone data, e-mail/message contents or geolocation.

## Disable network transfer

The installer includes an option to disable Central communication. On a new installation with communication disabled, the service does not enroll, does not create a network-authentication identity key and does not transmit health or inventory data.

On an already-enrolled installation, disabling communication stops all network transfer but may preserve the existing local device identity so communication can later be re-enabled without creating a duplicate device registration.

Re-running Setup can enable or disable communication later.

## Local storage

Local Agent state is stored under `%ProgramData%\TSD\SupportAgent` with restricted Windows ACLs. Logs are readable to local users only where explicitly configured by Setup; identity and enrollment state remain restricted to SYSTEM/Administrators.

## Cryptographic identity

When communication is enabled, the Agent creates a non-exportable ECDSA P-256 machine key. TPM-backed storage is preferred when available, with Windows Software KSP as compatibility fallback.

## Retention

Uninstallation stops future transmissions and removes local state/key material. Records already received by the configured Central can remain according to the retention policy of the Central operator.

## Third-party components

The application uses .NET runtime/framework components and Microsoft open-source .NET packages. These components do not independently receive endpoint telemetry from this Agent.

## Operator responsibility

The person or organization deploying the Agent is responsible for having authorization to monitor the endpoint and for providing any additional privacy notices required by local law, contract or workplace policy.
