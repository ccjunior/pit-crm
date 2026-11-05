namespace PitCrm.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string ReadCustomersAndLocations = "ReadCustomersAndLocations";
    public const string ManageCustomersAndLocations = "ManageCustomersAndLocations";
    public const string ManageTeam = "ManageTeam";
    public const string OperateDemands = "OperateDemands";
    public const string ChangeOwnPassword = "ChangeOwnPassword";
}
