using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SchoolAdmission.Tests;

[Collection("pg")]
public sealed class AdmissionApiTests(PostgresFixture pg)
{
    // 17-digit birth registration numbers: 20150000000000000 + n
    private static string Sid(int n) => (20150000000000000L + n).ToString();

    private static async Task<Guid> CreateClass(HttpClient http, int seats)
    {
        var res = await http.PostAsJsonAsync("/classes", new { name = $"Class {Guid.NewGuid():N}", totalSeats = seats });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Send(HttpClient http, HttpMethod method, string url, string? studentId)
    {
        var req = new HttpRequestMessage(method, url);
        if (studentId != null) req.Headers.Add("X-Student-Id", studentId);
        return http.SendAsync(req);
    }

    private static Task<HttpResponseMessage> Apply(HttpClient http, Guid classId, string sid) =>
        Send(http, HttpMethod.Post, $"/classes/{classId}/holds", sid);

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> GetClass(HttpClient http, Guid id) =>
        await http.GetFromJsonAsync<JsonElement>($"/classes/{id}");

    // ---- required test 1: create class ----
    [Fact]
    public async Task CreateClass_Returns201_AndAppearsInList()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        var res = await http.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await Json(res);
        Assert.Equal("Class 6 - Morning Shift", created.GetProperty("name").GetString());
        Assert.Equal(120, created.GetProperty("totalSeats").GetInt32());

        var id = created.GetProperty("id").GetGuid();
        var one = await GetClass(http, id);
        Assert.Equal(0, one.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, one.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(120, one.GetProperty("availableSeats").GetInt32());

        var list = await http.GetFromJsonAsync<JsonElement>("/classes");
        Assert.Contains(list.EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
    }

    // ---- required test 2: capacity ----
    [Fact]
    public async Task OneSeat_TwoStudents_OneSucceeds_OneGets409()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 1);

        var first = await Apply(http, classId, Sid(1));
        var second = await Apply(http, classId, Sid(2));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var hold = await Json(first);
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
        Assert.Equal(Sid(1), hold.GetProperty("studentId").GetString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---- required test 3: concurrency ----
    [Fact]
    public async Task Concurrency_100Requests_10Seats_ExactlyTenHolds()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 10);

        var tasks = Enumerable.Range(1, 100).Select(i => Apply(http, classId, Sid(i))).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(90, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await GetClass(http, classId);
        Assert.Equal(10, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }

    // ---- bonus: two API processes, one database ----
    [Fact]
    public async Task TwoApiInstances_SameDatabase_NeverOverbook()
    {
        await using var a = new ApiFactory(pg.ConnectionString);
        await using var b = new ApiFactory(pg.ConnectionString);
        var httpA = a.CreateClient();
        var httpB = b.CreateClient();
        var classId = await CreateClass(httpA, 10);

        var tasks = Enumerable.Range(1, 80)
            .Select(i => Apply(i % 2 == 0 ? httpA : httpB, classId, Sid(i)))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(70, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(10, (await GetClass(httpB, classId)).GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Confirm_IsIdempotent_CountsOnce_AndOnlyOwnerCanConfirm()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 5);
        var holdId = (await Json(await Apply(http, classId, Sid(1)))).GetProperty("holdId").GetGuid();

        var stranger = await Send(http, HttpMethod.Post, $"/holds/{holdId}/confirm", Sid(2));
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);

        for (var i = 0; i < 3; i++)
        {
            var ok = await Send(http, HttpMethod.Post, $"/holds/{holdId}/confirm", Sid(1));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal("CONFIRMED", (await Json(ok)).GetProperty("status").GetString());
        }

        var cls = await GetClass(http, classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(4, cls.GetProperty("availableSeats").GetInt32());

        // a confirmed admission cannot be cancelled
        var cancel = await Send(http, HttpMethod.Delete, $"/holds/{holdId}", Sid(1));
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Fact]
    public async Task ConcurrentConfirms_OfSameHold_CountOnce()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 5);
        var holdId = (await Json(await Apply(http, classId, Sid(1)))).GetProperty("holdId").GetGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Send(http, HttpMethod.Post, $"/holds/{holdId}/confirm", Sid(1))).ToArray());

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, (await GetClass(http, classId)).GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task DuplicateApplication_Returns409_ButAllowedAgainAfterCancel()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 5);

        var first = await Apply(http, classId, Sid(1));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Apply(http, classId, Sid(1))).StatusCode);

        var holdId = (await Json(first)).GetProperty("holdId").GetGuid();
        var cancel = await Send(http, HttpMethod.Delete, $"/holds/{holdId}", Sid(1));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var status = (await http.GetFromJsonAsync<JsonElement>($"/holds/{holdId}")).GetProperty("status").GetString();
        Assert.Equal("CANCELLED", status);
        Assert.Equal(HttpStatusCode.Created, (await Apply(http, classId, Sid(1))).StatusCode);

        var mine = await (await Send(http, HttpMethod.Get, "/me/holds", Sid(1))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, mine.GetArrayLength());
    }

    [Fact]
    public async Task ExpiredHold_FreesSeat_WithoutAnyApiCall_AndCannotBeConfirmed()
    {
        await using var factory = new ApiFactory(pg.ConnectionString, holdSeconds: 2);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 1);
        var holdId = (await Json(await Apply(http, classId, Sid(1)))).GetProperty("holdId").GetGuid();

        Assert.Equal(1, (await GetClass(http, classId)).GetProperty("heldSeats").GetInt32());

        await Task.Delay(TimeSpan.FromSeconds(3.5)); // nobody calls cancel/confirm

        var cls = await GetClass(http, classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
        Assert.Equal("EXPIRED", (await http.GetFromJsonAsync<JsonElement>($"/holds/{holdId}")).GetProperty("status").GetString());

        var confirm = await Send(http, HttpMethod.Post, $"/holds/{holdId}/confirm", Sid(1));
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);

        // the freed seat is available to someone else
        Assert.Equal(HttpStatusCode.Created, (await Apply(http, classId, Sid(2))).StatusCode);
    }

    [Fact]
    public async Task BadInput_Returns4xx_Never500()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var classId = await CreateClass(http, 3);
        var missing = Guid.NewGuid();

        async Task<HttpStatusCode> Post(string body)
        {
            var res = await http.PostAsync("/classes", new StringContent(body, Encoding.UTF8, "application/json"));
            return res.StatusCode;
        }

        Assert.Equal(HttpStatusCode.BadRequest, await Post("{ not json"));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("{\"name\":\"\",\"totalSeats\":5}"));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("{\"name\":\"A\",\"totalSeats\":0}"));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("{\"name\":\"A\",\"totalSeats\":-4}"));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("{\"name\":\"A\",\"totalSeats\":\"many\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("null"));

        Assert.Equal(HttpStatusCode.BadRequest, (await Apply(http, classId, "123")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Apply(http, classId, "abcdefghijklmnopq")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(http, HttpMethod.Post, $"/classes/{classId}/holds", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(http, HttpMethod.Get, "/me/holds", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Apply(http, missing, Sid(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/classes/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/holds/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/holds/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(http, HttpMethod.Post, $"/holds/{missing}/confirm", Sid(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(http, HttpMethod.Delete, $"/holds/{missing}", Sid(1))).StatusCode);
    }
}
