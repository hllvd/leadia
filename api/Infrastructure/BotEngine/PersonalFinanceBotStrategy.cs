using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.BotEngine;

public class PersonalFinanceBotStrategy : IBotStrategy
{
    public BotType SupportedType => BotType.PersonalFinance;

    public async Task<string> ProcessMessageAsync(User user, string message, CancellationToken ct = default)
    {
        // Google Sheets integration removed. Strategy still offers basic confirmations.
        var intent = ParseIntent(message);

        return intent switch
        {
            Intent.RegisterExpense  => await HandleRegisterExpenseAsync(user, message, ct),
            Intent.RegisterIncome   => await HandleRegisterIncomeAsync(user, message, ct),
            Intent.QuerySpending    => Task.FromResult("📌 Relatórios desativados: integração com Google Sheets removida."),
            Intent.QueryIncome      => Task.FromResult("📌 Relatórios desativados: integração com Google Sheets removida."),
            Intent.MonthlySummary   => Task.FromResult("📌 Relatórios desativados: integração com Google Sheets removida."),
            Intent.Help             => BuildHelpMessage(),
            _                       => "Não entendi 🤔 Tente: 'gastei R$50 em alimentação' ou 'quanto gastei esse mês?'\n\nDigite *ajuda* para ver todos os comandos."
        };
    }

    private static Intent ParseIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("gastei") || lower.Contains("gasto") || lower.Contains("paguei"))
            return Intent.RegisterExpense;
        if (lower.Contains("recebi") || lower.Contains("receita") || lower.Contains("salário") || lower.Contains("salario"))
            return Intent.RegisterIncome;
        if (lower.Contains("quanto gastei") || lower.Contains("total gasto") || lower.Contains("gastos"))
            return Intent.QuerySpending;
        if (lower.Contains("quanto recebi") || lower.Contains("total receita"))
            return Intent.QueryIncome;
        if (lower.Contains("resumo") || lower.Contains("extrato") || lower.Contains("relatório") || lower.Contains("relatorio"))
            return Intent.MonthlySummary;
        if (lower.Contains("ajuda") || lower.Contains("help") || lower.Contains("comandos"))
            return Intent.Help;
        return Intent.Unknown;
    }

    private async Task<string> HandleRegisterExpenseAsync(User user, string message, CancellationToken ct)
    {
        var (amount, category) = ExtractAmountAndCategory(message);
        if (amount <= 0) return "Não consegui identificar o valor 😕 Tente: 'gastei R$50 em alimentação'";

        // Acknowledge locally — external persistence removed with Google Sheets
        return $"✅ Despesa registrada localmente!\n💸 R$ {amount:F2} em *{category}*";
    }

    private async Task<string> HandleRegisterIncomeAsync(User user, string message, CancellationToken ct)
    {
        var (amount, category) = ExtractAmountAndCategory(message, defaultCategory: "Receita");
        if (amount <= 0) return "Não consegui identificar o valor 😕 Tente: 'recebi R$5000 de salário'";

        // Acknowledge locally — external persistence removed with Google Sheets
        return $"✅ Receita registrada localmente!\n💰 R$ {amount:F2} em *{category}*";
    }

    private async Task<string> HandleQuerySpendingAsync(User user, string message, CancellationToken ct)
    {
        return "📌 Relatórios desativados: integração com Google Sheets removida.";
    }

    private async Task<string> HandleQueryIncomeAsync(User user, string message, CancellationToken ct)
    {
        return "📌 Relatórios desativados: integração com Google Sheets removida.";
    }

    private async Task<string> HandleMonthlySummaryAsync(User user, string message, CancellationToken ct)
    {
        return "📌 Relatórios desativados: integração com Google Sheets removida.";
    }

    private static string BuildHelpMessage() =>
        """
        🤖 *Comandos disponíveis:*
        ──────────────────
        💸 Registrar gasto:
           "gastei R$50 em alimentação"
        💰 Registrar receita:
           "recebi R$5000 de salário"
        📊 Ver gastos do mês:
           "quanto gastei em março?"
        📈 Ver receitas do mês:
           "quanto recebi esse mês?"
        📋 Resumo mensal:
           "me dá um resumo do mês"
        ──────────────────
        """;

    private static (decimal Amount, string Category) ExtractAmountAndCategory(
        string message, string defaultCategory = "Outros")
    {
        // Extract R$ value
        var amountMatch = System.Text.RegularExpressions.Regex.Match(
            message, @"R?\$?\s*(\d+(?:[.,]\d{1,2})?)");
        decimal amount = 0;
        if (amountMatch.Success)
            decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount);

        // Extract category keyword
        var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "alimentação", "Alimentação" }, { "comida", "Alimentação" }, { "restaurante", "Alimentação" },
            { "transporte", "Transporte" }, { "uber", "Transporte" }, { "gasolina", "Transporte" },
            { "saúde", "Saúde" }, { "remédio", "Saúde" }, { "médico", "Saúde" },
            { "lazer", "Lazer" }, { "cinema", "Lazer" }, { "viagem", "Lazer" },
            { "moradia", "Moradia" }, { "aluguel", "Moradia" }, { "condomínio", "Moradia" },
            { "salário", "Salário" }, { "freelance", "Freelance" }, { "receita", "Receita" }
        };

        var category = defaultCategory;
        var lower = message.ToLowerInvariant();
        foreach (var (keyword, cat) in categoryMap)
        {
            if (lower.Contains(keyword.ToLowerInvariant()))
            {
                category = cat;
                break;
            }
        }

        return (amount, category);
    }

    private static int? ExtractMonth(string message)
    {
        var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
            { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
            { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
        };
        foreach (var (name, num) in months)
            if (message.Contains(name, StringComparison.OrdinalIgnoreCase))
                return num;
        return null;
    }

    private enum Intent
    {
        RegisterExpense, RegisterIncome, QuerySpending, QueryIncome, MonthlySummary, Help, Unknown
    }
}
