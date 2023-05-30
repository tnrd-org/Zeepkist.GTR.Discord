using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Results;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;

namespace TNRD.Zeepkist.GTR.Discord;

internal class DiscordMessageSender
{
    private readonly IDiscordRestChannelAPI channelApi;
    private readonly HttpClient httpClient;
    private readonly GTRContext context;
    private readonly ILogger<DiscordMessageSender> logger;

    private readonly HashSet<int> sentRecords;

    public DiscordMessageSender(
        IDiscordRestChannelAPI channelApi,
        HttpClient httpClient,
        GTRContext context,
        ILogger<DiscordMessageSender> logger
    )
    {
        this.channelApi = channelApi;
        this.httpClient = httpClient;
        this.context = context;
        this.logger = logger;
        sentRecords = new HashSet<int>();
    }

    public async void SendMessage(RecordId? recordId)
    {
        try
        {
            if (Settings.Channel == null)
                return;

            if (recordId == null)
                return;

            if (sentRecords.Contains(recordId.Id))
                return;

            Record? record = await context.Records.AsNoTracking()
                .Where(x => x.Id == recordId.Id)
                .FirstOrDefaultAsync();

            if (record == null)
                return;

            if (!record.IsWr)
                return;

            if (string.IsNullOrEmpty(record.ScreenshotUrl))
                return;

            sentRecords.Add(record.Id);

            string username = await GetUsername(record);
            (string? level, string? thumbnailUrl) = await GetTrackString(record);

            EmbedBuilder builder = new EmbedBuilder();

            builder.WithTitle($"{username} has set a new world record!");
            builder.WithAuthor("Zeepkist GTR",
                "https://zeepkist-gtr.com",
                "https://cdn.discordapp.com/avatars/1106610501674348554/b60163ed3528ab1864fa4466830b2e0b.webp?size=128");
            builder.WithThumbnailUrl(GetThumbnailUrl(thumbnailUrl));
            builder.WithImageUrl(GetScreenshotUrl(record));

            builder.AddField("Level", level);
            builder.AddField("Time", GetFormattedTime(record.Time!.Value));
            builder.AddField("Splits",
                string.Join(", ", record.Splits?.Select(x => GetFormattedTime(x)) ?? Array.Empty<string>()));

            Result<Embed> embed = builder.Build();
            if (!embed.IsSuccess)
            {
                logger.LogError("Failed to build embed: {Error}", embed.ToString());
                return;
            }

            Result<IMessage> result = await channelApi.CreateMessageAsync(Settings.Channel.ID,
                embeds: new List<IEmbed>()
                {
                    embed.Entity
                });

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to send message to Discord: {Error}", result.ToString());
            }
            else
            {
                logger.LogInformation("Sent message to Discord");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send message to Discord");
        }
    }

    private async Task<string> GetUsername(Record record)
    {
        string json = await httpClient.GetStringAsync($"https://api.dev.zeepkist-gtr.com/users/{record.User}");
        UserResponseModel? user = JsonConvert.DeserializeObject<UserResponseModel>(json);
        return user?.SteamName ?? "Unknown";
    }

    private async Task<(string level, string thumbnailUrl)> GetTrackString(Record record)
    {
        string json = await httpClient.GetStringAsync($"https://api.dev.zeepkist-gtr.com/levels/{record.Level}");
        LevelResponseModel? level = JsonConvert.DeserializeObject<LevelResponseModel>(json);
        return (level == null ? "Unknown" : $"{level.Name} by {level.Author}", level.ThumbnailUrl);
    }

    private static string GetFormattedTime(double time)
    {
        if (time < 0.0)
            time = 0.0;
        TimeSpan timeSpan = TimeSpan.FromSeconds(time);
        return timeSpan.Hours > 0
            ? string.Format(CultureInfo.InvariantCulture,
                "{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                timeSpan.Hours,
                timeSpan.Minutes,
                timeSpan.Seconds,
                timeSpan.Milliseconds)
            : string.Format(CultureInfo.InvariantCulture,
                "{1:D2}:{2:D2}.{3:D3}",
                timeSpan.Hours,
                timeSpan.Minutes,
                timeSpan.Seconds,
                timeSpan.Milliseconds);
    }

    private static string GetThumbnailUrl(string url)
    {
        if (url.StartsWith("https://storage.googleapis.com/zeepkist-gtr/thumbnails/"))
            return url;

        string newUrl = url.Replace(
            "https://storage.googleapis.com/download/storage/v1/b/zeepkist-gtr/o/",
            "https://storage.googleapis.com/zeepkist-gtr/");
        return newUrl;
    }

    private static string GetScreenshotUrl(Record record)
    {
        if (record.ScreenshotUrl!.StartsWith("https://storage.googleapis.com/zeepkist-gtr/screenshots/"))
            return record.ScreenshotUrl;

        string newUrl = record.ScreenshotUrl.Replace(
            "https://storage.googleapis.com/download/storage/v1/b/zeepkist-gtr/o/",
            "https://storage.googleapis.com/zeepkist-gtr/");
        return newUrl;
    }
}
