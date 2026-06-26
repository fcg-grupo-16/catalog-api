using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.ValueObjects;
using FluentAssertions;

namespace Fcg.Catalog.UnitTests.ValueObjects;

public class PrecoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(29.90)]
    [InlineData(199.99)]
    public void DeveCriarPreco_QuandoValorValido(decimal valor)
    {
        var preco = new Preco(valor);

        preco.Valor.Should().Be(valor);
        preco.Moeda.Should().Be("BRL");
    }

    [Fact]
    public void DeveCriarPrecoGratuito()
    {
        var preco = new Preco(0);

        preco.Valor.Should().Be(0);
    }

    [Fact]
    public void DeveLancarExcecao_QuandoValorNegativo()
    {
        var act = () => new Preco(-1);

        act.Should().Throw<ValidacaoException>()
            .WithMessage("O preço deve ser um valor positivo.");
    }

    [Fact]
    public void DeveNormalizarMoedaParaMaiusculo()
    {
        var preco = new Preco(10, "usd");

        preco.Moeda.Should().Be("USD");
    }

    [Fact]
    public void DeveSerIgualQuandoMesmoValorEMoeda()
    {
        var preco1 = new Preco(29.90m);
        var preco2 = new Preco(29.90m);

        preco1.Should().Be(preco2);
    }
}
