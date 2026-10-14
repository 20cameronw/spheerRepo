namespace Frontier.Hyperspace
{
    public sealed partial class EncounterLibrary
    {
        /// <summary>
        /// Upper tier bound of the early and mid templates (the lower bound lives in the template itself). Easy formations retire as the tiers climb so deep routes are never padded with
        /// trivial fillers; late templates and rewards stay available. Returns 12 when the template has no cap.
        /// </summary>
        static int TierCap(string id)
        {
            switch (id)
            {
                case "drone_line": return 3;
                case "drone_v": return 4;
                case "drone_column": return 2;
                case "asteroid_slalom": return 6;
                case "gate_slit_wide": return 4;
                case "drone_wedge": return 5;
                case "zigzag_pair": return 5;
                case "swooper_pair": return 6;
                case "pincer": return 6;
                case "asteroid_field": return 8;
                case "zigzag_stream": return 8;
                case "swooper_arc": return 9;
                case "minefield_scatter": return 8;
                case "turret_single": return 7;
                case "gate_slit_slide": return 8;
                case "gate_slot": return 9;
                case "drone_helix": return 8;
                case "seeker_single": return 9;
                case "elite_single": return 8;
                case "minefield_corridor": return 9;
                case "drone_slot_rows": return 10;
                case "drone_rain": return 9;
                default: return 12;
            }
        }
    }
}
