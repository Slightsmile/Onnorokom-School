using Dapper;
using Npgsql;

namespace Backend;

public class SeatFullException : Exception { }
public class DuplicateActiveHoldException : Exception { }
public class NotFoundException : Exception { }
public class ForbiddenException : Exception { }
public class InvalidHoldStateException : Exception
{
    public InvalidHoldStateException(string message) : base(message) { }
}

public class AdmissionService
{
    private readonly Db _db;
    private readonly int _holdDurationSeconds;

    public AdmissionService(Db db)
    {
        _db = db;
        _holdDurationSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var v) ? v : 600;
    }

    private static bool IsActive(string status, DateTime expiresAt, DateTime now) =>
        status == "CONFIRMED" || (status == "HELD" && expiresAt > now);

    private static string EffectiveStatus(string status, DateTime expiresAt, DateTime now) =>
        status == "HELD" && expiresAt <= now ? "EXPIRED" : status;

    public async Task<CreateClassResponse> CreateClassAsync(string name, int totalSeats)
    {
        var id = "c_" + Guid.NewGuid().ToString("N")[..10];
        using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT INTO classes (id, name, total_seats) VALUES (@id, @name, @totalSeats)",
            new { id, name, totalSeats });
        return new CreateClassResponse(id, name, totalSeats);
    }

    public async Task<List<ClassDto>> ListClassesAsync()
    {
        using var conn = await _db.OpenAsync();
        var classes = await conn.QueryAsync<(string Id, string Name, int TotalSeats)>(
            "SELECT id, name, total_seats FROM classes ORDER BY id");

        var result = new List<ClassDto>();
        var now = DateTime.UtcNow;
        foreach (var c in classes)
        {
            var (held, confirmed) = await CountSeatsAsync(conn, c.Id, now);
            result.Add(new ClassDto(c.Id, c.Name, c.TotalSeats, held, confirmed, c.TotalSeats - held - confirmed));
        }
        return result;
    }

    public async Task<ClassDto?> GetClassAsync(string classId)
    {
        using var conn = await _db.OpenAsync();
        var c = await conn.QuerySingleOrDefaultAsync<(string Id, string Name, int TotalSeats)?>(
            "SELECT id, name, total_seats FROM classes WHERE id = @classId", new { classId });
        if (c is null) return null;
        var now = DateTime.UtcNow;
        var (held, confirmed) = await CountSeatsAsync(conn, c.Value.Id, now);
        return new ClassDto(c.Value.Id, c.Value.Name, c.Value.TotalSeats, held, confirmed, c.Value.TotalSeats - held - confirmed);
    }

    private static async Task<(int Held, int Confirmed)> CountSeatsAsync(NpgsqlConnection conn, string classId, DateTime now)
    {
        var rows = await conn.QueryAsync<(string Status, DateTime ExpiresAt)>(
            "SELECT status, expires_at FROM holds WHERE class_id = @classId AND status IN ('HELD', 'CONFIRMED')",
            new { classId });
        int held = 0, confirmed = 0;
        foreach (var r in rows)
        {
            if (r.Status == "CONFIRMED") confirmed++;
            else if (r.Status == "HELD" && r.ExpiresAt > now) held++;
        }
        return (held, confirmed);
    }

    public async Task<HoldDto> CreateHoldAsync(string classId, string studentId)
    {
        using var conn = await _db.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var className = await conn.QuerySingleOrDefaultAsync<(string Name, int TotalSeats)?>(
            "SELECT name, total_seats FROM classes WHERE id = @classId FOR UPDATE",
            new { classId }, tx);
        if (className is null) throw new NotFoundException();

        var now = DateTime.UtcNow;
        var activeRows = await conn.QueryAsync<(string Status, DateTime ExpiresAt, string StudentId)>(
            "SELECT status, expires_at, student_id FROM holds WHERE class_id = @classId AND status IN ('HELD', 'CONFIRMED')",
            new { classId }, tx);

        int occupied = 0;
        foreach (var r in activeRows)
        {
            if (r.Status == "CONFIRMED") occupied++;
            else if (r.Status == "HELD" && r.ExpiresAt > now) occupied++;

            if (r.StudentId == studentId && IsActive(r.Status, r.ExpiresAt, now))
                throw new DuplicateActiveHoldException();
        }

        if (occupied >= className.Value.TotalSeats) throw new SeatFullException();

        var holdId = "h_" + Guid.NewGuid().ToString("N")[..10];
        var expiresAt = now.AddSeconds(_holdDurationSeconds);
        await conn.ExecuteAsync(
            @"INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at)
              VALUES (@holdId, @classId, @studentId, 'HELD', @now, @expiresAt)",
            new { holdId, classId, studentId, now, expiresAt }, tx);

        await tx.CommitAsync();
        return new HoldDto(holdId, classId, className.Value.Name, studentId, "HELD", expiresAt);
    }

    public async Task<HoldDto> GetHoldAsync(string holdId)
    {
        using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync<HoldRow>(
            @"SELECT h.id AS HoldId, h.class_id AS ClassId, c.name AS ClassName, h.student_id AS StudentId,
                     h.status AS Status, h.expires_at AS ExpiresAt
              FROM holds h JOIN classes c ON c.id = h.class_id
              WHERE h.id = @holdId",
            new { holdId });
        var row = rows.FirstOrDefault();
        if (row.HoldId is null) throw new NotFoundException();
        var now = DateTime.UtcNow;
        return new HoldDto(row.HoldId, row.ClassId, row.ClassName, row.StudentId,
            EffectiveStatus(row.Status, row.ExpiresAt, now), row.ExpiresAt);
    }

    public async Task<HoldDto> ConfirmHoldAsync(string holdId, string studentId)
    {
        using var conn = await _db.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var rows = await conn.QueryAsync<HoldRow>(
            @"SELECT h.id AS HoldId, h.class_id AS ClassId, c.name AS ClassName, h.student_id AS StudentId,
                     h.status AS Status, h.expires_at AS ExpiresAt
              FROM holds h JOIN classes c ON c.id = h.class_id
              WHERE h.id = @holdId FOR UPDATE OF h",
            new { holdId }, tx);
        var row = rows.FirstOrDefault();
        if (row.HoldId is null) throw new NotFoundException();
        if (row.StudentId != studentId) throw new ForbiddenException();

        var now = DateTime.UtcNow;
        if (row.Status == "CONFIRMED")
        {
            await tx.CommitAsync();
            return new HoldDto(row.HoldId, row.ClassId, row.ClassName, studentId, "CONFIRMED", row.ExpiresAt);
        }

        if (row.Status != "HELD" || row.ExpiresAt <= now)
            throw new InvalidHoldStateException("Hold is not active and cannot be confirmed.");

        await conn.ExecuteAsync(
            "UPDATE holds SET status = 'CONFIRMED', confirmed_at = @now WHERE id = @holdId",
            new { holdId, now }, tx);
        await tx.CommitAsync();

        return new HoldDto(row.HoldId, row.ClassId, row.ClassName, studentId, "CONFIRMED", row.ExpiresAt);
    }

    public async Task CancelHoldAsync(string holdId, string studentId)
    {
        using var conn = await _db.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var row = await conn.QuerySingleOrDefaultAsync<(string StudentId, string Status, DateTime ExpiresAt)?>(
            "SELECT student_id, status, expires_at FROM holds WHERE id = @holdId FOR UPDATE",
            new { holdId }, tx);
        if (row is null) throw new NotFoundException();
        if (row.Value.StudentId != studentId) throw new ForbiddenException();

        var now = DateTime.UtcNow;
        if (!IsActive(row.Value.Status, row.Value.ExpiresAt, now))
            throw new InvalidHoldStateException("Hold is not active and cannot be cancelled.");

        await conn.ExecuteAsync(
            "UPDATE holds SET status = 'CANCELLED' WHERE id = @holdId", new { holdId }, tx);
        await tx.CommitAsync();
    }

    public async Task<List<HoldDto>> ListMyHoldsAsync(string studentId)
    {
        using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync<HoldRow>(
            @"SELECT h.id AS HoldId, h.class_id AS ClassId, c.name AS ClassName, h.student_id AS StudentId,
                     h.status AS Status, h.expires_at AS ExpiresAt
              FROM holds h JOIN classes c ON c.id = h.class_id
              WHERE h.student_id = @studentId
              ORDER BY h.created_at DESC",
            new { studentId });

        var now = DateTime.UtcNow;
        return rows.Select(r => new HoldDto(r.HoldId, r.ClassId, r.ClassName, r.StudentId,
            EffectiveStatus(r.Status, r.ExpiresAt, now), r.ExpiresAt)).ToList();
    }

    private struct HoldRow
    {
        public string HoldId { get; set; }
        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string StudentId { get; set; }
        public string Status { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
