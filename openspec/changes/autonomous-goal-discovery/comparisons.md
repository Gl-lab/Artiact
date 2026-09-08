# Preregistered quality protocol v1

Recorded before implementation, 2026-09-08. Keep losses and amendments visible.

Synthetic common bounds: 20 actions, 300 seconds, capacity 100; standard same-layer maps; XP threshold 26; gather cooldown 5, move 7; deterministic one-unit drops. Skills absent from a fixture are unsupported, not level zero. Baselines: fixed mining then woodcutting, each next level with equal values; lowest available skill next level with ordinal tie-breaking. R12 must execute all strategies with the same state and budget. Maximum permitted regression for equal useful outcomes: 0 actions, 0 confirmed cooldown seconds and 0 unproductive moves relative to each baseline. At least one useful-result win or strictly fewer commands without cooldown growth is required.

| Fixture | Independent useful result / acceptable first goal |
|---|---|
| New character: mining=1, woodcutting=1, on wood map; only level-1 resources | woodcutting next level, avoiding an unnecessary move |
| Uneven: mining=9, woodcutting=1, on mining map; mining level-9 trainer plus level-10 unlock | mining 10, opening the higher XP-base resource; lowest level alone is insufficient |
| Bank stock: new-character fixture with 10 ore in permitted bank | woodcutting next level; existing ore does not manufacture recipe utility or override travel cost |
| Inaccessible unlock: uneven fixture but level-10 resource behind unsupported access, on wood map | woodcutting next level; locked resource cannot earn utility |

R11 checks decisions and explanations only. Execution ceilings and useful-result win remain unverified until R12; passing Inspect does not pass the execution gate. R13/R14 must extend this protocol before tuning their utility formulas.
