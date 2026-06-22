# RDP Man

RDP Man is a small, container-ready systems and RDP management module for an existing ticket system. It manages servers, credentials, audit events, and downloadable `.rdp` connection files through a web UI and REST API.

## Features

- Server/system inventory
- Encrypted credential storage
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

Use the included `docker-compose.yml` as a Portainer stack. Persist `/app/data` with a volume.

```yaml
services:
  rdpman:
    image: rdpman:latest
    ports:
      - "8095:8000"
    volumes:
      - rdpman_data:/app/data
    environment:
      RDP_MAN_SECRET_KEY: "replace-with-a-long-random-value"
      RDP_MAN_ADMIN_USERNAME: "admin"
      RDP_MAN_ADMIN_PASSWORD: "replace-me"
      RDP_MAN_API_TOKEN: "replace-me-too"
volumes:
  rdpman_data:
```

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
- `GET /api/v1/audit-events`

## Ticket-System Integration

The ticket system should treat RDP Man as a separate module service:

1. Store the RDP Man base URL and API token in the ticket-system configuration.
2. Link tickets to RDP Man systems by `external_ref`, hostname, customer, or system id.
3. Show a "Connect RDP" action in tickets that points to `/api/v1/systems/{id}/rdp`.
4. Optionally write ticket ids into RDP Man audit metadata when launching a session.

## Security Notes

- Passwords are encrypted at rest with Fernet.
- `RDP_MAN_SECRET_KEY` must be stable. Changing it makes existing encrypted passwords unreadable.
- The `.rdp` download intentionally does not embed passwords.
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

