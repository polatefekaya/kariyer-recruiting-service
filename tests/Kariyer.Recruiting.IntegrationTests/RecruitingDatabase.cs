using Kariyer.Recruiting.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kariyer.Recruiting.IntegrationTests;

/// <summary>
/// A real Postgres with the service's migrations applied and the Node-owned tables stood in.
/// Shared by the whole assembly: container startup dominates the run time of these tests.
/// </summary>
public sealed class RecruitingDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("kariyer")
        .WithUsername("kariyer")
        .WithPassword("kariyer")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (NpgsqlConnection connection = new(ConnectionString))
        {
            await connection.OpenAsync();

            await using NpgsqlCommand command = new(
                await File.ReadAllTextAsync("public_standins.sql"), connection);

            await command.ExecuteNonQueryAsync();
        }

        await using RecruitingDbContext db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public RecruitingDbContext CreateContext()
    {
        DbContextOptionsBuilder<RecruitingDbContext> options = new();

        options.UseNpgsql(ConnectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RecruitingDbContext.Schema));

        return new RecruitingDbContext(options.Options);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(sql, connection);

        object? value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<RecruitingDatabase>
{
    public const string Name = "recruiting-database";
}
