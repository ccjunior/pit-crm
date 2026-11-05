namespace PitCrm.Domain.Organizations;

public sealed class Organization
{
    private Organization()
    {
    }

    public Organization(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new ArgumentException("Organization name is required and limited to 200 characters.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
