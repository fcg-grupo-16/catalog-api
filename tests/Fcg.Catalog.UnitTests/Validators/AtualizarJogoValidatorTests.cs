using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.Validators;
using Fcg.Catalog.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Fcg.Catalog.UnitTests.Validators;

public class AtualizarJogoValidatorTests
{
    private readonly AtualizarJogoValidator _validator = new();

    [Fact]
    public void DevePassar_QuandoDadosValidos()
    {
        var dto = new AtualizarJogoRequestDto("Título Válido", "Descrição válida", GeneroJogo.Acao, 29.90m);

        var result = _validator.TestValidate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DeveRetornarErro_QuandoTituloVazio(string? titulo)
    {
        var dto = new AtualizarJogoRequestDto(titulo!, "Descrição", GeneroJogo.Acao, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Titulo);
    }

    [Fact]
    public void DeveRetornarErro_QuandoTituloExcedeMaximo()
    {
        var dto = new AtualizarJogoRequestDto(new string('A', 201), "Descrição", GeneroJogo.Acao, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Titulo);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DeveRetornarErro_QuandoDescricaoVazia(string? descricao)
    {
        var dto = new AtualizarJogoRequestDto("Título", descricao!, GeneroJogo.Acao, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Descricao);
    }

    [Fact]
    public void DeveRetornarErro_QuandoDescricaoExcedeMaximo()
    {
        var dto = new AtualizarJogoRequestDto("Título", new string('A', 2001), GeneroJogo.Acao, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Descricao);
    }

    [Fact]
    public void DeveRetornarErro_QuandoPrecoNegativo()
    {
        var dto = new AtualizarJogoRequestDto("Título", "Descrição", GeneroJogo.Acao, -1);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Preco);
    }

    [Fact]
    public void DevePassar_QuandoPrecoZero()
    {
        var dto = new AtualizarJogoRequestDto("Título", "Descrição", GeneroJogo.Acao, 0);

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Preco);
    }

    [Fact]
    public void DeveRetornarErro_QuandoGeneroInvalido()
    {
        var dto = new AtualizarJogoRequestDto("Título", "Descrição", (GeneroJogo)999, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Genero);
    }
}
