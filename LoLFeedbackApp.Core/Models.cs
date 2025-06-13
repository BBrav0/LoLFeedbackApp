using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LoLFeedbackApp.Core.Models
{
    // DTO for the local client API response
    public class SummonerDto
    {
        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("tagLine")]
        public string TagLine { get; set; } = string.Empty;

        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty;
        public string DisplayName => $"{GameName}#{TagLine}";
    }

    // DTO for the public Account API response
    public class AccountDto
    {
        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("tagLine")]
        public string TagLine { get; set; } = string.Empty;
    }

    // DTOs for Match Details
    public class MatchDto
    {
        [JsonPropertyName("info")]
        public MatchInfo Info { get; set; } = new();
    }

    public class MatchInfo
    {
        [JsonPropertyName("participants")]
        public List<Participant> Participants { get; set; } = new();
        [JsonPropertyName("teams")]
        public List<Team> Teams { get; set; } = new();
    }

    public class Participant
    {
        [JsonPropertyName("summonerName")]
        public string SummonerName { get; set; } = string.Empty;
        [JsonPropertyName("championName")]
        public string ChampionName { get; set; } = string.Empty;
        [JsonPropertyName("kills")]
        public int Kills { get; set; }
        [JsonPropertyName("deaths")]
        public int Deaths { get; set; }
        [JsonPropertyName("assists")]
        public int Assists { get; set; }
        [JsonPropertyName("totalDamageDealtToChampions")]
        public int TotalDamageDealtToChampions { get; set; }
        [JsonPropertyName("teamId")]
        public int TeamId { get; set; }
        [JsonPropertyName("puuid")]
        public string Puuid { get; set; } = string.Empty;
        public string KDA => $"{Kills}/{Deaths}/{Assists}";
    }

    public class Team
    {
        [JsonPropertyName("teamId")]
        public int TeamId { get; set; }
        [JsonPropertyName("win")]
        public bool Win { get; set; }
    }
} 