using Catalogue.Infrastructure.Extensions;
using Catalogue.Web.Components;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor / Razor ───────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Infrastructure ───────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Validation ───────────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();

// ── Notification service ─────────────────────────────────────────────────────
builder.Services.AddScoped<Catalogue.Web.Services.NotificationService>();

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Minimal API: CSV export ──────────────────────────────────────────────────
app.MapGet("/api/catalogue/export", async (
    Catalogue.Core.Interfaces.ICatalogueRepository repo,
    string? server, string? database, string? table, string? column, string? persistence,
    HttpContext ctx) =>
{
    var all = await repo.GetColumnsAsync(filter: Catalogue.Core.Interfaces.ColumnFilter.All);

    var filtered = all.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(server))      filtered = filtered.Where(c => c.ServerName.Contains(server,   StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(database))    filtered = filtered.Where(c => c.DatabaseName.Contains(database, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(table))       filtered = filtered.Where(c => c.TableName.Contains(table,     StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(column))      filtered = filtered.Where(c => c.ColumnName.Contains(column,   StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(persistence)) filtered = filtered.Where(c => c.PersistenceType.ToString() == persistence);

    var rows = filtered.ToList();

    ctx.Response.ContentType = "text/csv";
    ctx.Response.Headers.ContentDisposition = "attachment; filename=catalogue-export.csv";

    await using var writer = new System.IO.StreamWriter(ctx.Response.Body, leaveOpen: true);
    await writer.WriteLineAsync("Server,Database,Schema,Table,Column,PersistenceType,IsInDaoAnalysis,IsAddedByApi,IsSelectedForLoad,ModifiedAt,ModifiedBy");

    foreach (var r in rows)
    {
        await writer.WriteLineAsync(
            $"{Csv(r.ServerName)},{Csv(r.DatabaseName)},{Csv(r.SchemaName)},{Csv(r.TableName)},{Csv(r.ColumnName)}," +
            $"{r.PersistenceType},{r.IsInDaoAnalysis},{r.IsAddedByApi},{r.IsSelectedForLoad}," +
            $"{r.ModifiedAt?.ToString("yyyy-MM-dd HH:mm:ss")},{Csv(r.ModifiedBy ?? "")}");
    }

    static string Csv(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n')
        ? $"\"{v.Replace("\"", "\"\"")}\""
        : v;
});

app.Run();

// For integration testing
public partial class Program { }
