using System.Net;
using Audibly.Core.Metadata;
using Xunit;

namespace Audibly.Audio.Tests.Metadata;

public sealed class AudibleMetadataClientTests
{
    private const string Empty = "{\"products\":[]}";

    [Fact]
    public async Task Search_queries_both_regions_de_first()
    {
        var handler = new FakeHandler(new()
        {
            ["api.audible.de"] = _ => Ok(Fixtures.Read("search-de.json")),
            ["api.audible.com"] = _ => Ok(Empty)
        });

        var hits = await new AudibleMetadataClient(new HttpClient(handler)).SearchAsync("Panem Collins", default);

        Assert.Equal("de", hits[0].Region);
        Assert.Contains(handler.Requests, uri => uri.Host == "api.audible.de");
        Assert.Contains(handler.Requests, uri => uri.Host == "api.audible.com");
        Assert.All(handler.Requests, uri =>
        {
            Assert.Contains("keywords=Panem%20Collins", uri.Query);
            Assert.Contains("num_results=10", uri.Query);
        });
    }

    [Fact]
    public async Task Search_survives_one_region_failing()
    {
        var handler = new FakeHandler(new()
        {
            ["api.audible.de"] = _ => Ok(Fixtures.Read("search-de.json")),
            ["api.audible.com"] = _ => throw new TaskCanceledException("timeout")
        });

        var hits = await new AudibleMetadataClient(new HttpClient(handler)).SearchAsync("Panem", default);

        Assert.NotEmpty(hits);
    }

    [Fact]
    public async Task Search_throws_when_both_fail()
    {
        var handler = new FakeHandler(new()
        {
            ["api.audible.de"] = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            ["api.audible.com"] = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        });

        await Assert.ThrowsAsync<MetadataLookupException>(() =>
            new AudibleMetadataClient(new HttpClient(handler)).SearchAsync("Panem", default));
    }

    [Fact]
    public async Task Search_with_blank_keywords_does_not_call_network()
    {
        var handler = new FakeHandler(new());

        var hits = await new AudibleMetadataClient(new HttpClient(handler)).SearchAsync("  ", default);

        Assert.Empty(hits);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Details_uses_region_of_hit()
    {
        var handler = new FakeHandler(new() { ["api.audnex.us"] = _ => Ok(Fixtures.Read("audnexus-panem2.json")) });
        var hit = new AudibleHit("B008CND4RS", "us", "", "", "", null, null, 1, null, null, null);

        var details = await new AudibleMetadataClient(new HttpClient(handler)).GetDetailsAsync(hit, default);

        Assert.Equal("https://api.audnex.us/books/B008CND4RS?region=us", handler.Requests.Single().ToString());
        Assert.Equal("Maria Koschny", details.Narrator);
    }

    [Fact]
    public async Task Details_404_throws_lookup_exception()
    {
        var handler = new FakeHandler(new() { ["api.audnex.us"] = _ => new HttpResponseMessage(HttpStatusCode.NotFound) });
        var hit = new AudibleHit("X", "de", "", "", "", null, null, 1, null, null, null);

        await Assert.ThrowsAsync<MetadataLookupException>(() =>
            new AudibleMetadataClient(new HttpClient(handler)).GetDetailsAsync(hit, default));
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class FakeHandler(Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> byHost)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests) Requests.Add(request.RequestUri!);
            return Task.FromResult(byHost[request.RequestUri!.Host](request));
        }
    }
}
