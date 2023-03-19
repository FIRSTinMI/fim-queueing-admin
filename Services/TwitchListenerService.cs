using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;

namespace fim_queueing_admin.Services;

public class TwitchListenerService : IService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;

    public TwitchListenerService(IServiceProvider services, IConfiguration config)
    {
        _services = services;
        _config = config;
    }

    public async Task<IEnumerable<string>> GetListenedChannels()
    {
        var twitch = _services.GetService<TwitchAPI>();
        if (twitch is null) throw new ApplicationException("Twitch API not set up");

        // We can assume that if we have an online subscription then we're fully set up for that channel
        var subscriptions =
            (await twitch.Helix.EventSub.GetEventSubSubscriptionsAsync(type: "stream.online")).Subscriptions.Where(x =>
                x.Transport.Callback == _config["Twitch:WebhookEndpoint"]).ToList();
        if (!subscriptions.Any())
        {
            return new List<string>();
        }
        var users = await twitch.Helix.Users.GetUsersAsync(ids: subscriptions
            .Select(x => x.Condition["broadcaster_user_id"]).ToList());

        return users.Users.Select(x => x.Login);
    }
    
    public async Task AddTwitchListener(string username)
    {
        var twitch = _services.GetService<TwitchAPI>();
        if (twitch is null) return;

        var userInfo = (await twitch.Helix.Users.GetUsersAsync(logins: new List<string> { username })).Users
            .FirstOrDefault();
        if (userInfo is null) throw new ApplicationException("User not found");

        // Add a webhook for both going online and going offline
        var hookTasks = (new List<string>() { "stream.online", "stream.offline" }).Select(e =>
            twitch.Helix.EventSub.CreateEventSubSubscriptionAsync(e, "1",
                new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", userInfo.Id }
                }, EventSubTransportMethod.Webhook, null, _config["Twitch:WebhookEndpoint"],
                _config["Twitch:WebhookSecret"]));

        await Task.WhenAll();
        if (hookTasks.Any(t => t.IsFaulted)) throw new ApplicationException("Adding a webhook failed");
    }

    public async Task DeleteAllWebhooks(bool justMatchingUrls = true)
    {
        var twitch = _services.GetService<TwitchAPI>();
        if (twitch is null) return;

        var hooks = (await twitch.Helix.EventSub.GetEventSubSubscriptionsAsync()).Subscriptions;

        if (justMatchingUrls)
        {
            hooks = hooks.Where(x => x.Transport.Callback == _config["Twitch:WebhookEndpoint"]).ToArray();
        }

        var deleteTasks = hooks.Select(h => twitch.Helix.EventSub.DeleteEventSubSubscriptionAsync(h.Id));
        await Task.WhenAll(deleteTasks);
    }
}