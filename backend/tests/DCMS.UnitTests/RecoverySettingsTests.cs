using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using Xunit;

namespace DCMS.UnitTests;

public sealed class RecoverySettingsTests
{
    [Fact]
    public void DefaultsAreDevelopmentPlaceholdersAndPassValidation()
    {
        var settings = new RecoverySettings();
        Assert.True(settings.IsValid());
        Assert.Equal(15, settings.TokenLifetimeMinutes); // GBR-082 baseline: 15 minutes
        Assert.Equal(2, settings.ResendCooldownMinutes);
        Assert.Equal(3, settings.MaxResends);
        Assert.Equal(5, settings.MaxVerifyAttempts);
        Assert.Equal(5, settings.RateLimitPerAccountPerHour);
        Assert.Equal(20, settings.RateLimitPerIpPerHour);
        Assert.True(Enum.IsDefined(settings.Channel));
    }

    [Fact]
    public void ValidateThrowsForEveryOutOfRangeValue()
    {
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { TokenLifetimeMinutes = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { TokenLifetimeMinutes = 2000 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { ResendCooldownMinutes = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { MaxResends = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { MaxVerifyAttempts = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { RateLimitPerAccountPerHour = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { RateLimitPerIpPerHour = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { CodeLength = 3 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { CodeLength = 20 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new RecoverySettings { Channel = (RecoveryChannel)99 }.Validate());
    }

    [Fact]
    public void OnlyTheLinkChannelUsesTheLinkFlow()
    {
        Assert.True(new RecoverySettings { Channel = RecoveryChannel.Link }.IsLinkChannel);
        Assert.False(new RecoverySettings { Channel = RecoveryChannel.Email }.IsLinkChannel);
        Assert.False(new RecoverySettings { Channel = RecoveryChannel.Phone }.IsLinkChannel);
    }

    [Fact]
    public void ValidateAcceptsABoundaryConfiguration()
    {
        var settings = new RecoverySettings
        {
            TokenLifetimeMinutes = 1440, ResendCooldownMinutes = 0, MaxResends = 20,
            MaxVerifyAttempts = 1, RateLimitPerAccountPerHour = 1000, RateLimitPerIpPerHour = 10000,
            CodeLength = 4, Channel = RecoveryChannel.Phone
        };
        settings.Validate();
        Assert.True(settings.IsValid());
    }
}
