using NickFinancas.Domain.Common;

namespace NickFinancas.Domain.Spaces;

public sealed class FinancialSpace : Entity
{
    private readonly List<FinancialSpaceMember> _members = [];

    public FinancialSpace(Guid id, string name, FinancialSpaceKind kind, Guid ownerId) : base(id)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("O proprietario e obrigatorio.", nameof(ownerId));
        }

        Name = RequireName(name);
        Kind = kind;
        _members.Add(new FinancialSpaceMember(ownerId, FinancialSpaceRole.Administrator));
    }

    public string Name { get; private set; }

    public FinancialSpaceKind Kind { get; }

    public IReadOnlyCollection<FinancialSpaceMember> Members => _members.AsReadOnly();

    public void AddMember(Guid userId, FinancialSpaceRole role = FinancialSpaceRole.Member)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O usuario e obrigatorio.", nameof(userId));
        }

        if (Kind == FinancialSpaceKind.Individual)
        {
            throw new InvalidOperationException("Um espaco individual nao aceita outros membros.");
        }

        if (_members.Any(member => member.UserId == userId))
        {
            return;
        }

        _members.Add(new FinancialSpaceMember(userId, role));
    }

    private static string RequireName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O nome do espaco e obrigatorio.", nameof(value));
        }

        return value.Trim();
    }
}

