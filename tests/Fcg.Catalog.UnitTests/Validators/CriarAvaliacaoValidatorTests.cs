using Fcg.Catalog.Application.DTOs.Request;
using Fcg.Catalog.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Fcg.Catalog.UnitTests.Validators;

public class CriarAvaliacaoValidatorTests
{
    private readonly CriarAvaliacaoValidator _validator = new();

    [Fact]
    public void DevePassar_QuandoDadosValidos()
    {
        var dto = new CriarAvaliacaoRequestDto(
            "jogo-1",
            5,
            "Comentário válido",
            "Bom jogo",
            ["rpg", "história"],
            new Dictionary<string, string> { ["plataforma"] = "PC" });

        var result = _validator.TestValidate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void DeveRetornarErro_QuandoNotaForaDoIntervalo(int nota)
    {
        var dto = new CriarAvaliacaoRequestDto("jogo-1", nota, "comentário");

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Nota);
    }

    [Fact]
    public void DeveRetornarErro_QuandoComentarioVazio()
    {
        var dto = new CriarAvaliacaoRequestDto("jogo-1", 4, " ");

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Comentario);
    }
}
