using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Diagnostics;

public class BuildNotification : IPostprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("Build Succeeded! Opening output folder...");
            
            // Open the build folder
            string outputPath = report.summary.outputPath;
            if (System.IO.File.Exists(outputPath)) // It's a file (e.g. .app or .exe)
            {
                outputPath = System.IO.Path.GetDirectoryName(outputPath);
            }
            
            if (System.IO.Directory.Exists(outputPath))
            {
                Process.Start("open", outputPath);
            }

            // Optional: Show a dialog (might block batch mode, so checking for batch mode)
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Build Complete", 
                    $"Build finished successfully in {report.summary.totalTime.TotalSeconds:F1} seconds.\nOutput: {outputPath}", 
                    "OK");
            }
        }
        else if (report.summary.result == BuildResult.Failed)
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Build Failed", 
                    $"Build failed with {report.summary.totalErrors} errors.", 
                    "OK");
            }
        }
    }
}