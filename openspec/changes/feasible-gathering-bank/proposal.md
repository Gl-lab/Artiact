# R23 — Feasible gathering with permitted banking

Base R22 `9384ec2`. Confirmed gap: discovery skips aggregate inventory rejection whenever BankRetain exists, while FullPathStrategy adds only a capacity-based time allowance and omits deposit/return actions. A policy does not prove that protected drops, slots or repeated trips fit.

Replace this autonomous gathering estimate with a bounded conservative stock simulation. Reuse existing BankPrerequisite, action journal, preflight and reconciliation unchanged. No new API, permission, withdrawal, mock mechanic or live action.
