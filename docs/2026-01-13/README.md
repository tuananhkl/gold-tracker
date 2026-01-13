# Gold Tracker – Incident Notes (2026-01-13)

This folder documents the issues we hit today (UI + logging + Kubernetes), including:

- The **context & symptoms** we observed
- The **debugging mindset** (what to check first, what hypotheses to form)
- The **commands we ran** and how to interpret their outputs
- The **root causes**
- The **fixes**, including code changes
- A short **summary & lessons learned**

## Index

- [`01-kibana-missing-log-level.md`](./01-kibana-missing-log-level.md) – Missing/incorrect log levels in Kibana
- [`02-k8s-duplicate-environments-default-vs-gold-dev.md`](./02-k8s-duplicate-environments-default-vs-gold-dev.md) – Duplicate app deployments and cleanup
- [`04-fix-es-tls-scraper-warnings-and-kustomize-errors.md`](./04-fix-es-tls-scraper-warnings-and-kustomize-errors.md) – ES TLS warning + scraper anomaly noise + kustomize apply failures
- [`03-appendix-commands-cheatsheet.md`](./03-appendix-commands-cheatsheet.md) – Useful commands used in this session

