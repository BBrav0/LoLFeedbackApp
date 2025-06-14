using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms; // Required for RichTextBox
using LoLFeedbackApp.Core.Models; // Import the Models namespace

namespace LoLFeedbackApp.Core
{
    public class RiotApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string AMERICAS_URL = "https://americas.api.riotgames.com";
        private readonly RichTextBox _statusBox;

        public RiotApiService(RichTextBox statusBox)
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

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get account by Riot ID. Status: {response.StatusCode}, Response: {content}");
                }

                return JsonSerializer.Deserialize<AccountDto>(content);
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error looking up account: {ex.Message}\r\n");
                return null;
            }
        }

        public async Task<List<string>?> GetMatchHistory(string puuid, int count = 20)
        {
            if (string.IsNullOrEmpty(puuid))
            {
                _statusBox.AppendText("Error: PUUID is null or empty, cannot get match history.\r\n");
                return null;
            }

            try
            {
                var url = $"{AMERICAS_URL}/lol/match/v5/matches/by-puuid/{puuid}/ids?type=ranked&start=0&count={count}";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get match history. Status: {response.StatusCode}, Response: {content}");
                }

                return JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error getting match history: {ex.Message}\r\n");
                return null;
            }
        }

        public async Task<TimelineFrameDto?> GetMatchFrameAtOneMinute(string matchId)
        { 
            if (string.IsNullOrEmpty(matchId))
            {
                _statusBox.AppendText("Error: Match ID is null or empty, cannot get timeline.\r\n");
                return null;
            }

            try
            {
                // Construct the URL for the timeline endpoint
                var url = $"{AMERICAS_URL}/lol/match/v5/matches/{matchId}/timeline";
                //_statusBox.AppendText($"Fetching match timeline from: {url}\r\n");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get match timeline. Status: {response.StatusCode}, Response: {content}");
                }

                var timelineData = JsonSerializer.Deserialize<MatchTimelineDto>(content);
                if (timelineData == null)
                {
                    _statusBox.AppendText("Failed to deserialize timeline data.\r\n");
                    return null;
                }

                // The timeline contains a list of frames, recorded each minute.
                // Index 0 = 0 minute mark (start of game)
                // Index 1 = 1 minute mark
                if (timelineData.Info.Frames.Count > 1)
                {
                    _statusBox.AppendText("Successfully parsed timeline. Returning frame for 1-minute mark.\r\n");
                    return timelineData.Info.Frames[1]; // Return the frame for the 1-minute mark
                }
                else
                {
                    _statusBox.AppendText("Match was too short or timeline data is unavailable.\r\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error getting match timeline: {ex.Message}\r\n");
                return null;
            }
        }
        public async Task<MatchDto?> GetMatchDetails(string matchId)
        {
            try
            {
                var url = $"{AMERICAS_URL}/lol/match/v5/matches/{matchId}";
                _statusBox.AppendText($"\r\n");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get match details. Status: {response.StatusCode}, Response: {content}");
                }

                return JsonSerializer.Deserialize<MatchDto>(content);
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error getting match details: {ex.Message}\r\n");
                return null;
            }
        }

        public async Task<MatchTimelineDto?> GetMatchTimeline(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                _statusBox.AppendText("Error: Match ID is null or empty, cannot get timeline.\r\n");
                return null;
            }

            try
            {
                var url = $"{AMERICAS_URL}/lol/match/v5/matches/{matchId}/timeline";
                _statusBox.AppendText($"Fetching match timeline from: {url}\r\n");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get match timeline. Status: {response.StatusCode}, Response: {content}");
                }

                return JsonSerializer.Deserialize<MatchTimelineDto>(content);
            }
            catch (Exception ex)
            {
                _statusBox.AppendText($"Error getting match timeline: {ex.Message}\r\n");
                return null;
            }
        }
    }
} 