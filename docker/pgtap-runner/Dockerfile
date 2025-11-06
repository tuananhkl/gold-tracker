FROM postgres:16

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       postgresql-16-pgtap \
       perl \
       make \
       libtap-parser-sourcehandler-pgtap-perl \
    && rm -rf /var/lib/apt/lists/*

ENV PGHOST=postgres \
    PGPORT=5432 \
    PGUSER=gold \
    PGPASSWORD=gold \
    PGDATABASE=gold

WORKDIR /tests

CMD ["pg_prove", "-h", "postgres", "-p", "5432", "-U", "gold", "/tests/*.sql"]

