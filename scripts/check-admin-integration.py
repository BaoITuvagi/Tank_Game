"""Read-only preflight for admin hooks that can be lost when game sources are replaced.

This catches missing integration points; protocol and behavioral compatibility still
require the Java/Unity integration tests. It never patches gameplay automatically.
"""
import argparse
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def check(server=True, client=True):
    errors = []

    def require(relative, *patterns):
        path = ROOT / relative
        if not path.is_file():
            errors.append(f"Missing file: {relative}")
            return
        source = path.read_text(encoding="utf-8-sig")
        for pattern in patterns:
            if not re.search(pattern, source, re.DOTALL):
                errors.append(f"Missing admin hook in {relative}: {pattern}")

    if server:
        base = "source/server/src/main/java/tank/"
        require(base + "Server.java", r"new\s+AdminWeb\s*\(", r"admin\.start\s*\(",
                r"admin\.close\s*\(", r"admin\.isBanned\s*\(",
                r"admin\.store\.identity\s*\(", r"closeWithReason\s*\(", r"accessGate")
        require(base + "AdminWeb.java", r"/api/notice", r"Protocol\.NOTICE")
        require(base + "AdminStore.java")
        require(base + "AdminSetup.java")
        for resource in ("index.html", "app.js", "style.css"):
            require("source/server/src/main/resources/admin/" + resource)
        require("config/server.properties", r"(?m)^admin\.enabled\s*=\s*(?:true|false)\s*$")
    if client:
        base = "source/client/Assets/Scripts/"
        require(base + "ClientCore.Messages.cs", r"case\s+Op\.Notice\s*:",
                r"AdminNoticeChannel\.Publish\s*\(")
        require(base + "AdminNotices.cs", r"class\s+AdminNoticeQueue", r"class\s+AdminNoticeChannel")
        require(base + "AdminNoticeOverlay.cs", r"RuntimeInitializeOnLoadMethod",
                r"AdminNoticeChannel\.Received\s*\+=", r"MenuTheme", r"GUI\.BeginScrollView")
        for name in ("AdminNotices", "AdminNoticeOverlay"):
            require(base + name + ".cs.meta", r"guid:\s*[a-f0-9]{32}")
    if errors:
        print("ADMIN INTEGRATION CHECK FAILED", file=sys.stderr)
        for error in errors:
            print(" - " + error, file=sys.stderr)
        print("Game updates may have overwritten admin hooks. Restore the integration and run its tests before building.", file=sys.stderr)
        return False
    print("Admin integration hooks present. Run Java/Unity tests to verify behavior after game updates.")
    return True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--server", action="store_true")
    mode.add_argument("--client", action="store_true")
    args = parser.parse_args()
    sys.exit(0 if check(server=not args.client, client=not args.server) else 1)
