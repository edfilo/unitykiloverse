using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class K1L0SceneCapture : MonoBehaviour
{
    private static RenderTexture sceneRT;
    private static K1L0SceneCapture instance;

    void OnEnable()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (instance == this) instance = null;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam != Camera.main) return;

        if (sceneRT == null || sceneRT.width != Screen.width || sceneRT.height != Screen.height)
        {
            if (sceneRT != null) sceneRT.Release();
            sceneRT = new RenderTexture(Screen.width / 2, Screen.height / 2, 0, RenderTextureFormat.ARGB32);
            sceneRT.filterMode = FilterMode.Bilinear;
            sceneRT.Create();
            Shader.SetGlobalTexture("_K1L0SceneTex", sceneRT);
        }

        Graphics.Blit(cam.targetTexture ?? cam.activeTexture, sceneRT);
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        var cam = Camera.main;
        if (cam == null) return;
        cam.gameObject.AddComponent<K1L0SceneCapture>();
    }
}
