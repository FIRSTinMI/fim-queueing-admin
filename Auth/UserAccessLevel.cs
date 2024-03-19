namespace fim_queueing_admin.Auth;

public class UserAccessLevel
{
    public const string ReadOnly = "2687ba63-c15a-47fa-bbc3-b9c4802a6fd6";
    public const string Editor = "3c3aac31-0349-4b90-bafb-3344bd4c8333";
    public const string Admin = "9ab85fca-724e-42d5-a4a2-cd1dff1f45e2";
}

public static class Action
{
    public const string ViewDisplay = nameof(ViewDisplay);
    public const string ManageDisplay = nameof(ManageDisplay);
    public const string ViewEvent = nameof(ViewEvent);
    public const string ManageEvent = nameof(ManageEvent);
    public const string CreateEvent = nameof(CreateEvent);
    public const string ViewAlert = nameof(ViewAlert);
    public const string ManageAlert = nameof(ManageAlert);
    public const string CreateAlert = nameof(CreateAlert);
    public const string ViewCart = nameof(ViewCart);
    public const string ManageCart = nameof(ManageCart);
    public const string CreateCart = nameof(CreateCart);
    public const string ViewUser = nameof(ViewUser);
    public const string ManageUser = nameof(ManageUser);
    public const string Admin = nameof(Admin);
    
    internal static readonly Dictionary<string, string[]> ActionMap = new()
    {
        {
            UserAccessLevel.ReadOnly,
            [ViewDisplay, ViewEvent, ViewAlert, ViewCart]
        },
        {
            UserAccessLevel.Editor,
            [
                ViewDisplay, ManageDisplay, ViewEvent, ManageEvent,
                ViewAlert, ManageAlert, CreateAlert, ViewCart, ManageCart
            ]
        },
        {
            UserAccessLevel.Admin,
            typeof(Action).GetFields().Select(f => (string)f.GetValue(null)!).ToArray() // All permissions
        }
    };
}