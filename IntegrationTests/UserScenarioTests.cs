using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests;

public class UserScenarioTests
{
    private readonly string _baseUrl;

    public UserScenarioTests()
    {
        _baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "https://localhost:5001";
    }

    [Fact]
    public async Task FullUserScenario_RealDockerPath_ShouldSucceed()
    {
        // ---------------------------------------------------------------------------------
        // DYNAMISATION DES DONNÉES (Pour pouvoir exécuter le test en boucle sans conflit)
        // ---------------------------------------------------------------------------------
        var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Génère un suffixe court unique (ex: "a1b2c3d4")
        var aliceEmail = $"alice.{uniqueId}@example.com";
        var bobEmail = $"bob.{uniqueId}@example.com";

        // ---------------------------------------------------------------------------------
        // CONFIGURATION HTTP CLIENTS
        // ---------------------------------------------------------------------------------
        var aliceHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        using var aliceClient = new HttpClient(aliceHandler) { BaseAddress = new Uri(_baseUrl) };

        var bobHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        using var bobClient = new HttpClient(bobHandler) { BaseAddress = new Uri(_baseUrl) };

        // -------------------------------------------------------------
        // ÉTAPE 1 : Inscription & Connexion d'Alice
        // -------------------------------------------------------------
        var registerAliceResponse = await aliceClient.PostAsJsonAsync("/api/auth/register", new
        {
            BusinessName = $"Alice Retrogaming {uniqueId}",
            Email = aliceEmail,
            Password = "SuperPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerAliceResponse.StatusCode);

        var loginAliceResponse = await aliceClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = aliceEmail,
            Password = "SuperPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginAliceResponse.StatusCode);

        // -------------------------------------------------------------
        // ÉTAPE 2 : Création de l'article par Alice
        // -------------------------------------------------------------
        var createItemResponse = await aliceClient.PostAsJsonAsync("/api/items", new
        {
            CategoryCode = "CONSOLES",
            Title = $"Game Boy Color Clear Purple ({uniqueId})",
            Description = "Test automatisé isolé via Docker Compose.",
            Price = 85.00,
            MetadataJson = "{\"ShippingFee\": 4.90}"
        });
        Assert.Equal(HttpStatusCode.OK, createItemResponse.StatusCode);

        var itemResult = await createItemResponse.Content.ReadFromJsonAsync<ItemCreatedResponseDto>();
        Assert.NotNull(itemResult);

        // -------------------------------------------------------------
        // ÉTAPE 3 : Inscription & Connexion de Bob
        // -------------------------------------------------------------
        var registerBobResponse = await bobClient.PostAsJsonAsync("/api/auth/register", new
        {
            BusinessName = $"Bob Collector {uniqueId}",
            Email = bobEmail,
            Password = "BobPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerBobResponse.StatusCode);

        var loginBobResponse = await bobClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = bobEmail,
            Password = "BobPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginBobResponse.StatusCode);

        // -------------------------------------------------------------
        // ÉTAPE 4 : Bob parcourt le catalogue
        // -------------------------------------------------------------
        var catalogueResponse = await bobClient.GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, catalogueResponse.StatusCode);

        var items = await catalogueResponse.Content.ReadFromJsonAsync<List<CollectibleItemDto>>();
        Assert.NotNull(items);
        Assert.Contains(items, item => item.Id == itemResult.ItemId);

        // -------------------------------------------------------------
        // ÉTAPE 5 : AJOUT - Récupération unitaire par ID (GetItemById)
        // -------------------------------------------------------------
        var itemByIdResponse = await bobClient.GetAsync($"/api/items/{itemResult.ItemId}");
        Assert.Equal(HttpStatusCode.OK, itemByIdResponse.StatusCode);

        var detailedItem = await itemByIdResponse.Content.ReadFromJsonAsync<CollectibleItemDto>();
        Assert.NotNull(detailedItem);
        Assert.Equal($"Game Boy Color Clear Purple ({uniqueId})", detailedItem.Title);

        // -------------------------------------------------------------
        // ÉTAPE 6 : AJOUT - Sécurité : Bob tente de supprimer l'objet d'Alice
        // -------------------------------------------------------------
        var bobDeleteResponse = await bobClient.DeleteAsync($"/api/items/{itemResult.ItemId}");
        Assert.Equal(HttpStatusCode.Forbidden, bobDeleteResponse.StatusCode); // Doit renvoyer 403 !

        // -------------------------------------------------------------
        // ÉTAPE 7 : AJOUT - Succès : Alice supprime son propre objet
        // -------------------------------------------------------------
        var aliceDeleteResponse = await aliceClient.DeleteAsync($"/api/items/{itemResult.ItemId}");
        Assert.Equal(HttpStatusCode.OK, aliceDeleteResponse.StatusCode);

        // -------------------------------------------------------------
        // ÉTAPE 8 : AJOUT - Vérification : L'objet a bien disparu du catalogue
        // -------------------------------------------------------------
        var postDeleteCatalogueResponse = await bobClient.GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, postDeleteCatalogueResponse.StatusCode);

        var updatedItems = await postDeleteCatalogueResponse.Content.ReadFromJsonAsync<List<CollectibleItemDto>>();
        Assert.NotNull(updatedItems);
        Assert.DoesNotContain(updatedItems, item => item.Id == itemResult.ItemId); // Ne doit plus y être !
    }
}

public record ItemCreatedResponseDto(string Message, long ItemId);
public record CollectibleItemDto(long Id, string Title, string CategoryCode, decimal Price, string MetadataJson);