using System.Text;

namespace ShaunWhiteReborn.Services;

public static class GatorSoap
{
    private const string ContentNamespace = "http://gatorservice.ubi.com/2008/01";
    private const string AuthNamespace = "http://gatorservice.ubi.com/2008/08";

    private static readonly (int Id, string Type, bool Specific)[] Fields =
    {
        (1, "String", true),
        (2, "String", true),
        (3, "String", true),
        (4, "Int32", true),
        (5, "Int32", true),
        (6, "String", true),
        (7, "String", true),
        (8, "Int32", true),
        (9, "String", true),
        (10, "Float", true),
        (11, "String", true),
        (12, "String", true),
        (13, "String", true),
        (256, "Int32", false),
        (257, "Int32", false),
        (258, "Int32", false),
    };

    private static readonly string FieldsDefinitionsResponse = BuildFieldsDefinitions();

    public static bool IsKnown(string action) => action switch
    {
        "AuthenticatePS3Account" or "GetFieldsDefinitions" or "GetSlotInfoByGame" or "Search" or
        "NewContentID" or "SaveContent" or "SaveContentComplete" => true,
        _ => false,
    };

    public static string Respond(string action) => action switch
    {
        "AuthenticatePS3Account" => Envelope(
            $"<AuthenticatePS3AccountResponse xmlns=\"{AuthNamespace}\"><AuthenticatePS3AccountResult>SWS-REBORN-TOKEN</AuthenticatePS3AccountResult></AuthenticatePS3AccountResponse>"),
        "GetFieldsDefinitions" => FieldsDefinitionsResponse,
        "GetSlotInfoByGame" => Envelope(
            $"<GetSlotInfoByGameResponse xmlns=\"{ContentNamespace}\"><GetSlotInfoByGameResult>" +
            "<a:SlotInfo><a:ContentTypeID>4097</a:ContentTypeID><a:Used>0</a:Used><a:Total>10</a:Total></a:SlotInfo>" +
            "</GetSlotInfoByGameResult></GetSlotInfoByGameResponse>"),
        "Search" => Envelope(
            $"<SearchResponse xmlns=\"{ContentNamespace}\"><SearchResult><a:ReturnedResultCount>0</a:ReturnedResultCount>" +
            "<a:SearchResults/><a:TotalResultCount>0</a:TotalResultCount></SearchResult></SearchResponse>"),
        "NewContentID" => Envelope(
            $"<NewContentIDResponse xmlns=\"{ContentNamespace}\"><NewContentIDResult>10001</NewContentIDResult></NewContentIDResponse>"),
        _ => Envelope($"<{(action.Length == 0 ? "Request" : action)}Response xmlns=\"{ContentNamespace}\"/>"),
    };

    private static string Envelope(string body) =>
        $"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:a=\"{ContentNamespace}\"><s:Body>{body}</s:Body></s:Envelope>";

    private static string BuildFieldsDefinitions()
    {
        var sb = new StringBuilder();
        sb.Append($"<GetFieldsDefinitionsResponse xmlns=\"{ContentNamespace}\"><GetFieldsDefinitionsResult>");
        sb.Append("<a:ContentTypeFields><a:FieldsDefinitions>");
        foreach (var (id, type, specific) in Fields)
        {
            sb.Append($"<a:FieldDefinitionDTO><a:ID>{id}</a:ID><a:ClientType>{type}</a:ClientType>");
            sb.Append($"<a:SpecificFieldDefinition>{(specific ? "true" : "false")}</a:SpecificFieldDefinition>");
            sb.Append("<a:SearchableField>true</a:SearchableField></a:FieldDefinitionDTO>");
        }
        sb.Append("</a:FieldsDefinitions></a:ContentTypeFields></GetFieldsDefinitionsResult></GetFieldsDefinitionsResponse>");
        return Envelope(sb.ToString());
    }
}
