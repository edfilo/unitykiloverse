using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class CreatePlayerAnimatorController
{
    [InitializeOnLoadMethod]
    private static void OnEditorInitialize()
    {
        EditorApplication.delayCall += () =>
        {
            string controllerPath = "Assets/Animations/PlayerController.controller";
            if (!System.IO.File.Exists(controllerPath))
            {
                Debug.Log("[Auto-Setup] Creating Player Animator Controller...");
                CreateController();
            }
        };
    }

    [MenuItem("Kiloverse/Create Player Animator Controller")]
    public static void CreateController()
    {
        string controllerPath = "Assets/Animations/PlayerController.controller";
        
        // Create the controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // Add parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        
        // Get the first layer
        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;
        
        // Load animation clips
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ToonSoldiers_WW2_demo/animation/infantry_combat_idle.FBX");
        AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ToonSoldiers_WW2_demo/animation/infantry_combat_run.FBX");
        
        // Create Idle state
        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;
        stateMachine.defaultState = idleState;
        
        // Create Run state
        AnimatorState runState = stateMachine.AddState("Run", new Vector3(250, 100, 0));
        runState.motion = runClip;
        
        // Add transitions
        // Idle to Run
        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.25f;
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        
        // Run to Idle
        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.25f;
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        
        // Save the controller
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Player Animator Controller created at: " + controllerPath);
        
        // Now assign it to the ToonSoldierModel
        AssignControllerToPlayer(controller);
    }

    private static void AssignControllerToPlayer(AnimatorController controller)
    {
        if (controller == null)
        {
            Debug.LogError("Controller is null, cannot assign.");
            return;
        }
        
        // Find the Player GameObject
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("Player GameObject not found in scene.");
            return;
        }
        
        // Find ToonSoldierModel child
        Transform toonSoldierModel = player.transform.Find("ToonSoldierModel");
        if (toonSoldierModel == null)
        {
            Debug.LogWarning("ToonSoldierModel not found as child of Player.");
            return;
        }
        
        // Get the Animator component
        Animator animator = toonSoldierModel.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Animator component not found on ToonSoldierModel.");
            return;
        }
        
        // Assign the controller
        animator.runtimeAnimatorController = controller;
        
        Debug.Log("Animator Controller assigned to ToonSoldierModel!");
    }
}
