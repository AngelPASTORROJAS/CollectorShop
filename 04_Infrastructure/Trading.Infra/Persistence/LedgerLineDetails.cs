namespace Trading.Infra.Persistence;

/// <summary>
/// Modèle d'entité structuré représentant le détail complet d'une transaction avec ses écritures comptables
/// </summary>
public record TransactionDetailsEntity
{
    public TransactionEntity Transaction { get; init; }
    public List<LedgerLineDetails> LedgerLines { get; init; } = new();
}

/// <summary>
/// Sous-structure représentant une ligne d'écriture comptable immuable
/// </summary>
public record LedgerLineDetails
{
    public long LedgerId { get; init; }
    public LedgerAccountType AccountType { get; init; }
    public decimal DebitAmount { get; init; }
    public decimal CreditAmount { get; init; }
    public string Description { get; init; }
    public DateTime RecordedAt { get; init; }
}