using FluentValidation;

namespace GestionProjects.Application.Validators.Admin;

public record CreateServiceInput(string? Code, string? Libelle, string? DirectionId, bool EstActive);
public record UpdateServiceInput(Guid Id, string? Code, string? Libelle, string? DirectionId, bool EstActive);

public sealed class CreateServiceValidator : AbstractValidator<CreateServiceInput>
{
    public CreateServiceValidator()
    {
        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est requis.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.DirectionId)
            .NotEmpty().WithMessage("La direction est requise.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("L'identifiant de direction est invalide.");

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Le code ne peut pas dépasser 50 caractères.");
    }
}

public sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceInput>
{
    public UpdateServiceValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("L'identifiant du service est requis.");

        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est requis.")
            .MaximumLength(200).WithMessage("Le libellé ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Le code est requis.")
            .MaximumLength(50).WithMessage("Le code ne peut pas dépasser 50 caractères.");

        RuleFor(x => x.DirectionId)
            .NotEmpty().WithMessage("La direction est requise.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("L'identifiant de direction est invalide.");
    }
}
