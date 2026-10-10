# Security

Report suspected vulnerabilities through this repository's private vulnerability reporting feature when available. Do not include credentials, player data or working exploits in a public issue. If private reporting is unavailable, open an issue asking for a private contact channel without disclosing the vulnerability.

## Hosting boundaries

Use HTTPS, a trusted reverse proxy and a dedicated unprivileged service account. Keep SQLite data, Data Protection keys, content packs, backups and environment files outside the public web root. Configure the public URL, allowed hosts and trusted proxy explicitly for your deployment.

Mail credentials belong in a private environment or secret store. The repository includes no deployed accounts, passwords or databases. Examples and screenshot explorers are fictional.

Back up the database together with its matching Data Protection keys and content packs. Test restoration. Review runtime and dependency updates regularly; self-contained .NET deployments need a rebuilt release to receive runtime updates.

The application uses server-side authorization, antiforgery checks, revision checks, password hashing and scoped session cookies. These controls do not replace secure host configuration or regular maintenance. See the [deployment guide](docs/Deployment.md).
