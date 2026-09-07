# Design

Read bank metadata and every bank item page when Bank policy is configured. Keep bank stock in the observation fingerprint but outside immutable catalog identity, since deposits mutate it. Save bank state in R2 observations. Deposit response updates character and bank together and verifies exact conservation against baseline; pending reconciliation checks both stores.

Bank policy lists permitted codes and quantity retained per code. It activates only on inventory-pressure gathering candidates; use existing standard same-layer map validation. Move to the lowest supported bank map, deposit at most 20 distinct permitted items, then the unchanged gathering goal naturally returns to its resource. Repeated travel/actions consume the common run budget.

Official OpenAPI 8.2.3 inspected 2026-09-07: GET /my/bank and paginated /my/bank/items; POST /my/{name}/action/bank/deposit/item accepts an array of code/quantity, returns cooldown/items/bank/character. Documented rejection includes full bank, absent bank, missing stock, active transaction and cooldown. No retries for this POST.
