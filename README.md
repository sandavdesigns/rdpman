# RDP Man

RDP Man is a small, container-ready systems and RDP management module for an existing ticket system. It manages servers, credentials, audit events, and downloadable `.rdp` connection files through a web UI and REST API.

## Features

- Server/system inventory
- Encrypted credential storage
- Active connection overview
- RDP file download per system
- Audit log for connection and credential actions
- REST API for ticket-system integration
- Basic-auth protected web UI
- Token-protected API
- SQLite by default, suitable for a first Portainer deployment

## Quick Start

```bash
cp .env.example .env
docker compose up --build
```

Open `http://localhost:8095`.

Default credentials from `.env.example`:

- UI user: `admin`
- UI password: `change-me`
- API token: `change-me-api-token`

Change these values before using the service anywhere real.

## Portainer

Use `portainer-stack.yml` as a Git stack in Portainer.

Repository URL:

```text
https://github.com/sandavdesigns/rdpman.git
```

Compose path:

```text
portainer-stack.yml
```

Set these Portainer environment variables:

```text
RDP_MAN_SECRET_KEY=replace-with-a-long-random-secret
RDP_MAN_ADMIN_USERNAME=admin
RDP_MAN_ADMIN_PASSWORD=replace-with-a-strong-password
RDP_MAN_API_TOKEN=replace-with-a-long-random-api-token
```

More details are in `PORTAINER.md`.

## API

Send the API token as `Authorization: Bearer <token>` or `X-API-Token: <token>`.

Useful endpoints:

- `GET /api/v1/systems`
- `POST /api/v1/systems`
- `GET /api/v1/systems/{system_id}`
- `PUT /api/v1/systems/{system_id}`
- `DELETE /api/v1/systems/{system_id}`
- `GET /api/v1/systems/{system_id}/rdp`
- `GET /api/v1/systems/{system_id}/credentials`
- `POST /api/v1/systems/{system_id}/credentials`
- `GET /api/v1/connections`
- `GET /api/v1/connections/{connection_id}/rdp`
- `POST /api/v1/connections/{connection_id}/close`
- `GET /api/v1/audit-events`

## Ticket-System Integration

The ticket system should treat RDP Man as a separate module service:

1. Store the RDP Man base URL and API token in the ticket-system configuration.
2. Link tickets to RDP Man systems by `external_ref`, hostname, or system id.
3. Show a "Connect RDP" action in tickets that points to `/api/v1/systems/{id}/rdp`.
4. Optionally write ticket ids into RDP Man audit metadata when launching a session.
5. Show active sessions from `/api/v1/connections` when the ticket system should display a connection switcher.

## Security Notes

- Passwords are encrypted at rest with Fernet.
- `RDP_MAN_SECRET_KEY` must be stable. Changing it makes existing encrypted passwords unreadable.
- The `.rdp` download intentionally does not embed passwords.
- Active connections are tracked by RDP Man when a connection file is generated or reopened.
- Without a Windows agent, RDP Man cannot automatically detect when a local `mstsc.exe` window was closed.
- Put this service behind HTTPS and your normal access controls.
- For production, consider replacing stored passwords with an integration to Bitwarden, KeePass, Vault, Windows Credential Manager, or a dedicated Windows agent.
- Browser-based apps cannot reliably start `mstsc.exe` directly. A Windows agent is the recommended next step for one-click RDP launches.

## Development

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements-dev.txt
uvicorn app.main:app --reload
pytest
```
