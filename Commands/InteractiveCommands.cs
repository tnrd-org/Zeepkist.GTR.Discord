//
//  SPDX-FileName: InteractiveCommands.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: MIT
//

using System.ComponentModel;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Discord.Pagination.Extensions;
using Remora.Rest.Core;
using Remora.Results;

namespace TNRD.Zeepkist.GTR.Discord.Commands;

/// <summary>
/// Defines commands with various interactivity types.
/// </summary>
// [Group("interactive")]
public class InteractiveCommands : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionAPI;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveCommands"/> class.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="feedback">The feedback service.</param>
    /// <param name="interactionAPI">The interaction API.</param>
    public InteractiveCommands(
        ICommandContext context,
        FeedbackService feedback,
        IDiscordRestInteractionAPI interactionAPI
    )
    {
        _context = context;
        _feedback = feedback;
        _interactionAPI = interactionAPI;
    }

    [Command("start")]
    public async Task<IResult> StartAsync([ChannelTypes(ChannelType.GuildText)] IChannel channel)
    {
        Settings.Channel = channel;
        return await _feedback.SendContextualSuccessAsync("Started");
    }

    [Command("stop")]
    public async Task<IResult> StopAsync()
    {
        Settings.Channel = null;
        return await _feedback.SendContextualSuccessAsync("Stopped");
    }
}
