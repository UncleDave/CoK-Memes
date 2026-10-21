using System.Text.Json;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace ChampionsOfKhazad.Bot.Tests;

public class MemberLoreIdentityTests
{
    private const ulong DiscordUserId = 1234567890123456789;

    [Theory]
    [InlineData(1UL)]
    [InlineData(DiscordUserId)]
    [InlineData((ulong)long.MaxValue)]
    public void BindingRoundTripsAcrossModelsSnapshotsDocumentsAndRevisionHistoryWithoutBecomingLoreText(ulong discordUserId)
    {
        var member = Member() with { DiscordUserId = discordUserId };
        var snapshot = LoreEntrySnapshot.FromLore(member);
        var revision = new LoreRevision(
            "bound",
            new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc),
            "admin-dm",
            1,
            "update",
            snapshot with
            {
                DiscordUserId = null,
            },
            snapshot
        );
        var document = LoreDocument.FromSnapshot(snapshot) with { Revision = revision.Id, History = [revision] };
        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<LoreDocument>(bson);

        Assert.Equal(discordUserId, snapshot.DiscordUserId);
        Assert.Equal((long)discordUserId, bson[Field<LoreDocument>(nameof(LoreDocument.DiscordUserId))].AsInt64);
        var afterBson = bson[Field<LoreDocument>(nameof(LoreDocument.History))][0][Field<LoreRevision>(nameof(LoreRevision.After))];
        Assert.Equal((long)discordUserId, afterBson[Field<LoreEntrySnapshot>(nameof(LoreEntrySnapshot.DiscordUserId))].AsInt64);
        Assert.Equivalent(member, restored.ToModel());
        Assert.Equivalent(document, restored);
        Assert.Null(restored.History[0].Before!.DiscordUserId);
        Assert.Equal(discordUserId, restored.History[0].After!.DiscordUserId);
        Assert.Equivalent(snapshot, restored.ToEditState().Entry);
        Assert.Equal(Member().ToString(), member.ToString());
        Assert.Equal(Member().ToString(), document.Content);
        Assert.DoesNotContain(discordUserId.ToString(), document.Content);
    }

    [Theory]
    [InlineData(false, DiscordUserId)]
    [InlineData(true, DiscordUserId)]
    [InlineData(false, (ulong)long.MaxValue)]
    [InlineData(true, (ulong)long.MaxValue)]
    public void MemberJsonReadsNumericAndStringBindingsAndAlwaysWritesPrecisionSafeStrings(bool quoted, ulong discordUserId)
    {
        var member = Member() with { DiscordUserId = discordUserId };
        var json = JsonSerializer.Serialize(member, JsonSerializerOptions.Web);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, parsed.RootElement.GetProperty("discordUserId").ValueKind);
        Assert.Equal(discordUserId.ToString(), parsed.RootElement.GetProperty("discordUserId").GetString());

        var input = quoted ? json : json.Replace($"\"discordUserId\":\"{discordUserId}\"", $"\"discordUserId\":{discordUserId}");
        Assert.Equivalent(member, JsonSerializer.Deserialize<MemberLore>(input, JsonSerializerOptions.Web));
        Assert.Null(
            JsonSerializer
                .Deserialize<MemberLore>(JsonSerializer.Serialize(Member(), JsonSerializerOptions.Web), JsonSerializerOptions.Web)!
                .DiscordUserId
        );
    }

    [Fact]
    public void RuntimeMemberContractsKeepStringBindingsWhenTheDeclaredTypeIsILore()
    {
        ILore member = Member() with { DiscordUserId = DiscordUserId };

        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize((object)member, JsonSerializerOptions.Web));
        Assert.Equal(DiscordUserId.ToString(), parsed.RootElement.GetProperty("discordUserId").GetString());
        Assert.Equal(member.Name, parsed.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void LegacyMemberDocumentsAndOldSnapshotsRemainUnboundWithoutMetadataMigration()
    {
        var snapshot = LoreEntrySnapshot.FromLore(Member());
        var revision = new LoreRevision("old", DateTime.UtcNow, "lore-store", null, "update", snapshot, snapshot);
        var bson = (LoreDocument.FromSnapshot(snapshot) with { History = [revision] }).ToBsonDocument();
        foreach (var property in new[] { nameof(LoreDocument.DiscordUserId), nameof(LoreDocument.Revision), nameof(LoreDocument.Deleted) })
            bson.Remove(Field<LoreDocument>(property));
        var history = bson[Field<LoreDocument>(nameof(LoreDocument.History))].AsBsonArray;
        foreach (var property in new[] { nameof(LoreRevision.Before), nameof(LoreRevision.After) })
            history[0][Field<LoreRevision>(property)].AsBsonDocument.Remove(Field<LoreEntrySnapshot>(nameof(LoreEntrySnapshot.DiscordUserId)));

        var restored = BsonSerializer.Deserialize<LoreDocument>(bson);
        Assert.Null(restored.DiscordUserId);
        Assert.Null(restored.Revision);
        Assert.False(restored.Deleted);
        Assert.Equivalent(Member(), restored.ToModel());
        Assert.Null(restored.ToEditState().Entry!.DiscordUserId);
        Assert.Null(restored.History[0].Before!.DiscordUserId);
        Assert.Null(restored.History[0].After!.DiscordUserId);
        Assert.Equivalent(Member(), LoreDocument.FromSnapshot(restored.History[0].Before!).ToModel());

        bson.Remove(Field<LoreDocument>(nameof(LoreDocument.History)));
        Assert.Empty(BsonSerializer.Deserialize<LoreDocument>(bson).History);
    }

    private static MemberLore Member() => new("Grim", "they", "UK", "Shaman", "Elevator joke") { Aliases = ["Grimbles"], Roles = ["Raider"] };

    private static string Field<T>(string property) => BsonClassMap.LookupClassMap(typeof(T)).GetMemberMap(property).ElementName;
}
