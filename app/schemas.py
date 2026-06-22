from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, ConfigDict, Field


class SystemBase(BaseModel):
    name: str = Field(min_length=1, max_length=160)
    hostname: str = Field(min_length=1, max_length=255)
    ip_address: Optional[str] = None
    customer: Optional[str] = None
    location: Optional[str] = None
    environment: Optional[str] = None
    status: str = "active"
    notes: Optional[str] = None
    external_ref: Optional[str] = None


class SystemCreate(SystemBase):
    pass


class SystemUpdate(BaseModel):
    name: Optional[str] = Field(default=None, min_length=1, max_length=160)
    hostname: Optional[str] = Field(default=None, min_length=1, max_length=255)
    ip_address: Optional[str] = None
    customer: Optional[str] = None
    location: Optional[str] = None
    environment: Optional[str] = None
    status: Optional[str] = None
    notes: Optional[str] = None
    external_ref: Optional[str] = None


class SystemRead(SystemBase):
    id: int
    created_at: datetime
    updated_at: datetime

    model_config = ConfigDict(from_attributes=True)


class CredentialCreate(BaseModel):
    label: str = "Default"
    username: str = Field(min_length=1, max_length=255)
    password: str = Field(min_length=1)
    domain: Optional[str] = None
    is_active: bool = True


class CredentialRead(BaseModel):
    id: int
    system_id: int
    label: str
    username: str
    domain: Optional[str]
    is_active: bool
    created_at: datetime
    updated_at: datetime

    model_config = ConfigDict(from_attributes=True)


class AuditEventRead(BaseModel):
    id: int
    actor: str
    action: str
    system_id: Optional[int]
    credential_id: Optional[int]
    details: Optional[str]
    created_at: datetime

    model_config = ConfigDict(from_attributes=True)
