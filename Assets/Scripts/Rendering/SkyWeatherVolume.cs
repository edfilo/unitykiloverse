using UnityEngine;

public sealed class SkyWeatherVolume : MonoBehaviour
{
    static SkyWeatherVolume instance; ParticleSystem rain, snow;
    public static void SetEffect(int effect, float intensity)
    {
        if (instance == null) { var g=new GameObject("K1L0 Weather Volume"); DontDestroyOnLoad(g); instance=g.AddComponent<SkyWeatherVolume>(); instance.rain=instance.Make(false); instance.snow=instance.Make(true); }
        instance.Set(instance.rain,effect==1||effect==4,Mathf.Lerp(350,1200,Mathf.Max(.35f,intensity)));
        instance.Set(instance.snow,effect==2,420);
    }
    ParticleSystem Make(bool flakes)
    {
        var g=new GameObject(flakes?"Snow Volume":"Rain Volume"); g.transform.SetParent(transform); var p=g.AddComponent<ParticleSystem>(); p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=p.main; m.loop=true; m.startLifetime=flakes?5f:1.15f; m.startSpeed=flakes?2.2f:24f; m.startSize=flakes?new ParticleSystem.MinMaxCurve(.018f,.045f):new ParticleSystem.MinMaxCurve(.006f,.012f); m.startColor=flakes?new Color(.9f,.95f,1f,.82f):new Color(.55f,.7f,.85f,.48f); m.simulationSpace=ParticleSystemSimulationSpace.World; m.gravityModifier=flakes?.025f:.18f;
        var e=p.emission; e.rateOverTime=0; var s=p.shape; s.shapeType=ParticleSystemShapeType.Box; s.scale=new Vector3(18,1,18); var r=p.GetComponent<ParticleSystemRenderer>(); r.renderMode=flakes?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch; r.lengthScale=flakes?1:8; r.velocityScale=flakes?0:.025f; return p;
    }
    void LateUpdate(){ if(Camera.main!=null) transform.position=Camera.main.transform.position+Vector3.up*7f; }
    void Set(ParticleSystem p,bool on,float rate){ var e=p.emission;e.rateOverTime=on?rate:0;if(on&&!p.isPlaying)p.Play(true);else if(!on&&p.isPlaying)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); }
}
