#!/usr/bin/env python3
"""Structural validator for Kubernetes YAML manifests.

CI replacement for `kubectl apply --dry-run=client --validate=false`,
which still requires an apiserver connection for API group discovery
(fails with "dial tcp [::1]:8080: connect: connection refused" in
CI runners that have no cluster).

This script performs a purely client-side check:

  1. The file is well-formed YAML.
  2. Every non-empty document is a mapping.
  3. Every document has the top-level keys apiVersion and kind.
  4. Every document has a metadata mapping with a name.

It deliberately does NOT validate resource schemas (required spec
fields, type mismatches, unknown properties, etc.) — that is the
job of kubeconform, which runs in the next step. This script is the
cheap pre-check that catches typos, missing fields and malformed
YAML before the heavier schema lookup.

Exits 0 on success, 1 if any file fails the structural check, with
each error printed as a `::error::` annotation so GitHub Actions
renders it inline in the run summary.
"""
from __future__ import annotations

import sys
from typing import Sequence

import yaml

REQUIRED_TOP_LEVEL: tuple[str, ...] = ("apiVersion", "kind")


def validate_file(path: str) -> list[str]:
    """Return a list of error strings for `path`; empty if the file is valid."""
    errors: list[str] = []

    try:
        with open(path, "r", encoding="utf-8") as fp:
            # `safe_load_all` rejects Python-specific tags and arbitrary
            # objects, which is what we want for config files coming from
            # a CI pipeline. The all-variant handles multi-document streams
            # (e.g. `---` separated resources in one file).
            docs = list(yaml.safe_load_all(fp))
    except OSError as exc:
        return [f"{path}: cannot read file: {exc}"]
    except yaml.YAMLError as exc:
        return [f"{path}: YAML parse error: {exc}"]

    for index, doc in enumerate(docs):
        # Empty documents (e.g. a trailing `---`) are legal in YAML
        # streams and carry no manifest, so we skip them.
        if doc is None:
            continue

        if not isinstance(doc, dict):
            errors.append(
                f"{path} doc #{index + 1}: top-level is "
                f"{type(doc).__name__}, expected mapping"
            )
            continue

        for field in REQUIRED_TOP_LEVEL:
            if field not in doc:
                errors.append(
                    f"{path} doc #{index + 1}: missing required field '{field}'"
                )

        metadata = doc.get("metadata")
        if not isinstance(metadata, dict):
            errors.append(
                f"{path} doc #{index + 1}: missing or invalid 'metadata' "
                f"(must be a mapping with a 'name' key)"
            )
            continue

        if "name" not in metadata:
            errors.append(
                f"{path} doc #{index + 1}: missing required field 'metadata.name'"
            )

    return errors


def main(paths: Sequence[str]) -> int:
    if not paths:
        print("::error::validate-k8s-manifests: no files supplied", file=sys.stderr)
        return 1

    all_errors: list[str] = []
    for path in paths:
        all_errors.extend(validate_file(path))

    if all_errors:
        for err in all_errors:
            print(f"::error::{err}")
        print(
            f"\nvalidate-k8s-manifests: {len(all_errors)} structural "
            f"error(s) across {len(paths)} file(s).",
            file=sys.stderr,
        )
        return 1

    print(
        f"validate-k8s-manifests: structural check OK for {len(paths)} file(s)."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
