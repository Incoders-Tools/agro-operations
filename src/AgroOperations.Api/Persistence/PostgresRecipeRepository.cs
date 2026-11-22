using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresRecipeRepository(string connectionString) : IRecipeRepository
{
    private static Recipe MapRecipe(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetInt64(7));
    private static RecipeLine MapLine(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetString(4), r.GetInt64(5));

    public Recipe Create(Guid plotId, Guid campaignId, Guid laborTypeId, string stage, string targetUse, string responsibleEngineer)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO recipes(id,plot_id,campaign_id,labor_type_id,stage,target_use,responsible_engineer)
            SELECT @id, p.id, c.id, l.id, @stage, @targetUse, @responsible
            FROM plots p, campaigns c, labor_types l WHERE p.id=@plot AND c.id=@campaign AND l.id=@labor
            RETURNING id,plot_id,campaign_id,labor_type_id,stage,target_use,responsible_engineer,revision
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("plot", plotId);
        cmd.Parameters.AddWithValue("campaign", campaignId);
        cmd.Parameters.AddWithValue("labor", laborTypeId);
        cmd.Parameters.AddWithValue("stage", stage);
        cmd.Parameters.AddWithValue("targetUse", targetUse);
        cmd.Parameters.AddWithValue("responsible", responsibleEngineer);
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        return MapRecipe(row);
    }

    public Recipe Update(Guid id, long revision, string stage, string targetUse, string responsibleEngineer)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("""
            UPDATE recipes SET stage=@stage,target_use=@targetUse,responsible_engineer=@responsible,revision=revision+1
            WHERE id=@id AND revision=@revision
            RETURNING id,plot_id,campaign_id,labor_type_id,stage,target_use,responsible_engineer,revision
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("stage", stage);
        cmd.Parameters.AddWithValue("targetUse", targetUse);
        cmd.Parameters.AddWithValue("responsible", responsibleEngineer);
        using (var row = cmd.ExecuteReader())
            if (row.Read()) return MapRecipe(row);
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM recipes WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<Recipe> List()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,plot_id,campaign_id,labor_type_id,stage,target_use,responsible_engineer,revision FROM recipes ORDER BY id", db);
        using var row = cmd.ExecuteReader();
        var recipes = new List<Recipe>();
        while (row.Read()) recipes.Add(MapRecipe(row));
        return recipes;
    }

    public RecipeLine CreateLine(Guid recipeId, string productName, decimal doseValue, string doseUnit)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO recipe_lines(id,recipe_id,product_name,dose_value,dose_unit)
            SELECT @id, r.id, @product, @dose, @unit FROM recipes r WHERE r.id=@recipe
            RETURNING id,recipe_id,product_name,dose_value,dose_unit,revision
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("recipe", recipeId);
        cmd.Parameters.AddWithValue("product", productName);
        cmd.Parameters.AddWithValue("dose", doseValue);
        cmd.Parameters.AddWithValue("unit", doseUnit);
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        return MapLine(row);
    }

    public RecipeLine UpdateLine(Guid id, long revision, string productName, decimal doseValue, string doseUnit)
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("""
            UPDATE recipe_lines SET product_name=@product,dose_value=@dose,dose_unit=@unit,revision=revision+1
            WHERE id=@id AND revision=@revision
            RETURNING id,recipe_id,product_name,dose_value,dose_unit,revision
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("product", productName);
        cmd.Parameters.AddWithValue("dose", doseValue);
        cmd.Parameters.AddWithValue("unit", doseUnit);
        using (var row = cmd.ExecuteReader())
            if (row.Read()) return MapLine(row);
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM recipe_lines WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public IReadOnlyList<RecipeLine> ListLines()
    {
        using var db = new NpgsqlConnection(connectionString);
        db.Open();
        using var cmd = new NpgsqlCommand("SELECT id,recipe_id,product_name,dose_value,dose_unit,revision FROM recipe_lines ORDER BY id", db);
        using var row = cmd.ExecuteReader();
        var lines = new List<RecipeLine>();
        while (row.Read()) lines.Add(MapLine(row));
        return lines;
    }
}
