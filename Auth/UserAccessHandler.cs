using Microsoft.AspNetCore.Authorization;

namespace fim_queueing_admin.Auth;

public class UserAccessHandler : AuthorizationHandler<UserAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserAccessRequirement requirement)
    {
        // If user does not have the claim, get out of here
        if (!context.User.HasClaim(c => c.Type == ClaimTypes.AccessLevel)) return Task.CompletedTask;

        // Get their level
        var accessLevel = context.User.FindFirst(c => c.Type == ClaimTypes.AccessLevel)!.Value;
        
        // Succeed if the level has the necessary permission
        if (Action.ActionMap[accessLevel].Contains(requirement.Action)) context.Succeed(requirement);

        return Task.CompletedTask;
    }
}