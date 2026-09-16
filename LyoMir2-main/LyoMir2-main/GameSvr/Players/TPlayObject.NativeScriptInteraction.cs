using GameSvr.PasEngine;
using SystemModule;

namespace GameSvr
{
    /// <summary>
    /// sub_6B8CC4 Find-gated script-interaction branch — client-invoked function
    /// names from <c>Config\validScriptFunc.txt</c>.
    ///
    /// Native sub_6B8CC4 is the player↔script text bus (TaskBoardScript.cs). After
    /// the four prologue gates it splits on the script object:
    ///   • HelperQuest board (CM 4651) → GotoLabel. C# = NativeTaskBoardTextCommandRun.
    ///     0x6B8E6D..0x6B8EB5 does not consult the valid-func TStringList.
    ///   • Other script objects (NPC +0x570, monster script, 月老, …) go through the
    ///     [player+0x18B4] host vmt+0x8C path, which queries that list via
    ///     TStringList.Find. Those object identities are not modelled; the Find
    ///     query itself is. CM_MERCHANTDLGSELECT (0x6D8F14, sister of ClickNPC's
    ///     0x6D8EE9 → sub_6B8B28) is the live client text entry that can supply a
    ///     function name rather than an @label.
    ///
    /// @labels stay on UserSelect / TryCallNpcLabel (vmt+0x44 GotoLabel). Compiled
    /// Envir This_Npc/This_Player methods and CallPlayerFunc stay ungated — native
    /// still executes those as TPsNpc/TPlayer RTTI without this list.
    /// </summary>
    public partial class TPlayObject
    {
        /// <summary>
        /// sub_6B8CC4 Find query. Unknown name → native reject (false / no-op) and
        /// one throttled unknown-PAS line. Listed name → true (CallFunc ABI of the
        /// unmodelled vmt+0x8C host is not invented here).
        /// </summary>
        internal bool TryNativeScriptInteractionFunction(string text)
        {
            // sub_6B8CC4 0x6B8CF3 / 0x6B8CFF / 0x6B8D0C: ghost / death / dealing.
            if (m_boGhost || m_boDeath || m_boDealing)
                return false;
            // 0x6B8D19 / 0x6B8D21: text non-nil and Length ≥ 1.
            if (string.IsNullOrEmpty(text))
                return false;

            var functionName = string.Empty;
            HUtil32.GetValidStr3(text, ref functionName, new[] { '\r' });
            functionName = functionName.Trim();
            if (functionName.Length == 0)
                return false;

            if (!NativeValidScriptFunctionRegistry.Find(functionName))
            {
                PasApiBridge.TraceUnknownPasName("ScriptInteraction", functionName);
                return false;
            }

            return true;
        }
    }
}
