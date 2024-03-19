using Microsoft.AspNetCore.Authorization;

namespace fim_queueing_admin.Auth;

public class UserAccessRequirement(string action) : IAuthorizationRequirement
{
    public string Action { get; } = action;
}

public class AuthorizeOperationAttribute(string action) : AuthorizeAttribute,
    IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        return new[] { new UserAccessRequirement(action) };
    }
}