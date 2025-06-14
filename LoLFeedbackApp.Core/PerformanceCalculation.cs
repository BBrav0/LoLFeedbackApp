using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace LoLFeedbackApp.Core
{
        //1 MIN check kills deaths gold, before minions spawn, lanes irrelevant
        //5 MIN check kills death gold, lanes very important
        //10 MIN check kills death gold, lanes important, compare objectives
        //14 MIN check gold, TURRETS SUPER IMPORTANT PLATING FALLS AFTER, objectives check, less important, grubs and drag, check cs for crazy differences
        //20 mins, check gold kills death, check objectives, check for crazy cs differences
        //25 mins, check gold kills deaths, check objectives, check for crazy cs differences, check for tier2/tier3 turrets being down
        //30 mins, inhib comparison/objectives final check
/// <summary>
/// Holds a precise snapshot of a player's stats at a specific moment in a match.
/// </summary>
    public class PlayerStatsAtTime
    {
        // Static info about the player
        public int ParticipantId { get; set; }
        public string SummonerName { get; set; } = string.Empty;
        public string ChampionName { get; set; } = string.Empty;
        public string Lane { get; set; } = string.Empty;
        public int TeamId { get; set; }

        // The stats calculated for a specific frame/minute
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Gold { get; set; }
        public int CreepScore { get; set; }
        public int DamageToChampions { get; set; }
        public int Level { get; set; }

        public string KDA => $"{Kills}/{Deaths}/{Assists}";
    }

    /// <summary>
    /// A class dedicated to analyzing match data.
    /// </summary>
    public class PerformanceCalculation
    {
        private string myPuuid = string.Empty;

        /// <summary>
        /// Analyzes a match and generates a detailed performance report.
        /// </summary>
        public string AnalyzeMatch(MatchDto matchDetails, MatchTimelineDto matchTimeline, string userPuuid)
        {
            myPuuid = userPuuid;  // Set the global variable
            var report = new StringBuilder();
            var mainUserParticipant = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == userPuuid);
            
            if (mainUserParticipant == null)
                return "Could not find user in the match.";


            int userTeamId = mainUserParticipant.TeamId;

            // Analyze key timestamps
            var timestamps = new[] { 1, 5, 10, 14, 20 };

            //solo impact score
            var sis = new[] {50.0, 50.0, 50.0, 50.0, 50.0, 50.0};

            //team impact score
            var tis = new[] {50.0, 50.0, 50.0, 50.0, 50.0, 50.0};

            foreach (var minute in timestamps)
            {
                List<PlayerStatsAtTime> pstats = GetPlayerStatsAtMinute(minute, matchDetails, matchTimeline);
                PlayerStatsAtTime? me = pstats.FirstOrDefault(p => p.SummonerName == mainUserParticipant.SummonerName);
                if (me == null) continue;
            
                switch (minute) {
                    case 1:
                        int allyKills = 0;
                        int enemKills = 0;
                        foreach(var player in pstats) {
                            if (player.Kills > 0) {
                                if(player.TeamId==1) {
                                    allyKills+=player.Kills;
                                }
                                else {
                                    enemKills+=player.Kills;
                                }
                            }
                        }
                        int ds = me.Deaths;
                        int ks = me.Kills;
                        int assists = me.Assists;

                        for (int i = 0; i<allyKills; i++) {
                            if (ks > 0) {
                                ks--;
                                sis[0]+=25;
                            }
                            else if (assists > 0) {
                                assists--;
                                sis[0]+=12.5;
                                tis[0]+=12.5;
                            }
                            else {
                                tis[0]+=25;
                            }
                        }
                        for (int i = 0; i<enemKills; i++) {
                            if (ds > 0) {
                                ds--;
                                sis[0]-=25;
                            }
                            else {
                                tis[0]-=25;
                            }
                        }

                        report.AppendLine("MINUTE 1 STATS (50 baseline):");
                        report.AppendLine($"Your Score: {sis[0]}/100");
                        report.AppendLine($"Team Score: {tis[0]}/100");
                        break;

                    case 5:
                        break;

                    case 10:
                        break;

                    case 14:
                        break;

                    case 20:
                        break;
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Calculates the precise stats for every player at a specific minute in the game by processing the timeline.
        /// </summary>
        /// <param name="minute">The minute to get stats for (e.g., 10 for the 10-minute mark).</param>
        /// <param name="matchDetails">The full MatchDto object from the API, used for static player info.</param>
        /// <param name="matchTimeline">The full MatchTimelineDto object from the API, used for event processing.</param>
        /// <returns>A list of PlayerStatsAtTime objects with the calculated stats, or an empty list if the match was too short.</returns>
        public List<PlayerStatsAtTime> GetPlayerStatsAtMinute(int minute, MatchDto matchDetails, MatchTimelineDto matchTimeline)
        {
            // First, check if the game even lasted until the requested minute.
            // The frame at index 'minute' represents the state at the end of that minute.
            if (matchTimeline.Info.Frames.Count <= minute)
            {
                Console.WriteLine($"Match was shorter than {minute} minutes.");
                return new List<PlayerStatsAtTime>(); // Return an empty list
            }

            // Create a dictionary to hold the stats for each player, for easy lookups.
            var statsDictionary = new Dictionary<int, PlayerStatsAtTime>();
            var mainUserParticipant = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == myPuuid);
            if (mainUserParticipant == null)
            {
                // If we can't find the user, we can't determine teams.
                return new List<PlayerStatsAtTime>();
            }
            int userTeamId = mainUserParticipant.TeamId;
            foreach (var participant in matchDetails.Info.Participants)
            {
                statsDictionary[participant.ParticipantId] = new PlayerStatsAtTime
                {
                    ParticipantId = participant.ParticipantId,
                    SummonerName = participant.SummonerName,
                    ChampionName = participant.ChampionName,
                    Lane = participant.Lane,
                    TeamId = (participant.TeamId == userTeamId ? 1 : 2)
                };
            }

            // Loop through all frames from the beginning of the game UP TO the desired minute.
            // This is necessary to correctly count all Kills, Deaths, and Assists from events.
            for (int i = 1; i <= minute; i++)
            {
                var currentFrame = matchTimeline.Info.Frames[i];

                // Update the "easy" stats (Gold, CS, Damage) for every player.
                // This will overwrite the previous minute's stats with the current minute's stats.
                foreach (var participantFrame in currentFrame.ParticipantFrames.Values)
                {
                    if (statsDictionary.TryGetValue(participantFrame.ParticipantId, out var stats))
                    {
                        stats.Gold = participantFrame.TotalGold;
                        stats.CreepScore = participantFrame.CreepScore;
                        stats.DamageToChampions = participantFrame.DamageStats.TotalDamageDoneToChampions;
                        stats.Level = participantFrame.Level;
                    }
                }

                // Process the events in this frame to calculate K/D/A cumulatively.
                foreach (var gameEvent in currentFrame.Events)
                {
                    if (gameEvent.Type == "CHAMPION_KILL")
                    {
                        // Add a death to the victim
                        if (statsDictionary.ContainsKey(gameEvent.VictimId))
                        {
                            statsDictionary[gameEvent.VictimId].Deaths++;
                        }

                        // Add a kill to the killer (if it wasn't an execution)
                        if (statsDictionary.ContainsKey(gameEvent.KillerId))
                        {
                            statsDictionary[gameEvent.KillerId].Kills++;
                        }

                        // Add assists to all assisting players
                        foreach (int assistId in gameEvent.AssistingParticipantIds)
                        {
                            if (statsDictionary.ContainsKey(assistId))
                            {
                                statsDictionary[assistId].Assists++;
                            }
                        }
                    }
                }
            }

            // The dictionary now contains the precise stats at the target minute.
            // Return it as a simple list.
            return statsDictionary.Values.ToList();
        }
    }


    // ##################################################################
    // ## RIOT API DATA TRANSFER OBJECTS (DTOs)
    // ## Includes updates needed for timeline event processing.
    // ##################################################################

    // DTOs for Match Details
    public class MatchDto { [JsonPropertyName("info")] public MatchInfo Info { get; set; } = new(); }
    public class MatchInfo
    {
        [JsonPropertyName("participants")] public List<Participant> Participants { get; set; } = new();
        [JsonPropertyName("teams")] public List<Team> Teams { get; set; } = new();
    }

    public class Participant
    {
        [JsonPropertyName("puuid")] public string Puuid { get; set; } = string.Empty;
        [JsonPropertyName("participantId")] public int ParticipantId { get; set; }
        [JsonPropertyName("summonerName")] public string SummonerName { get; set; } = string.Empty;
        [JsonPropertyName("championName")] public string ChampionName { get; set; } = string.Empty;
        [JsonPropertyName("teamPosition")] public string Lane { get; set; } = string.Empty;
        [JsonPropertyName("teamId")] public int TeamId { get; set; }
        [JsonPropertyName("kills")] public int Kills { get; set; }
        [JsonPropertyName("deaths")] public int Deaths { get; set; }
        [JsonPropertyName("assists")] public int Assists { get; set; }
        [JsonPropertyName("totalDamageDealtToChampions")] public int TotalDamageDealtToChampions { get; set; }
        public string KDA => $"{Kills}/{Deaths}/{Assists}";
    }

    public class Team
    {
        [JsonPropertyName("teamId")] public int TeamId { get; set; }
        [JsonPropertyName("win")] public bool Win { get; set; }
    }

    // DTOs for Match Timeline
    public class MatchTimelineDto { [JsonPropertyName("info")] public TimelineInfoDto Info { get; set; } = new(); }
    public class TimelineInfoDto { [JsonPropertyName("frames")] public List<TimelineFrameDto> Frames { get; set; } = new(); }
    public class TimelineFrameDto
    {
        [JsonPropertyName("participantFrames")] public Dictionary<string, TimelineParticipantFrameDto> ParticipantFrames { get; set; } = new();
        [JsonPropertyName("events")] public List<TimelineEventDto> Events { get; set; } = new(); // Required for K/D/A
        [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    }

    public class TimelineParticipantFrameDto
    {
        [JsonPropertyName("participantId")] public int ParticipantId { get; set; }
        [JsonPropertyName("totalGold")] public int TotalGold { get; set; }
        [JsonPropertyName("minionsKilled")] public int MinionsKilled { get; set; }
        [JsonPropertyName("jungleMinionsKilled")] public int JungleMinionsKilled { get; set; }
        [JsonPropertyName("level")] public int Level { get; set; }
        [JsonPropertyName("damageStats")] public DamageStatsDto DamageStats { get; set; } = new(); // Required for Damage
        public int CreepScore => MinionsKilled + JungleMinionsKilled;
    }

    // DTOs for Event Data within the Timeline
    public class DamageStatsDto
    {
        [JsonPropertyName("totalDamageDoneToChampions")]
        public int TotalDamageDoneToChampions { get; set; }
    }

    public class TimelineEventDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("killerId")]
        public int KillerId { get; set; }
        [JsonPropertyName("victimId")]
        public int VictimId { get; set; }
        [JsonPropertyName("assistingParticipantIds")]
        public List<int> AssistingParticipantIds { get; set; } = new();
    }
}