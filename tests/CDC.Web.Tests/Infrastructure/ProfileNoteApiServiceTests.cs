using System.Net;
using System.Text;
using System.Text.Json;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileNoteApiServiceTests
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private sealed class StubHandler(HttpStatusCode statusCode, string json = "{}") : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Url { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Url = request.RequestUri?.PathAndQuery;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static ProfileSectionsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private static QuestionReferenceDto Reference(Guid sectionId, Guid questionId) =>
        new() { ProfileSectionId = sectionId, ProfileQuestionId = questionId };

    [Fact]
    public async Task UpdateProfileNotesAsync_PutsTheInsertsAndUpdates_AsTheApiExpectsThem()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var versionId = Guid.NewGuid();
        var noteTypeId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var insertId = Guid.NewGuid();
        var updateId = Guid.NewGuid();
        ProfileNoteInsert[] inserts = [new(insertId, "<p>New</p>", [Reference(sectionId, firstQuestionId)])];
        ProfileNoteUpdate[] updates = [new(updateId, "<p>Old</p>", RowVersion, [Reference(sectionId, secondQuestionId)], [Reference(sectionId, firstQuestionId)])];

        var result = await CreateService(handler).UpdateProfileNotesAsync(versionId, noteTypeId, inserts, updates);

        Assert.Equal(ProfileNoteChangeOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal("/api/profile-notes", handler.Url);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        Assert.Equal(versionId, root.GetProperty("profileVersionId").GetGuid());
        Assert.Equal(noteTypeId, root.GetProperty("noteTypeId").GetGuid());

        var insert = Assert.Single(root.GetProperty("inserts").EnumerateArray());
        Assert.Equal(insertId, insert.GetProperty("id").GetGuid());
        Assert.Equal("<p>New</p>", insert.GetProperty("noteText").GetString());
        var added = Assert.Single(insert.GetProperty("questionReferenceAdds").EnumerateArray());
        Assert.Equal(sectionId, added.GetProperty("profileSectionId").GetGuid());
        Assert.Equal(firstQuestionId, added.GetProperty("profileQuestionId").GetGuid());

        var update = Assert.Single(root.GetProperty("updates").EnumerateArray());
        Assert.Equal(updateId, update.GetProperty("id").GetGuid());
        Assert.Equal("AQIDBAUGBwg=", update.GetProperty("lastUpdated").GetString());
        var linked = Assert.Single(update.GetProperty("questionReferenceAdds").EnumerateArray());
        Assert.Equal(secondQuestionId, linked.GetProperty("profileQuestionId").GetGuid());
        var unlinked = Assert.Single(update.GetProperty("questionReferenceRemoves").EnumerateArray());
        Assert.Equal(firstQuestionId, unlinked.GetProperty("profileQuestionId").GetGuid());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, ProfileNoteChangeOutcome.Success)]
    [InlineData(HttpStatusCode.NoContent, ProfileNoteChangeOutcome.Success)]
    [InlineData(HttpStatusCode.BadRequest, ProfileNoteChangeOutcome.ValidationFailed)]
    [InlineData(HttpStatusCode.Conflict, ProfileNoteChangeOutcome.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError, ProfileNoteChangeOutcome.Error)]
    [InlineData(HttpStatusCode.Forbidden, ProfileNoteChangeOutcome.Error)]
    public async Task UpdateProfileNotesAsync_ClassifiesTheResponseStatus(HttpStatusCode statusCode, ProfileNoteChangeOutcome expected)
    {
        var result = await CreateService(new StubHandler(statusCode)).UpdateProfileNotesAsync(Guid.NewGuid(), Guid.NewGuid(), [], []);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == ProfileNoteChangeOutcome.Success, string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task UpdateProfileNotesAsync_ExplainsAConflictToTheUser()
    {
        var result = await CreateService(new StubHandler(HttpStatusCode.Conflict)).UpdateProfileNotesAsync(Guid.NewGuid(), Guid.NewGuid(), [], []);

        Assert.Equal("Another user has changed this note. Your change has not been saved; reopen the note and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetProfileQuestionInfoListAsync_ReadsTheSectionsQuestions()
    {
        var sectionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, $"[{{\"id\":\"{questionId}\",\"name\":\"Is it endemic?\",\"questionNumber\":3}}]");

        var questions = await CreateService(handler).GetProfileQuestionInfoListAsync(sectionId);

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal($"/api/profile-questions?profileSectionId={sectionId}", handler.Url);
        var question = Assert.Single(questions);
        Assert.Equal(questionId, question.Id);
        Assert.Equal("Is it endemic?", question.Name);
        Assert.Equal(3, question.QuestionNumber);
    }

    [Fact]
    public async Task GetProfileQuestionInfoListAsync_ReturnsAnEmptyList_ForANullBody()
    {
        var questions = await CreateService(new StubHandler(HttpStatusCode.OK, "null")).GetProfileQuestionInfoListAsync(Guid.NewGuid());

        Assert.Empty(questions);
    }

    [Fact]
    public async Task GetProfileQuestionInfoListAsync_Throws_WhenTheApiFails()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetProfileQuestionInfoListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfileNotesBySectionAsync_ReadsTheNotesWithTheirRowVersionAndQuestionLinks()
    {
        var noteId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var json = $"[{{\"id\":\"{noteId}\",\"noteText\":\"<p>x</p>\",\"lastUpdated\":\"AQIDBAUGBwg=\",\"questionReferences\":[{{\"profileSectionId\":\"{sectionId}\",\"profileQuestionId\":\"{questionId}\"}}]}}]";

        var notes = await CreateService(new StubHandler(HttpStatusCode.OK, json)).GetProfileNotesBySectionAsync(Guid.NewGuid(), sectionId, Guid.NewGuid());

        var note = Assert.Single(notes);
        Assert.Equal(noteId, note.Id);
        Assert.Equal(RowVersion, note.LastUpdated);
        Assert.Equal(questionId, Assert.Single(note.QuestionReferences).ProfileQuestionId);
    }
}
