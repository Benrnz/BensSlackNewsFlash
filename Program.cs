using System.Net;
using System.Text.Json;

public static class Program
{
    private static readonly HttpClient Client = new HttpClient();
    
    public static async Task Main(string[] args)
    {
        string appToken = File.ReadAllText("../../../ignore/BotUserOAuthToken.txt").Trim();
        var channels = await ListUserChannels(appToken);

        Console.WriteLine("Enter start date (yyyy-MM-dd):");
        string? startDate = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(startDate))
        {
            Console.WriteLine("Start date cannot be empty.");
            return;
        }

        Console.WriteLine("Enter end date (yyyy-MM-dd):");
        string? endDate = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(endDate))
        {
            Console.WriteLine("End date cannot be empty.");
            return;
        }

        string outputFilePath = "SlackMessages.txt";

        try
        {
            var slackMessages = await FetchSlackMessages(appToken, startDate, endDate);
            WriteMessagesToFile(slackMessages, outputFilePath);
            Console.WriteLine($"Messages written to {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Client.Dispose();
        }
    }

    private static async Task<string> FetchSlackMessages(string token, string startDate, string endDate)
    {
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        string url = $"https://slack.com/api/conversations.history?oldest={startDate}&latest={endDate}";
        var response = await Client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch messages from Slack API.");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private static void WriteMessagesToFile(string messagesJson, string filePath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var messages = JsonSerializer.Deserialize<dynamic>(messagesJson, options);

        if (messages == null || messages["messages"] == null)
        {
            throw new Exception("No messages found.");
        }

        using var writer = new StreamWriter(filePath);
        foreach (var message in messages["messages"])
        {
            writer.WriteLine(message["text"]);
        }
    }

    private static async Task<string> ListUserChannels(string token)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        string url = "https://slack.com/api/users.conversations";
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch user channels from Slack API.");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> AuthenticateUser()
    {
        const string clientId = "YOUR_CLIENT_ID"; // Replace with your Slack app's Client ID
        const string clientSecret = "YOUR_CLIENT_SECRET"; // Replace with your Slack app's Client Secret
        const string redirectUri = "http://localhost:5000/callback";

        string authUrl = $"https://slack.com/oauth/v2/authorize?client_id={clientId}&scope=channels:read,groups:read,im:read,mpim:read&redirect_uri={redirectUri}";
        Console.WriteLine("Please open the following URL in your browser to authenticate:");
        Console.WriteLine(authUrl);

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/callback/");
        listener.Start();

        Console.WriteLine("Waiting for authentication callback...");
        var context = await listener.GetContextAsync();
        var query = context.Request.QueryString;
        string code = query["code"];

        using var writer = new StreamWriter(context.Response.OutputStream);
        writer.WriteLine("Authentication successful! You can close this window.");
        context.Response.Close();

        using var client = new HttpClient();
        var tokenResponse = await client.PostAsync("https://slack.com/api/oauth.v2.access", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new Exception("Failed to exchange authorization code for access token.");
        }

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
        var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        return tokenJson.GetProperty("access_token").GetString();
    }
}
