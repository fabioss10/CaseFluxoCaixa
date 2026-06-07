using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;

public class SaldoConsolidado
{
    public DateOnly Data { get; private set; }

    public decimal TotalCreditos { get; private set; }

    public decimal TotalDebitos { get; private set; }

    public decimal Saldo { get; private set; }

    public DateTime UltimaAtualizacao { get; private set; }



    private SaldoConsolidado()
    {
    }

    public SaldoConsolidado(DateOnly data, decimal saldoDiaAnterior = 0)
    {
        Data = data;
        TotalCreditos = 0;
        TotalDebitos = 0;
        Saldo = saldoDiaAnterior;
        UltimaAtualizacao = DateTime.UtcNow;
    }

    public void AplicarLancamento(Lancamento lancamento)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        switch (lancamento.Tipo)
        {
            case TipoLancamento.Credito:
                TotalCreditos += lancamento.Valor;
                Saldo += lancamento.Valor;
                break;

            case TipoLancamento.Debito:
                TotalDebitos += lancamento.Valor;
                Saldo -= lancamento.Valor;
                break;

            default:
                throw new InvalidOperationException(
                    "Tipo de lançamento inválido.");
        }

        UltimaAtualizacao = DateTime.UtcNow;
    }

    public static SaldoConsolidado CriarComLancamento(
        Lancamento lancamento,
        decimal saldoDiaAnterior)
    {
        var saldo = new SaldoConsolidado(
            DateOnly.FromDateTime(lancamento.DataCriacao),
            saldoDiaAnterior);

        saldo.AplicarLancamento(lancamento);

        return saldo;
    }

    public static SaldoConsolidado CriarSemLancamento(
        Lancamento lancamento,
        decimal saldoDiaAnterior)
    {
        var saldo = new SaldoConsolidado(
            DateOnly.FromDateTime(lancamento.DataCriacao),
            saldoDiaAnterior);

        saldo.AplicarLancamento(lancamento);

        return saldo;
    }

    public static SaldoConsolidado CriarSaldoVazio(
        DateOnly data,
        decimal saldoDiaAnterior)
    {
        return new SaldoConsolidado(data, saldoDiaAnterior);
    }

}