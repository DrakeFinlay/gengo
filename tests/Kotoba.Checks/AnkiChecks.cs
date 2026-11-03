using System.Net;
using System.Text;
using System.Text.Json;
using Kotoba.Infrastructure;

static class AnkiChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var transport = new AnkiFixture(); using var client = new AnkiKanjiClient(transport);
        var result = await client.Find("床", new());
        check(result.Notes.Length == 1 && result.Notes[0].Meaning == "bed" && result.Profile == "Test profile", "kanji lookup uses exact deck note meaning; excludes 床屋");
        check(transport.Actions.All(a => a is "getActiveProfile" or "findNotes" or "notesInfo"), "Anki integration issues only read operations");
        check(transport.LastQuery == "\"deck:漢字\" \"Front:*床*\"", "Anki candidate search includes HTML-wrapped fields, scoped to configured deck");
        var reads = transport.NoteReads; await client.Find("床", new());
        check(transport.NoteReads == reads, "same-profile kanji results use bounded cache");
        transport.Profile = "Other profile"; await client.Find("床", new());
        check(transport.NoteReads == reads + 1, "kanji cache is isolated by active Anki profile");
        client.ClearCache(); await client.Find("床", new()); check(transport.NoteReads == reads + 2, "refresh clears cached kanji meanings");
        transport.Character = "分"; transport.Meaning = "part";
        var wrapped = await client.Find("分", new());
        check(wrapped.Notes.Length == 1 && wrapped.DisplayMeaning == "part", "HTML-wrapped 分 is found and compound 分解 is excluded");
        transport.Empty = true;
        var missing = await client.Find("龘", new()); check(missing.Notes.Length == 0 && missing.DisplayMeaning == "unknown", "missing kanji has an explicit unknown display result");
        transport.Empty = false; transport.Character = "龘"; transport.Meaning = "dragons";
        check((await client.Find("龘", new())).DisplayMeaning == "dragons", "a missing-card result is not cached after the card is added");
        check(AnkiKanjiClient.CleanField("<ruby>床<rt>ゆか</rt></ruby>") == "床", "kanji matching strips ruby readings");
        check(AnkiKanjiClient.CleanField("<div>bed &amp; floor</div><script>bad()</script>[sound:x.mp3]") == "bed & floor", "Anki HTML is cleaned to plain text without executing content");
        try { await client.Find("床屋", new()); check(false, "multi-character expression rejected for kanji lookup"); } catch (ArgumentException) { check(true, "multi-character expression rejected for kanji lookup"); }
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await client.Find("床", new(), canceled.Token); check(false, "kanji request cancellation"); } catch (OperationCanceledException) { check(true, "kanji request cancellation"); }
        using var failed = new AnkiKanjiClient(new BrokenFixture());
        try { await failed.Find("床", new()); check(false, "unavailable Anki reports failure without fallback meanings"); } catch (HttpRequestException) { check(true, "unavailable Anki reports failure without fallback meanings"); }
    }
    private sealed class AnkiFixture : HttpMessageHandler
    {
        public string Profile = "Test profile";
        public readonly List<string> Actions = [];
        public string LastQuery = "";
        public int NoteReads;
        public string Character = "床";
        public string Meaning = "bed";
        public bool Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri!.Host != "127.0.0.1" || request.RequestUri.Port != 8765) throw new Exception("Non-loopback request");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var action = json.RootElement.GetProperty("action").GetString()!; Actions.Add(action);
            object result;
            if (action == "getActiveProfile") result = Profile;
            else if (action == "findNotes") { LastQuery = json.RootElement.GetProperty("params").GetProperty("query").GetString()!; result = Empty ? Array.Empty<long>() : new long[] { 1, 2 }; }
            else if (action == "notesInfo") { NoteReads++; result = new[] { new { noteId = 1L, fields = new { Front = new { value = "<div>" + Character + "</div>" }, Back = new { value = Meaning } } }, new { noteId = 2L, fields = new { Front = new { value = Character + "解" }, Back = new { value = "compound" } } } }; }
            else throw new Exception("Unexpected action: " + action);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { result, error = (string?)null }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class BrokenFixture : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new HttpRequestException("Anki is closed");
    }
}
