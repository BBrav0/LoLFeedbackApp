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
                                statusBox.AppendText($"Found local summoner: {localSummoner.DisplayName}\r\n");

                                var correctAccount = await _riotApiService.GetAccountByRiotIdAsync(localSummoner.GameName, localSummoner.TagLine);

                                if (correctAccount != null && !string.IsNullOrEmpty(correctAccount.Puuid))
                                {
                                    statusBox.AppendText($"Found account with PUUID: {correctAccount.Puuid}\r\n");

                                    var matches = await _riotApiService.GetMatchHistory(correctAccount.Puuid);
                                    if (matches != null && matches.Any())
                                    {
                                        var latestMatchId = matches.First();
                                        var matchDetails = await _riotApiService.GetMatchDetails(latestMatchId);
                                        
                                        if (matchDetails != null)
                                        {
                                            statusBox.AppendText($"\r\nLatest Match Details:\r\n");
                                            statusBox.AppendText($"Match ID: {latestMatchId}\r\n\r\n");

                                            var team1 = matchDetails.Info.Participants.Take(5).ToList();
                                            var team2 = matchDetails.Info.Participants.Skip(5).Take(5).ToList();

                                            // Determine max widths for each stat column for proper alignment
                                            int maxNameLen = Math.Max("Summoner (Champion)".Length, matchDetails.Info.Participants.Max(p => $"{p.SummonerName} ({p.ChampionName})".Length)) + 2;
                                            int maxKDALen = Math.Max("KDA".Length, matchDetails.Info.Participants.Max(p => p.KDA.Length)) + 2;
                                            int maxDamageLen = Math.Max("Damage Dealt".Length, matchDetails.Info.Participants.Max(p => p.TotalDamageDealtToChampions.ToString("N0").Length)) + 2;

                                            // Header
                                            string team1Status = matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 100)?.Win == true ? "(Won)" : "(Lost)";
                                            string team2Status = matchDetails.Info.Teams.FirstOrDefault(t => t.TeamId == 200)?.Win == true ? "(Won)" : "(Lost)";
                                            string team1Header = ($"Team 1 {team1Status}").PadRight(maxNameLen + maxKDALen + maxDamageLen);
                                            string team2Header = ($"Team 2 {team2Status}");
                                            statusBox.AppendText($"{team1Header}{team2Header}\r\n");

                                            string separatorLine = new string('-', (maxNameLen + maxKDALen + maxDamageLen) * 2 + 4); // +4 for spacing between teams
                                            statusBox.AppendText($"{separatorLine}\r\n\r\n");

                                            // Store the original font to revert after bolding
                                            Font originalFont = statusBox.Font;
                                            Font boldFont = new Font(originalFont, FontStyle.Bold);

                                            // Player Stats
                                            for (int i = 0; i < 5; i++)
                                            {
                                                var p1 = team1[i];
                                                var p2 = team2[i];

                                                // Format Team 1 Player
                                                string p1NameAndChamp = $"{p1.SummonerName} ({p1.ChampionName})";
                                                if (p1.Puuid == correctAccount.Puuid)
                                                {
                                                    p1NameAndChamp += " (YOU)";
                                                }
                                                string p1KDA = p1.KDA;
                                                string p1Damage = p1.TotalDamageDealtToChampions.ToString("N0");

                                                // Format Team 2 Player
                                                string p2NameAndChamp = $"{p2.SummonerName} ({p2.ChampionName})";
                                                if (p2.Puuid == correctAccount.Puuid)
                                                {
                                                    p2NameAndChamp += " (YOU)";
                                                }
                                                string p2KDA = p2.KDA;
                                                string p2Damage = p2.TotalDamageDealtToChampions.ToString("N0");

                                                // Recalculate maxNameLen if (YOU) is added to ensure proper padding
                                                int currentMaxNameLen = Math.Max("Summoner (Champion)".Length, matchDetails.Info.Participants.Max(p => $"{p.SummonerName} ({p.ChampionName})" + (p.Puuid == correctAccount.Puuid ? " (YOU)" : "")).Length) + 2;

                                                // Construct the full lines, padding each part
                                                string p1Line = p1NameAndChamp.PadRight(currentMaxNameLen) +
                                                                p1KDA.PadRight(maxKDALen) +
                                                                p1Damage.PadRight(maxDamageLen);

                                                string p2Line = p2NameAndChamp.PadRight(currentMaxNameLen) +
                                                                p2KDA.PadRight(maxKDALen) +
                                                                p2Damage.PadRight(maxDamageLen);

                                                // Print Team 1 Player
                                                if (p1.Puuid == correctAccount.Puuid)
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

                                                // Print Team 2 Player
                                                if (p2.Puuid == correctAccount.Puuid)
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
                            }
                        }
                    }
                }
                else
                {
                    statusBox.Text = "League client is not running.";
                }
            }
            catch (Exception ex)
            {
                statusBox.AppendText($"ERROR: {ex.Message}\r\n{ex.StackTrace}\r\n");
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
} 