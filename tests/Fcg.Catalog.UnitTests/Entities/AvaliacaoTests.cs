using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using FluentAssertions;

namespace Fcg.Catalog.UnitTests.Entities;

public class AvaliacaoTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Criar_DeveAceitarNotasNoIntervaloValido(int nota)
    {
        var avaliacao = new Avaliacao(
            "jogo-1",
            "usuario-1",
            nota,
            "comentário válido");

        avaliacao.Nota.Should().Be(nota);
        avaliacao.Comentario.Should().Be("comentário válido");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Criar_DeveRejeitarNotasForaDoIntervalo(int nota)
    {
        var act = () => new Avaliacao("jogo-1", "usuario-1", nota, "comentário válido");

        act.Should().Throw<ValidacaoException>()
            .WithMessage($"*{Avaliacao.NotaMinima}*{Avaliacao.NotaMaxima}*");
    }

    [Fact]
    public void Criar_DeveRejeitarComentarioVazio()
    {
        var act = () => new Avaliacao("jogo-1", "usuario-1", 4, " ");

        act.Should().Throw<ValidacaoException>()
            .WithMessage("*comentário*");
    }

    [Fact]
    public void Criar_DeveRejeitarComentarioMuitoLongo()
    {
        var comentario = new string('x', 4001);

        var act = () => new Avaliacao("jogo-1", "usuario-1", 4, comentario);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("*4000*");
    }

    [Fact]
    public void Criar_DeveNormalizarTagsEEliminarDuplicadas()
    {
        var avaliacao = new Avaliacao(
            "jogo-1",
            "usuario-1",
            5,
            "comentário",
            tags: [" História ", "história", "RPG", "rpg", "  Aventura  "]);

        avaliacao.Tags.Should().BeEquivalentTo(["história", "rpg", "aventura"]);
    }

    [Fact]
    public void Criar_DeveRejeitarMaisDeDezTags()
    {
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();

        var act = () => new Avaliacao("jogo-1", "usuario-1", 4, "comentário", tags: tags);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("*10 tags*");
    }

    [Fact]
    public void Criar_DeveTransformarContextoNuloEmDicionarioVazio()
    {
        var avaliacao = new Avaliacao("jogo-1", "usuario-1", 4, "comentário");

        avaliacao.Contexto.Should().NotBeNull();
        avaliacao.Contexto.Should().BeEmpty();
    }
}
