using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class QuestRewardVerificationBuild
{
    public static void Build()
    {
        var output = Environment.GetEnvironmentVariable("ECHODIA_QUEST_PLAYER");
        if (string.IsNullOrEmpty(output) || !System.IO.Path.IsPathRooted(output))
            throw new InvalidOperationException("Set ECHODIA_QUEST_PLAYER to an absolute .exe path.");
        var settings = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
            extraScriptingDefines = new[] { "ECHODIA_QUEST_VERIFICATION" }
        };
        var report = BuildPipeline.BuildPlayer(settings);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Reward verification build failed: " + report.summary.result);
    }
}
