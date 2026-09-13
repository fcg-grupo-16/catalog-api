using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Domain.Entities;
using FluentValidation;

namespace Fcg.Catalog.Application.Validators;

public sealed class CriarAvaliacaoValidator : AbstractValidator<CriarAvaliacaoRequestDto>
{
    public CriarAvaliacaoValidator()
    {
        RuleFor(x => x.JogoId).NotEmpty().WithMessage("O jogo é obrigatório.");

        RuleFor(x => x.Nota)
            .InclusiveBetween(Avaliacao.NotaMinima, Avaliacao.NotaMaxima)
            .WithMessage($"A nota deve estar entre {Avaliacao.NotaMinima} e {Avaliacao.NotaMaxima}.");

        RuleFor(x => x.Comentario)
            .NotEmpty().WithMessage("O comentário é obrigatório.")
            .MaximumLength(4000);

        RuleFor(x => x.Titulo).MaximumLength(120).When(x => x.Titulo is not null);

        RuleFor(x => x.Tags!).Must(t => t.Count <= 10)
            .When(x => x.Tags is not null)
            .WithMessage("No máximo 10 tags por avaliação.");

        // Teto no sub-documento livre: "flexível" não pode virar "cliente grava 10 MB de lixo".
        RuleFor(x => x.Contexto!).Must(c => c.Count <= 20)
            .When(x => x.Contexto is not null)
            .WithMessage("No máximo 20 chaves em 'contexto'.");
        RuleForEach(x => x.Contexto!)
            .Must(kv => kv.Key.Length <= 50 && kv.Value.Length <= 500)
            .When(x => x.Contexto is not null)
            .WithMessage("Chaves de 'contexto' até 50 e valores até 500 caracteres.");
    }
}