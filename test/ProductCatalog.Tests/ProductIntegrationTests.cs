using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Api.Data;
using ProductCatalog.Api.Models;
using Xunit;

namespace ProductCatalog.Tests;

public class ProductIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__CatalogDbConnection";

    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=CatalogTestDb;User Id=sa;" +
        "Password=DevOpsPass2026!#;TrustServerCertificate=True;";

    private readonly WebApplicationFactory<Program> _factory;

    public ProductIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Priorité à la variable d'environnement (CI), sinon valeur locale par défaut
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? DefaultConnectionString;

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Supprime l'ancienne configuration du DbContext
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

                // Connexion directe au vrai moteur SQL Server
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString));
            });
        });

        EnsureDatabaseCreated();
    }

    [Fact]
    public async Task AddProduct_And_VerifyPersistenceInRealDatabase()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueName = "Serveur-Rack-" + Guid.NewGuid().ToString()[..6];
        var product = new Product { Name = uniqueName, Price = 1299.99m };

        // Act : insertion via l'endpoint API
        var response = await client.PostAsJsonAsync("/api/products", product);

        // Assert : réponse HTTP
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert : persistance réelle dans SQL Server
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persisted = await db.Products.FirstOrDefaultAsync(p => p.Name == uniqueName);

        Assert.NotNull(persisted);
        Assert.Equal(1299.99m, persisted.Price);
    }

    private void EnsureDatabaseCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }
}