using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Safqa.API;

[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var org = Context.User?.FindFirst("org")?.Value;
        if (org is not null) await Groups.AddToGroupAsync(Context.ConnectionId, org);
        await base.OnConnectedAsync();
    }
}
