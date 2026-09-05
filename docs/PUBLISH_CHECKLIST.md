[Reading 45 lines from start (total: 45 lines, 0 remaining)]

# Public Repository / SignPath Checklist

## Completed locally

- Apache-2.0 license.
- Agent and Setup source isolated from Central/private server code.
- Privacy policy.
- Installer disclosure and communication-disable option.
- Uninstall capability.
- Security policy.
- Code signing policy.
- GitHub-hosted CI build.
- Two-stage SignPath workflow template.
- No private key/token/password detected in source-tree scan.

## Must be completed before public push

- Replace `OWNER` in project RepositoryUrl values with the final GitHub owner.
- Confirm public repository name/URL.
- Add final GitHub maintainer/team links to `docs/CODE_SIGNING_POLICY.md`.
- Add CODEOWNERS after the GitHub username/team is known.
- Enable MFA on the GitHub account.
- Run final secret scan on the exact Git commit.

## Must be completed before SignPath application

- Publish repository publicly.
- Create a real GitHub release from public source.
- Publish/download documentation for the released unsigned candidate.
- Confirm repository is actively maintained.
- Confirm all packaged components remain OSS.
- Submit SignPath Foundation application.

## After acceptance

- Install SignPath GitHub App.
- Configure SignPath organization/project/policies/artifact configurations.
- Add `SIGNPATH_API_TOKEN` as GitHub Actions secret.
- Add the required SignPath IDs/slugs as repository variables.
- Set `SIGNPATH_ENABLED=true` only after configuration is complete.
- Require manual approval for every release signing request.

## Production gate

No customer-wide distribution or automatic update until signed Agent and signed Setup pass Windows, Defender and upgrade-LAB validation.
