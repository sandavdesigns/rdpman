from __future__ import annotations

from typing import Optional

from app.models import Credential, System


def build_rdp_file(system: System, credential: Optional[Credential] = None) -> str:
    username = ""
    if credential:
        username = credential.username
        if credential.domain:
            username = f"{credential.domain}\\{credential.username}"

    lines = [
        "screen mode id:i:2",
        "use multimon:i:1",
        "desktopwidth:i:1920",
        "desktopheight:i:1080",
        "session bpp:i:32",
        "redirectclipboard:i:1",
        "redirectprinters:i:0",
        "redirectsmartcards:i:1",
        "authentication level:i:2",
        "prompt for credentials:i:1",
        f"full address:s:{system.hostname}",
    ]
    if username:
        lines.append(f"username:s:{username}")
    return "\r\n".join(lines) + "\r\n"
