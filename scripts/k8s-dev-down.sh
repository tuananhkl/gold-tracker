#!/usr/bin/env bash
set -euo pipefail
kubectl delete ns gold-dev --ignore-not-found
kind delete cluster --name ${CLUSTER_NAME:-gold-dev} || true

