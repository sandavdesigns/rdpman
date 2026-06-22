from __future__ import annotations

import base64
import hashlib
import secrets
from typing import Optional

from cryptography.fernet import Fernet
from fastapi import Depends, Header, HTTPException, Request, status
from fastapi.security import HTTPBasic, HTTPBasicCredentials, HTTPBearer

from app.config import Settings, get_settings

basic_security = HTTPBasic()
bearer_security = HTTPBearer(auto_error=False)


def _fernet_key(secret_key: str) -> bytes:
    digest = hashlib.sha256(secret_key.encode("utf-8")).digest()
    return base64.urlsafe_b64encode(digest)


def encrypt_value(value: str, settings: Settings | None = None) -> str:
    active_settings = settings or get_settings()
    return Fernet(_fernet_key(active_settings.secret_key)).encrypt(value.encode("utf-8")).decode("utf-8")


def decrypt_value(value: str, settings: Settings | None = None) -> str:
    active_settings = settings or get_settings()
    return Fernet(_fernet_key(active_settings.secret_key)).decrypt(value.encode("utf-8")).decode("utf-8")


def require_ui_user(
    credentials: HTTPBasicCredentials = Depends(basic_security),
    settings: Settings = Depends(get_settings),
) -> str:
    username_ok = secrets.compare_digest(credentials.username, settings.admin_username)
    password_ok = secrets.compare_digest(credentials.password, settings.admin_password)
    if not (username_ok and password_ok):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid authentication credentials",
            headers={"WWW-Authenticate": "Basic"},
        )
    return credentials.username


def require_api_token(
    request: Request,
    authorization=Depends(bearer_security),
    x_api_token: Optional[str] = Header(default=None),
    settings: Settings = Depends(get_settings),
) -> str:
    candidate = x_api_token
    if authorization:
        candidate = authorization.credentials
    if not candidate or not secrets.compare_digest(candidate, settings.api_token):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid API token")
    return request.headers.get("X-Actor", "api")
