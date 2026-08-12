# Kart Category Service — Messaging Contract

Source of truth: [`contracts/message-bus-manifest.json`](./message-bus-manifest.json). Nothing here is hand-maintained config — `RabbitMqTopologyProvisioner` (from `Kart.Shared.Messaging`) reads that manifest at startup and declares every exchange, queue, binding, DLQ, and retry tier from it. This doc is a human-readable index over that manifest plus the consumer manifest of the service that binds to it.

Last verified: 2026-07-30, against the current state of the repo (not the platform design docs — see the caveat at the bottom).

## Exchanges owned by this service

| Exchange | Type | Durable | Purpose |
|---|---|---|---|
| `category.exchange` | topic | yes | All events published by Category Service |

Category owns no DLX and declares no queues of its own — it consumes zero events (see below), so it has nothing to dead-letter.

## Published events

| Event | Exchange | Routing Key | Exchange Type |
|---|---|---|---|
| `CategoryUpdated` | `category.exchange` | `category.category.updated` | topic |

This is the service's only published event, covering create/rename/move/deprecate on the `Category` aggregate. Payload: `categoryId`, `name`, `parentId`, `path`, `operation`, `occurredAt`.

## Who consumes what

### `CategoryUpdated`

| Consumer | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|
| **Search Service** | `search.category-events.queue` | 30s → 60s → 120s (3 tiers) | `search.category-events.dlq` (on `search.dlx`) |

Search dispatches `ConsumeCategoryUpdatedCommand`, which writes only its own `CategoryLookup` projection (a denormalized `categoryId → name` mapping used to label search results) — it never fans out to every `SearchDocument` referencing the category, and rejects stale-ordered redeliveries by comparing `occurredAt` against the stored value. A missing or stale lookup degrades a facet's display label only; it never blocks indexing or querying.

**No other real consumer exists.** Category's own design doc (`kart-platform/docs/services/kart-category-service/event-contract.md`) names **Analytics** as the sole intended consumer (per ADR-0004/ADR-0008) — but `kart-analytics-service` is a README-only stub with no code, so that consumer doesn't exist. Search's consumption was added later, by its own `ADR-0018` (catalog signal sourcing), and isn't reflected in Category's own event-contract at all — it's a real, code-backed consumer that the publisher's own docs don't mention.

### Summary

| Event | Real Consumer(s) | Documented-but-unbuilt Consumer(s) |
|---|---|---|
| `CategoryUpdated` | Search Service | Analytics (stub repo, no code) |

## What this service consumes

**Nothing.** Category consumes zero events — confirmed by the empty `queues`/`deadLetterQueues` arrays in its manifest and by its own event-contract doc ("Consumed Events: None"). Every domain write (create/rename/move/deprecate) is driven synchronously, either by client-facing reads or by Admin's own RBAC-gated write-API calls — never by reacting to another service's event.

## Retry & dead-letter mechanics

Same manifest-driven mechanism used platform-wide (`Kart.Shared.Messaging`): the retry ladder and DLQ for `CategoryUpdated` live entirely in the *consumer's* manifest (Search's, above), not Category's. On handler failure, Search's consumer republishes to the next tier's TTL-based retry queue (dead-lettering back to the origin queue on expiry); once all tiers are exhausted, it nacks without requeue and the message lands on `search.category-events.dlq`. Category's own outbox relay retries indefinitely against broker connectivity issues rather than dead-lettering — this event has no DLQ of its own on the publish side.

## Caveat

Category's own design doc (`event-contract.md`) names a shared `catalog.dlq`/`category.category-updated.dlq` intended for an Analytics consumer that was never built, and doesn't mention Search's consumption at all — Search's `event-contract.md` (added under ADR-0018) is the accurate source for the real, running consumer. Of the 19 services in the monorepo, only 10 have real implementations (kart-identity-service, kart-user-service, kart-cart-service, kart-category-service, kart-delivery-tracking-service, kart-inventory-service, kart-offer-service, kart-payment-service, kart-product-service, kart-search-service); the rest are README-only stubs.
