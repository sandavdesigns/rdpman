import os

os.environ["RDP_MAN_DATABASE_URL"] = "sqlite:///:memory:"
os.environ["RDP_MAN_SECRET_KEY"] = "test-secret"
os.environ["RDP_MAN_API_TOKEN"] = "test-token"

from fastapi.testclient import TestClient  # noqa: E402

from app.database import Base, engine  # noqa: E402
from app.main import app  # noqa: E402


client = TestClient(app)


def setup_function():
    Base.metadata.drop_all(bind=engine)
    Base.metadata.create_all(bind=engine)


def auth_headers():
    return {"Authorization": "Bearer test-token", "X-Actor": "pytest"}


def test_system_lifecycle_and_rdp_download():
    create_response = client.post(
        "/api/v1/systems",
        headers=auth_headers(),
        json={
            "name": "Terminalserver 01",
            "hostname": "ts01.example.local",
            "ip_address": "10.20.30.40",
        },
    )

    assert create_response.status_code == 201
    system_id = create_response.json()["id"]

    list_response = client.get("/api/v1/systems", headers=auth_headers())
    assert list_response.status_code == 200
    assert list_response.json()[0]["hostname"] == "ts01.example.local"

    rdp_response = client.get(f"/api/v1/systems/{system_id}/rdp?ticket_id=T-1000", headers=auth_headers())
    assert rdp_response.status_code == 200
    assert "full address:s:ts01.example.local" in rdp_response.text
    assert "prompt for credentials:i:1" in rdp_response.text

    connections_response = client.get("/api/v1/connections", headers=auth_headers())
    assert connections_response.status_code == 200
    connections = connections_response.json()
    assert len(connections) == 1
    assert connections[0]["system_id"] == system_id
    assert connections[0]["hostname"] == "ts01.example.local"
    assert connections[0]["status"] == "active"
    assert connections[0]["ticket_id"] == "T-1000"

    close_response = client.post(f"/api/v1/connections/{connections[0]['id']}/close", headers=auth_headers())
    assert close_response.status_code == 200
    assert close_response.json()["status"] == "closed"


def test_credential_response_never_returns_password():
    system_response = client.post(
        "/api/v1/systems",
        headers=auth_headers(),
        json={"name": "Server", "hostname": "server.local"},
    )
    system_id = system_response.json()["id"]

    credential_response = client.post(
        f"/api/v1/systems/{system_id}/credentials",
        headers=auth_headers(),
        json={"label": "Admin", "username": "administrator", "password": "super-secret"},
    )

    assert credential_response.status_code == 201
    body = credential_response.json()
    assert body["username"] == "administrator"
    assert "password" not in body
    assert "encrypted_password" not in body
