using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class BotService(IBotRepository botRepository)
{
    public async Task<IEnumerable<BotDto>> GetAllAsync(CancellationToken ct = default)
    {
        var bots = await botRepository.GetAllAsync(ct);
        return bots.Select(MapToDto);
    }

    public async Task<BotDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var bot = await botRepository.GetByIdAsync(id, ct);
        return bot is null ? null : MapToDto(bot);
    }

    public async Task<BotDto?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        var bot = await botRepository.GetByNumberAsync(number, ct);
        return bot is null ? null : MapToDto(bot);
    }

    public async Task<BotDto> CreateAsync(CreateBotDto dto, CancellationToken ct = default)
    {
        var bot = new Bot
        {
            BotNumber = dto.BotNumber,
            BotName = dto.BotName,
            Prompt = dto.Prompt,
            Soul = dto.Soul,
            IsAgent = dto.IsAgent,
            Description = dto.Description
        };
        await botRepository.AddAsync(bot, ct);
        return MapToDto(bot);
    }

    public async Task<bool> ToggleActiveAsync(string id, CancellationToken ct = default)
    {
        var bot = await botRepository.GetByIdAsync(id, ct);
        if (bot is null) return false;
        bot.IsActive = !bot.IsActive;
        bot.UpdatedAt = DateTimeOffset.UtcNow;
        await botRepository.UpdateAsync(bot, ct);
        return true;
    }

    public async Task<BotDto?> UpdateAsync(string id, UpdateBotDto dto, CancellationToken ct = default)
    {
        var bot = await botRepository.GetByIdAsync(id, ct);
        if (bot is null) return null;

        if (dto.BotNumber is not null) bot.BotNumber = dto.BotNumber;
        if (dto.BotName is not null) bot.BotName = dto.BotName;
        if (dto.Prompt is not null) bot.Prompt = dto.Prompt;
        if (dto.Soul is not null) bot.Soul = dto.Soul;
        if (dto.IsAgent is not null) bot.IsAgent = dto.IsAgent.Value;
        if (dto.Description is not null) bot.Description = dto.Description;

        bot.UpdatedAt = DateTimeOffset.UtcNow;
        await botRepository.UpdateAsync(bot, ct);
        return MapToDto(bot);
    }

    private static BotDto MapToDto(Bot b) => new(
        b.Id, b.BotNumber, b.BotName,
        b.Prompt, b.Soul, b.IsAgent, b.Description,
        b.IsActive, b.CreatedAt);
}
