using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.Validators;
using Fcg.Catalog.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Fcg.Catalog.UnitTests.Validators;

public class CriarJogoValidatorTests
{
    private readonly CriarJogoValidator _validator = new();

    [Fact]
    public void DevePassar_QuandoDadosValidos()
    {
        var dto = new CriarJogoRequestDto("Título", "Descrição", GeneroJogo.Acao, 59.90m, DateTime.Now);

        var result = _validator.TestValidate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DeveRetornarErro_QuandoTituloVazio(string? titulo)
    {
        var dto = new CriarJogoRequestDto(titulo!, "Descrição", GeneroJogo.Acao, 59.90m, DateTime.Now);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Titulo);
    }

    [Fact]
    public void DeveRetornarErro_QuandoPrecoNegativo()
    {
        var dto = new CriarJogoRequestDto("Título", "Descrição", GeneroJogo.Acao, -1, DateTime.Now);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Preco);
    }

    [Fact]
    public void DevePassar_QuandoPrecoZero()
    {
        var dto = new CriarJogoRequestDto("Título", "Descrição", GeneroJogo.Acao, 0, DateTime.Now);

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Preco);
    }
}
