#!/bin/bash
# Create IBM MQ resources using the ibmmq-transport CLI tool.
# Covers endpoint queues, infrastructure queues, topics, and subscriptions.
#
# Channels, auth records, listeners, and queue manager settings are NOT
# managed here — those remain in ibmmq-mqsc.ini (loaded at container startup).
#
# Usage:
#   ./setup-queues.sh
#
# Connection defaults to environment variables (IBMMQ_HOST, IBMMQ_PORT, etc.)
# or can be overridden:
#   IBMMQ_HOST=localhost IBMMQ_QUEUE_MANAGER=QM1 ./setup-queues.sh

set -euo pipefail

CMD="ibmmq-transport"

# Connection args must precede the subcommand (McMaster CLI requirement)
CONN=("--queue-manager" "${IBMMQ_QUEUE_MANAGER:-QM1}")

if [[ -n "${IBMMQ_HOST:-}" ]]; then
    CONN+=("--host" "$IBMMQ_HOST")
fi
if [[ -n "${IBMMQ_CHANNEL:-}" ]]; then
    CONN+=("--channel" "$IBMMQ_CHANNEL")
fi
if [[ -n "${IBMMQ_USER:-}" ]]; then
    CONN+=("--user" "$IBMMQ_USER")
fi
if [[ -n "${IBMMQ_PASSWORD:-}" ]]; then
    CONN+=("--password" "$IBMMQ_PASSWORD")
fi

# --- Endpoint queues ---

for endpoint in \
    Acme.Sales \
    Acme.Billing \
    Acme.Dashboard \
    Acme.Sender \
    Acme.Shipping \
    Acme.LegacySender
do
    $CMD "${CONN[@]}" endpoint create "$endpoint"
done

# --- Infrastructure queues ---

for queue in \
    audit \
    error \
    Particular.ServiceControl \
    Particular.ServiceControl.Audit \
    Particular.Monitoring \
    ServiceControl.ThroughputData
do
    $CMD "${CONN[@]}" queue create "$queue" --max-depth 999999999
done

# --- Benchmark queues ---

$CMD "${CONN[@]}" queue create bench.a --max-depth 999999999
$CMD "${CONN[@]}" queue create bench.b --max-depth 999999999

# --- Subscriptions ---
# --assembly enables polymorphic subscriptions (e.g. ExpressOrderPlaced inherits OrderPlaced)
# OrderPlaced (published by Acme.Sales)
#   Subscribers on IBM MQ side: Acme.Billing, bridge
# OrderShipped (published by bridge from RabbitMQ)
#   Subscribers on IBM MQ side: Acme.Sales, Acme.Billing, Acme.Dashboard

MESSAGES_DLL="src/Acme.Shared/bin/Release/net10.0/Acme.Shared.dll"

$CMD "${CONN[@]}" endpoint subscribe Acme.Billing   Acme.OrderPlaced  --assembly "$MESSAGES_DLL"
$CMD "${CONN[@]}" endpoint subscribe Acme.Billing   Acme.OrderShipped --assembly "$MESSAGES_DLL"
$CMD "${CONN[@]}" endpoint subscribe Acme.Sales     Acme.OrderShipped --assembly "$MESSAGES_DLL"
$CMD "${CONN[@]}" endpoint subscribe Acme.Dashboard Acme.OrderShipped --assembly "$MESSAGES_DLL"
