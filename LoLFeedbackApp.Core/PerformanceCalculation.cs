namespace LoLFeedbackApp.Core {
    public class PerformanceCalculation {

        //1 MIN check kills deaths gold, before minions spawn, lanes irrelevant
        //5 MIN check kills death gold, lanes very important
        //10 MIN check kills death gold, lanes important, compare objectives
        //14 MIN check gold, TURRETS SUPER IMPORTANT PLATING FALLS AFTER, objectives check, less important, grubs and drag, check cs for crazy differences
        //20 mins, check gold kills death, check objectives, check for crazy cs differences
        //25 mins, check gold kills deaths, check objectives, check for crazy cs differences, check for tier2/tier3 turrets being down
        //30 mins, inhib comparison/objectives final check
        


        
    }

    public class Summoner {
        public string Name { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Damage { get; set; }

        public Summoner(string n, int k, int d, int a, int dmg) {
            Name = n;
            Kills = k;
            Deaths = d;
            Assists = a;
            Damage = dmg;
        }
    }
}