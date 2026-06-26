using Fcg.Catalog.Domain.Enums;
using Fcg.Catalog.Domain.Exceptions;
using Fcg.Catalog.Domain.ValueObjects;

namespace Fcg.Catalog.Domain.Entities;

public sealed class Jogo
{
    public string Id { get; private set; }
    public string Titulo { get; private set; }
    public string Descricao { get; private set; }
    public GeneroJogo Genero { get; private set; }
    public Preco Preco { get; private set; }
    public DateTime DataLancamento { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public bool Ativo { get; private set; }

    public Jogo(string titulo, string descricao, GeneroJogo genero, Preco preco, DateTime dataLancamento)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ValidacaoException("O título do jogo é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ValidacaoException("A descrição do jogo é obrigatória.");
        }

        Id = string.Empty;
        Titulo = titulo.Trim();
        Descricao = descricao.Trim();
        Genero = genero;
        Preco = preco ?? throw new ValidacaoException("O preço é obrigatório.");
        DataLancamento = dataLancamento;
        DataCriacao = DateTime.UtcNow;
        Ativo = true;
    }

    private Jogo() // Para deserialização do MongoDB
    {
        Id = string.Empty;
        Titulo = string.Empty;
        Descricao = string.Empty;
        Preco = null!;
    }

    public void AtualizarTitulo(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ValidacaoException("O título do jogo é obrigatório.");
        }

        Titulo = titulo.Trim();
    }

    public void AtualizarDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ValidacaoException("A descrição do jogo é obrigatória.");
        }

        Descricao = descricao.Trim();
    }

    public void AtualizarPreco(Preco preco)
    {
        Preco = preco ?? throw new ValidacaoException("O preço é obrigatório.");
    }

    public void AtualizarGenero(GeneroJogo genero)
    {
        Genero = genero;
    }

    public void Desativar() => Ativo = false;

    public void Ativar() => Ativo = true;
}
