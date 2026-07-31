#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;

namespace MText
{
    internal static class ScriptDefineManager
    {
        const string MTextDefine = "MODULAR_3D_TEXT";

        [InitializeOnLoadMethod]
        static void AddScriptDefine()
        {
            BuildTargetGroup currentTarget = EditorUserBuildSettings.selectedBuildTargetGroup;

            if (currentTarget == BuildTargetGroup.Unknown)
                return;

            // string scriptDefinesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentTarget).Trim(); //TODO: 2023. https://docs.unity3d.com/2023.1/Documentation/ScriptReference/PlayerSettings.GetScriptingDefineSymbolsForGroup.html
#pragma warning disable CS0618 // Type or member is obsolete
            string scriptDefinesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentTarget).Trim(); //TODO: 2023. https://docs.unity3d.com/2023.1/Documentation/ScriptReference/PlayerSettings.GetScriptingDefineSymbolsForGroup.html
#pragma warning restore CS0618 // Type or member is obsolete
            string[] scriptDefines = scriptDefinesString.Split(';');

            if (scriptDefines.Contains(MTextDefine))
                return;

            //This shouldn't be needed for 1 symbol but an existing third party tool was causing issue or this is really needed and I don't understand how this works
            if (scriptDefinesString.EndsWith(";", StringComparison.InvariantCulture) == false)
            {
                scriptDefinesString += ";";
            }

            scriptDefinesString += MTextDefine;

#pragma warning disable CS0618 // Type or member is obsolete
            PlayerSettings.SetScriptingDefineSymbolsForGroup(currentTarget, scriptDefinesString);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
#endif