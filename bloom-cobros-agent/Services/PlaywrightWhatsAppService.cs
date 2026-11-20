using Microsoft.Playwright;

namespace BloomCobrosAgent.Services;

public sealed class PlaywrightWhatsAppService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowserContext? _context;

    public async Task OpenPreparedChatAsync(string phone, string message)
    {
        if (_context == null)
        {
            _playwright = await Playwright.CreateAsync();
            var profile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BloomCobrosAgent", "PlaywrightProfile");
            Directory.CreateDirectory(profile);
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(profile,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Channel = "msedge",
                    Args = ["--start-maximized"],
                    IgnoreHTTPSErrors = false
                });
        }

        var page = _context.Pages.FirstOrDefault(p => !p.IsClosed) ?? await _context.NewPageAsync();
        var url = $"https://web.whatsapp.com/send?phone={Uri.EscapeDataString(phone)}&text={Uri.EscapeDataString(message)}";
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await page.BringToFrontAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
        {
            try { await _context.CloseAsync(); } catch { }
        }
        _playwright?.Dispose();
    }
}
