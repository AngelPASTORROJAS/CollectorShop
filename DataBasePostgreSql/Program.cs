using System.Reflection;
using Npgsql;

namespace DataBasePostgreSql;

public class Program
{
    public const string DbUsers = "DbUsers";
    public const string DbCollector = "DbCollector";
    public const string DbFinance = "DbFinance";

    public static int Main(string[] args)
    {
        Console.WriteLine("====================================================");
        Console.WriteLine(" Moteur de Migration Natif et Sécurisé (.NET 10)");
        Console.WriteLine("====================================================");

        // 1. Récupération sécurisée des connexions (Variables d'env pour la CI/CD)
        string cnxUsers = Environment.GetEnvironmentVariable("DB_USERS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=users_db;Username=postgres;Password=root;";

        string cnxFinance = Environment.GetEnvironmentVariable("DB_FINANCE_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=finance_db;Username=postgres;Password=root;";

        // 2. Exécution séquentielle des migrations pour chaque base
        if (!ExecuteMigration(DbUsers, cnxUsers)) return 1;
        if (!ExecuteMigration(DbFinance, cnxFinance)) return 1;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n Toutes les bases de données sont à jour. Fin de la CI/CD.");
        Console.ResetColor();
        return 0;
    }

    private static bool ExecuteMigration(string dbName, string connectionString)
    {
        Console.WriteLine($"\nAnalyse du dossier de scripts : [{dbName}]...");
        var assembly = Assembly.GetExecutingAssembly();

        // Extraction et tri alphabétique des scripts embarqués (ex: DataBasePostgreSql.DbUsers.V1_...sql)
        var scriptNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains($".{dbName}."))
            .OrderBy(name => name)
            .ToList();

        if (!scriptNames.Any())
        {
            Console.WriteLine($" Aucun script trouvé pour {dbName}.");
            return true;
        }

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Étape A : Initialisation de la table d'audit si elle n'existe pas
            string createHistoryTableSql = @"
                CREATE TABLE IF NOT EXISTS migration_history (
                    id SERIAL PRIMARY KEY,
                    script_name VARCHAR(255) NOT NULL UNIQUE,
                    applied_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
                );";

            using (var cmd = new NpgsqlCommand(createHistoryTableSql, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Étape B : Boucle d'application séquentielle
            foreach (var scriptName in scriptNames)
            {
                // Extraction du nom de fichier propre (ex: V1_0_0__init_users_and_access.sql)
                string shortName = scriptName.Substring(scriptName.IndexOf($".{dbName}.") + dbName.Length + 2);

                // Vérification de l'état du script en base
                using var checkCmd = new NpgsqlCommand("SELECT COUNT(1) FROM migration_history WHERE script_name = @name", connection);
                checkCmd.Parameters.AddWithValue("name", shortName);
                long alreadyApplied = (long)checkCmd.ExecuteScalar()!;

                if (alreadyApplied > 0) continue;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($" Application du script : {shortName}");
                Console.ResetColor();

                // Lecture du contenu SQL brut incorporé dans l'assembly
                using var stream = assembly.GetManifestResourceStream(scriptName)!;
                using var reader = new StreamReader(stream);
                string sqlText = reader.ReadToEnd();

                // Exécution isolée dans une transaction dédiée par script
                using var transaction = connection.BeginTransaction();
                try
                {
                    using var runCmd = new NpgsqlCommand(sqlText, connection, transaction);
                    runCmd.ExecuteNonQuery();

                    // Journalisation du succès de la migration
                    using var logCmd = new NpgsqlCommand("INSERT INTO migration_history (script_name) VALUES (@name)", connection, transaction);
                    logCmd.Parameters.AddWithValue("name", shortName);
                    logCmd.ExecuteNonQuery();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Erreur lors de l'exécution du script {shortName} : {ex.Message}", ex);
                }
            }

            return true;
        }
        catch (Exception globalEx)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" ÉCHEC CRITIQUE de la migration pour [{dbName}] : {globalEx.Message}");
            Console.ResetColor();
            return false;
        }
    }
}