using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class NormalUserChannelAccessTests
{
    [Fact]
    public void EveryoneReadableChannelIsAllowed()
    {
        var result = NormalUserChannelAccess.CanRead([ReadPermissions(), new GuildPermissions()], null, []);

        Assert.True(result);
    }

    [Fact]
    public void NormalRoleCanGrantAccessDeniedToEveryone()
    {
        var everyoneOverwrite = new OverwritePermissions(viewChannel: PermValue.Deny, readMessageHistory: PermValue.Deny);
        var normalRoleOverwrite = new OverwritePermissions(viewChannel: PermValue.Allow, readMessageHistory: PermValue.Allow);

        var result = NormalUserChannelAccess.CanRead([new GuildPermissions(), new GuildPermissions()], everyoneOverwrite, [normalRoleOverwrite]);

        Assert.True(result);
    }

    [Fact]
    public void NormalRoleBasePermissionsAllowChannelWithoutOverwrites()
    {
        var result = NormalUserChannelAccess.CanRead([new GuildPermissions(), ReadPermissions()], null, []);

        Assert.True(result);
    }

    [Fact]
    public void EveryoneDenialRequiresNormalRoleAllowance()
    {
        var everyoneOverwrite = new OverwritePermissions(viewChannel: PermValue.Deny, readMessageHistory: PermValue.Deny);

        var result = NormalUserChannelAccess.CanRead([ReadPermissions(), new GuildPermissions()], everyoneOverwrite, []);

        Assert.False(result);
    }

    [Fact]
    public void NormalRoleDenialOverridesEveryoneAccess()
    {
        var normalRoleOverwrite = new OverwritePermissions(viewChannel: PermValue.Deny);

        var result = NormalUserChannelAccess.CanRead([ReadPermissions(), new GuildPermissions()], null, [normalRoleOverwrite]);

        Assert.False(result);
    }

    [Fact]
    public void MissingHistoryPermissionIsRejected()
    {
        var result = NormalUserChannelAccess.CanRead([new GuildPermissions(viewChannel: true), new GuildPermissions()], null, []);

        Assert.False(result);
    }

    [Fact]
    public void AllowFromOneRoleWinsOverDenialFromAnotherRole()
    {
        var deny = new OverwritePermissions(viewChannel: PermValue.Deny, readMessageHistory: PermValue.Deny);
        var allow = new OverwritePermissions(viewChannel: PermValue.Allow, readMessageHistory: PermValue.Allow);

        var result = NormalUserChannelAccess.CanRead([new GuildPermissions(), new GuildPermissions(), new GuildPermissions()], null, [deny, allow]);

        Assert.True(result);
    }

    private static GuildPermissions ReadPermissions() => new(viewChannel: true, readMessageHistory: true);
}
