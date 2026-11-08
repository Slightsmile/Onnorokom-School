using Backend;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<AdmissionService>();
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.EnsureSchemaAsync();
            break;
        }
        catch (Exception) when (attempt < 10)
        {
            await Task.Delay(1000);
        }
    }
}

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (NotFoundException)
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.WriteAsJsonAsync(new ApiError("not_found", "Resource not found."));
    }
    catch (ForbiddenException)
    {
        ctx.Response.StatusCode = 403;
        await ctx.Response.WriteAsJsonAsync(new ApiError("forbidden", "This hold does not belong to this student."));
    }
    catch (SeatFullException)
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new ApiError("seat_full", "No seats available for this class."));
    }
    catch (DuplicateActiveHoldException)
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new ApiError("duplicate_active_hold", "Student already has an active hold or confirmed seat for this class."));
    }
    catch (InvalidHoldStateException ex)
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new ApiError("invalid_hold_state", ex.Message));
    }
    catch (ArgumentException ex)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new ApiError("bad_request", ex.Message));
    }
});

string RequireStudentId(HttpRequest req)
{
    if (!req.Headers.TryGetValue("X-Student-Id", out var values) || string.IsNullOrWhiteSpace(values))
        throw new ArgumentException("X-Student-Id header is required.");
    return values.ToString();
}

app.MapPost("/classes", async (AdmissionService svc, CreateClassRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body.Name) || body.TotalSeats <= 0)
        throw new ArgumentException("name and a positive totalSeats are required.");
    var result = await svc.CreateClassAsync(body.Name, body.TotalSeats);
    return Results.Json(new { id = result.Id, name = result.Name, totalSeats = result.TotalSeats }, statusCode: 201);
});

app.MapGet("/classes", async (AdmissionService svc) =>
{
    var classes = await svc.ListClassesAsync();
    return Results.Json(classes.Select(c => new
    {
        id = c.Id, name = c.Name, totalSeats = c.TotalSeats,
        heldSeats = c.HeldSeats, confirmedSeats = c.ConfirmedSeats, availableSeats = c.AvailableSeats
    }));
});

app.MapGet("/classes/{classId}", async (AdmissionService svc, string classId) =>
{
    var c = await svc.GetClassAsync(classId);
    if (c is null) throw new NotFoundException();
    return Results.Json(new
    {
        id = c.Id, name = c.Name, totalSeats = c.TotalSeats,
        heldSeats = c.HeldSeats, confirmedSeats = c.ConfirmedSeats, availableSeats = c.AvailableSeats
    });
});

app.MapPost("/classes/{classId}/holds", async (AdmissionService svc, HttpRequest req, string classId) =>
{
    var studentId = RequireStudentId(req);
    var hold = await svc.CreateHoldAsync(classId, studentId);
    return Results.Json(new
    {
        holdId = hold.HoldId, classId = hold.ClassId, studentId = hold.StudentId,
        status = hold.Status, expiresAt = hold.ExpiresAt
    }, statusCode: 201);
});

app.MapGet("/holds/{holdId}", async (AdmissionService svc, string holdId) =>
{
    var hold = await svc.GetHoldAsync(holdId);
    return Results.Json(new
    {
        holdId = hold.HoldId, classId = hold.ClassId, status = hold.Status, expiresAt = hold.ExpiresAt
    });
});

app.MapPost("/holds/{holdId}/confirm", async (AdmissionService svc, HttpRequest req, string holdId) =>
{
    var studentId = RequireStudentId(req);
    var hold = await svc.ConfirmHoldAsync(holdId, studentId);
    return Results.Json(new { holdId = hold.HoldId, status = hold.Status });
});

app.MapDelete("/holds/{holdId}", async (AdmissionService svc, HttpRequest req, string holdId) =>
{
    var studentId = RequireStudentId(req);
    await svc.CancelHoldAsync(holdId, studentId);
    return Results.StatusCode(204);
});

app.MapGet("/me/holds", async (AdmissionService svc, HttpRequest req) =>
{
    var studentId = RequireStudentId(req);
    var holds = await svc.ListMyHoldsAsync(studentId);
    return Results.Json(holds.Select(h => new
    {
        holdId = h.HoldId, classId = h.ClassId, className = h.ClassName,
        status = h.Status, expiresAt = h.ExpiresAt
    }));
});

app.Run();
