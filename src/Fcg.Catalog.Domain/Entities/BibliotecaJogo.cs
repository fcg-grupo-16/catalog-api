using Fcg.Catalog.Domain.Exceptions;

namespace Fcg.Catalog.Domain.Entities;

public sealed class BibliotecaJogo
{
    public string Id { get; private set; }
    public string UsuarioId { get; private set; }
    public string JogoId { get; private set; }
    public DateTime DataAquisicao { get; private set; }

    public BibliotecaJogo(string usuarioId, string jogoId)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new ValidacaoException("O identificador do usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(jogoId))
        {
            throw new ValidacaoException("O identificador do jogo é obrigatório.");
        }

        Id = string.Empty;
        UsuarioId = usuarioId;
        JogoId = jogoId;
        DataAquisicao = DateTime.UtcNow;
    }

    private BibliotecaJogo() // Para deserialização do MongoDB
    {
        Id = string.Empty;
        UsuarioId = string.Empty;
        JogoId = string.Empty;
    }
}
