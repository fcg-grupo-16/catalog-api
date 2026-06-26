using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Exceptions;
using FluentAssertions;

namespace Fcg.Catalog.UnitTests.Entities;

public class BibliotecaJogoTests
{
    [Fact]
    public void DeveCriarBibliotecaJogo_QuandoDadosValidos()
    {
        var bibliotecaJogo = new BibliotecaJogo("usuario-id", "jogo-id");

        bibliotecaJogo.UsuarioId.Should().Be("usuario-id");
        bibliotecaJogo.JogoId.Should().Be("jogo-id");
        bibliotecaJogo.DataAquisicao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoUsuarioIdVazioOuNulo(string? usuarioId)
    {
        var act = () => new BibliotecaJogo(usuarioId!, "jogo-id");

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O identificador do usuário é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoJogoIdVazioOuNulo(string? jogoId)
    {
        var act = () => new BibliotecaJogo("usuario-id", jogoId!);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O identificador do jogo é obrigatório.");
    }
}
