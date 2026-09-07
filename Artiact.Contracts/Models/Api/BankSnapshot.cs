using System.Collections.Immutable;

namespace Artiact.Contracts.Models.Api;

// Aggregated from bank details and every item page; not an action response DTO.
public sealed record BankSnapshot(int Slots, ImmutableDictionary<string, int> Items);
