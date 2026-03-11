using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class UserService(
    IUserRepository userRepository,
    IBotRepository botRepository)
{
    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await userRepository.GetAllAsync(ct);
        return users.Select(MapToDto);
    }

    public async Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        var bot = await botRepository.GetByIdAsync(dto.BotId, ct)
            ?? throw new InvalidOperationException($"Bot '{dto.BotId}' not found.");

        if (!bot.IsActive)
            throw new InvalidOperationException($"Bot '{bot.BotName}' is not active.");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            WhatsAppNumber = dto.WhatsAppNumber,
            BotId = dto.BotId,
            BotType = bot.BotType,
            Role = UserRole.User
        };

        await userRepository.AddAsync(user, ct);

        // Google Sheets integration removed: skip automatic sheet creation

        return MapToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(string id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null) return null;

        if (dto.Name is not null) user.Name = dto.Name;
        if (dto.Email is not null) user.Email = dto.Email;
        if (dto.WhatsAppNumber is not null) user.WhatsAppNumber = dto.WhatsAppNumber;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await userRepository.UpdateAsync(user, ct);
        return MapToDto(user);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null) return false;
        await userRepository.DeleteAsync(id, ct);
        return true;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;
        return user;
    }

    private static UserDto MapToDto(User u) => new(
        u.Id, u.Name, u.Email, u.WhatsAppNumber,
        u.Role, u.BotType, u.BotId, u.CreatedAt);
}
