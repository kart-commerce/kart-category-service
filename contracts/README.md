# Contracts

`api-contract.yaml` is a synced copy of the approved contract owned by
`kart-platform/docs/services/kart-category-service/api-contract.yaml` (the
source of truth). Update it only by re-copying the upstream file after a new
contract revision is approved there — never edit it directly in this repo.
`tests/ContractTests/Fixtures/api-contract.yaml` is this service's own working
copy the contract tests validate against; keep both copies in sync when the
upstream contract changes.

`message-bus-manifest.json` is likewise a synced copy of
`kart-platform/docs/services/kart-category-service/message-bus-manifest.json`
— this service's own RabbitMQ topology (`category.exchange`, owned by this
service alone; no shared platform-wide exchange, per `kart-conventions.md`
§"RabbitMQ"). Copied into `src/Api` at build time (see
`KartCategoryService.Api.csproj`) so `RabbitMqOptions.ManifestPath` (resolved
against `AppContext.BaseDirectory`) finds it at runtime, where
`RabbitMqTopologyStartupHostedService` scans it to declare every
exchange/queue/binding/DLQ/retry-tier at startup — nothing messaging-related
is hardcoded in C#. Update it only by re-copying the upstream file after a
manifest revision is approved there.
