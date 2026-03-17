namespace Domain.Constants;

public static class FactKeys
{
    public const string Intent = "Intent";
    public const string PropertyType = "Property Type";
    public const string Location = "Location";
    public const string Budget = "Budget";
    public const string MinPrice = "Min Price";
    public const string MaxPrice = "Max Price";
    public const string Bedrooms = "Bedrooms";
    public const string Garage = "Garage";
    public const string ApprovedFinancing = "Approved Financing";
    public const string Purpose = "Purpose";
    public const string PurchaseTimeline = "Purchase Timeline";
    public const string ViewingInterest = "Viewing Interest";
    public const string MentionedProperty = "Mentioned Property";
    public const string LeadScore = "Lead Score";
    public const string Children = "Children";
    public const string Pet = "Pet";

    public static readonly IReadOnlyList<string> All = 
    [
        Intent,
        PropertyType,
        Location,
        Budget,
        MinPrice,
        MaxPrice,
        Bedrooms,
        Garage,
        ApprovedFinancing,
        Purpose,
        PurchaseTimeline,
        ViewingInterest,
        MentionedProperty,
        LeadScore,
        Children,
        Pet
    ];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        { Intent, "Intenção" },
        { PropertyType, "Tipo de imóvel" },
        { Location, "Localização" },
        { Budget, "Orçamento" },
        { MinPrice, "Preço mínimo" },
        { MaxPrice, "Preço máximo" },
        { Bedrooms, "Quartos" },
        { Garage, "Garagem" },
        { ApprovedFinancing, "Financiamento aprovado" },
        { Purpose, "Finalidade" },
        { PurchaseTimeline, "Prazo de compra" },
        { ViewingInterest, "Interesse em visita" },
        { MentionedProperty, "Imóvel mencionado" },
        { LeadScore, "Lead Score" },
        { Children, "Filhos" },
        { Pet, "Pet" }
    };
}
