FROM icr.io/ibm-messaging/mq:latest

USER root
COPY ibmmq-users.sh /etc/mqm/ibmmq-users.sh
RUN chmod +x /etc/mqm/ibmmq-users.sh && /etc/mqm/ibmmq-users.sh \
    # Remove mqsimpleauth — it only accepts 'admin' and 'app' usernames.
    # runmqdevserver always injects it into the service component config at
    # startup, so removing the config file isn't enough. Delete the .so itself.
    && rm -f /opt/mqm/lib64/mqsimpleauth.so \
    # Replace PAM config for MQ with pam_permit (accept all passwords).
    # Required because rootless podman ignores suid bits, preventing pam_unix
    # from verifying passwords. This enables CONNAUTH ADOPTCTX(YES) to adopt
    # the MQCSP user identity without actual password validation.
    && printf '#%%PAM-1.0\nauth     required  pam_permit.so\naccount  required  pam_permit.so\n' > /etc/pam.d/ibmmq

# Restore the base image default user — runmqdevserver handles its own setup
USER 1001
