using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text.Json; // For JsonSerializer
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoLFeedbackApp.Core.Models; // For DTOs

namespace LoLFeedbackApp.Core
{
    public class MainForm : Form
    {
        private readonly RiotApiService _riotApiService;
        private readonly RichTextBox statusBox;

        private static readonly HttpClient localApiClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });

        public MainForm()
        {
            this.Text = "LoL Feedback App";
            this.Size = new System.Drawing.Size(1200, 600);

            statusBox = new RichTextBox
            {
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Location = new Point(10, 10),
                Size = new System.Drawing.Size(760, 540),
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
            try
            {
                var lockfilePath = @"C:\Riot Games\League of Legends\lockfile";
                if (File.Exists(lockfilePath))
                {
                    string lockfileContent;
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
                        return;
                    }

                    if (!string.IsNullOrEmpty(lockfileContent))
                    {
                        var match = Regex.Match(lockfileContent, @"^LeagueClient:(\d+):(\d+):([^:]+):([^:]+)$");
                        if (match.Success)
                        {
                            var port = match.Groups[2].Value;
                            var password = match.Groups[3].Value;

                            var localSummoner = await GetCurrentSummoner(port, password);

                            if (localSummoner != null)
                            {
                                var correctAccount = await _riotApiService.GetAccountByRiotIdAsync(localSummoner.GameName, localSummoner.TagLine);

                                if (correctAccount != null && !string.IsNullOrEmpty(correctAccount.Puuid))
                                {
                                    // Save to cache when we get fresh data
                                    await PlayerCache.SaveCacheDataAsync(
                                        correctAccount.Puuid,
                                        localSummoner.GameName,
                                        localSummoner.TagLine
                                    );

                                    await DisplayMatchHistory(correctAccount.Puuid);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // League client is not running, try to use cached data
                    var cachedData = await PlayerCache.LoadCacheDataAsync();
                    if (cachedData != null && PlayerCache.IsCacheValid(cachedData))
                    {
                        statusBox.AppendText("League client is not running. Using cached player data.\r\n");
                        await DisplayMatchHistory(cachedData.Puuid);
                    }
                    else
                    {
                        statusBox.Text = "League client is not running and no valid cached data found.";
                    }
                }
            }
            catch (Exception ex)
            {
                statusBox.AppendText($"ERROR: {ex.Message}\r\n{ex.StackTrace}\r\n");
            }
        }

        private async Task DisplayMatchHistory(string puuid)
        {
            var matches = await _riotApiService.GetMatchHistory(puuid);
            if (matches != null && matches.Any())
            {
                var latestMatchId = matches.First();
                var matchDetails = await _riotApiService.GetMatchDetails(latestMatchId);
                
                if (matchDetails != null)
                {
                    var team1 = matchDetails.Info.Participants.Take(5).ToList();
                    var team2 = matchDetails.Info.Participants.Skip(5).Take(5).ToList();

                    // Find which team the player is on
                    var playerTeam = team1.Any(p => p.Puuid == puuid) ? team1 : team2;
                    var enemyTeam = playerTeam == team1 ? team2 : team1;

                    // Determine max widths for each stat column for proper alignment
                    int maxNameLen = Math.Max("Summoner (Champion)".Length, matchDetails.Info.Participants.Max(p => $"{p.SummonerName} ({p.ChampionName})".Length)) + 2;
                    int maxKDALen = Math.Max("KDA".Length, matchDetails.Info.Participants.Max(p => p.KDA.Length)) + 2;
                    int maxDamageLen = Math.Max("Damage Dealt".Length, matchDetails.Info.Participants.Max(p => p.TotalDamageDealtToChampions.ToString("N0").Length)) + 2;

                    // Header
                    string playerTeamStatus = playerTeam.First().TeamId == 100 ? 
                        (matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 100)?.Win == true ? "(Won)" : "(Lost)") :
                        (matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 200)?.Win == true ? "(Won)" : "(Lost)");
                    string enemyTeamStatus = playerTeam.First().TeamId == 100 ?
                        (matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 200)?.Win == true ? "(Won)" : "(Lost)") :
                        (matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 100)?.Win == true ? "(Won)" : "(Lost)");
                    string playerTeamHeader = ($"Your Team {playerTeamStatus}").PadRight(maxNameLen + maxKDALen + maxDamageLen);
                    string enemyTeamHeader = ($"Enemy Team {enemyTeamStatus}");
                    statusBox.AppendText($"{playerTeamHeader}{enemyTeamHeader}\r\n");

                    string separatorLine = new string('-', (maxNameLen + maxKDALen + maxDamageLen) * 2 + 4); // +4 for spacing between teams
                    statusBox.AppendText($"{separatorLine}\r\n");

                    // Store the original font to revert after bolding
                    Font originalFont = statusBox.Font;
                    Font boldFont = new Font(originalFont, FontStyle.Bold);

                    // Player Stats
                    for (int i = 0; i < 5; i++)
                    {
                        var p1 = playerTeam[i];
                        var p2 = enemyTeam[i];

                        // Format Player Team Member
                        string p1NameAndChamp = $"{p1.SummonerName} ({p1.ChampionName})";
                        if (p1.Puuid == puuid)
                        {
                            p1NameAndChamp += " (YOU)";
                        }
                        string p1KDA = p1.KDA;
                        string p1Damage = p1.TotalDamageDealtToChampions.ToString("N0");

                        // Format Enemy Team Member
                        string p2NameAndChamp = $"{p2.SummonerName} ({p2.ChampionName})";
                        if (p2.Puuid == puuid)
                        {
                            p2NameAndChamp += " (YOU)";
                        }
                        string p2KDA = p2.KDA;
                        string p2Damage = p2.TotalDamageDealtToChampions.ToString("N0");

                        // Recalculate maxNameLen if (YOU) is added to ensure proper padding
                        int currentMaxNameLen = Math.Max("Summoner (Champion)".Length, matchDetails.Info.Participants.Max(p => $"{p.SummonerName} ({p.ChampionName})" + (p.Puuid == puuid ? " (YOU)" : "")).Length) + 2;

                        // Construct the full lines, padding each part
                        string p1Line = p1NameAndChamp.PadRight(currentMaxNameLen) +
                                        p1KDA.PadRight(maxKDALen) +
                                        p1Damage.PadRight(maxDamageLen);

                        string p2Line = p2NameAndChamp.PadRight(currentMaxNameLen) +
                                        p2KDA.PadRight(maxKDALen) +
                                        p2Damage.PadRight(maxDamageLen);

                        // Print Player Team Member
                        if (p1.Puuid == puuid)
                        {
                            statusBox.SelectionFont = boldFont;
                            statusBox.AppendText(p1Line);
                            statusBox.SelectionFont = originalFont;
                        }
                        else
                        {
                            statusBox.AppendText(p1Line);
                        }
                        
                        statusBox.AppendText("    "); // Spacing between teams

                        // Print Enemy Team Member
                        if (p2.Puuid == puuid)
                        {
                            statusBox.SelectionFont = boldFont;
                            statusBox.AppendText(p2Line);
                            statusBox.SelectionFont = originalFont;
                        }
                        else
                        {
                            statusBox.AppendText(p2Line);
                        }
                        statusBox.AppendText($"\r\n");
                    }
                    statusBox.AppendText($"\r\n"); // Add an extra newline at the end
                }
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
                    var summoner = JsonSerializer.Deserialize<SummonerDto>(content);
                    if (summoner != null)
                    {
                        summoner.Puuid = summoner.Puuid.Trim();
                        statusBox.AppendText($"Parsed summoner - DisplayName: {summoner.DisplayName}");
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
} 