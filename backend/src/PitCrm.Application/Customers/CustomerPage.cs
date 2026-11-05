namespace PitCrm.Application.Customers;

public sealed record CustomerPage(
    IReadOnlyList<CustomerDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
