using System.Text;
using System.Text.Json;
using Catalogue.Core.Abstractions;
using Catalogue.Core.Models.Dacpac;
using Catalogue.Core.Utilities;

namespace Catalogue.Infrastructure.Generation;

public class ReportWriterService
{
    private readonly IGenerationLogger _logger;

    public ReportWriterService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public bool WriteJsonReport(string outputDirectory, List<ElementDiscoveryReport> reports)
    {
        try
        {
            var reportsDir = Path.Combine(outputDirectory, "DiscoveryReports");
            Directory.CreateDirectory(reportsDir);

            foreach (var report in reports)
            {
                var fileName = $"{report.Server}_{report.Database}_Discovery.json";
                var filePath = Path.Combine(reportsDir, fileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(report, options);
                File.WriteAllText(filePath, json);

                _logger.LogInfo($"[{report.Server}].[{report.Database}] - Wrote discovery report: {fileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write JSON discovery reports: {ex.Message}");
            return false;
        }
    }

    public bool WriteHtmlReport(string outputDirectory, List<ElementDiscoveryReport> reports)
    {
        try
        {
            var reportsDir = Path.Combine(outputDirectory, "DiscoveryReports");
            Directory.CreateDirectory(reportsDir);

            foreach (var report in reports)
            {
                var fileName = $"{report.Server}_{report.Database}_Discovery.html";
                var filePath = Path.Combine(reportsDir, fileName);

                var html = GenerateHtmlReport(report);
                File.WriteAllText(filePath, html);

                _logger.LogInfo($"[{report.Server}].[{report.Database}] - Wrote HTML discovery report: {fileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write HTML discovery reports: {ex.Message}");
            return false;
        }
    }

    private string GenerateHtmlReport(ElementDiscoveryReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>Discovery Report: [{report.Server}].[{report.Database}]</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background-color: #f5f5f5; }");
        sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("        h1 { color: #333; border-bottom: 3px solid #0078d4; padding-bottom: 10px; }");
        sb.AppendLine("        h2 { color: #0078d4; margin-top: 30px; border-bottom: 2px solid #e0e0e0; padding-bottom: 8px; }");
        sb.AppendLine("        h3 { color: #555; margin-top: 20px; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 15px; background-color: white; }");
        sb.AppendLine("        th { background-color: #0078d4; color: white; padding: 12px; text-align: left; font-weight: 600; }");
        sb.AppendLine("        td { padding: 10px 12px; border-bottom: 1px solid #e0e0e0; }");
        sb.AppendLine("        tr:hover { background-color: #f9f9f9; }");
        sb.AppendLine("        .badge { display: inline-block; padding: 4px 12px; border-radius: 12px; font-size: 14px; font-weight: 600; }");
        sb.AppendLine("        .badge-primary { background-color: #0078d4; color: white; }");
        sb.AppendLine("        .badge-secondary { background-color: #6c757d; color: white; }");
        sb.AppendLine("        .badge-info { background-color: #17a2b8; color: white; }");
        sb.AppendLine("        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 20px 0; }");
        sb.AppendLine("        .summary-card { background-color: #f8f9fa; padding: 15px; border-radius: 6px; border-left: 4px solid #0078d4; }");
        sb.AppendLine("        .summary-card .count { font-size: 32px; font-weight: bold; color: #0078d4; }");
        sb.AppendLine("        .summary-card .label { font-size: 14px; color: #666; margin-top: 5px; }");
        sb.AppendLine("        .empty-state { padding: 40px; text-align: center; color: #999; font-style: italic; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // Header
        sb.AppendLine($"        <h1>Database Discovery Report</h1>");
        sb.AppendLine($"        <p><strong>Server:</strong> {report.Server}</p>");
        sb.AppendLine($"        <p><strong>Database:</strong> {report.Database}</p>");
        sb.AppendLine($"        <p><strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

        // Summary Section
        sb.AppendLine("        <h2>Summary</h2>");
        sb.AppendLine("        <div class=\"summary-grid\">");
        
        AddSummaryCard(sb, "Stored Procedures", report.StoredProcedures.Count);
        AddSummaryCard(sb, "Sequences", report.Sequences.Count);
        AddSummaryCard(sb, "Triggers", report.Triggers.Count);
        AddSummaryCard(sb, "Extended Properties", report.ExtendedProperties.Count);
        AddSummaryCard(sb, "Spatial Columns", report.SpatialColumns.Count);
        AddSummaryCard(sb, "HierarchyId Columns", report.HierarchyIdColumns.Count);
        
        sb.AppendLine("        </div>");

        // Stored Procedures
        if (report.StoredProcedures.Any())
        {
            sb.AppendLine("        <h2>Stored Procedures</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Name</th><th>Location</th><th>Details</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var item in report.StoredProcedures.OrderBy(x => x.Name))
            {
                sb.AppendLine($"                <tr><td>{item.Name}</td><td>{item.Location}</td><td>{item.Details}</td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        // Sequences
        if (report.Sequences.Any())
        {
            sb.AppendLine("        <h2>Sequences</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Name</th><th>Location</th><th>Details</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var item in report.Sequences.OrderBy(x => x.Name))
            {
                sb.AppendLine($"                <tr><td>{item.Name}</td><td>{item.Location}</td><td>{item.Details}</td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        // Triggers
        if (report.Triggers.Any())
        {
            sb.AppendLine("        <h2>Triggers</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Name</th><th>Location</th><th>Details</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var item in report.Triggers.OrderBy(x => x.Name))
            {
                sb.AppendLine($"                <tr><td>{item.Name}</td><td>{item.Location}</td><td>{item.Details}</td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        // Extended Properties
        if (report.ExtendedProperties.Any())
        {
            sb.AppendLine("        <h2>Extended Properties</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Name</th><th>Location</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var item in report.ExtendedProperties.OrderBy(x => x.Name))
            {
                sb.AppendLine($"                <tr><td>{item.Name}</td><td>{item.Location}</td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        // Element Type Counts
        if (report.ElementTypeCounts.Any())
        {
            sb.AppendLine("        <h2>All Element Types</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Element Type</th><th>Count</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var kvp in report.ElementTypeCounts.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"                <tr><td>{kvp.Key}</td><td><span class=\"badge badge-primary\">{kvp.Value}</span></td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        // Unhandled Element Types
        if (report.UnhandledElementTypes.Any())
        {
            sb.AppendLine("        <h2>Unhandled Element Types</h2>");
            sb.AppendLine("        <p>The following element types were found but are not currently processed by the generator:</p>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <thead>");
            sb.AppendLine("                <tr><th>Element Type</th></tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");
            foreach (var type in report.UnhandledElementTypes)
            {
                sb.AppendLine($"                <tr><td>{type}</td></tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
        }

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private void AddSummaryCard(StringBuilder sb, string label, int count)
    {
        sb.AppendLine("            <div class=\"summary-card\">");
        sb.AppendLine($"                <div class=\"count\">{count}</div>");
        sb.AppendLine($"                <div class=\"label\">{label}</div>");
        sb.AppendLine("            </div>");
    }

    public bool WriteIndexHtml(string outputDirectory, List<ElementDiscoveryReport> reports)
    {
        try
        {
            var reportsDir = Path.Combine(outputDirectory, "DiscoveryReports");
            Directory.CreateDirectory(reportsDir);

            var indexPath = Path.Combine(reportsDir, "index.html");
            var html = GenerateIndexHtml(reports);
            File.WriteAllText(indexPath, html);

            _logger.LogInfo("Generated discovery reports index: ./output/DiscoveryReports/index.html");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write discovery reports index: {ex.Message}");
            return false;
        }
    }

    private string GenerateIndexHtml(List<ElementDiscoveryReport> reports)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Database Discovery Reports</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background-color: #f5f5f5; }");
        sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("        h1 { color: #333; border-bottom: 3px solid #0078d4; padding-bottom: 10px; }");
        sb.AppendLine("        .intro { color: #666; margin: 20px 0; line-height: 1.6; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; background-color: white; }");
        sb.AppendLine("        th { background-color: #0078d4; color: white; padding: 12px; text-align: left; font-weight: 600; }");
        sb.AppendLine("        td { padding: 12px; border-bottom: 1px solid #e0e0e0; }");
        sb.AppendLine("        tr:hover { background-color: #f9f9f9; }");
        sb.AppendLine("        a { color: #0078d4; text-decoration: none; font-weight: 500; }");
        sb.AppendLine("        a:hover { text-decoration: underline; }");
        sb.AppendLine("        .badge { display: inline-block; padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 600; margin-left: 8px; }");
        sb.AppendLine("        .badge-json { background-color: #28a745; color: white; }");
        sb.AppendLine("        .badge-html { background-color: #17a2b8; color: white; }");
        sb.AppendLine("        .summary { background-color: #f8f9fa; padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #0078d4; }");
        sb.AppendLine("        .timestamp { color: #999; font-size: 14px; margin-top: 20px; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // Header
        sb.AppendLine("        <h1>Database Discovery Reports</h1>");
        sb.AppendLine("        <p class=\"intro\">This page provides access to all generated database discovery reports. These reports contain information about database elements that were discovered during the DACPAC analysis.</p>");
        
        // Summary
        sb.AppendLine("        <div class=\"summary\">");
        sb.AppendLine($"            <strong>Total Reports:</strong> {reports.Count}<br>");
        sb.AppendLine($"            <strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("        </div>");

        // Reports Table
        sb.AppendLine("        <table>");
        sb.AppendLine("            <thead>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>Server</th>");
        sb.AppendLine("                    <th>Database</th>");
        sb.AppendLine("                    <th>Reports</th>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("            </thead>");
        sb.AppendLine("            <tbody>");

        foreach (var report in reports.OrderBy(r => r.Server).ThenBy(r => r.Database))
        {
            var htmlFileName = $"{report.Server}_{report.Database}_Discovery.html";
            var jsonFileName = $"{report.Server}_{report.Database}_Discovery.json";

            sb.AppendLine("                <tr>");
            sb.AppendLine($"                    <td>{report.Server}</td>");
            sb.AppendLine($"                    <td>{report.Database}</td>");
            sb.AppendLine("                    <td>");
            sb.AppendLine($"                        <a href=\"{htmlFileName}\">View Report</a>");
            sb.AppendLine("                        <span class=\"badge badge-html\">HTML</span>");
            sb.AppendLine($"                        <a href=\"{jsonFileName}\">Download JSON</a>");
            sb.AppendLine("                        <span class=\"badge badge-json\">JSON</span>");
            sb.AppendLine("                    </td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
