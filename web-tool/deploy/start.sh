#!/bin/bash
# Starts the unmodified Orleans silo, waits for its client gateway, then starts
# the web tool. Both run inside one container so localhost clustering applies.
set -e

echo "[start] launching Orleans silo..."
(cd /app/silo && exec dotnet NIST.CVP.ACVTS.Orleans.ServerHost.dll) &
SILO_PID=$!

echo "[start] waiting for the Orleans gateway (localhost:30000)..."
for _ in $(seq 1 120); do
  if (exec 3<>/dev/tcp/localhost/30000) 2>/dev/null; then
    exec 3<&- 3>&-
    echo "[start] Orleans gateway is up."
    break
  fi
  if ! kill -0 "$SILO_PID" 2>/dev/null; then
    echo "[start] ERROR: the Orleans silo exited during startup." >&2
    exit 1
  fi
  sleep 1
done

echo "[start] launching the web tool on :8080..."
cd /app/web
exec dotnet Acvp.WebTool.Api.dll
