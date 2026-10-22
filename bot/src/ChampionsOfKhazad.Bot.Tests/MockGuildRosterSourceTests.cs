using System.Text.Json.Nodes;
using ChampionsOfKhazad.Bot.Roster;

namespace ChampionsOfKhazad.Bot.Tests;

public class MockGuildRosterSourceTests
{
    [Fact]
    public async Task MockReadsClassicStyleRosterAndSeparateRetailStyleProfessionDocuments()
    {
        using var files = new FixtureFiles();
        var source = new MockGuildRosterSource(files.RosterFile);
        var roster = await source.GetRosterAsync(TestContext.Current.CancellationToken);
        Assert.True(source.IsMock);
        Assert.True(source.HasSeparateProfessionRequests);
        Assert.Equal("Mock Chippy", roster.GuildName);
        Assert.Equal("Borin Ironhide", roster.Characters[0].FullName);
        Assert.Equal("1001", roster.Characters[0].Id);
        Assert.Equal("Élodie", roster.Characters[1].FirstName);
        var professions = await source.GetProfessionsAsync(roster.Characters[0], TestContext.Current.CancellationToken);
        Assert.True(professions!.Complete);
        Assert.Equal(["Blacksmithing", "Cooking"], professions.Skills.Select(skill => skill.Name).ToArray());
        Assert.Equal("1", professions.Skills[0].Tier);
        Assert.Equal(74, professions.Skills[1].Skill);
        Assert.Equal(DateTimeKind.Utc, professions.ObservedAtUtc.Kind);
    }

    [Fact]
    public async Task EditingFixtureChangesNextResponseAndMissingOrMismatchedDocumentsAreNotEmptyProfessions()
    {
        using var files = new FixtureFiles();
        var source = new MockGuildRosterSource(files.RosterFile);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(files.RosterFile, TestContext.Current.CancellationToken))!;
        json["members"]![0]!["character"]!["level"] = 20;
        await File.WriteAllTextAsync(files.RosterFile, json.ToJsonString(), TestContext.Current.CancellationToken);
        var roster = await source.GetRosterAsync(TestContext.Current.CancellationToken);
        Assert.Equal(20, roster.Characters[0].Level);
        var path = Path.Combine(files.Directory, "professions", "1001.json");
        await File.WriteAllTextAsync(path, "{\"character\":{\"id\":999},\"primaries\":[],\"secondaries\":[]}", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetProfessionsAsync(roster.Characters[0], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            source.GetProfessionsAsync(roster.Characters[0] with { Id = "12345" }, TestContext.Current.CancellationToken)
        );
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.GetProfessionsAsync(roster.Characters[0] with { Id = "../private" }, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task MissingProfessionSectionIsPartialWhileExplicitEmptySectionsAreKnownEmpty()
    {
        using var files = new FixtureFiles();
        var source = new MockGuildRosterSource(files.RosterFile);
        var member = (await source.GetRosterAsync(TestContext.Current.CancellationToken)).Characters[0];
        var path = Path.Combine(files.Directory, "professions", "1001.json");
        await File.WriteAllTextAsync(path, "{\"character\":{\"id\":1001},\"primaries\":[]}", TestContext.Current.CancellationToken);
        var partial = await source.GetProfessionsAsync(member, TestContext.Current.CancellationToken);
        Assert.False(partial!.Complete);
        Assert.Empty(partial.Skills);
        await File.WriteAllTextAsync(
            path,
            "{\"character\":{\"id\":1001},\"primaries\":[],\"secondaries\":[]}",
            TestContext.Current.CancellationToken
        );
        Assert.True((await source.GetProfessionsAsync(member, TestContext.Current.CancellationToken))!.Complete);
    }

    private sealed class FixtureFiles : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "opencode", "roster-" + Guid.NewGuid().ToString("N"));
        public string RosterFile => Path.Combine(Directory, "guild-roster.mock.json");

        public FixtureFiles()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ChampionsOfKhazad.Bot.slnx")))
                root = root.Parent;
            if (root is null)
                throw new InvalidOperationException("Cannot locate source fixtures.");
            var source = Path.Combine(root.FullName, "src", "ChampionsOfKhazad.Bot", "Fixtures");
            System.IO.Directory.CreateDirectory(Path.Combine(Directory, "professions"));
            File.Copy(Path.Combine(source, "guild-roster.mock.json"), RosterFile);
            foreach (var file in System.IO.Directory.GetFiles(Path.Combine(source, "professions")))
                File.Copy(file, Path.Combine(Directory, "professions", Path.GetFileName(file)));
        }

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
