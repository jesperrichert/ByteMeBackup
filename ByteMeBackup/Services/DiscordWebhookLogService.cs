using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ByteMeBackup.Configuration;
using Spectre.Console;

namespace ByteMeBackup.Services;

public class DiscordWebhookLogService
{
    private ConfigService ConfigService;

    public DiscordWebhookLogService(ConfigService configService)
    {
        ConfigService = configService;
    }

    public async Task SendLogAsync(string message)
    {
        var config = ConfigService.Get();
        if (string.IsNullOrEmpty(config!.DiscordWebhookUrl))
        {
            AnsiConsole.Markup("[red]Discord webhook URL is not configured![/]\n");
            return;
        }

        using var httpClient = new HttpClient();
        var payload = new
        {
            content = message,
            username = "ByteMeBackup",
        };

        var response = await httpClient.PostAsJsonAsync(config.DiscordWebhookUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            AnsiConsole.Markup("[red]Discord webhook upload failed![/]\n");
        }
    }
}