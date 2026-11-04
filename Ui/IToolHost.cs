using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace Arise_RTM
{
    /// <summary>What the panels need from the main window: the engine, the chosen character, and a guarded way to run operations.</summary>
    public interface IToolHost
    {
        StatEngine Engine { get; }
        PS4Connection Connection { get; }

        /// <summary>Selected character slot (0 = Character 1, 1 = Character 2), -1 when none is chosen.</summary>
        int Slot { get; }

        /// <summary>Chooses Character 1 (0) or Character 2 (1), as the toolbar does.</summary>
        void ChooseCharacter(int slot);

        void Log(string message);
        void SetStatus(string text, Color color);
        bool Confirm(string message, string caption);

        /// <summary>
        /// Runs <paramref name="work"/> with the UI blocked, a progress bar and Cancel. Handles the checks common to every stat operation:
        /// character chosen, handlers available, confirmation, unverified-handler warning, game still running.
        /// <paramref name="confirmDetails"/> makes the confirmation appear even when "Ask before applying" is off; <paramref name="changesStats"/> is false for reads and teleports; <paramref name="needsCharacter"/> is false for what does not depend on the character.
        /// </summary>
        Task RunAsync(string title, IEnumerable<string> requiredHandlers, IEnumerable<string> optionalHandlers,
            Func<OptionContext, OptionResult> work, string confirmDetails = null, bool changesStats = true, bool needsCharacter = true);

        Task RunOptionAsync(IRecoveryOption option);
    }

    internal static class UiStyle
    {
        public static readonly Color Muted = Theme.Muted;
    }
}
