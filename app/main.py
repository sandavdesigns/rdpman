from __future__ import annotations

from contextlib import asynccontextmanager
from typing import Annotated
from typing import Optional

from fastapi import Depends, FastAPI, Form, HTTPException, Query, Request, Response
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import get_settings
from app.database import get_db, init_db
from app.models import AuditEvent, Credential, System
from app.rdp import build_rdp_file
from app.schemas import AuditEventRead, CredentialCreate, CredentialRead, SystemCreate, SystemRead, SystemUpdate
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
            or needle in (system.customer or "").lower()
            or needle in (system.ip_address or "").lower()
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
    customer: Annotated[Optional[str], Form()] = None,
    location: Annotated[Optional[str], Form()] = None,
    environment: Annotated[Optional[str], Form()] = None,
    status: Annotated[str, Form()] = "active",
    notes: Annotated[Optional[str], Form()] = None,
    db: Session = Depends(get_db),
    user: str = Depends(require_ui_user),
):
    system = System(
        name=name,
        hostname=hostname,
        ip_address=ip_address,
        customer=customer,
        location=location,
        environment=environment,
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
    details = f"ticket_id={ticket_id}" if ticket_id else None
    record_audit(db, "rdp.downloaded", actor, system_id=system.id, credential_id=credential_id, details=details)
    filename = f"{system.name.replace(' ', '_')}.rdp"
    return Response(
        content=build_rdp_file(system, credential),
        media_type="application/x-rdp",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )
