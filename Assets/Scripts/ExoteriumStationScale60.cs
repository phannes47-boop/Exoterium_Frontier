using UnityEngine;

// Erzwingt die vereinbarte Stationsbreite auch bei aelteren Inspector-Werten.
[DisallowMultipleComponent]
public sealed class ExoteriumStationScale60 : MonoBehaviour
{
    public float TargetWidth=120f;
    GameObject currentStation;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){
        var game=Object.FindFirstObjectByType<FrontierGame>();
        if(game!=null && game.GetComponent<ExoteriumStationScale60>()==null)
            game.gameObject.AddComponent<ExoteriumStationScale60>();
    }
    void LateUpdate(){
        var obj=GameObject.Find("Raumstation – 3D");
        if(obj==null || obj==currentStation)return;
        currentStation=obj;
        var renderers=obj.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)return;
        Bounds bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        float currentWidth=Mathf.Max(bounds.size.x,bounds.size.z);
        if(currentWidth>.001f)obj.transform.localScale*=120f/currentWidth;
    }
}
