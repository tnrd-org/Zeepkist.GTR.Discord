using System.Net.WebSockets;
using Newtonsoft.Json;
using Websocket.Client;

namespace TNRD.Zeepkist.GTR.Discord;

internal class WebSocketHostedService : IHostedService
{
    private readonly WebsocketClient client;
    private readonly DiscordMessageSender discordMessageSender;

    public WebSocketHostedService(DiscordMessageSender discordMessageSender)
    {
        this.discordMessageSender = discordMessageSender;
        client = new WebsocketClient(new Uri("wss://stream.zeepkist-gtr.com/ws"));
        client.MessageReceived.Subscribe(OnMessage);
    }

    private void OnMessage(ResponseMessage responseMessage)
    {
        PublishableRecord? publishableRecord = JsonConvert.DeserializeObject<PublishableRecord>(responseMessage.Text);
        discordMessageSender.SendMessage(publishableRecord);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.Start();
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.Stop(WebSocketCloseStatus.NormalClosure, "Closing");
    }
}
