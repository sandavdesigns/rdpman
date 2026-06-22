from __future__ import annotations

from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_name: str = "RDP Man"
    database_url: str = "sqlite:///./data/rdpman.sqlite3"
    secret_key: str = "dev-only-change-me"
    admin_username: str = "admin"
    admin_password: str = "change-me"
    api_token: str = "change-me-api-token"

    model_config = SettingsConfigDict(
        env_prefix="RDP_MAN_",
        env_file=".env",
        extra="ignore",
    )


@lru_cache
def get_settings() -> Settings:
    return Settings()
