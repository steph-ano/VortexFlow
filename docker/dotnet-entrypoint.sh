#!/bin/sh
# VortexFlow.Api entrypoint wrapper.
#
# The compose file passes connection strings with custom
# `PasswordFile=/run/secrets/<name>` and `passwordFile=...` placeholders
# so the secret files (mounted by the compose `secrets:` block) are
# the single source of truth for the database / broker / cache
# passwords, and the connection string env vars themselves never
# contain plaintext passwords. The Npgsql, StackExchange.Redis and
# RabbitMQ.Client libraries used by the .NET code do NOT understand
# this placeholder syntax; they expect the standard
# `Password=<plaintext>` (Npgsql), `password=<plaintext>` (Redis)
# and `amqp://user:<plaintext>@host/` (RabbitMQ) forms.
#
# This wrapper reads each referenced secret file and substitutes
# the plaintext value into the corresponding connection string
# env var before exec'ing the .NET runtime. The plaintext
# passwords are still in the container's environment after the
# substitution (so they appear in `docker inspect`), but the
# compose file itself and the env vars in the rendered compose
# remain free of secrets.
#
# Written in POSIX sh (BusyBox ash on Alpine, /bin/sh on Debian) so
# it runs in the mcr.microsoft.com/dotnet/aspnet:8.0-alpine base
# image, which does NOT ship with bash. The `env | grep` + `cut`
# pattern is the POSIX-portable equivalent of bash's `${!name}`
# indirect variable expansion.

set -e

# resolve_env NAME: print the current value of the env var NAME.
# Equivalent to bash's ${!NAME}; avoids the bashism.
resolve_env() {
    env | sed -n "s/^$1=//p" | head -n 1
}

substitute_password() {
    name=$1
    file=$2
    if [ -z "$file" ] || [ ! -f "$file" ]; then
        return
    fi
    current=$(resolve_env "$name")
    if [ -z "$current" ]; then
        return
    fi
    pw=$(cat "$file")
    # Strip the placeholder (PasswordFile=... or passwordFile=...,
    # including the value) and append the standard form with the
    # plaintext value. The pattern matches both casings.
    new=$(printf '%s' "$current" | sed -E "s/[Pp]assword[Ff]ile=[^;]*//g; s/[Pp]assword[Ff]ile=//g")
    case "$name" in
        ConnectionStrings__Postgres)
            # Npgsql expects `Password=...` in the standard form.
            new=$(printf '%s' "$new" | sed -E "s/$/Password=$pw;/" | sed -E 's/;;/;/g; s/;$//')
            ;;
        ConnectionStrings__Redis)
            # StackExchange.Redis ConfigurationOptions expects
            # `password=...` (lowercase).
            new=$(printf '%s' "$new" | sed -E "s/$/,password=$pw/" | sed -E 's/,,/,/g; s/,$//')
            ;;
        ConnectionStrings__RabbitMq)
            # amqp://user:passwordFile=@host/ -> amqp://user:password@host/
            new=$(printf '%s' "$new" | sed -E "s|passwordFile=|$pw|")
            ;;
    esac
    export "$name=$new"
}

substitute_password ConnectionStrings__Postgres /run/secrets/postgres_password
substitute_password ConnectionStrings__Redis    /run/secrets/redis_password
substitute_password ConnectionStrings__RabbitMq /run/secrets/rabbitmq_password

exec dotnet VortexFlow.Api.dll
