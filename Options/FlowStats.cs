using System.Text.RegularExpressions;

namespace Arise_RTM
{
    /// <summary>
    /// Story/heist flow stats: writing them can block the Lester and Simeon invites on that character.
    /// The Debug tab warns before it writes one.
    /// </summary>
    public static class FlowStats
    {
        private static readonly Regex Pattern = new Regex(
            "FM_VEH_TX1|_FLOW|H3OPT_|H4CNF_|HEIST_START|HEIST_END|HEIST_PLANNING|H4_PROGRESS|H4_COOLDOWN|" +
            "GANGOPS_HEIST_STATUS|BS_CONTRACT_STRAND|FIXER_STORY_STRAND|CUTSCENE_MID_|HEIST_CUTS_DONE|HEIST_PRE_PLAN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsFlow(string statName)
        {
            return !string.IsNullOrEmpty(statName) && Pattern.IsMatch(statName);
        }

        public const string Explanation =
            "These stats are story/heist flow flags: writing them can block the invites from Lester and Simeon on that character.";
    }
}
