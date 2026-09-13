using Fcg.Catalog.Domain.Exceptions;

namespace Fcg.Catalog.Domain.Entities;

/// <summary>
/// Avaliação de um jogo feita por um usuário.
/// </summary>
/// <remarks>
/// Entidade desenhada para persistência em documento (MongoDB, collection <c>avaliacoes</c>) e não
/// em tabela: <see cref="Tags"/> é uma coleção embutida e <see cref="Contexto"/> é um sub-documento
/// de chaves LIVRES. Em modelo relacional isso exigiria duas tabelas extras e um `JOIN` por leitura;
/// aqui o documento é lido inteiro num único acesso.
/// </remarks>
public sealed class Avaliacao
{
    /// <summary>Nota mínima aceita.</summary>
    public const int NotaMinima = 1;

    /// <summary>Nota máxima aceita.</summary>
    public const int NotaMaxima = 5;

    private const int ComentarioMaxLength = 4000;
    private const int MaxTags = 10;

    public string Id { get; private set; } = string.Empty;
    public string JogoId { get; private set; }
    public string UsuarioId { get; private set; }

    /// <summary>Nota de 1 a 5.</summary>
    public int Nota { get; private set; }

    /// <summary>Título curto, OPCIONAL — parte do "schema flexível".</summary>
    public string? Titulo { get; private set; }

    public string Comentario { get; private set; }

    /// <summary>
    /// Tags livres (ex.: "história", "difícil", "bug"). Coleção embutida no documento: em relacional
    /// seria uma tabela de associação.
    /// </summary>
    public IReadOnlyList<string> Tags { get; private set; }

    /// <summary>
    /// Sub-documento de chaves LIVRES (ex.: <c>plataforma</c>, <c>horasJogadas</c>,
    /// <c>versaoDoJogo</c>). É o coração da "flexibilidade" do requisito: um cliente novo pode
    /// enviar campos que ainda não existiam, sem migração de schema e sem deploy.
    /// </summary>
    public IReadOnlyDictionary<string, string> Contexto { get; private set; }

    /// <summary>Contador de "achei útil". Incrementado atomicamente com <c>$inc</c>.</summary>
    public int VotosUteis { get; private set; }

    public DateTime DataCriacao { get; private set; }

    public Avaliacao(
        string jogoId,
        string usuarioId,
        int nota,
        string comentario,
        string? titulo = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? contexto = null)
    {
        if (string.IsNullOrWhiteSpace(jogoId))
            throw new ValidacaoException("O jogo da avaliação é obrigatório.");

        if (string.IsNullOrWhiteSpace(usuarioId))
            throw new ValidacaoException("O usuário da avaliação é obrigatório.");

        if (nota < NotaMinima || nota > NotaMaxima)
            throw new ValidacaoException($"A nota deve estar entre {NotaMinima} e {NotaMaxima}.");

        if (string.IsNullOrWhiteSpace(comentario))
            throw new ValidacaoException("O comentário da avaliação é obrigatório.");

        if (comentario.Length > ComentarioMaxLength)
            throw new ValidacaoException($"O comentário não pode passar de {ComentarioMaxLength} caracteres.");

        var tagsNormalizadas = (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // Teto no número de tags: sem limite, o documento cresceria sem controle e o índice
        // multikey em tags degradaria. Limite de domínio, não de banco.
        if (tagsNormalizadas.Count > MaxTags)
            throw new ValidacaoException($"No máximo {MaxTags} tags por avaliação.");

        JogoId = jogoId;
        UsuarioId = usuarioId;
        Nota = nota;
        Titulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim();
        Comentario = comentario.Trim();
        Tags = tagsNormalizadas;
        Contexto = contexto ?? new Dictionary<string, string>();
        VotosUteis = 0;
        DataCriacao = DateTime.UtcNow;
    }

    // Construtor privado para a desserialização do driver do MongoDB.
    private Avaliacao()
    {
        JogoId = string.Empty;
        UsuarioId = string.Empty;
        Comentario = string.Empty;
        Tags = [];
        Contexto = new Dictionary<string, string>();
    }

    /// <summary>Reconstrói a entidade a partir do documento persistido (usado pelo repositório).</summary>
    public static Avaliacao Restaurar(
        string id, string jogoId, string usuarioId, int nota, string? titulo, string comentario,
        IReadOnlyList<string> tags, IReadOnlyDictionary<string, string> contexto,
        int votosUteis, DateTime dataCriacao) => new()
        {
            Id = id, JogoId = jogoId, UsuarioId = usuarioId, Nota = nota, Titulo = titulo,
            Comentario = comentario, Tags = tags, Contexto = contexto,
            VotosUteis = votosUteis, DataCriacao = dataCriacao
        };
}