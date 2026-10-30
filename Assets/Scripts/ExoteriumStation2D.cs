using UnityEngine;

// Station als zwei echte transparente, kameraorientierte Bildebenen.
// Die Schiffe werden zwischen Stationskoerper und oberem Turm gerendert.
public static class ExoteriumStation2D
{
    static GameObject Layer(Transform root, string name, Texture2D texture, int queue)
    {
        var go=new GameObject(name);
        go.transform.SetParent(root,false);
        go.transform.localPosition=Vector3.zero;
        go.transform.localRotation=Quaternion.Euler(90f,0f,0f);
        var mesh=new Mesh();
        float w=126f, h=120.4f;
        mesh.vertices=new [] {new Vector3(-w/2,-h/2,0),new Vector3(w/2,-h/2,0),
            new Vector3(w/2,h/2,0),new Vector3(-w/2,h/2,0)};
        mesh.uv=new [] {new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
        mesh.triangles=new [] {0,1,2,0,2,3};
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var material=new Material(Shader.Find("Exoterium/StationLayer"));
        material.mainTexture=texture;
        material.renderQueue=queue;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
        return go;
    }
    public static GameObject Build(Vector3 position)
    {
        var root=new GameObject("Raumstation – 2.5D");
        root.transform.position=position+Vector3.up*4f;
        var baseTexture=Resources.Load<Texture2D>("Station/Station_Base");
        var towerTexture=Resources.Load<Texture2D>("Station/Station_Tower");
        if(baseTexture==null || towerTexture==null)
        {
            Debug.LogError("Exoterium: Stationstexturen fehlen unter Assets/Resources/Station.");
            return root;
        }
        Layer(root.transform,"01 Stationskoerper – hinter Schiffen",baseTexture,2005);
        Layer(root.transform,"02 Hoher Turm – vor Schiffen",towerTexture,2015);
        return root;
    }
}
