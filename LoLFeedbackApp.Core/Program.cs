using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Linq;

namespace LoLFeedbackApp.Core
{
    // Class to hold the response from the /lol-summoner/v1/current-summoner endpoint
    public class SummonerDto
    {
        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("tagLine")]
        public string TagLine { get; set; } = string.Empty;

        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty; // This is the incorrect PUUID from the client

        public string DisplayName => $"{GameName}#{TagLine}";
    }

    // Class to hold the response from the /riot/account/v1/accounts/by-riot-id endpoint
    public class AccountDto
    {
        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty; // This is the CORRECT PUUID from the Riot servers

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("tagLine")]
        public string TagLine { get; set; } = string.Empty;
    }

    public class RiotApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string AMERICAS_URL = "https://americas.api.riotgames.com";
        private readonly TextBox _statusBox;

        public RiotApiService(TextBox statusBox)
        {
            _statusBox = statusBox;
            var apiKey = Environment.GetEnvironmentVariable("RIOT_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("RIOT_API_KEY environment variable is not set.");
            }
            _statusBox.AppendText($"API Key loaded: {apiKey.Substring(0, 4)}...\r\n");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
        }

        // Looks up an account using the Riot ID (game name + tag line) to get the correct PUUID.
        public async Task<AccountDto?> GetAccountByRiotIdAsync(string gameName, string tagLine)
        {
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(tagLine))
            {
                _statusBox.AppendText("Error: Game name or tag line from client is empty.\r\n");
                return null;
            }

            try
            {
                var url = $"{AMERICAS_URL}/riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";
                _statusBox.AppendText($"Looking up correct PUUID from: {url}\r\n");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get account by Riot ID. Status: {response.StatusCode}, Response: {content}");
                }

                _statusBox.AppendText($"Account lookup successful: {content}\r\n");
                return JsonSerializer.Deserialize<AccountDto>(content);
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error looking up account: {ex.Message}\r\n");
                return null;
            }
        }

        // Gets match history using a PUUID.
        public async Task<List<string>?> GetMatchHistory(string puuid, int count = 20)
        {
            if (string.IsNullOrEmpty(puuid))
            {
                _statusBox.AppendText("Error: PUUID is null or empty, cannot get match history.\r\n");
                return null;
            }

            try
            {
                var url = $"{AMERICAS_URL}/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count={count}";
                _statusBox.AppendText($"Making request to: {url}\r\n");
                _statusBox.AppendText($"Headers: {string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {h.Value.First()}"))}\r\n");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get match history. Status: {response.StatusCode}, Response: {content}");
                }

                _statusBox.AppendText($"Match history response: {content}\r\n");
                return JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error getting match history: {ex.Message}\r\n");
                return null;
            }
        }
    }


    public class MainForm : Form
    {
        private readonly RiotApiService _riotApiService;
        private readonly TextBox statusBox;

        private static readonly HttpClient localApiClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        public MainForm()
        {
            this.Text = "LoL Feedback App";
            this.Size = new System.Drawing.Size(800, 600);

            statusBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 10),
                Size = new Size(760, 540),
                ReadOnly = true,
                Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0)
            };
            this.Controls.Add(statusBox);

            try
            {
                _riotApiService = new RiotApiService(statusBox);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Riot API service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }

            _ = CheckLeagueClient();
        }

        private async Task CheckLeagueClient()
        {
            while (true)
            {
                try
                {
                    var lockfilePath = @"C:\Riot Games\League of Legends\lockfile";
                    if (File.Exists(lockfilePath))
                    {
                        string lockfileContent = null;
                        try
                        {
                            using (var fileStream = new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var reader = new StreamReader(fileStream))
                            {
                                lockfileContent = await reader.ReadToEndAsync();
                            }
                        }
                        catch (IOException)
                        {
                            statusBox.AppendText("Waiting for lockfile to be available...\r\n");
                            await Task.Delay(5000);
                            continue;
                        }

                        if (!string.IsNullOrEmpty(lockfileContent))
                        {
                            var match = Regex.Match(lockfileContent, @"^LeagueClient:(\d+):(\d+):([^:]+):([^:]+)$");
                            if (match.Success)
                            {
                                var port = match.Groups[2].Value;
                                var password = match.Groups[3].Value;

                                // Get summoner info (with the bad PUUID) from the local client
                                var localSummoner = await GetCurrentSummoner(port, password);

                                if (localSummoner != null)
                                {
                                    // *** THIS IS THE FINAL CORRECT LOGIC ***

                                    // 1. Use the gameName and tagLine from the client...
                                    statusBox.AppendText($"Found local summoner: {localSummoner.DisplayName}\r\n");
                                    statusBox.AppendText($"Local client PUUID (incorrect): {localSummoner.Puuid}\r\n");

                                    // 2. ...to look up the CORRECT account details from the Riot API.
                                    var correctAccount = await _riotApiService.GetAccountByRiotIdAsync(localSummoner.GameName, localSummoner.TagLine);

                                    if (correctAccount != null && !string.IsNullOrEmpty(correctAccount.Puuid))
                                    {
                                        statusBox.AppendText($"Riot API PUUID (correct): {correctAccount.Puuid}\r\n");

                                        // 3. Use the CORRECT PUUID to get the match history.
                                        var matches = await _riotApiService.GetMatchHistory(correctAccount.Puuid);
                                        if (matches != null)
                                        {
                                            foreach (var matchId in matches)
                                            {
                                                statusBox.AppendText($"  - Match ID: {matchId}\r\n");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        statusBox.Text = "Waiting for League client to start...";
                    }
                }
                catch (Exception ex)
                {
                    statusBox.AppendText($"ERROR: {ex.Message}\r\n{ex.StackTrace}\r\n");
                }
                await Task.Delay(5000);
            }
        }

        private async Task<SummonerDto?> GetCurrentSummoner(string port, string password)
        {
            try
            {
                localApiClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"riot:{password}"))
                );

                var response = await localApiClient.GetAsync($"https://127.0.0.1:{port}/lol-summoner/v1/current-summoner");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    statusBox.AppendText($"Raw summoner data: {content}\r\n");
                    var summoner = JsonSerializer.Deserialize<SummonerDto>(content);
                    if (summoner != null)
                    {
                        summoner.Puuid = summoner.Puuid.Trim();
                        statusBox.AppendText($"Parsed summoner - DisplayName: {summoner.DisplayName}, Puuid: {summoner.Puuid}\r\n");
                    }
                    return summoner;
                }
                else
                {
                    statusBox.AppendText($"Failed to get summoner data. Status: {response.StatusCode}, Error: {content}\r\n");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                statusBox.AppendText($"HTTP Request failed for local client: {ex.Message}\r\n");
                return null;
            }
        }
    }


    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                LoadEnvironmentVariables();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application failed to start: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LoadEnvironmentVariables()
        {
            try
            {
                var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException(".env file not found. Please create a .env file with your RIOT_API_KEY.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading .env file: {ex.Message}", ex);
            }
        }
    }
}