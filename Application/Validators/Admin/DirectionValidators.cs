using FluentValidation;

namespace GestionProjects.Application.Validators.Admin;

public record CreateDirectionInput(string? Code, string? Libelle, string? DSIId, bool EstActive);
public record UpdateDirectionInput(Guid Id, string? Code, string? Libelle, string? DSIId, bool EstActive);

public sealed class CreateDirectionValidator : AbstractValidator<CreateDirectionInput>
{
    public CreateDirectionValidator()
    {
        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est requis.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Le code ne peut pas dépasser 50 caractères.")
            .Matches(@"^[A-Z0-9\-]*$").When(x => !string.IsNullOrWhiteSpace(x.Code))
            .WithMessage("Le code ne peut contenir que des lettres majuscules, chiffres et tirets.");

        RuleFor(x => x.DSIId)
            .Must(id => id == null || Guid.TryParse(id, out _))
            .WithMessage("L'identifiant du responsable est invalide.");
    }
}

public sealed class UpdateDirectionValidator : AbstractValidator<UpdateDirectionInput>
{
    public UpdateDirectionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("L'identifiant de la direction est requis.");

        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est requis.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Le code est requis.")
            .MaximumLength(50).WithMessage("Le code ne peut pas dépasser 50 caractères.")
            .Matches(@"^[A-Z0-9\-]*$").WithMessage("Le code ne peut contenir que des lettres majuscules, chiffres et tirets.");

        RuleFor(x => x.DSIId)
            .Must(id => id == null || Guid.TryParse(id, out _))
            .WithMessage("L'identifiant du responsable est invalide.");
    }
}
