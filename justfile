# Show available recipes
help:
    @just --list

# Build solution only (no containers). Pass `true` to use IBMMQ project reference.
dotnet-build use_source="false":
    dotnet build ibmmq-bridge-demo.slnx -p:UseIBMMQSource={{use_source}}

# Build all container images (pulls ServiceControl from ghcr.io)
build:
    mkdir .packages
    #!/usr/bin/env bash
    set -euo pipefail
    podman compose build --parallel &
    podman compose pull --parallel sc-ibmmq sc-audit-ibmmq sc-audit-rabbitmq sc-mon-ibmmq sc-mon-rabbitmq sc-rabbitmq &
    wait

# Start infrastructure + endpoints
up *ARGS:
    podman compose up -d --remove-orphans {{ARGS}}

# Stop everything
down *ARGS:
    podman compose down {{ARGS}}

# Follow logs
logs:
    podman compose logs -f

# Recreate all containers (clears journald logs)
recreate:
    podman compose up -d --force-recreate

# Run the sender interactively on host
sender:
    dotnet run --project src/Acme.Sender

# Run the legacy EBCDIC sender on host
legacy-sender:
    dotnet run --project src/Acme.LegacySender

# Run the dashboard on host
dashboard:
    dotnet run --project src/Acme.Dashboard

# Pack all projects into ~/.packages
pack:
    dotnet pack ibmmq-bridge-demo.slnx -o .packages

# Build, start, open dashboard, and attach tmux log monitor
demo: up monitor
    xdg-open http://localhost:6001

# Tmux split view tailing logs for all endpoints and bridge
monitor:
    #!/usr/bin/env bash
    set -euo pipefail
    SESSION="ibmmq-demo"
    tmux kill-session -t "$SESSION" 2>/dev/null || true
    tmux new-session -d -s "$SESSION" -n logs
    tmux set-option -t "$SESSION" pane-border-status top
    tmux set-option -t "$SESSION" pane-border-format ' #{pane_title} '
    # Top-left: Sales
    tmux select-pane -T 'Sales'
    tmux send-keys -t "$SESSION" 'podman logs -f sales 2>&1' Enter
    # Top-right: Billing
    tmux split-window -h -t "$SESSION"
    tmux select-pane -T 'Billing'
    tmux send-keys -t "$SESSION" 'podman logs -f billing 2>&1' Enter
    # Middle-left: Dashboard
    tmux split-window -v -t "$SESSION:logs.0"
    tmux select-pane -T 'Dashboard'
    tmux send-keys -t "$SESSION" 'podman logs -f dashboard 2>&1' Enter
    # Middle-right: Shipping
    tmux split-window -v -t "$SESSION:logs.1"
    tmux select-pane -T 'Shipping'
    tmux send-keys -t "$SESSION" 'podman logs -f shipping 2>&1' Enter
    # Bottom: Bridge (full width)
    tmux split-window -v -f -t "$SESSION"
    tmux select-pane -T 'Bridge'
    tmux send-keys -t "$SESSION" 'podman logs -f bridge 2>&1' Enter
    # Even out the layout
    tmux select-layout -t "$SESSION" tiled
    tmux attach -t "$SESSION"
