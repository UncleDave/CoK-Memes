namespace ChampionsOfKhazad.Bot.GenAi;

public record CustomerDisciplinePage(IReadOnlyList<CustomerDisciplineCustomer> Customers, int? NextOffset);
