namespace PitCrm.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string ReadCustomersAndLocations = "ReadCustomersAndLocations";
    public const string ManageCustomersAndLocations = "ManageCustomersAndLocations";
    public const string ManageTeam = "ManageTeam";
    public const string OperateDemands = "OperateDemands";
    public const string ManageProposals = "ManageProposals";
    public const string ManageProposalReasons = "ManageProposalReasons";
    public const string MentionProposalUsers = "MentionProposalUsers";
    public const string ViewProposalAudit = "ViewProposalAudit";
    public const string RestoreProposalTimeline = "RestoreProposalTimeline";
    public const string ViewProposalAuditGrant = "ViewProposalAudit";
    public const string RestoreProposalTimelineGrant = "RestoreProposalTimeline";
    public const string ChangeOwnPassword = "ChangeOwnPassword";
    public const string MustChangePasswordClaim = "must_change_password";
}

public interface IOrganizationPermissionGrantLookup
{
    Task<bool> HasGrantAsync(Guid organizationId, Guid userId, string permission,
        CancellationToken cancellationToken);
}
