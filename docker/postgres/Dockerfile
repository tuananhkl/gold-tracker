FROM postgres:16

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       postgresql-16-pgtap \
    && rm -rf /var/lib/apt/lists/*

# Default Postgres image will create the DB from env vars; no extra init scripts here.


