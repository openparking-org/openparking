.PHONY: setup dev test migrate logs clean

setup: ## Initial setup: copy env, build, migrate, seed
	cp -n .env.example .env || true
	docker compose build
	docker compose run --rm api dotnet ef database update --project OpenParking.Infrastructure --startup-project OpenParking.Api

dev: ## Start all services locally
	docker compose up

test: ## Run all tests (backend + AI)
	cd api && dotnet test OpenParking.sln
	docker compose run --rm -v "$(PWD)/ai:/app" ai sh -c "pip install -r requirements-dev.txt && PYTHONPATH=/app pytest"

migrate: ## Create and apply a new EF migration (usage: make migrate name=AddPenalty)
	docker compose run --rm api dotnet ef migrations add $(name) --project OpenParking.Infrastructure --startup-project OpenParking.Api
	docker compose run --rm api dotnet ef database update --project OpenParking.Infrastructure --startup-project OpenParking.Api

logs: ## Tail all service logs
	docker compose logs -f

clean: ## Stop and remove all containers and volumes
	docker compose down -v
