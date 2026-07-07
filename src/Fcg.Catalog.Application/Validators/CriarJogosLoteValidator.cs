using Fcg.Catalog.Application.DTOs.Request;
using FluentValidation;

namespace Fcg.Catalog.Application.Validators;

public sealed class CriarJogosLoteValidator : AbstractValidator<List<CriarJogoRequestDto>>
{
    public CriarJogosLoteValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("A lista de jogos não pode estar vazia.")
            .Must(x => x.Count <= 100).WithMessage("Não é permitido criar mais de 100 jogos por lote.");
    }
}
