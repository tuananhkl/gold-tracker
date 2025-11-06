.PHONY: db\:up db\:migrate db\:reset db\:test db\:psql

DC=docker compose

db\:up:
	$(DC) up -d postgres

db\:migrate:
	$(DC) run --rm flyway

db\:reset:
	$(DC) down -v
	$(DC) up -d postgres
	@sleep 3
	$(DC) run --rm flyway

db\:test:
	$(DC) build pgtap-runner
	$(DC) run --rm pgtap-runner

db\:psql:
	$(DC) exec -e PGPASSWORD=gold postgres psql -U gold -d gold


