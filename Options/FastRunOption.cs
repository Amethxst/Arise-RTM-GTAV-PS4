using System.Collections.Generic;

namespace Arise_RTM
{
    /// <summary>Unlocks the permanent fast run: the six ability stats of the selected character are set to -1.</summary>
    public sealed class FastRunOption : OptionBase
    {
        private static readonly string[] AbilityStats =
        {
            "CHAR_ABILITY_1_UNLCK", "CHAR_ABILITY_2_UNLCK", "CHAR_ABILITY_3_UNLCK",
            "CHAR_FM_ABILITY_1_UNLCK", "CHAR_FM_ABILITY_2_UNLCK", "CHAR_FM_ABILITY_3_UNLCK"
        };

        public override string Name => "Fast run";

        public override string Description => "Adds the permanent fast run to the selected character. Switch sessions to apply.";

        public override IReadOnlyList<string> RequiredHandlers { get; } = new[] { HandlerKeys.StatSetInt };

        public override IReadOnlyList<string> OptionalHandlers { get; } = new[] { HandlerKeys.StatGetInt, HandlerKeys.StatSave };

        public override OptionResult Run(OptionContext context)
        {
            OptionResult result = new OptionResult();

            foreach (string suffix in AbilityStats)
                context.WriteInt(result, context.CharacterStat(suffix), -1);

            context.SaveIfRequested(result);
            return result;
        }
    }
}
