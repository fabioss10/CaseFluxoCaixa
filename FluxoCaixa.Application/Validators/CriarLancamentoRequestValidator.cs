using FluentValidation;
using FluxoCaixa.Application.DTOs;

namespace FluxoCaixa.Application.Validators
{
    public class CriarLancamentoRequestValidator : AbstractValidator<CriarLancamentoRequest>
    {

        // =======================================================================================
        // ARQUITETURA DE VALIDAÇÃO DE ENTRADA (EDGE VALIDATION & FAIL-FAST PRINCIPLE)
        // =======================================================================================
        // 1.
        // Esta classe implementa o princípio Fail-Fast (Falhe Rápido) na borda mais externa da API.
        // O objetivo é interceptar e rejeitar qualquer payload corrompido ou semanticamente inválido
        // (como valores zerados ou Enums de tipo inexistentes) assim que o JSON atinge a Controller.
        // Isso impede que requisições inválidas consumam processamento das Services, poluam o banco
        // de dados transacional ou gerem registros quebrados e 'Detached' na tabela de Outbox.
        //
        // 2.
        // A) DATA ANNOTATIONS (Atributos [Required], [Range] no DTO): Poluem as classes de modelo,
        //    possuem suporte limitado a validações complexas e acoplam regras de validação ao DTO.
        // B) VALIDAÇÃO MANUAL (Ifs dentro do Serviço/Controller): Mistura regras de infraestrutura
        //    com lógica de negócio (violação do Single Responsibility Principle), gerando código
        //    duplicado e de difícil manutenção/teste.
        // C) FLUENTVALIDATION: Mantém as regras 100% isoladas em classes dedicadas, permite testes
        //    unitários limpos das validações e oferece o método '.IsInEnum()', que valida de forma
        //    estrita se o valor enviado pertence ao range real do Enum, sem falhas de parse de string.
        //
        // 3.
        // A classe é registrada no container de DI via 'AddValidatorsFromAssemblyContaining' no Program.cs.
        // Graças ao pacote de AutoValidation habilitado ('AddFluentValidationAutoValidation'), o ASP.NET
        // intercepta o ciclo de Model Binding automaticamente. Se as regras falharem, a API aborta 
        // a execução imediatamente e devolve um 'HTTP 400 Bad Request' padronizado com os erros,
        // garantindo que a Service receba estritamente dados limpos e confiáveis.
        // =======================================================================================

        public CriarLancamentoRequestValidator()
        {
            // Validação de negócio básica para o valor
            RuleFor(x => x.Valor)
                .GreaterThan(0)
                .WithMessage("O valor do lançamento deve ser maior que zero.");

            // VALIDAÇÃO FAIL-FAST DO ENUM:
            // O método IsInEnum() valida automaticamente se a string ou número enviado no JSON
            // pertence estritamente às opções cadastradas no seu Enum.
            RuleFor(x => x.Tipo)
                .IsInEnum()
                .WithMessage("O tipo de lançamento fornecido é inválido. Escolha uma opção permitida.");
        }
    }
}
