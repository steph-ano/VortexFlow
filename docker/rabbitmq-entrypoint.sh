#!/bin/sh
# RabbitMQ 3.13+ entrypoint wrapper.
#
# RabbitMQ 3.13 deprecated the RABBITMQ_DEFAULT_USER_FILE and
# RABBITMQ_DEFAULT_PASS_FILE env vars; setting them is now treated
# as a fatal startup error ("deprecated environment variables
# detected"), which causes the container to exit and docker to
# restart it in a loop. The replacement is the standard
# RABBITMQ_DEFAULT_USER / RABBITMQ_DEFAULT_PASS env vars, but we
# do not want to bake the password into the compose file or have
# it appear in `docker inspect` output.
#
# This wrapper reads the credentials from the secret-file mounts
# (provided by the compose `secrets:` block) and exports them as
# regular env vars before exec'ing the upstream entrypoint. The
# password is still visible to `docker inspect` on this container
# (a known limitation of the new env-var API), but the compose
# file itself contains no secret material and the secret files
# remain the single source of truth on the host.

# We use `set -e` (not `set -u`) so the wrapper is tolerant to being
# invoked without positional args by compose's entrypoint replacement.
set -e

if [ -f /run/secrets/rabbitmq_user ]; then
    export RABBITMQ_DEFAULT_USER=$(cat /run/secrets/rabbitmq_user)
fi
if [ -f /run/secrets/rabbitmq_password ]; then
    export RABBITMQ_DEFAULT_PASS=$(cat /run/secrets/rabbitmq_password)
fi

# Hand off to the upstream entrypoint that ships in the official
# rabbitmq image. It owns the rest of the boot sequence (config
# file rendering, plugin loading, log file setup, etc.). We forward
# any args the image's CMD might have provided (e.g. "rabbitmq-server").
exec docker-entrypoint.sh "$@"
