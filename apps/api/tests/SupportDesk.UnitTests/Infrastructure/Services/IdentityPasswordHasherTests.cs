using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Hash_NeverContainsThePassword() =>
        Assert.DoesNotContain("correct horse", _hasher.Hash("correct horse"));

    [Fact]
    public void Hash_IsSalted_SoTheSamePasswordHashesDifferentlyEachTime() =>
        Assert.NotEqual(_hasher.Hash("correct horse"), _hasher.Hash("correct horse"));

    [Fact]
    public void Verify_AcceptsTheRightPassword() =>
        Assert.True(_hasher.Verify(_hasher.Hash("correct horse"), "correct horse"));

    [Theory]
    [InlineData("correct hors")]
    [InlineData("Correct horse")]
    [InlineData("")]
    public void Verify_RejectsAnyOtherPassword(string attempt) =>
        Assert.False(_hasher.Verify(_hasher.Hash("correct horse"), attempt));

    [Fact]
    public void Verify_WithoutAHash_IsAlwaysFalse() =>
        Assert.False(_hasher.Verify(null, "anything"));
}
