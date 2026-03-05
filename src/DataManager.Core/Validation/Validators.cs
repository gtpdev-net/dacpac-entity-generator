using DataManager.Core.Models.Entities;
using FluentValidation;

namespace DataManager.Core.Validation;

public class ServerValidator : AbstractValidator<Server>
{
    public ServerValidator()
    {
        RuleFor(x => x.ServerName)
            .NotEmpty().WithMessage("Server name is required.")
            .MaximumLength(255).WithMessage("Server name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Role must be a valid ServerRole value.");
    }
}

public class TargetDatabaseValidator : AbstractValidator<TargetDatabase>
{
    public TargetDatabaseValidator()
    {
        RuleFor(x => x.DatabaseName)
            .NotEmpty().WithMessage("Database name is required.")
            .MaximumLength(255).WithMessage("Database name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.ServerId)
            .GreaterThan(0).WithMessage("A parent server must be selected.");
    }
}

public class ServerConnectionValidator : AbstractValidator<ServerConnection>
{
    public ServerConnectionValidator()
    {
        RuleFor(x => x.Hostname)
            .NotEmpty().WithMessage("Hostname is required.")
            .MaximumLength(255).WithMessage("Hostname must not exceed 255 characters.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Port must be between 1 and 65535.")
            .When(x => x.Port.HasValue);

        RuleFor(x => x.NamedInstance)
            .MaximumLength(128).WithMessage("Named instance must not exceed 128 characters.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required when using SQL Authentication.")
            .MaximumLength(255).WithMessage("Username must not exceed 255 characters.")
            .When(x => x.AuthenticationType == AuthenticationType.SqlAuth);

        RuleFor(x => x.AuthenticationType)
            .IsInEnum().WithMessage("Authentication type must be a valid value.");
    }
}

public class SourceDatabaseValidator : AbstractValidator<SourceDatabase>
{
    public SourceDatabaseValidator()
    {
        RuleFor(x => x.DatabaseName)
            .NotEmpty().WithMessage("Database name is required.")
            .MaximumLength(255).WithMessage("Database name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.ServerId)
            .GreaterThan(0).WithMessage("A parent server must be selected.");
    }
}

public class SourceTableValidator : AbstractValidator<SourceTable>
{
    public SourceTableValidator()
    {
        RuleFor(x => x.SchemaName)
            .NotEmpty().WithMessage("Schema name is required.")
            .MaximumLength(128).WithMessage("Schema name must not exceed 128 characters.");

        RuleFor(x => x.TableName)
            .NotEmpty().WithMessage("Table name is required.")
            .MaximumLength(255).WithMessage("Table name must not exceed 255 characters.");

        RuleFor(x => x.EstimatedRowCount)
            .GreaterThanOrEqualTo(0).WithMessage("Estimated row count must be a non-negative number.")
            .When(x => x.EstimatedRowCount.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(4000).WithMessage("Notes must not exceed 4000 characters.");

        RuleFor(x => x.DatabaseId)
            .GreaterThan(0).WithMessage("A parent database must be selected.");
    }
}

public class SourceColumnValidator : AbstractValidator<SourceColumn>
{
    private static readonly char[] ValidPersistenceTypes = { 'R', 'D' };

    public SourceColumnValidator()
    {
        RuleFor(x => x.ColumnName)
            .NotEmpty().WithMessage("Column name is required.")
            .MaximumLength(255).WithMessage("Column name must not exceed 255 characters.");

        RuleFor(x => x.PersistenceType)
            .Must(pt => ValidPersistenceTypes.Contains(pt))
            .WithMessage("Persistence type must be 'R' (Relational) or 'D' (Document).");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be a non-negative number.");

        RuleFor(x => x.TableId)
            .GreaterThan(0).WithMessage("A parent table must be selected.");
    }
}
