# Examples

- Player HP100, fire10 and water10 vs HP30, fire resistance50 and water resistance0: outgoing15, two exchanges. Opponent fire3+water4 with zero player resistances gives maximum loss14.
- Per-channel rounding: attack1 at 50% bonus becomes2; resistance25 makes2; incoming critical makes3. Never round a sum before its channels.
- Secondary negative/out-of-range values or missing wire fields return Unknown/unsupported; effects remain unsupported. Existing zero-secondary fire fixture values remain unchanged.
