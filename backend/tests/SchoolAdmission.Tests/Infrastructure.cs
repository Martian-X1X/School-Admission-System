using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SchoolAdmission.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolAdmission.Tests;

/// <summary>One real PostgreSQL container shared by all tests (needs Docker running).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { MaxPoolSize = 30 }.ConnectionString;

    public Task InitializeAsync() => _pg.StartAsync();
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
}

[CollectionDefinition("pg")]
public sealed class PgCollection : ICollectionFixture<PostgresFixture> { }

/// <summary>An in-process API instance pointing at the shared database.</summary>
public sealed class ApiFactory(string connectionString, int holdSeconds = 600) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            services.RemoveAll<AdmissionOptions>();
            services.AddSingleton(new AdmissionOptions(holdSeconds, 1));
        });
    }
}
