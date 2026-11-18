using NickFinancas.Domain.Common;

namespace NickFinancas.Domain.Users;

public sealed class User : Entity
{
    public User(Guid id, string displayName) : base(id)
    {
        DisplayName = RequireName(displayName);
    }

    public string DisplayName { get; private set; }

    private static string RequireName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O nome do usuario e obrigatorio.", nameof(value));
        }

        return value.Trim();
    }
}

