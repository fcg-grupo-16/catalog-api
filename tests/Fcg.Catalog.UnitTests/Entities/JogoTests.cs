using Fcg.Catalog.Domain.Entities;
using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.ValueObjects;
using FluentAssertions;

namespace Fcg.Catalog.UnitTests.Entities;

public class JogoTests
{
    private static readonly Preco PrecoValido = new(59.90m);
    private static readonly DateTime DataLancamento = new(2024, 1, 15);

    [Fact]
    public void DeveCriarJogo_QuandoDadosValidos()
    {
        var jogo = new Jogo("Título do Jogo", "Descrição do jogo", GeneroJogo.Acao, PrecoValido, DataLancamento);

        jogo.Titulo.Should().Be("Título do Jogo");
        jogo.Descricao.Should().Be("Descrição do jogo");
        jogo.Genero.Should().Be(GeneroJogo.Acao);
        jogo.Preco.Should().Be(PrecoValido);
        jogo.DataLancamento.Should().Be(DataLancamento);
        jogo.Ativo.Should().BeTrue();
        jogo.DataCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoTituloVazioOuNulo(string? titulo)
    {
        var act = () => new Jogo(titulo!, "Descrição", GeneroJogo.RPG, PrecoValido, DataLancamento);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O título do jogo é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoDescricaoVaziaOuNula(string? descricao)
    {
        var act = () => new Jogo("Título", descricao!, GeneroJogo.RPG, PrecoValido, DataLancamento);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("A descrição do jogo é obrigatória.");
    }

    [Fact]
    public void DeveLancarExcecao_QuandoPrecoNulo()
    {
        var act = () => new Jogo("Título", "Descrição", GeneroJogo.RPG, null!, DataLancamento);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O preço é obrigatório.");
    }

    [Fact]
    public void DeveAtualizarTitulo()
    {
        var jogo = new Jogo("Título Original", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        jogo.AtualizarTitulo("Novo Título");

        jogo.Titulo.Should().Be("Novo Título");
    }

    [Fact]
    public void DeveAtualizarPreco()
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);
        var novoPreco = new Preco(29.90m);

        jogo.AtualizarPreco(novoPreco);

        jogo.Preco.Should().Be(novoPreco);
    }

    [Fact]
    public void DeveDesativarEAtivar()
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        jogo.Desativar();
        jogo.Ativo.Should().BeFalse();

        jogo.Ativar();
        jogo.Ativo.Should().BeTrue();
    }

    [Fact]
    public void DeveAtualizarDescricao()
    {
        var jogo = new Jogo("Título", "Descrição Original", GeneroJogo.Acao, PrecoValido, DataLancamento);

        jogo.AtualizarDescricao("Nova Descrição");

        jogo.Descricao.Should().Be("Nova Descrição");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoAtualizarDescricaoVaziaOuNula(string? descricao)
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        var act = () => jogo.AtualizarDescricao(descricao!);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("A descrição do jogo é obrigatória.");
    }

    [Fact]
    public void DeveAtualizarGenero()
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        jogo.AtualizarGenero(GeneroJogo.RPG);

        jogo.Genero.Should().Be(GeneroJogo.RPG);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DeveLancarExcecao_QuandoAtualizarTituloVazioOuNulo(string? titulo)
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        var act = () => jogo.AtualizarTitulo(titulo!);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O título do jogo é obrigatório.");
    }

    [Fact]
    public void DeveLancarExcecao_QuandoAtualizarPrecoNulo()
    {
        var jogo = new Jogo("Título", "Descrição", GeneroJogo.Acao, PrecoValido, DataLancamento);

        var act = () => jogo.AtualizarPreco(null!);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O preço é obrigatório.");
    }
}
