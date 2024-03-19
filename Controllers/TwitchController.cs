using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlackNet;
using SlackNet.WebApi;
using TwitchLib.Api;

namespace fim_queueing_admin.Controllers;

public class TwitchController(ILogger<TwitchController> logger, IConfiguration config) : Controller
{
    /// <summary>
    /// The main endpoint that handles all of our Twitch webhooks.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Webhook([FromServices] IServiceProvider services)
    {
        // All calls need to get verified, including the challenges we're sent on first creation
        if (!await VerifyMessage(Request)) return BadRequest("Bad HMAC");
        
        var messageType = Request.Headers["Twitch-Eventsub-Message-Type"].FirstOrDefault();
        if (messageType is null) return BadRequest("Expected a message type");
        var messageBody = (await JsonDocument.ParseAsync(Request.Body)).RootElement;

        switch (messageType)
        {
            // When we set up a new webhook we need to verify with Twitch
            case "webhook_callback_verification":
            {
                logger.LogInformation("Verifying a webhook from Twitch");

                if (!messageBody.TryGetProperty("challenge", out var challenge))
                {
                    return BadRequest("No challenge");
                }
                logger.LogInformation("Challenge {}", challenge.GetString());
                Response.StatusCode = StatusCodes.Status200OK;
                return Ok(challenge.GetString()!);
            }
            case "revocation":
                logger.LogWarning("Revoked webhook {}", JsonSerializer.Serialize(messageBody));
                return Ok();
            case "notification":
                logger.LogInformation("Notification webhook {}", JsonSerializer.Serialize(messageBody));
                if (messageBody.TryGetProperty("subscription", out var subscription) &&
                    subscription.TryGetProperty("type", out var type) &&
                    (type.GetString() == "stream.online" || type.GetString() == "stream.offline"))
                {
                    // Send a slack message for the online or offline events
                    if (!messageBody.TryGetProperty("event", out var evt) ||
                        !evt.TryGetProperty("broadcaster_user_login", out var login))
                    {
                        logger.LogWarning("Could not get broadcaster_user_login from message");
                        return BadRequest();
                    }
                    var slackMessage = type.GetString() switch
                    {
                        "stream.offline" => $"🟥 {login} has gone offline on Twitch",
                        "stream.online" => $"🟢 {login} has started streaming on Twitch",
                        _ => throw new ApplicationException("Unreachable (slack message in webhook endpoint)")
                    };

                    var slack = services.GetService<ISlackApiClient>();
                    if (slack is null)
                    {
                        logger.LogWarning("Cannot send Slack messages without slack being configured");
                        return Ok("Could not send slack message, ignoring");
                    }
                    await slack.Chat.PostMessage(new Message()
                    {
                        Channel = config["Slack:NotificationChannel"],
                        Text = slackMessage
                    });

                    return Ok();
                }
                return BadRequest();
            default:
                return BadRequest();
        }
    }

    private async Task<bool> VerifyMessage(HttpRequest request)
    {
        // Make sure we can read the body more than once
        if (!request.Body.CanSeek)
        {
            request.EnableBuffering();
        }

        var webhookSecret = config["Twitch:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret)) throw new ApplicationException("No Twitch Webhook secret set");
        
        request.Body.Position = 0;
        var body = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync();
        request.Body.Position = 0;
        
        // Build up the HMAC and compare against what we were given
        var msgToVerify = new StringBuilder();
        msgToVerify.Append(request.Headers["Twitch-Eventsub-Message-Id"].First());
        msgToVerify.Append(request.Headers["Twitch-Eventsub-Message-Timestamp"].First());
        msgToVerify.Append(body);
        var computedHmac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret),
            Encoding.UTF8.GetBytes(msgToVerify.ToString()));
        return string.Equals(request.Headers["Twitch-Eventsub-Message-Signature"].First(),
            "sha256=" + Convert.ToHexString(computedHmac), StringComparison.OrdinalIgnoreCase);
    }
}
