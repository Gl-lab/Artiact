# Design

Parse a bounded drop list in GatheringStrategy. Preserve the existing target, map, access, progression and raw-state guards. For each declared item, verify the inventory delta is within min/max when it occurs; optional rate > 1 may have zero delta. Guaranteed items must occur. All undeclared inventory stays unchanged. Reserve the sum of maxima before move/gather. This deliberately overestimates capacity; no probability or optimality claim.
