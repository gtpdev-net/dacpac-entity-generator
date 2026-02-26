using Catalogue.Core.Models;
using FluentValidation;

namespace Catalogue.Core.Validation;

public class SourceValidator : AbstractValidator<Source>
{
    public SourceValidator()
    {
        RuleFor(x => x.ServerName)
            .NotEmpty().WithMessage("Server name is required.")
            .MaximumLength(255).WithMessage("Server name must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
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

        RuleFor(x => x.SourceId)
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
