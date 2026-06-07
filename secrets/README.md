# Docker Compose secrets directory

This directory holds the secret files that `docker-compose.yml`,
`docker-compose.beta.yml`, `docker-compose.prod.yml`, and
`docker-compose.observability.yml` mount into the containers via
`/run/secrets/<name>`. Each file should contain the single secret value
(no trailing newline, no JSON wrapper).

## Expected files (development)

| File                          | Example command                                      |
| ----------------------------- | ---------------------------------------------------- |
| `postgres_password.txt`       | `openssl rand -base64 48 > postgres_password.txt`    |
| `redis_password.txt`          | `openssl rand -base64 48 > redis_password.txt`       |
| `rabbitmq_user.txt`           | `echo -n vortexflow > rabbitmq_user.txt`             |
| `rabbitmq_password.txt`       | `openssl rand -base64 48 > rabbitmq_password.txt`    |
| `rabbitmq_url.txt`            | `printf 'amqp://vortexflow:%s@rabbitmq:5672/' "$(cat rabbitmq_password.txt)" > rabbitmq_url.txt` |
| `redis_url.txt`               | `printf 'redis://:%s@redis:6379/0' "$(cat redis_password.txt)" > redis_url.txt` |
| `admin_password.txt`          | `openssl rand -base64 18 > admin_password.txt`       |
| `api_key_internal.txt`        | `openssl rand -base64 48 > api_key_internal.txt`     |

## Production

In production, mount `/etc/vortexflow/secrets/` from a secret store
(Vault, AWS Secrets Manager via External Secrets Operator, Sealed
Secrets, SOPS, etc.) — never commit real values to this directory.

## For Kubernetes

The companion manifest at `k8s/secrets.yaml` defines a `Secret` of the
same names. In production, populate it via External Secrets Operator
(recommended), Sealed Secrets, or `kubectl create secret generic` from
an out-of-band `.env.production` file:

```sh
kubectl create secret generic vortexflow-secrets \
  --from-env-file=.env.production \
  -n vortexflow
```

The k8s deployment and statefulset manifests in `k8s/services.yaml` and
`k8s/infrastructure.yaml` consume the secret via `envFrom: secretRef` and
`valueFrom: secretKeyRef`, so no plaintext credential ever appears in
a PodSpec on disk.
