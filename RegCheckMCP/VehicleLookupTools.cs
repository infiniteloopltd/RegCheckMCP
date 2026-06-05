using ModelContextProtocol.Server;

using System.ComponentModel;
using System.Xml.Linq;
using RegCheckMCP.Auth;

[McpServerToolType]
public class VehicleLookupTools
{
  

    private static readonly Dictionary<string, string> CountryEndpoints = new()
    {
        { "AE", "CheckUAE" },
        { "AR", "CheckArgentina" },
        { "AT", "CheckAustria" },
        { "AU", "CheckAustralia" },
        { "BO", "CheckBolivia" },
        { "BR", "CheckBrazil" },
        { "CA", "CheckCanada" },
        { "CH", "CheckSwitzerland" },
        { "CL", "CheckChile" },
        { "CN", "CheckChina" },
        { "CO", "CheckColombia" },
        { "CR", "CheckCostaRica" },
        { "CY", "CheckCyprus" },
        { "CZ", "CheckCzechRepublic" },
        { "DE", "CheckGermany" },
        { "DK", "CheckDenmark" },
        { "EC", "CheckEcuador" },
        { "EE", "CheckEstonia" },
        { "ES", "CheckSpain" },
        { "FI", "CheckFinland" },
        { "FR", "CheckFrance" },
        { "GB", "Check" },
        { "GR", "CheckGreece" },
        { "HR", "CheckCroatia" },
        { "HU", "CheckHungary" },
        { "ID", "CheckIndonesia" },
        { "IE", "CheckIreland" },
        { "IL", "CheckIsrael" },
        { "IN", "CheckIndia" },
        { "IS", "CheckIceland" },
        { "IT", "CheckItaly" },
        { "KZ", "CheckKazakhstan" },
        { "LK", "CheckSriLanka" },
        { "LT", "CheckLithuania" },
        { "LV", "CheckLatvia" },
        { "MT", "CheckMalta" },
        { "MX", "CheckMexico" },
        { "MY", "CheckMalaysia" },
        { "NG", "CheckNigeria" },
        { "NL", "CheckNetherlands" },
        { "NO", "CheckNorway" },
        { "NZ", "CheckNewZealand" },
        { "OM", "CheckOman" },
        { "PE", "CheckPeru" },
        { "PK", "CheckPakistan" },
        { "PL", "CheckPoland" },
        { "PT", "CheckPortugal" },
        { "RO", "CheckRomania" },
        { "RU", "CheckRussia" },
        { "SE", "CheckSweden" },
        { "SG", "CheckSingapore" },
        { "SI", "CheckSlovenia" },
        { "SK", "CheckSlovakia" },
        { "TN", "CheckTunisia" },
        { "TW", "CheckTaiwan" },
        { "UA", "CheckUkraine" },
        { "US", "CheckUSA" },
        { "ZA", "CheckSouthAfrica" },
    };

    // Countries that require a state/province parameter
    private static readonly HashSet<string> StateRequired = new() { "AU", "CA", "US", "PK" };

    private readonly HttpClient _http;


    private readonly IHttpContextAccessor _httpContextAccessor;

    public VehicleLookupTools(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _http = httpClientFactory.CreateClient("regcheck");
        _httpContextAccessor = httpContextAccessor;
    }



    [McpServerTool(Name = "lookup_vehicle", Title = "Look up vehicle by registration", ReadOnly = true)]
    [Description(
        "Look up a vehicle registration plate for any supported country. " +
        "Supported country codes: AE, AR, AT, AU, BO, BR, CA, CH, CL, CN, CO, CR, CY, CZ, DE, DK, " +
        "EC, EE, ES, FI, FR, GR, GB, HR, HU, ID, IE, IL, IN, IS, IT, KZ, LK, LT, LV, MT, MX, MY, NG, " +
        "NL, NO, NZ, OM, PE, PK, PL, PT, RO, RU, SE, SG, SI, SK, TN, TW, UA, US, ZA. " +
        "For AU, CA, US and PK a state code is also required, e.g. 'NSW', 'ON', 'NC'.")]

    public async Task<string> LookupVehicle(
        [Description("Vehicle registration plate number.")]
        string registration,
        [Description("Two-letter ISO country code, e.g. 'IE', 'DE', 'FR'.")]
        string countryCode,
        [Description("State or province code. Required for AU, CA, US, PK. Leave empty for all other countries.")]
        string? state = null)
    {
        var country = countryCode.Trim().ToUpper();

        if (!CountryEndpoints.TryGetValue(country, out var endpoint))
            return $"Error: Country code '{country}' is not supported. " +
                   $"Supported codes: {string.Join(", ", CountryEndpoints.Keys.Order())}";

        if (StateRequired.Contains(country) && string.IsNullOrWhiteSpace(state))
            return $"Error: A state/province code is required for country '{country}'. " +
                   $"Please provide the state parameter, e.g. 'NSW' for Australia, 'ON' for Canada, 'NC' for USA.";

        return await CallEndpoint(endpoint, registration, state);
    }

    private async Task<string> CallEndpoint(string endpoint, string registration, string? state)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        Console.WriteLine($"[AUTH] HttpContext is null: {httpContext is null}");

        if (httpContext is not null)
        {
            foreach (var header in httpContext.Request.Headers)
                Console.WriteLine($"[AUTH] Header: {header.Key} = {header.Value}");
        }

        var authHeader = httpContext?.Request.Headers["Authorization"].FirstOrDefault();
        var apiKey = httpContext?.Request.Headers["X-Api-Key"].FirstOrDefault();

        Console.WriteLine($"[AUTH] authHeader='{authHeader}' apiKey='{apiKey}'");


        string? username = null;

  

        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // OAuth path — extract username from access token
            var token = authHeader["Bearer ".Length..].Trim();
            var (extractedUsername, _) = AccessToken.DecodeAccessToken(token);

            if (extractedUsername is null)
                return "Error: Invalid or expired Bearer token.";

            username = extractedUsername;
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Legacy path — API key is the username
            username = apiKey;
        }
        else
        {
            return "Error: No credentials provided. Supply either an Authorization: Bearer token or X-Api-Key header.";
        }

        var plate = Uri.EscapeDataString(registration.Trim());
        var url = $"https://www.regcheck.org.uk/api/reg.asmx/{endpoint}" +
                  $"?RegistrationNumber={plate}&username={username}";

        if (!string.IsNullOrWhiteSpace(state))
            url += $"&State={Uri.EscapeDataString(state.Trim())}";

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return $"Error: API returned {(int)response.StatusCode} {response.StatusCode}. Body: {errorBody}";
        }

        var xml = await response.Content.ReadAsStringAsync();

        var vehicleJson = XDocument.Parse(xml)
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "vehicleJson")
            ?.Value;

        if (string.IsNullOrEmpty(vehicleJson))
            return $"Error: vehicleJson node not found in response. Raw: {xml}";

        return vehicleJson;
    }
}