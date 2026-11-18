using NickFinancas.Domain.Spaces;

namespace NickFinancas.Domain.Tests.Spaces;

public sealed class FinancialSpaceTests
{
    [Fact]
    public void SharedSpaceAllowsAnotherMember()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var space = new FinancialSpace(Guid.NewGuid(), "Casa & Futuro", FinancialSpaceKind.Shared, ownerId);

        space.AddMember(memberId);

        Assert.Contains(space.Members, member => member.UserId == memberId);
    }

    [Fact]
    public void IndividualSpaceRejectsAnotherMember()
    {
        var space = new FinancialSpace(
            Guid.NewGuid(),
            "Individual do Alex",
            FinancialSpaceKind.Individual,
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => space.AddMember(Guid.NewGuid()));
    }
}

