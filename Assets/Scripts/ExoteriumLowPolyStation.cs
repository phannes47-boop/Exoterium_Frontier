using UnityEngine;

// Eigenstaendige, zur Laufzeit erzeugte Low-Poly-Raumstation.
// Kein externer Download, keine Bildtextur und kein fremdes 3D-Modell erforderlich.
public static class ExoteriumLowPolyStation
{
    static Material Make(Color color, bool emission=false)
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        if(shader==null)shader=Shader.Find("Unlit/Color");
        var m=new Material(shader);
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
        if(m.HasProperty("_Color"))m.SetColor("_Color",color);
        if(emission && m.HasProperty("_EmissionColor")){
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor",color*2.0f);
        }
        return m;
    }

    static GameObject Cube(Transform parent,string name,Vector3 localPos,Vector3 localScale,Material mat,float angle=0f)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=localPos;go.transform.localScale=localScale;
        go.transform.localRotation=Quaternion.Euler(0,angle,0);
        go.GetComponent<Renderer>().sharedMaterial=mat;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Cylinder(Transform parent,string name,Vector3 localPos,Vector3 scale,Material mat,int sides=12)
    {
        // Ein Cylinder-Primitive ist ein bewusst einfaches Low-Poly-Bauteil.
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=localPos;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        Object.Destroy(go.GetComponent<Collider>());
        return go;
    }
    static void Ring(Transform parent,string name,float radius,float tubeRadius,float y,Material mat,int segments=20,int sides=6)
    {
        var vertices=new Vector3[segments*sides];
        var triangles=new int[segments*sides*6];
        for(int i=0;i<segments;i++){
            float a=i*Mathf.PI*2f/segments;
            Vector3 outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            for(int j=0;j<sides;j++){
                float b=j*Mathf.PI*2f/sides;
                vertices[i*sides+j]=outward*(radius+Mathf.Cos(b)*tubeRadius)+Vector3.up*(y+Mathf.Sin(b)*tubeRadius);
                int nextI=(i+1)%segments,nextJ=(j+1)%sides;
                int t=(i*sides+j)*6;
                int a0=i*sides+j,b0=nextI*sides+j,c0=nextI*sides+nextJ,d0=i*sides+nextJ;
                triangles[t]=a0;triangles[t+1]=b0;triangles[t+2]=c0;
                triangles[t+3]=a0;triangles[t+4]=c0;triangles[t+5]=d0;
            }
        }
        var mesh=new Mesh {name=name};
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
        var go=new GameObject(name);
        go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
    public static GameObject Build(Vector3 position)
    {
        var root=new GameObject("Raumstation – 3D");
        root.transform.position=position;
        var hull=Make(new Color(.24f,.33f,.43f));
        var panel=Make(new Color(.14f,.20f,.29f));
        var silver=Make(new Color(.40f,.49f,.59f));
        var cyan=Make(new Color(.05f,.67f,1f),true);
        var blue=Make(new Color(.08f,.30f,.65f),true);

        // Ein einziges zusammenhaengendes Stationsmodell.
        Cylinder(root.transform,"Zentraler Hauptkern",Vector3.zero,new Vector3(6.5f,2.3f,6.5f),hull);
        Cylinder(root.transform,"Kern-Oberdeck",new Vector3(0,2.5f,0),new Vector3(5.2f,.8f,5.2f),silver);
        Cylinder(root.transform,"Kommandoturm",new Vector3(0,5.2f,0),new Vector3(2.1f,2.3f,2.1f),panel);
        Cylinder(root.transform,"Turm-Signalleuchte",new Vector3(0,7.8f,0),new Vector3(.9f,.3f,.9f),cyan);
        Ring(root.transform,"Hauptrahmen",16.5f,2.4f,.3f,hull,24,6);
        Ring(root.transform,"Aeusserer Panzerkranz",19f,.8f,.3f,silver,24,5);
        Ring(root.transform,"Blauer Leuchtring",16.5f,.35f,2.1f,cyan,32,5);
        Ring(root.transform,"Kern-Lichtring",6.5f,.35f,1.7f,cyan,24,5);
        Ring(root.transform,"Unterer Strukturkranz",14.8f,.8f,-2.2f,panel,24,5);

        for(int i=0;i<6;i++){
            float angle=i*60f;
            float radians=angle*Mathf.Deg2Rad;
            Vector3 dir=new Vector3(Mathf.Sin(radians),0,Mathf.Cos(radians));
            Cube(root.transform,"Verbundener Dockarm "+i,dir*11f,new Vector3(3.8f,2.2f,15.5f),hull,angle);
            Cube(root.transform,"Dockarm-Licht "+i,dir*11f+Vector3.up*1.25f,new Vector3(.55f,.18f,11.8f),cyan,angle);
            Cube(root.transform,"Dockplattform "+i,dir*23f,new Vector3(8.0f,1.7f,7f),silver,angle);
            Cube(root.transform,"Docktor "+i,dir*26f+Vector3.up*.25f,new Vector3(5.5f,.75f,.35f),blue,angle);
            Cube(root.transform,"Seitliche Panzerung "+i,dir*19.5f+Vector3.up*2.1f,new Vector3(2.8f,3f,2.5f),panel,angle);
        }
        for(int i=0;i<8;i++){
            float a=i*Mathf.PI/4f;
            Vector3 dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
            Cube(root.transform,"Aussenlicht "+i,dir*19f+Vector3.up*.6f,new Vector3(.9f,.45f,1.1f),cyan,i*45f);
        }
        return root;
    }
}
