using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

public sealed class PostgresStaffRepository(string connectionString) : IStaffRepository
{
    private const string Columns = "id,full_name,title,role,license_number,email,phone,active,notes,revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
    private static Staff Map(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), Text(r, 2), r.GetString(3), Text(r, 4), Text(r, 5), Text(r, 6), r.GetBoolean(7), Text(r, 8), r.GetInt64(9));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    private static void Bind(NpgsqlCommand cmd, StaffDraft staff)
    {
        cmd.Parameters.AddWithValue("fullName", staff.FullName!);
        cmd.Parameters.Add(Varchar("title", staff.Title));
        cmd.Parameters.AddWithValue("role", staff.Role!);
        cmd.Parameters.Add(Varchar("license", staff.LicenseNumber));
        cmd.Parameters.Add(Varchar("email", staff.Email));
        cmd.Parameters.Add(Varchar("phone", staff.Phone));
        cmd.Parameters.AddWithValue("active", staff.Active);
        cmd.Parameters.Add(Varchar("notes", staff.Notes));
    }

    public Staff Create(StaffDraft staff)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO staff(id,full_name,title,role,license_number,email,phone,active,notes)
            VALUES (@id,@fullName,@title,@role,@license,@email,@phone,@active,@notes) RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(cmd, staff);
        try
        {
            using var row = cmd.ExecuteReader();
            row.Read();
            return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
    }

    public Staff Update(Guid id, long revision, StaffDraft staff)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE staff SET full_name=@fullName,title=@title,role=@role,license_number=@license,email=@email,phone=@phone,active=@active,notes=@notes,
              revision=revision+1
            WHERE id=@id AND revision=@revision RETURNING {Columns}
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, staff);
        try
        {
            using (var row = cmd.ExecuteReader())
                if (row.Read()) return Map(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
        using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM staff WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public Staff? Get(Guid id)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"SELECT {Columns} FROM staff WHERE id=@id", db);
        cmd.Parameters.AddWithValue("id", id);
        using var row = cmd.ExecuteReader();
        return row.Read() ? Map(row) : null;
    }

    public IReadOnlyList<Staff> List(string? role, bool? active)
    {
        using var db = Open();
        var filters = new List<string>();
        if (role is not null) filters.Add("role=@role");
        if (active is not null) filters.Add("active=@active");
        using var cmd = new NpgsqlCommand($"""
            SELECT {Columns} FROM staff {(filters.Count == 0 ? "" : "WHERE " + string.Join(" AND ", filters))} ORDER BY full_name,id
            """, db);
        if (role is not null) cmd.Parameters.AddWithValue("role", role);
        if (active is { } value) cmd.Parameters.AddWithValue("active", value);
        using var row = cmd.ExecuteReader();
        var staff = new List<Staff>();
        while (row.Read()) staff.Add(Map(row));
        return staff;
    }
}
