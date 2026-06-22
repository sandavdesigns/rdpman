from __future__ import annotations

from contextlib import asynccontextmanager
from typing import Annotated
from typing import Optional

from fastapi import Depends, FastAPI, Form, HTTPException, Query, Request, Response
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.config import get_settings
from app.database import get_db, init_db
from app.models import AuditEvent, ConnectionSession, Credential, System
from app.rdp import build_rdp_file
from app.schemas import (
    AuditEventRead,
    ConnectionSessionRead,
    CredentialCreate,
    CredentialRead,
    SystemCreate,
    SystemRead,
    SystemUpdate,
)
from app.security import encrypt_value, require_api_token, require_ui_user


@asynccontextmanager
async def lifespan(active_app: FastAPI):
    init_db()
    yield


app = FastAPI(title="RDP Man", version="0.1.0", lifespan=lifespan)
app.mount("/static", StaticFiles(directory="app/static"), name="static")
templates = Jinja2Templates(directory="app/templates")


def record_audit(
    db: Session,
    action: str,
    actor: str,
    system_id: int | None = None,
    credential_id: int | None = None,
    details: str | None = None,
) -> None:
    db.add(
        AuditEvent(
            actor=actor,
            action=action,
            system_id=system_id,
            credential_id=credential_id,
            details=details,
        )
    )
    db.commit()


def get_system_or_404(db: Session, system_id: int) -> System:
    system = db.get(System, system_id)
    if not system:
        raise HTTPException(status_code=404, detail="System not found")
    return system


def get_connection_or_404(db: Session, connection_id: int) -> ConnectionSession:
    connection = db.get(ConnectionSession, connection_id)
    if not connection:
        raise HTTPException(status_code=404, detail="Connection not found")
    return connection


def serialize_connection(connection: ConnectionSession) -> dict:
    return {
        "id": connection.id,
        "system_id": connection.system_id,
        "credential_id": connection.credential_id,
        "actor": connection.actor,
        "status": connection.status,
        "ticket_id": connection.ticket_id,
        "started_at": connection.started_at,
        "last_opened_at": connection.last_opened_at,
        "closed_at": connection.closed_at,
        "system_name": connection.system.name,
        "hostname": connection.system.hostname,
        "ip_address": connection.system.ip_address,
    }


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/", response_class=HTMLResponse)
def systems_page(
    request: Request,
    q: Optional[str] = None,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    query = select(System).order_by(System.name)
    systems = list(db.scalars(query))
    if q:
        needle = q.lower()
        systems = [
            system
            for system in systems
            if needle in system.name.lower()
            or needle in system.hostname.lower()
            or needle in (system.ip_address or "").lower()
            or needle in (system.notes or "").lower()
        ]
    return templates.TemplateResponse(
        "systems.html",
        {"request": request, "systems": systems, "q": q or "", "user": user},
    )


@app.post("/systems")
def create_system_form(
    name: Annotated[str, Form()],
    hostname: Annotated[str, Form()],
    ip_address: Annotated[Optional[str], Form()] = None,
    status: Annotated[str, Form()] = "active",
    notes: Annotated[Optional[str], Form()] = None,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    system = System(
        name=name,
        hostname=hostname,
        ip_address=ip_address,
        status=status,
        notes=notes,
    )
    db.add(system)
    db.commit()
    db.refresh(system)
    record_audit(db, "system.created", user, system_id=system.id)
    return RedirectResponse("/", status_code=303)


@app.get("/systems/{system_id}", response_class=HTMLResponse)
def system_detail_page(
    request: Request,
    system_id: int,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    system = get_system_or_404(db, system_id)
    credentials = list(db.scalars(select(Credential).where(Credential.system_id == system_id).order_by(Credential.label)))
    audit_events = list(
        db.scalars(
            select(AuditEvent)
            .where(AuditEvent.system_id == system_id)
            .order_by(AuditEvent.created_at.desc())
            .limit(20)
        )
    )
    return templates.TemplateResponse(
        "system_detail.html",
        {
            "request": request,
            "system": system,
            "credentials": credentials,
            "audit_events": audit_events,
            "user": user,
        },
    )


@app.post("/systems/{system_id}/credentials")
def create_credential_form(
    system_id: int,
    label: Annotated[str, Form()],
    username: Annotated[str, Form()],
    password: Annotated[str, Form()],
    domain: Annotated[Optional[str], Form()] = None,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    get_system_or_404(db, system_id)
    credential = Credential(
        system_id=system_id,
        label=label,
        username=username,
        domain=domain,
        encrypted_password=encrypt_value(password),
    )
    db.add(credential)
    db.commit()
    db.refresh(credential)
    record_audit(db, "credential.created", user, system_id=system_id, credential_id=credential.id)
    return RedirectResponse(f"/systems/{system_id}", status_code=303)


@app.get("/systems/{system_id}/rdp")
def download_rdp_from_ui(
    system_id: int,
    credential_id: Optional[int] = Query(default=None),
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    return _rdp_response(db, system_id, credential_id, user)


@app.get("/connections", response_class=HTMLResponse)
def connections_page(
    request: Request,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    active_connections = list(
        db.scalars(
            select(ConnectionSession)
            .where(ConnectionSession.status == "active")
            .order_by(ConnectionSession.last_opened_at.desc())
        )
    )
    closed_connections = list(
        db.scalars(
            select(ConnectionSession)
            .where(ConnectionSession.status == "closed")
            .order_by(ConnectionSession.closed_at.desc())
            .limit(20)
        )
    )
    return templates.TemplateResponse(
        "connections.html",
        {
            "request": request,
            "active_connections": active_connections,
            "closed_connections": closed_connections,
            "user": user,
        },
    )


@app.get("/connections/{connection_id}/rdp")
def reopen_connection_from_ui(
    connection_id: int,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    return _connection_rdp_response(db, connection_id, user)


@app.post("/connections/{connection_id}/close")
def close_connection_from_ui(
    connection_id: int,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    connection = get_connection_or_404(db, connection_id)
    connection.status = "closed"
    connection.closed_at = func.now()
    db.commit()
    record_audit(db, "connection.closed", user, system_id=connection.system_id, credential_id=connection.credential_id)
    return RedirectResponse("/connections", status_code=303)


@app.get("/audit", response_class=HTMLResponse)
def audit_page(
    request: Request,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    events = list(db.scalars(select(AuditEvent).order_by(AuditEvent.created_at.desc()).limit(100)))
    return templates.TemplateResponse("audit.html", {"request": request, "events": events, "user": user})


@app.get("/api/v1/systems", response_model=list[SystemRead])
def api_list_systems(
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return list(db.scalars(select(System).order_by(System.name)))


@app.post("/api/v1/systems", response_model=SystemRead, status_code=201)
def api_create_system(
    payload: SystemCreate,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    system = System(**payload.model_dump())
    db.add(system)
    db.commit()
    db.refresh(system)
    record_audit(db, "system.created", actor, system_id=system.id)
    return system


@app.get("/api/v1/systems/{system_id}", response_model=SystemRead)
def api_get_system(
    system_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return get_system_or_404(db, system_id)


@app.put("/api/v1/systems/{system_id}", response_model=SystemRead)
def api_update_system(
    system_id: int,
    payload: SystemUpdate,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    system = get_system_or_404(db, system_id)
    for key, value in payload.model_dump(exclude_unset=True).items():
        setattr(system, key, value)
    db.commit()
    db.refresh(system)
    record_audit(db, "system.updated", actor, system_id=system.id)
    return system


@app.delete("/api/v1/systems/{system_id}", status_code=204)
def api_delete_system(
    system_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    system = get_system_or_404(db, system_id)
    db.delete(system)
    db.commit()
    record_audit(db, "system.deleted", actor, system_id=system_id)
    return Response(status_code=204)


@app.get("/api/v1/systems/{system_id}/credentials", response_model=list[CredentialRead])
def api_list_credentials(
    system_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    get_system_or_404(db, system_id)
    return list(db.scalars(select(Credential).where(Credential.system_id == system_id).order_by(Credential.label)))


@app.post("/api/v1/systems/{system_id}/credentials", response_model=CredentialRead, status_code=201)
def api_create_credential(
    system_id: int,
    payload: CredentialCreate,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    get_system_or_404(db, system_id)
    credential = Credential(
        system_id=system_id,
        label=payload.label,
        username=payload.username,
        domain=payload.domain,
        is_active=payload.is_active,
        encrypted_password=encrypt_value(payload.password),
    )
    db.add(credential)
    db.commit()
    db.refresh(credential)
    record_audit(db, "credential.created", actor, system_id=system_id, credential_id=credential.id)
    return credential


@app.get("/api/v1/systems/{system_id}/rdp")
def api_download_rdp(
    system_id: int,
    credential_id: Optional[int] = Query(default=None),
    ticket_id: Optional[str] = Query(default=None),
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return _rdp_response(db, system_id, credential_id, actor, ticket_id=ticket_id)


@app.get("/api/v1/audit-events", response_model=list[AuditEventRead])
def api_audit_events(
    limit: int = Query(default=100, ge=1, le=500),
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return list(db.scalars(select(AuditEvent).order_by(AuditEvent.created_at.desc()).limit(limit)))


@app.get("/api/v1/connections", response_model=list[ConnectionSessionRead])
def api_list_connections(
    status: str = Query(default="active"),
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    query = select(ConnectionSession).order_by(ConnectionSession.last_opened_at.desc())
    if status != "all":
        query = query.where(ConnectionSession.status == status)
    return [serialize_connection(connection) for connection in db.scalars(query)]


@app.get("/api/v1/connections/{connection_id}", response_model=ConnectionSessionRead)
def api_get_connection(
    connection_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return serialize_connection(get_connection_or_404(db, connection_id))


@app.get("/api/v1/connections/{connection_id}/rdp")
def api_reopen_connection(
    connection_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    return _connection_rdp_response(db, connection_id, actor)


@app.post("/api/v1/connections/{connection_id}/close", response_model=ConnectionSessionRead)
def api_close_connection(
    connection_id: int,
    db: Session = Depends(get_db),
    actor: str = Depends(require_api_token),
):
    connection = get_connection_or_404(db, connection_id)
    connection.status = "closed"
    connection.closed_at = func.now()
    db.commit()
    db.refresh(connection)
    record_audit(db, "connection.closed", actor, system_id=connection.system_id, credential_id=connection.credential_id)
    return serialize_connection(connection)


def _rdp_response(
    db: Session,
    system_id: int,
    credential_id: Optional[int],
    actor: str,
    ticket_id: Optional[str] = None,
) -> Response:
    system = get_system_or_404(db, system_id)
    credential = None
    if credential_id:
        credential = db.get(Credential, credential_id)
        if not credential or credential.system_id != system.id:
            raise HTTPException(status_code=404, detail="Credential not found")
    connection = ConnectionSession(
        system_id=system.id,
        credential_id=credential_id,
        actor=actor,
        ticket_id=ticket_id,
    )
    db.add(connection)
    db.commit()
    db.refresh(connection)
    details = f"connection_id={connection.id}"
    if ticket_id:
        details = f"{details}; ticket_id={ticket_id}"
    record_audit(db, "connection.started", actor, system_id=system.id, credential_id=credential_id, details=details)
    filename = f"{system.name.replace(' ', '_')}.rdp"
    return Response(
        content=build_rdp_file(system, credential),
        media_type="application/x-rdp",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


def _connection_rdp_response(db: Session, connection_id: int, actor: str) -> Response:
    connection = get_connection_or_404(db, connection_id)
    credential = None
    if connection.credential_id:
        credential = db.get(Credential, connection.credential_id)
    connection.status = "active"
    connection.last_opened_at = func.now()
    connection.closed_at = None
    db.commit()
    record_audit(
        db,
        "connection.reopened",
        actor,
        system_id=connection.system_id,
        credential_id=connection.credential_id,
        details=f"connection_id={connection.id}",
    )
    filename = f"{connection.system.name.replace(' ', '_')}.rdp"
    return Response(
        content=build_rdp_file(connection.system, credential),
        media_type="application/x-rdp",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )
