#!/bin/bash
# Create OS-level service accounts for IBM MQ authorization.
# MQ's SET AUTHREC PRINCIPAL('user') requires the user to exist in the OS.
#
# The dev image ships mqsimpleauth which only accepts 'admin' and 'app' as
# usernames. We remove it so MQ uses standard OS auth (amqzfu). With
# CHCKCLNT(OPTIONAL), passwords are never validated — which is necessary
# because rootless podman's nosuid volume mounts prevent MQ's suid-based
# PAM auth from working.

set -e

groupadd -f mqclient

# Start UIDs at 5000 to avoid colliding with mqm/admin users
# created later by runmqdevserver (which uses UIDs in the 1000-range)
uid=5000
for user in sales billing dashboard sender fmainframe bridge monitoring; do
    if ! id "$user" &>/dev/null; then
        useradd -u $uid -g mqclient -s /bin/false "$user"
    fi
    uid=$((uid + 1))
done

# Remove mqsimpleauth — it only accepts 'admin' and 'app' usernames,
# rejecting our per-service users. Keep only the standard OS auth module.
# The base image has a symlink /etc/mqm/qm-service-component.ini -> /run/...
# Replace it with a real file so runmqdevserver picks it up correctly.
rm -f /etc/mqm/qm-service-component.ini
cat > /etc/mqm/qm-service-component.ini <<'SVCEOF'
ServiceComponent:
   Service=AuthorizationService
   Name=MQSeries.UNIX.auth.service
   Module=amqzfu
   ComponentDataSize=0
SVCEOF
