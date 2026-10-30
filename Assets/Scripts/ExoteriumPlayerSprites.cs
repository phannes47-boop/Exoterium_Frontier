using UnityEngine;
public static class ExoteriumPlayerSprites
{
    public static bool IsNewPlayerShip(string name) {
        return name=="Scout" || name=="LeichterJaeger" || name=="MittlererJaeger" ||
               name=="SchwererJaeger" || name=="Kampfschiff";
    }
    public static void Attach(GameObject parent,string name)
    {
        var texture=Resources.Load<Texture2D>("PlayerShips/"+name);
        if(texture==null){Debug.LogError("Spielerschiff-Textur fehlt: "+name);return;}
        var go=new GameObject(name+" 2.5D");
        go.transform.SetParent(parent.transform,false);
        // Schiffsnase zeigt in die lokale +Z-Richtung; Textur oben ist die Nase.
        go.transform.localPosition=new Vector3(0,0.3f,0);
        var mesh=new Mesh();
        mesh.vertices=new [] {new Vector3(-7.5f,0,-7.5f),new Vector3(7.5f,0,-7.5f),
            new Vector3(7.5f,0,7.5f),new Vector3(-7.5f,0,7.5f)};
        mesh.uv=new [] {new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
        mesh.triangles=new [] {0,2,1,0,3,2};
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var shader=Shader.Find("Unlit/Transparent");
        if(shader==null)shader=Shader.Find("Sprites/Default");
        var mat=new Material(shader);
        mat.mainTexture=texture;
        go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
}
