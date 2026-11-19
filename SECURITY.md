# Security Policy

## Supported Versions
Only the latest major release (`v3.0.x`) receives security updates and vulnerability patches.

## Telemetry and Privacy
BeamModManager Ultimate collects **zero telemetry, analytics, or crash reports**.
Outbound network connections occur strictly on explicit user actions:
- Checking for program releases (`api.github.com/repos/Lancer10Evo/BeamNGModManager/releases/latest`)
- Querying VirusTotal API v3 by SHA-256 hash (`www.virustotal.com/api/v3/files/{sha256}`) when an API key is explicitly configured by the user (stored encrypted via Windows DPAPI)
- Browsing whitelisted mod portals inside the Mod Hub (`beamng.com`, `modland.net`, `playground.ru`, `nexusmods.com`)

## Reporting a Vulnerability
If you discover a security vulnerability (archive parser bypass, lexer bypass, path traversal, or privilege issue), please open a private security advisory or report it via GitHub Issues:
https://github.com/Lancer10Evo/BeamNGModManager/issues