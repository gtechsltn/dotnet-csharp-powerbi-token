using System.Runtime.Caching;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;

public class EmbedInfo
{
    public Guid ReportId { get; set; }
    public string EmbedUrl { get; set; }
    public string AccessToken { get; set; }
}

public class Embedder
{
    private static readonly string _clientId = "YourClientId";
    private static readonly string _clientSecret = "YourClientSecret";
    private static readonly string _tenantId = "YourTenantId";
    private static readonly string _username = "YourUsername";
    private static readonly string _password = "YourPassword";

    public static readonly string group_id = "YourGroupId";
    public static readonly string report_id = "YourReportId";

    // Power BI Service API Root URL
    const string urlPowerBiRestApiRoot = "https://api.powerbi.com/";

    public static string GetAppOnlyAccessToken()
    {
        var tenantAuthority = $"https://login.microsoftonline.com/{_tenantId}";

        var appConfidential = ConfidentialClientApplicationBuilder
            .Create(_clientId)
            .WithClientSecret(_clientSecret)
            .WithAuthority(tenantAuthority)
            .Build();

        var scopesDefault = new string[] { "https://analysis.windows.net/powerbi/api/.default" };
        var authResult = appConfidential.AcquireTokenForClient(scopesDefault).ExecuteAsync().Result;

        return authResult.AccessToken;
    }

    public static string GetUserAccessToken()
    {
        var tenantAuthority = $"https://login.microsoftonline.com/{_tenantId}";

        var pca = PublicClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority(tenantAuthority)
            .Build();

        var scopes = new string[] { "https://analysis.windows.net/powerbi/api/.default" };
        var securePassword = new System.Security.SecureString();
        foreach (char c in _password)
            securePassword.AppendChar(c);

        var authResult = pca.AcquireTokenByUsernamePassword(scopes, _username, securePassword)
            .ExecuteAsync()
            .Result;

        return authResult.AccessToken;
    }

    public static PowerBIClient GetPowerBiClient()
    {
        var tokenCredentials = new TokenCredentials(GetUserAccessToken(), "Bearer");
        return new PowerBIClient(new Uri(urlPowerBiRestApiRoot), tokenCredentials);
    }

    public static void SetCache(string key, object value, int seconds)
    {
        MemoryCache cache = MemoryCache.Default;
        CacheItemPolicy policy = new CacheItemPolicy
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(seconds)
        };
        cache.Set(key, value, policy);
    }

    public static object? GetCache(string key)
    {
        MemoryCache cache = MemoryCache.Default;
        if (cache.Contains(key))
        {
            return cache.Get(key);
        }
        else
        {
            return null; // Or handle the absence of the key as needed
        }
    }

    public static async Task<String> GetEmbedInfo(Guid workspaceId, Guid reportId)
    {
        string cacheKey = String.Format("{0}-{1}", workspaceId.ToString(), reportId.ToString());
        if (GetCache(cacheKey) != null)
        {
            return String.Format("{0}", GetCache(cacheKey));
        }

        var pbiClient = GetPowerBiClient();
        // var report = await pbiClient.Reports.GetReportInGroupAsync(workspaceId, reportId);
        // var embedUrl = report.EmbedUrl;

        var generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
        var tokenResponse = await pbiClient.Reports.GenerateTokenInGroupAsync(
            workspaceId,
            reportId,
            generateTokenRequestParameters
        );
        var embedToken = tokenResponse.Token;
        SetCache(cacheKey, embedToken, 3500);

        return embedToken;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // var appToken = Embedder.GetAppOnlyAccessToken(); //OK
        // PowerBIClient powerBIClient = Embedder.GetPowerBiClient(); //OK
        for (var i = 0; i < 2; i++)
        {
            var EmbedInfo = await Embedder.GetEmbedInfo(
                workspaceId: Guid.Parse(Embedder.group_id),
                reportId: Guid.Parse(Embedder.report_id)
            );
            Console.WriteLine(String.Format("Done {0}!", i));
        }
    }
}
