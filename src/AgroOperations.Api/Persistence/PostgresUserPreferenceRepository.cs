using AgroOperations.Api.Domain;
using Npgsql;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresUserPreferenceRepository(string connectionString) : IUserPreferenceRepository
{
    private const string Columns = "theme,revision";

    private static UserPreferences Map(NpgsqlDataReader r) => new(r.GetString(0), r.GetInt64(1));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    private static UserPreferences? Single(NpgsqlCommand cmd)
    {
        using var row = cmd.ExecuteReader();
        return row.Read() ? Map(row) : null;
    }

    public UserPreferences? Get(string identityEmail)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM user_preferences WHERE identity_email=@email", db);
        cmd.Parameters.AddWithValue("email", identityEmail);
        return Single(cmd);
    }

    public UserPreferences Create(string identityEmail, string theme)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO user_preferences(identity_email,theme) VALUES (@email,@theme)
            ON CONFLICT (identity_email) DO NOTHING RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("email", identityEmail);
        cmd.Parameters.AddWithValue("theme", theme);
        return Single(cmd) ?? throw new DomainConflictException();
    }

    public UserPreferences Update(string identityEmail, long revision, string theme)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE user_preferences SET theme=@theme,revision=revision+1,updated_at=now()
            WHERE identity_email=@email AND revision=@revision RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("email", identityEmail);
        cmd.Parameters.AddWithValue("revision", revision);
        cmd.Parameters.AddWithValue("theme", theme);
        return Single(cmd) ?? throw new DomainConflictException();
    }
}
