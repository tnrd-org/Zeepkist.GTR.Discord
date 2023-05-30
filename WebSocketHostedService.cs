using System.Net.WebSockets;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;
using Websocket.Client;

namespace TNRD.Zeepkist.GTR.Discord;

internal class WebSocketHostedService : IHostedService
{
    private readonly WebsocketClient client;
    private readonly DiscordMessageSender discordMessageSender;
    private readonly Queue<RecordId> queue;
    private readonly AutoResetEvent autoResetEvent;
    private readonly CancellationTokenSource cts;

    private Task? runner;

    public WebSocketHostedService(DiscordMessageSender discordMessageSender)
    {
        this.discordMessageSender = discordMessageSender;
        client = new WebsocketClient(new Uri("wss://stream.dev.zeepkist-gtr.com/ws"));
        client.MessageReceived.Subscribe(OnMessage);

        queue = new Queue<RecordId>();
        autoResetEvent = new AutoResetEvent(true);
        cts = new CancellationTokenSource();
    }

    private void OnMessage(ResponseMessage responseMessage)
    {
        autoResetEvent.WaitOne();

        try
        {
            RecordId? recordId = JsonConvert.DeserializeObject<RecordId>(responseMessage.Text);
            if (recordId != null)
                queue.Enqueue(recordId);
        }
        finally
        {
            autoResetEvent.Set();
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        runner = Runner(cts.Token);
        await client.Start();
    }

    private async Task Runner(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            autoResetEvent.WaitOne();

            try
            {
                if (queue.Count > 0)
                {
                    RecordId recordId = queue.Dequeue();
                    discordMessageSender.SendMessage(recordId);
                }
            }
            finally
            {
                autoResetEvent.Set();
            }

            await Task.Delay(1000);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cts.Cancel();

        await client.Stop(WebSocketCloseStatus.NormalClosure, "Closing");

        if (runner != null)
        {
            await runner;
        }
    }
}
