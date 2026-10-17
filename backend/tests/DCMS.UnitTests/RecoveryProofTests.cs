using DCMS.Domain.Features.Authentication;
using Xunit;

namespace DCMS.UnitTests;

public sealed class RecoveryProofTests
{
    [Fact]
    public void GeneratedCodeHasTheConfiguredLengthAndIsNumeric()
    {
        foreach (var length in new[] { 4, 6, 8, 12 })
        {
            var code = RecoveryProof.GenerateCode(length);
            Assert.Equal(length, code.Length);
            Assert.True(RecoveryProof.IsWellFormedCode(code, length), $"Code {code} is not a valid {length}-digit proof.");
        }
    }

    [Fact]
    public void GeneratedCodesAreNotRepeatedAcrossManyDraws()
    {
        // Cryptographic randomness must not collapse onto a single value.
        var codes = Enumerable.Range(0, 200).Select(_ => RecoveryProof.GenerateCode(6)).ToArray();
        Assert.True(codes.Distinct().Count() > 190, "Generated codes look non-random.");
    }

    [Fact]
    public void HashIsDeterministicButDifferentForDifferentProofs()
    {
        const string code = "123456";
        Assert.Equal(RecoveryProof.Hash(code), RecoveryProof.Hash(code));
        Assert.NotEqual(RecoveryProof.Hash(code), RecoveryProof.Hash("123457"));
    }

    [Fact]
    public void HashIsStoredAsUpperCaseHexOfSha256()
    {
        var hash = RecoveryProof.Hash("abc");
        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.True(char.IsAsciiHexDigit(character) && !char.IsLower(character)));
    }

    [Fact]
    public void MatchesAcceptsTheRightProofAndRejectsEverythingElse()
    {
        var stored = RecoveryProof.Hash("654321");
        Assert.True(RecoveryProof.Matches("654321", stored));
        Assert.False(RecoveryProof.Matches("654320", stored));
        Assert.False(RecoveryProof.Matches("", stored));
        Assert.False(RecoveryProof.Matches("654321", ""));
        Assert.False(RecoveryProof.Matches(null, stored));
        Assert.False(RecoveryProof.Matches("654321", null));
    }

    [Fact]
    public void MatchesIsCaseSensitiveForNonNumericProofs()
    {
        var stored = RecoveryProof.Hash("AbCdEf");
        Assert.True(RecoveryProof.Matches("AbCdEf", stored));
        Assert.False(RecoveryProof.Matches("abcdef", stored));
    }

    [Fact]
    public void GrantIsLongRandomHex()
    {
        var grants = Enumerable.Range(0, 50).Select(_ => RecoveryProof.GenerateGrant()).ToArray();
        Assert.All(grants, grant =>
        {
            Assert.Equal(64, grant.Length);
            Assert.All(grant, character => Assert.True(char.IsAsciiHexDigit(character) && !char.IsLower(character)));
        });
        Assert.Equal(50, grants.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(13)]
    [InlineData(-1)]
    public void GenerateCodeRejectsUnsupportedLengths(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecoveryProof.GenerateCode(length));
    }

    [Fact]
    public void WellFormedCodeRejectsNonNumericAndWrongLength()
    {
        Assert.False(RecoveryProof.IsWellFormedCode("12a456", 6));
        Assert.False(RecoveryProof.IsWellFormedCode("12345", 6));
        Assert.False(RecoveryProof.IsWellFormedCode("", 6));
        Assert.False(RecoveryProof.IsWellFormedCode(null, 6));
    }
}
