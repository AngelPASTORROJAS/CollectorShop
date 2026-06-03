using System.Data;
using Collector.Shared.Infrastructure;

namespace Collectors.Infra.Persistence;

/// <summary>
/// DTO de transfert pour le catalogue, lié de manière isomorphe à sp_get_collectible_item_by_id
/// </summary>
public record CollectibleItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public decimal Price { get; init; }
    public string Rarity { get; init; }

    // Le constructeur extrait les données proprement
    public CollectibleItemDto(DataRow row)
    {
        Id = row.GetGuid("id") ?? Guid.Empty;
        Name = row.GetString("name") ?? "Nom inconnu";
        Description = row.GetString("description") ?? "";

        // Pour le prix (decimal), on peut faire un cast direct s'il n'est pas nul,
        // ou utiliser une conversion sécurisée selon ce que renvoie PostgreSQL
        Price = row.IsNull("price") ? 0.00m : Convert.ToDecimal(row["price"]);

        Rarity = row.GetString("rarity") ?? "Common";
    }
}