using BlocksPlant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();
        EnsureProductionClientIdColumn(db);
        EnsureMaterialsSchema(db);

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User
                {
                    Username = "owner",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                    Role = UserRole.Owner,
                    FullName = "Plant Owner"
                },
                new User
                {
                    Username = "cashier",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123"),
                    Role = UserRole.Cashier,
                    FullName = "Front Desk Cashier"
                },
                new User
                {
                    Username = "operator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("operator123"),
                    Role = UserRole.Operator,
                    FullName = "Yard Operator"
                });
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new Product
                {
                    Name = "4 Inch Block",
                    SizeInches = 4,
                    PricePerBlock = 2.50m,
                    MinStock = 200,
                    Quantity = 500
                },
                new Product
                {
                    Name = "6 Inch Block",
                    SizeInches = 6,
                    PricePerBlock = 3.50m,
                    MinStock = 200,
                    Quantity = 400
                },
                new Product
                {
                    Name = "8 Inch Block",
                    SizeInches = 8,
                    PricePerBlock = 4.50m,
                    MinStock = 150,
                    Quantity = 300
                });
        }

        db.SaveChanges();
        SeedMaterialsAndRecipes(db);
    }

    private static void SeedMaterialsAndRecipes(AppDbContext db)
    {
        if (!db.RawMaterials.Any())
        {
            db.RawMaterials.AddRange(
                new RawMaterial
                {
                    Name = "Cement",
                    Unit = "bags",
                    QuantityOnHand = 80,
                    MinStock = 20,
                    Notes = "50 kg bags"
                },
                new RawMaterial
                {
                    Name = "Sand",
                    Unit = "tons",
                    QuantityOnHand = 25,
                    MinStock = 5,
                    Notes = "Fine sand"
                },
                new RawMaterial
                {
                    Name = "Aggregate",
                    Unit = "tons",
                    QuantityOnHand = 20,
                    MinStock = 5,
                    Notes = "3/8\" gravel"
                },
                new RawMaterial
                {
                    Name = "Water",
                    Unit = "liters",
                    QuantityOnHand = 5000,
                    MinStock = 500,
                    Notes = null
                });
            db.SaveChanges();
        }

        if (db.RecipeLines.Any()) return;

        var products = db.Products.OrderBy(p => p.SizeInches).ToList();
        var materials = db.RawMaterials.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        if (products.Count == 0 || materials.Count == 0) return;

        // Approximate placeholder qty per good block — owner can edit via Recipes UI.
        var recipes = new Dictionary<int, (decimal cement, decimal sand, decimal agg, decimal water)>
        {
            [4] = (0.020m, 0.005m, 0.004m, 0.8m),
            [6] = (0.028m, 0.007m, 0.006m, 1.1m),
            [8] = (0.035m, 0.009m, 0.008m, 1.4m)
        };

        foreach (var product in products)
        {
            if (!recipes.TryGetValue(product.SizeInches, out var r)) continue;
            void Add(string name, decimal qty)
            {
                if (!materials.TryGetValue(name, out var mat) || qty <= 0) return;
                db.RecipeLines.Add(new RecipeLine
                {
                    ProductId = product.Id,
                    RawMaterialId = mat.Id,
                    QuantityPerBlock = qty
                });
            }

            Add("Cement", r.cement);
            Add("Sand", r.sand);
            Add("Aggregate", r.agg);
            Add("Water", r.water);
        }

        db.SaveChanges();
    }

    /// <summary>
    /// EnsureCreated does not alter existing SQLite schemas; add ClientId for offline idempotency.
    /// </summary>
    private static void EnsureProductionClientIdColumn(AppDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE ProductionEntries ADD COLUMN ClientId TEXT NULL");
        }
        catch
        {
            // Column already exists
        }

        try
        {
            db.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductionEntries_ClientId ON ProductionEntries(ClientId) WHERE ClientId IS NOT NULL");
        }
        catch
        {
            // Index may already exist under another name
        }
    }

    /// <summary>
    /// EnsureCreated does not add new tables to an existing SQLite file — create if missing.
    /// </summary>
    private static void EnsureMaterialsSchema(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "RawMaterials" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RawMaterials" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Unit" TEXT NOT NULL,
                "QuantityOnHand" TEXT NOT NULL,
                "MinStock" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "IsActive" INTEGER NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "RecipeLines" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeLines" PRIMARY KEY AUTOINCREMENT,
                "ProductId" INTEGER NOT NULL,
                "RawMaterialId" INTEGER NOT NULL,
                "QuantityPerBlock" TEXT NOT NULL,
                CONSTRAINT "FK_RecipeLines_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RecipeLines_RawMaterials_RawMaterialId" FOREIGN KEY ("RawMaterialId") REFERENCES "RawMaterials" ("Id") ON DELETE CASCADE
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeLines_ProductId_RawMaterialId"
            ON "RecipeLines" ("ProductId", "RawMaterialId");
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "MaterialTransactions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MaterialTransactions" PRIMARY KEY AUTOINCREMENT,
                "RawMaterialId" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "QuantityDelta" TEXT NOT NULL,
                "QuantityAfter" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "ProductionEntryId" INTEGER NULL,
                "CreatedAt" TEXT NOT NULL,
                "CreatedByUserId" INTEGER NOT NULL,
                CONSTRAINT "FK_MaterialTransactions_RawMaterials_RawMaterialId" FOREIGN KEY ("RawMaterialId") REFERENCES "RawMaterials" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_MaterialTransactions_ProductionEntries_ProductionEntryId" FOREIGN KEY ("ProductionEntryId") REFERENCES "ProductionEntries" ("Id"),
                CONSTRAINT "FK_MaterialTransactions_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
    }
}
