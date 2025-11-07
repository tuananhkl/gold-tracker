#!/usr/bin/env bash
set -euo pipefail
kubectl delete ns gold-dev --ignore-not-found
# keep minikube running for reuse
