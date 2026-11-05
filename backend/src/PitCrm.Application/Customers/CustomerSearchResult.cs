namespace PitCrm.Application.Customers;

public sealed record CustomerSearchResult(IReadOnlyList<CustomerDto> Items, int TotalCount);
