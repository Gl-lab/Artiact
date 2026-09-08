# Combat discovery requirements

The service SHALL discover bounded next combat-level routes from observed catalogs without manual targets, opponents or equipment. It SHALL explain progress vs opening a safe route and include full preparation/recovery cost in selection.

It SHALL require explicit Fight/Equip/Craft/bank and Recovery permissions, enforce stock floors and durable aggregate resource budgets, and refuse unsupported effects/opponents/projections or unavailable chains before their action. Discovery SHALL NOT authorize live combat beyond the existing ADR boundary.

Changing current equipment or prerequisite skills SHALL change evaluated paths. A useful supported preparation SHALL be crafted and equipped through real clients in a deterministic scenario; after the parent level is reached its demand ends. Rejected paths SHALL NOT repeatedly consume resources.

Every-tick reconstruction, lost Fight/Equip/Use responses, changed catalogs and invalid verified results SHALL preserve the journal/budget and never replay an ambiguous POST. Completion and archive SHALL validate actual combat level and preserve manual checkpoint compatibility.
