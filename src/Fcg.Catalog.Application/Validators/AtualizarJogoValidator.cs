using Fcg.Catalog.Application.DTOs.Request;
using FluentValidation;

namespace Fcg.Catalog.Application.Validators;

public sealed class AtualizarJogoValidator : AbstractValidator<AtualizarJogoRequestDto>
{
    public AtualizarJogoValidator()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().WithMessage("O campo Título é obrigatório.")
            .MaximumLength(200).WithMessage("O campo Título deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage("O campo Descrição é obrigatório.")
            .MaximumLength(2000).WithMessage("O campo Descrição deve ter no máximo 2000 caracteres.");

        RuleFor(x => x.Genero)
            .IsInEnum().WithMessage("O gênero informado é inválido.");

        RuleFor(x => x.Preco)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser um valor positivo.");
    }
}
