using System;
using System.Collections.Generic;
using UnityEngine;

// EXOTERIUM: FRONTIER – Unity 6, C# / klassische Input-API
// Eine spielbare Einzelspieler-Grundlage; Netzwerk-MMO ist noch nicht implementiert.
public class FrontierGame : MonoBehaviour
{
    [Serializable] class SaveData {
        public int credits=2800, exoterium=0, ore=0, level=1, xp=0, kills=0, ship=0, map=0, rockets=40, ammo1=500, ammo2=0, ammo3=0, ammoType=1;
        public int laser1=2,laser2=0,laser3=0,shield1=1,shield2=0,shield3=0,repairTier=1,speedGenerators=0;
        public int[] weaponSlots,shieldSlots,speedSlots,extraSlots;
        public float hull=1000,shield=400;
    }
    class Enemy {
        public GameObject go; public string name; public int kind; public bool boss,aggro;
        public float hull,maxHull,shield,maxShield,nextFire,combatAt,roamAt,nextRocket;
        public Vector3 waypoint;
    }
    class Shot { public GameObject go; public Enemy enemy; public bool hostile,rocket; public float damage,speed,ttl; }
    class Pickup { public GameObject go; public bool crystal; }
    class FloatText { public Vector3 pos; public string text; public Color color; public float expires; }
    readonly string[] players={"Dispatcher","Omen","Challenger","Zenith","Executioner"};
    // Karten-NPCs: zwei AssetPack-Modelle (Bomber, Corvette_05); Boss nutzt Carrier.
    readonly string[] npcs={"Bomber","Corvette_05"};
    const string BossShipModel="Carrier";
    // Relatives Groessensystem wie bisher: Spieler/NPC = 15, Boss = 25.
    const float PlayerShipWidth=15f,NpcShipWidth=15f, BossShipWidth=25f;
    readonly List<Enemy> enemies=new List<Enemy>();
    readonly List<Shot> shots=new List<Shot>();
    readonly List<Pickup> pickups=new List<Pickup>();
    readonly List<FloatText> numbers=new List<FloatText>();
    readonly List<string> events=new List<string>();
    readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
    SaveData data=new SaveData();
    GameObject player,station,gate,stationZone,gateZone,targetRing;
    Camera cam; Enemy selected; Pickup gathering;
    int zoomLevel=1; readonly float[] zoomSizes={72f,66f,61f,56f};
    Vector3 destination,flightDirection=Vector3.forward;
    bool hasDestination,attack,paused; string panel="";
    float nextLaser,nextRocket,lastCombat,saveAt,noticeUntil; string notice="";
    const float MapW=2000f,MapH=1350f,SafeRadius=34f,PlayerRange=104f,NpcRange=87f;
    const float ProjectileSpeed=95f;
    readonly Vector3 stationPoint=new Vector3(145,0,MapH-145f);
    Vector3 GatePoint {get {return new Vector3(MapW-145f,0,145f);}}
    float MaxHull {get {return 1000+data.ship*240;}}
    float MaxShield {get {return 200+CountEquipped(data.shieldSlots,1)*200+CountEquipped(data.shieldSlots,2)*400+CountEquipped(data.shieldSlots,3)*650;}}
    float PlayerSpeed {get {return new[]{27f,32f,23f,29f,21f}[data.ship]+CountEquipped(data.speedSlots,1)*2.5f;}}
    int RequiredXP {get {return data.level*500;}}
    bool InSafe(Vector3 p) {return Vector3.Distance(Flat(p),GatePoint)<SafeRadius||(data.map==0&&Vector3.Distance(Flat(p),stationPoint)<SafeRadius);}
    bool SafeEnabled {get {return Time.time-lastCombat>=3f;}}
    static Vector3 Flat(Vector3 p){p.y=0;return p;}
    static float Dist(Vector3 a,Vector3 b){return Vector3.Distance(Flat(a),Flat(b));}
    void Awake(){
        Load();InitEquipment();Application.targetFrameRate=90;
        var ambient=new GameObject("Sun").AddComponent<Light>();ambient.type=LightType.Directional;ambient.intensity=1.7f;ambient.transform.rotation=Quaternion.Euler(50,-20,0);
        RenderSettings.ambientLight=new Color(.42f,.50f,.64f);
        cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=zoomSizes[zoomLevel];cam.transform.rotation=Quaternion.Euler(52,0,0);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
        player=Ship(players[data.ship],stationPoint+new Vector3(55,0,-20),false);
        CreateMap();
        Log("Willkommen bei Exoterium: Frontier – Unity C#");
    }
    Material Mat(string key,Color c,bool glow=false){
        if(mats.ContainsKey(key))return mats[key];
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        if(shader==null)shader=Shader.Find("Sprites/Default");
        var m=new Material(shader);m.color=c;
        if(glow&&m.HasProperty("_EmissionColor")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*1.5f);}
        mats[key]=m;return m;
    }
    GameObject Primitive(PrimitiveType type,string name,Vector3 pos,Vector3 scale,Material material,Transform parent=null){
        var o=GameObject.CreatePrimitive(type);o.name=name;if(parent!=null)o.transform.SetParent(parent,false);
        o.transform.position=pos;o.transform.localScale=scale;
        var r=o.GetComponent<Renderer>();if(r!=null)r.material=material;
        var col=o.GetComponent<Collider>();if(col!=null)Destroy(col);
        return o;
    }
    GameObject Ship(string model,Vector3 position,bool hostile){
        var root=new GameObject(model);root.transform.position=position+Vector3.up*1.8f;
        var prefab=Resources.Load<GameObject>("Ships/"+model+"/"+model);
        if(prefab!=null){
            var obj=Instantiate(prefab,root.transform);obj.name=model+" 3D";
            Bounds b=new Bounds(obj.transform.position,Vector3.zero);bool any=false;
            foreach(var r in obj.GetComponentsInChildren<Renderer>()){if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);}
            if(any){float factor=PlayerShipWidth/Mathf.Max(.01f,Mathf.Max(b.size.x,b.size.z));obj.transform.localScale*=factor;obj.transform.localPosition-=root.transform.InverseTransformVector((b.center-root.transform.position)*factor);}
            // Farbgebung nur an den instanziierten Schiffsmodellen; FBX-Materialien bleiben erhalten.
            ApplyShipVariantTexture(obj, model, hostile);
            // Die Original-FBX-Dateien bleiben als Unity-importierbare 3D-Assets erhalten.
        }else{
            Primitive(PrimitiveType.Capsule,"Fallback ship",root.transform.position,new Vector3(9,1.8f,PlayerShipWidth),Mat(hostile?"enemy":"player",hostile?Color.red:Color.cyan),root.transform);
        }
        PlaceShipAboveStructures(root);
        var collider=root.AddComponent<SphereCollider>();collider.radius=PlayerShipWidth*.66f;return root;
    }
    void DrawMapMarker(Rect mapRect, Vector3 world, Color color, string symbol, int size){
        float x=mapRect.x+Mathf.Clamp01(world.x/MapW)*mapRect.width;
        float y=mapRect.y+(1f-Mathf.Clamp01(world.z/MapH))*mapRect.height;
        Color previous=GUI.color;GUI.color=color;
        GUI.Label(new Rect(x-size*.5f,y-size*.5f,size+5,size+5),symbol);
        GUI.color=previous;
    }
    // Originale Farbtexturen aus Assets/Resources/Ships/<Modell>/ laden.
    // Kein pauschaler Farbfilter: Die Details der Originaltextur bleiben erhalten.
    // AssetPack-Schiffe ohne Red/Blue-PNG: bei hostile roetlich tinten.
    static readonly Color HostileShipTint=new Color(1f,.38f,.34f,1f);
    void ApplyShipVariantTexture(GameObject visual, string model, bool hostile){
        string variant=hostile ? "Red" : "Blue";
        Texture2D texture=Resources.Load<Texture2D>("Ships/"+model+"/"+model+"_"+variant);
        if(texture==null){
            if(hostile) TintShipVisual(visual, HostileShipTint);
            else Debug.LogWarning("Exoterium: Schiffstextur fehlt: "+model+"_"+variant);
            return;
        }
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){
            var originals=renderer.sharedMaterials;
            var instances=new Material[originals.Length];
            for(int i=0;i<originals.Length;i++){
                if(originals[i]==null)continue;
                Material material=new Material(originals[i]);
                if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
                if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",texture);
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
                if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
                instances[i]=material;
            }
            renderer.materials=instances;
        }
    }
    void TintShipVisual(GameObject visual, Color tint){
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){
            var originals=renderer.sharedMaterials;
            var instances=new Material[originals.Length];
            for(int i=0;i<originals.Length;i++){
                if(originals[i]==null)continue;
                Material material=new Material(originals[i]);
                if(material.HasProperty("_BaseColor")){
                    Color c=material.GetColor("_BaseColor");
                    material.SetColor("_BaseColor",new Color(c.r*tint.r,c.g*tint.g,c.b*tint.b,c.a));
                }
                if(material.HasProperty("_Color")){
                    Color c=material.color;
                    material.color=new Color(c.r*tint.r,c.g*tint.g,c.b*tint.b,c.a);
                }
                instances[i]=material;
            }
            renderer.materials=instances;
        }
    }
    // Scale only the visible ship geometry; the gameplay root remains at its position.
    void ScaleShipVisual(GameObject ship,float targetWidth){
        var renderers=ship.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)return;
        Bounds b=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
        float width=Mathf.Max(b.size.x,b.size.z);
        if(width<.001f)return;
        float factor=targetWidth/width;
        foreach(Transform child in ship.transform)child.localScale*=factor;
        var col=ship.GetComponent<SphereCollider>();if(col!=null)col.radius=targetWidth*.66f;
    }
    GameObject Ring(string name,Vector3 center,float radius,Color color,float thickness=.55f){
        var root=new GameObject(name);root.transform.position=center+Vector3.up*.25f;
        var line=root.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=81;line.widthMultiplier=thickness;line.material=Mat("ring-"+name,color,true);
        line.startColor=color;line.endColor=color;
        for(int i=0;i<81;i++){float a=i*Mathf.PI*2/80;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        return root;
    }
    // Station und Tor zeichnen ohne Tiefenschreibzugriff, bevor Schiffe gerendert werden.
    // Dadurch koennen die Schiffsmodelle visuell nicht dahinter verschwinden.
    void PlaceStructureBehindShips(GameObject structure){
        foreach(var r in structure.GetComponentsInChildren<Renderer>()){
            Material[] materials=r.materials;
            foreach(var m in materials){
                m.renderQueue=2001;
                if(m.HasProperty("_ZWrite"))m.SetFloat("_ZWrite",0f);
            }
            r.materials=materials;
        }
    }
    void PlaceShipAboveStructures(GameObject ship){
        foreach(var r in ship.GetComponentsInChildren<Renderer>()){
            Material[] materials=r.materials;
            foreach(var m in materials)m.renderQueue=2010;
            r.materials=materials;
        }
    }
    void CreateMap(){
        foreach(var e in enemies)if(e.go)Destroy(e.go);enemies.Clear();
        foreach(var p in pickups)if(p.go)Destroy(p.go);pickups.Clear();
        foreach(var s in shots)if(s.go)Destroy(s.go);shots.Clear();selected=null;gathering=null;
        if(station)Destroy(station);if(gate)Destroy(gate);if(stationZone)Destroy(stationZone);if(gateZone)Destroy(gateZone);if(targetRing)Destroy(targetRing);
        foreach(var old in GameObject.FindGameObjectsWithTag("Respawn"))Destroy(old);
        var floor=GameObject.Find("Space Map");if(floor)Destroy(floor);
        floor=Primitive(PrimitiveType.Cube,"Space Map",new Vector3(MapW/2,-3,MapH/2),new Vector3(MapW,1,MapH),Mat("map"+data.map,Color.black));
        // Neue 2.5D-Station aus zwei transparenten Tiefenebenen.
        station=ExoteriumStation2D.Build(stationPoint);
        station.SetActive(data.map==0);
        gate=new GameObject("Sprungtor – 3D");gate.transform.position=GatePoint+Vector3.up*3;
        for(int i=0;i<12;i++){
            float a=i*Mathf.PI*2/12;
            var part=Primitive(PrimitiveType.Cube,"Gate Segment",gate.transform.position+new Vector3(Mathf.Cos(a)*7,Mathf.Sin(a)*7,0),new Vector3(3,1.3f,2.5f),Mat("gate",new Color(.45f,.2f,.95f),true),gate.transform);
            part.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90);
        }
        PlaceStructureBehindShips(gate);
        stationZone=Ring("Station Schutzzone",stationPoint,SafeRadius,new Color(.2f,.8f,1f),.35f);stationZone.SetActive(data.map==0);
        gateZone=Ring("Tor Schutzzone",GatePoint,SafeRadius,new Color(.2f,.8f,1f),.35f);
        targetRing=Ring("TARGET LOCK",Vector3.zero,6.7f,new Color(1,.7f,.15f),.75f);targetRing.SetActive(false);
        // Boss bleibt in der Kartenmitte; normale NPCs erscheinen auch in Spielernaehe.
        if(data.map==0) SpawnEnemy(true);
        for(int i=0;i<25;i++)SpawnEnemy(false, i);
        for(int i=0;i<22;i++)SpawnPickup();
    }
    void SpawnEnemy(bool boss, int spawnIndex=-1){
        Vector3 pos;
        if(boss) pos=new Vector3(1000f,0,675f);
        else {
            // NPCs ueber die gesamte Karte verteilen; nicht direkt an Station/Tor.
            do {
                pos=new Vector3(UnityEngine.Random.Range(90f,MapW-90f),0,UnityEngine.Random.Range(90f,MapH-90f));
            } while(Dist(pos,stationPoint)<185f || Dist(pos,GatePoint)<130f
                    || Dist(pos,new Vector3(1000f,0,675f))<100f);
        }
        int kind=boss?0:UnityEngine.Random.Range(0,npcs.Length);
        string model=boss?BossShipModel:npcs[kind];
        var e=new Enemy{boss=boss,kind=kind,name=boss?"Aurora-Boss":"NPC",maxHull=boss?4800:650,maxShield=boss?2500:330};
        e.hull=e.maxHull;e.shield=e.maxShield;e.aggro=false;
        e.go=Ship(model,pos,true);
        // Explizit dieselbe relative Groesse wie bisher (NPC=Spielerbreite, Boss groesser).
        ScaleShipVisual(e.go, boss?BossShipWidth:NpcShipWidth);
        e.go.name=e.name+" – "+model;e.waypoint=pos;e.roamAt=0f;e.nextRocket=Time.time+UnityEngine.Random.Range(4f,9f);enemies.Add(e);
    }
    void SpawnPickup(){
        bool crystal=UnityEngine.Random.value<.35f;
        var p=Primitive(crystal?PrimitiveType.Cylinder:PrimitiveType.Cube,crystal?"Exoterium":"Bonusbox",new Vector3(UnityEngine.Random.Range(100f,1900f),1,UnityEngine.Random.Range(100f,1250f)),Vector3.one*(crystal?2.1f:1.8f),Mat(crystal?"crystal":"box",crystal?new Color(.7f,.2f,1):new Color(.1f,.85f,1),true));
        var col=p.AddComponent<BoxCollider>();col.isTrigger=true;pickups.Add(new Pickup{go=p,crystal=crystal});
    }
    void Update(){
        if(player==null)return;
        InputLoop();
        if(paused)return;
        float dt=Mathf.Min(Time.deltaTime,.05f);
        Move(dt);EnemyAI(dt);UpdateShots(dt);Gather();Regen(dt);
        if(attack&&Time.time>=nextLaser)FireLaser();
        if(selected!=null&&selected.go){targetRing.SetActive(true);targetRing.transform.position=Flat(selected.go.transform.position)+Vector3.up*.2f;targetRing.GetComponent<LineRenderer>().startColor=attack?Color.red:new Color(1,.65f,.1f);targetRing.GetComponent<LineRenderer>().endColor=attack?Color.red:new Color(1,.65f,.1f);}
        else targetRing.SetActive(false);
        for(int i=numbers.Count-1;i>=0;i--)if(Time.time>numbers[i].expires)numbers.RemoveAt(i);
        cam.transform.position=Vector3.Lerp(cam.transform.position,player.transform.position+new Vector3(0,85,-75),Mathf.Clamp01(dt*5));
        if(Time.time-saveAt>12){Save();saveAt=Time.time;}
    }
    void InputLoop(){
        if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;panel=paused?"Hilfe":"";}
        if(Input.GetKeyDown(KeyCode.I))Toggle("Inventar");
        if(Input.GetKeyDown(KeyCode.E))Toggle("Ausrüstung");
        if(Input.GetKeyDown(KeyCode.B))Toggle("Shop");
        if(Input.GetKeyDown(KeyCode.P))Toggle("Profil");
        if(Input.GetKeyDown(KeyCode.L))Toggle("Ereignislog");
        if(paused)return;
        // Zoomstufen geordnet von weit (0) bis nah (3).
        // Mausrad hoch = naeher, Mausrad runter = weiter weg.
        // An den Grenzen stoppen, kein unerwartetes Umspringen.
        float scroll=Input.mouseScrollDelta.y;
        if(scroll>0.01f)zoomLevel=Mathf.Min(zoomSizes.Length-1,zoomLevel+1);
        else if(scroll<-.01f)zoomLevel=Mathf.Max(0,zoomLevel-1);
        cam.orthographicSize=zoomSizes[zoomLevel];
        if(Input.GetKeyDown(KeyCode.LeftControl)||Input.GetKeyDown(KeyCode.RightControl))attack=!attack;
        if(Input.GetKeyDown(KeyCode.Alpha6))data.ammoType=1;
        if(Input.GetKeyDown(KeyCode.Alpha7))data.ammoType=2;
        if(Input.GetKeyDown(KeyCode.Alpha8))data.ammoType=3;
        if(Input.GetKeyDown(KeyCode.Alpha9)||Input.GetKeyDown(KeyCode.Space))FireRocket();
        for(int i=0;i<5;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))ChangeShip(i);
        if(Input.GetKeyDown(KeyCode.G)&&Dist(player.transform.position,GatePoint)<16)JumpGate();
        Vector3 keyboard=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
        if(keyboard.sqrMagnitude>.01f){flightDirection=keyboard.normalized;destination=Flat(player.transform.position)+flightDirection*35;hasDestination=true;gathering=null;}
        // Maustaste halten: kontinuierlich Flugziel setzen. Ausgewähltes Target bleibt erhalten.
        if(Input.GetMouseButton(0)&&!OverHUD(Input.mousePosition)){
            Ray ray=cam.ScreenPointToRay(Input.mousePosition);
            if(Input.GetMouseButtonDown(0)){
                RaycastHit[] hits=Physics.RaycastAll(ray,1000);
                foreach(var hit in hits){
                    Enemy e=enemies.Find(n=>n.go&&(hit.collider.transform==n.go.transform||hit.collider.transform.IsChildOf(n.go.transform)));
                    if(e!=null){selected=e;gathering=null;Notice("Target Lock: "+e.name);return;}
                    Pickup p=pickups.Find(n=>n.go&&(hit.collider.transform==n.go.transform||hit.collider.transform.IsChildOf(n.go.transform)));
                    if(p!=null){gathering=p;destination=Flat(p.go.transform.position);hasDestination=true;return;}
                }
            }
            Plane plane=new Plane(Vector3.up,Vector3.zero);float enter;
            if(plane.Raycast(ray,out enter)){destination=ClampMap(ray.GetPoint(enter));hasDestination=true;gathering=null;}
        }
    }
    bool OverHUD(Vector3 m){
        float scale=HudScale;
        float x=m.x/scale, y=(Screen.height-m.y)/scale;
        float w=HudWidth,h=HudHeight;
        return (x<252f&&y<124f) || (x>w-188f&&y<140f)
            || (x>w-258f&&y>h-229f)
            || (x<309f&&y>h-210f)
            || (x>w*.5f-163f&&x<w*.5f+163f&&y>h-109f)
            || (selected!=null&&x>w*.5f-122f&&x<w*.5f+122f&&y<83f)
            || (paused&&x>w*.5f-390f&&x<w*.5f+390f&&y>h*.5f-285f&&y<h*.5f+285f);
    }
    void Toggle(string name){if(paused&&panel==name){paused=false;panel="";}else{paused=true;panel=name;}}
    Vector3 ClampMap(Vector3 p){return new Vector3(Mathf.Clamp(p.x,8,MapW-8),0,Mathf.Clamp(p.z,8,MapH-8));}
    void Move(float dt){
        if(!hasDestination)return;
        var delta=Flat(destination-player.transform.position);
        if(delta.magnitude<.9f){hasDestination=false;return;}
        flightDirection=delta.normalized;player.transform.position+=flightDirection*Mathf.Min(PlayerSpeed*1.5f*dt,delta.magnitude);
        Vector3 face=(attack&&selected!=null&&selected.go)?Flat(selected.go.transform.position-player.transform.position):flightDirection;
        if(face.sqrMagnitude>.01f)player.transform.rotation=Quaternion.Slerp(player.transform.rotation,Quaternion.LookRotation(face),Mathf.Clamp01(dt*7));
    }
    void EnemyAI(float dt){
        Vector3 bossCenter=new Vector3(MapW*.5f,0,MapH*.5f);
        foreach(var e in enemies){
            if(e.go==null)continue;
            Vector3 position=Flat(e.go.transform.position);
            Vector3 delta=Flat(player.transform.position-e.go.transform.position);
            float distance=delta.magnitude;
            bool playerProtected=SafeEnabled&&InSafe(player.transform.position);
            bool npcProtected=SafeEnabled&&InSafe(e.go.transform.position);
            if(Time.time-e.combatAt>4f)e.shield=Mathf.Min(e.maxShield,e.shield+e.maxShield*.016f*dt);

            // Boss bleibt rund um die Kartenmitte und verfolgt nicht quer ueber die Karte.
            // Er greift Spieler in Reichweite mit Lasern und gelegentlich Raketen an.
            if(e.boss){
                if(Time.time>e.roamAt || Dist(position,e.waypoint)<5f){
                    e.roamAt=Time.time+UnityEngine.Random.Range(3f,6f);
                    e.waypoint=bossCenter+new Vector3(UnityEngine.Random.Range(-95f,95f),0,UnityEngine.Random.Range(-75f,75f));
                }
                Vector3 dir=Flat(e.waypoint-position);
                if(dir.sqrMagnitude>4f)e.go.transform.position=ClampMap(position+dir.normalized*dt*9f);
                if(distance<NpcRange && !playerProtected && !npcProtected){
                    if(delta.sqrMagnitude>.01f)e.go.transform.rotation=Quaternion.Slerp(e.go.transform.rotation,Quaternion.LookRotation(delta.normalized),Mathf.Clamp01(dt*6f));
                    if(Time.time>=e.nextFire){
                        e.nextFire=Time.time+1.8f;
                        Launch(e.go.transform.position,player.transform.position,null,true,70f,false,Color.red,0);
                        e.combatAt=Time.time;
                    }
                    if(Time.time>=e.nextRocket){
                        e.nextRocket=Time.time+UnityEngine.Random.Range(6f,11f);
                        Launch(e.go.transform.position,player.transform.position,null,true,125f,true,new Color(1f,.55f,.12f),0);
                        e.combatAt=Time.time;
                    }
                }else if(dir.sqrMagnitude>.01f){
                    e.go.transform.rotation=Quaternion.Slerp(e.go.transform.rotation,Quaternion.LookRotation(dir.normalized),Mathf.Clamp01(dt*3f));
                }
                continue;
            }

            // Normale NPCs patrouillieren frei ueber groessere Teile der Karte.
            // Erst nach Angriff durch den Spieler verfolgen sie ihn aktiv.
            bool engaged=e.aggro;
            if(engaged && !playerProtected){
                if(delta.sqrMagnitude>.01f)e.go.transform.rotation=Quaternion.Slerp(e.go.transform.rotation,Quaternion.LookRotation(delta.normalized),Mathf.Clamp01(dt*7f));
                if(distance<NpcRange && !npcProtected && Time.time>=e.nextFire){
                    e.nextFire=Time.time+1.65f;
                    Launch(e.go.transform.position,player.transform.position,null,true,30+e.kind*9,false,Color.red,0);
                    e.combatAt=Time.time;
                }
                float preferred=39f;
                if(distance>preferred+5f)e.go.transform.position=ClampMap(position+delta.normalized*dt*20f);
                else if(distance<preferred-13f&&distance>1f)e.go.transform.position=ClampMap(position-delta.normalized*dt*9f);
                continue;
            }
            if(Time.time>=e.roamAt || Dist(position,e.waypoint)<8f){
                e.roamAt=Time.time+UnityEngine.Random.Range(9f,17f);
                Vector3 next;
                int attempts=0;
                do{
                    next=ClampMap(position+new Vector3(UnityEngine.Random.Range(-330f,330f),0,UnityEngine.Random.Range(-270f,270f)));
                    attempts++;
                }while(attempts<12 && (Dist(next,stationPoint)<160f || Dist(next,GatePoint)<110f));
                e.waypoint=next;
            }
            Vector3 travel=Flat(e.waypoint-position);
            if(travel.sqrMagnitude>1f){
                e.go.transform.position=ClampMap(position+travel.normalized*dt*13f);
                e.go.transform.rotation=Quaternion.Slerp(e.go.transform.rotation,Quaternion.LookRotation(travel.normalized),Mathf.Clamp01(dt*4f));
            }
        }
    }
    int AmmoCount {get{return data.ammoType==1?data.ammo1:data.ammoType==2?data.ammo2:data.ammo3;}}
    void SpendAmmo(){if(data.ammoType==1)data.ammo1--;else if(data.ammoType==2)data.ammo2--;else data.ammo3--;}
    void FireLaser(){
        nextLaser=Time.time+.46f;
        if(selected==null||selected.go==null||AmmoCount<=0||Dist(player.transform.position,selected.go.transform.position)>PlayerRange||InSafe(player.transform.position)&&SafeEnabled||InSafe(selected.go.transform.position)&&SafeEnabled)return;
        SpendAmmo();lastCombat=Time.time;
        float baseDamage=CountEquipped(data.weaponSlots,1)*40+CountEquipped(data.weaponSlots,2)*60+CountEquipped(data.weaponSlots,3)*100;
        float dmg=baseDamage*data.ammoType/2f;
        Color c=data.ammoType==1?new Color(1,.1f,.2f):data.ammoType==2?new Color(.1f,.6f,1):new Color(.15f,1,.35f);
        Vector3 side=player.transform.right*1.3f;
        Launch(player.transform.position+side,selected.go.transform.position,selected,false,dmg,false,c,1);
        Launch(player.transform.position-side,selected.go.transform.position,selected,false,dmg,false,c,-1);
    }
    void FireRocket(){
        if(selected==null||selected.go==null||data.rockets<=0||Time.time<nextRocket||Dist(player.transform.position,selected.go.transform.position)>PlayerRange)return;
        nextRocket=Time.time+1.4f;data.rockets--;lastCombat=Time.time;
        Launch(player.transform.position,selected.go.transform.position,selected,false,170,true,new Color(1,.55f,.1f),0);
    }
    void Launch(Vector3 from,Vector3 to,Enemy enemy,bool hostile,float damage,bool rocket,Color color,float offset){
        var go=Primitive(PrimitiveType.Capsule,rocket?(hostile?"Boss-Rakete":"Rakete"):(hostile?"NPC Laser":"Doppel-Laser"),from,new Vector3(rocket?.7f:.35f,rocket?2.2f:1.5f,rocket?.7f:.35f),Mat("shot"+color.ToString(),color,true));
        shots.Add(new Shot{go=go,enemy=enemy,hostile=hostile,damage=damage,rocket=rocket,speed=rocket?60:ProjectileSpeed,ttl=rocket?2.5f:1.5f});
    }
    void UpdateShots(float dt){
        for(int i=shots.Count-1;i>=0;i--){
            var s=shots[i];var target=s.hostile?player:s.enemy!=null?s.enemy.go:null;
            if(target==null){Destroy(s.go);shots.RemoveAt(i);continue;}
            Vector3 delta=target.transform.position-s.go.transform.position;float distance=delta.magnitude,step=s.speed*dt;
            if(distance<step+2f){
                if(!(InSafe(target.transform.position)&&SafeEnabled)){
                    if(s.hostile)DamagePlayer(s.damage);else DamageEnemy(s.enemy,s.damage);
                }
                Destroy(s.go);shots.RemoveAt(i);
            }else{
                s.go.transform.position+=delta.normalized*step;
                s.go.transform.rotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                s.ttl-=dt;if(s.ttl<=0){Destroy(s.go);shots.RemoveAt(i);}
            }
        }
    }
    void DamageEnemy(Enemy e,float dmg){
        if(e==null||!enemies.Contains(e))return;
        float absorb=Mathf.Min(e.shield,dmg);e.shield-=absorb;e.hull-=dmg-absorb;e.aggro=true;e.combatAt=Time.time;
        numbers.Add(new FloatText{pos=e.go.transform.position+Vector3.up*7,text=Mathf.CeilToInt(dmg).ToString(),color=absorb>0?Color.cyan:Color.red,expires=Time.time+.8f});
        if(e.hull>0)return;
        int rewardCredits=e.boss?1200:80+e.kind*15;
        int rewardXP=e.boss?550:90+e.kind*25;
        data.kills++;data.credits+=rewardCredits;GainXP(rewardXP);
        Log(e.name+" zerstört: +"+rewardCredits+" Credits, +"+rewardXP+" EXP");Destroy(e.go);enemies.Remove(e);if(selected==e){selected=null;attack=false;}
        Invoke(nameof(Respawn),5f);
    }
    void Respawn(){SpawnEnemy(false);}
    void DamagePlayer(float dmg){
        lastCombat=Time.time;float absorb=Mathf.Min(data.shield,dmg);data.shield-=absorb;data.hull-=dmg-absorb;
        if(data.hull>0)return;
        data.hull=MaxHull;data.shield=MaxShield;data.map=0;player.transform.position=stationPoint+new Vector3(55,1.8f,-20);attack=false;hasDestination=false;CreateMap();Log("Schiff zerstört – Rückkehr zur Station.");
    }
    void Regen(float dt){
        if(Time.time-lastCombat<4)return;
        data.shield=Mathf.Min(MaxShield,data.shield+MaxShield*.015f*dt);
        int repair=EquippedRepairTier();if(repair>0)data.hull=Mathf.Min(MaxHull,data.hull+new[]{0,15,28,45}[repair]*dt);
    }
    void Gather(){
        if(gathering==null||!gathering.go)return;
        if(Dist(player.transform.position,gathering.go.transform.position)>3){destination=Flat(gathering.go.transform.position);hasDestination=true;return;}
        if(gathering.crystal){int gained=UnityEngine.Random.Range(1,4);data.exoterium+=gained;Log("+"+gained+" Exoterium eingesammelt");}
        else{int credits=UnityEngine.Random.Range(50,180),ammo=UnityEngine.Random.Range(10,30);data.credits+=credits;data.ammo1+=ammo;Log("Bonusbox: +"+credits+" Credits, +"+ammo+" T1-Munition");}
        Destroy(gathering.go);pickups.Remove(gathering);gathering=null;
    }
    void JumpGate(){data.map=1-data.map;player.transform.position=GatePoint+new Vector3(8,1.8f,8);lastCombat=-999;CreateMap();Log("Sprung zu Karte "+(data.map+1));}
    void ChangeShip(int index){data.ship=index;Vector3 pos=player.transform.position;Destroy(player);player=Ship(players[index],Flat(pos),false);data.hull=Mathf.Min(data.hull,MaxHull);Log("Schiff: "+players[index]);}
    void GainXP(int points){data.xp+=points;while(data.level<20&&data.xp>=RequiredXP){data.xp-=RequiredXP;data.level++;data.credits+=data.level*100;Log("Level "+data.level+" erreicht!");}}
    void Log(string s){events.Insert(0,DateTime.Now.ToString("HH:mm")+" · "+s);if(events.Count>30)events.RemoveAt(events.Count-1);Debug.Log(s);}
    void Notice(string s){notice=s;noticeUntil=Time.time+2.5f;}
    void Save(){PlayerPrefs.SetString("exoterium_unity_csharp_v2",JsonUtility.ToJson(data));PlayerPrefs.Save();}
    void Load(){string raw=PlayerPrefs.GetString("exoterium_unity_csharp_v2","");if(string.IsNullOrEmpty(raw))return;try{data=JsonUtility.FromJson<SaveData>(raw);data.ship=Mathf.Clamp(data.ship,0,4);data.ammoType=Mathf.Clamp(data.ammoType,1,3);data.repairTier=Mathf.Clamp(data.repairTier,0,3);}catch(Exception ex){Debug.LogWarning(ex.Message);data=new SaveData();}}
    void OnApplicationQuit(){Save();}
    // UI: Unity IMGUI als direkt lauffähiger Prototyp; später auf Canvas/uGUI oder UI Toolkit migrierbar.
    Texture2D white;
    void DrawBar(Rect r,float value,float max,Color c){
        if(white==null){white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();}
        Color old=GUI.color;GUI.color=new Color(.04f,.09f,.16f,.95f);GUI.DrawTexture(r,white);
        GUI.color=c;GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value/Mathf.Max(1,max)),r.height),white);GUI.color=old;
    }
    // Equipment: 0=leer, 1..3=Tier. Repair uses the same tiers in extra slots.
    // Inventory counts represent owned items; equipped counts are deducted from available stock.
    int CountEquipped(int[] slots,int tier){int n=0;if(slots!=null)foreach(int v in slots)if(v==tier)n++;return n;}
    int EquippedRepairTier(){if(data.extraSlots==null)return 0;foreach(int tier in data.extraSlots)if(tier>0)return tier;return 0;}
    int SlotCapacity(int group){return group==0?new[]{2,3,4,5,6}[data.ship]:group==1?4:group==2?4:2;}
    int[] Slots(int group){return group==0?data.weaponSlots:group==1?data.shieldSlots:group==2?data.speedSlots:data.extraSlots;}
    int Owned(int group,int tier){
        if(group==0)return tier==1?data.laser1:tier==2?data.laser2:data.laser3;
        if(group==1)return tier==1?data.shield1:tier==2?data.shield2:data.shield3;
        if(group==2)return tier==1?data.speedGenerators:0;
        return tier==data.repairTier&&tier>0?1:0;
    }
    int Available(int group,int tier){return Mathf.Max(0,Owned(group,tier)-CountEquipped(Slots(group),tier));}
    void InitEquipment(){
        if(data.weaponSlots==null){data.weaponSlots=new int[6];for(int i=0;i<Mathf.Min(data.laser1,2);i++)data.weaponSlots[i]=1;}
        if(data.shieldSlots==null){data.shieldSlots=new int[4];if(data.shield1>0)data.shieldSlots[0]=1;}
        if(data.speedSlots==null)data.speedSlots=new int[4];
        if(data.extraSlots==null){data.extraSlots=new int[2];data.extraSlots[0]=data.repairTier;}
        // Keep slot arrays consistent with their saved format.
        if(data.weaponSlots.Length!=6)Array.Resize(ref data.weaponSlots,6);
        if(data.shieldSlots.Length!=4)Array.Resize(ref data.shieldSlots,4);
        if(data.speedSlots.Length!=4)Array.Resize(ref data.speedSlots,4);
        if(data.extraSlots.Length!=2)Array.Resize(ref data.extraSlots,2);
    }
    readonly Dictionary<string,Texture2D> itemIcons=new Dictionary<string,Texture2D>();
    Texture2D ItemIcon(int group,int tier){
        string key=group+":"+tier;
        Texture2D tex;if(itemIcons.TryGetValue(key,out tex))return tex;
        tex=new Texture2D(32,32,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;
        Color accent=group==0?new Color(1f,.35f,.25f):group==1?new Color(.15f,.68f,1f):group==2?new Color(.2f,1f,.55f):new Color(1f,.82f,.25f);
        Color[] pixels=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){
            float dx=x-15.5f,dy=y-15.5f;bool shape=false;
            if(group==0)shape=(Mathf.Abs(dx)<3&&Mathf.Abs(dy)<12)||(Mathf.Abs(dx)<10&&Mathf.Abs(dy+8)<3);
            if(group==1)shape=(Mathf.Abs(dx)+Mathf.Abs(dy)*.8f<13&&Mathf.Abs(dx)+Mathf.Abs(dy)*.8f>8)||Mathf.Abs(dx)+Mathf.Abs(dy)*.8f<4;
            if(group==2)shape=(dx*dx+dy*dy<125&&dx*dx+dy*dy>55)||(Mathf.Abs(dx)<2&&Mathf.Abs(dy)<10);
            if(group==3)shape=(Mathf.Abs(dx)<3&&Mathf.Abs(dy)<11)||(Mathf.Abs(dy)<3&&Mathf.Abs(dx)<11);
            pixels[y*32+x]=shape?Color.Lerp(accent,Color.white,Mathf.Clamp01((tier-1)*.22f)):new Color(.025f,.05f,.09f,1);
        }
        tex.SetPixels(pixels);tex.Apply();itemIcons[key]=tex;return tex;
    }
    int dragGroup=-1,dragTier,dragSlot=-1;
    string[] equipNames={"WAFFEN","SCHILDE","SPEED","EXTRAS"};
    // Zweispaltige Ausruestung: links nur vorhandene Gegenstaende, rechts die Schiffsslots.
    void DrawEquipment(){
        GUILayout.Label("AUSRÜSTUNG  ·  Ziehen: Inventar ↔ Schiff");
        Rect canvas=GUILayoutUtility.GetRect(720f,380f,GUILayout.ExpandWidth(true));
        Event ev=Event.current;
        float left=canvas.x+8f, right=canvas.x+350f;
        GUI.Box(new Rect(canvas.x,canvas.y,325f,358f),"INVENTAR · VERFÜGBAR");
        GUI.Box(new Rect(canvas.x+337f,canvas.y,355f,358f),"SCHIFFSAUSRÜSTUNG");
        for(int group=0;group<4;group++){
            float y=canvas.y+31f+group*80f;
            GUI.Label(new Rect(left+7,y,90,20),equipNames[group]);
            int itemIndex=0;
            for(int tier=1;tier<=(group==2?1:3);tier++){
                int available=Available(group,tier);
                if(available<=0)continue; // Keine Platzhalter fuer nicht vorhandene Gegenstaende.
                Rect inv=new Rect(left+100+itemIndex*68,y,62,58);
                GUI.Box(inv,"");
                GUI.DrawTexture(new Rect(inv.x+16,inv.y+3,29,29),ItemIcon(group,tier));
                GUI.Label(new Rect(inv.x+7,inv.y+35,54,18),"T"+tier+" ×"+available);
                if(ev.type==EventType.MouseDown&&ev.button==0&&inv.Contains(ev.mousePosition)){
                    dragGroup=group;dragTier=tier;dragSlot=-1;ev.Use();
                }
                itemIndex++;
            }
            int[] slots=Slots(group);
            int capacity=SlotCapacity(group);
            GUI.Label(new Rect(right+5,y,80,20),equipNames[group]);
            for(int i=0;i<capacity;i++){
                Rect slot=new Rect(right+91+(i%4)*55,y+(i/4)*37,49,33);
                GUI.Box(slot,""); // Leere Slots bleiben bewusst leer.
                int tier=slots[i];
                if(tier>0){
                    GUI.DrawTexture(new Rect(slot.x+6,slot.y+2,28,28),ItemIcon(group,tier));
                    GUI.Label(new Rect(slot.x+35,slot.y+10,14,18),""+tier);
                }
                if(ev.type==EventType.MouseDown&&ev.button==0&&slot.Contains(ev.mousePosition)&&tier>0){
                    dragGroup=group;dragTier=tier;dragSlot=i;ev.Use();
                }
                if(ev.type==EventType.MouseUp&&ev.button==0&&slot.Contains(ev.mousePosition)&&dragGroup==group){
                    if(dragSlot==i){dragGroup=-1;ev.Use();}
                    else if(dragSlot>=0){int tmp=slots[i];slots[i]=slots[dragSlot];slots[dragSlot]=tmp;dragGroup=-1;Save();ev.Use();}
                    else if(slots[i]==0&&Available(group,dragTier)>0){slots[i]=dragTier;dragGroup=-1;Save();ev.Use();}
                }
            }
            Rect inventoryDrop=new Rect(left+96,y,215,62);
            if(ev.type==EventType.MouseUp&&ev.button==0&&inventoryDrop.Contains(ev.mousePosition)&&dragGroup==group&&dragSlot>=0){
                slots[dragSlot]=0;dragGroup=-1;Save();ev.Use();
            }
        }
        if(dragGroup>=0){
            if(ev.type==EventType.MouseUp&&ev.button==0){dragGroup=-1;ev.Use();}
            else if(ev.type==EventType.Repaint)GUI.DrawTexture(new Rect(ev.mousePosition.x-15,ev.mousePosition.y-15,30,30),ItemIcon(dragGroup,dragTier));
        }
        GUILayout.Label("Leere Felder = freie Schiffsslots · Nur gesammelte oder gekaufte Ausrüstung links");
    }
    static readonly string[] HudTabs={"Inventar","Ausrüstung","Shop","Profil","Ereignislog"};
    float HudScale { get { return Mathf.Min(1f, Mathf.Min(Screen.width/1280f, Screen.height/720f)); } }
    float HudWidth { get { return Screen.width/HudScale; } }
    float HudHeight { get { return Screen.height/HudScale; } }
    Vector2 panelScroll;
    string CompactEvent(string message, int maxLength=39){
        return message.Length>maxLength ? message.Substring(0,maxLength-1)+"…" : message;
    }
    void OnGUI(){
        Matrix4x4 originalMatrix=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(HudScale,HudScale,1f))*originalMatrix;
        GUI.skin.label.fontSize=12;GUI.skin.button.fontSize=12;
        // Schlankere Statusanzeigen; unveraenderte Kamera und Spielmechanik.
        GUI.Box(new Rect(12,12,238,110),"EXOTERIUM: FRONTIER");
        GUI.Label(new Rect(21,33,220,18),players[data.ship]+" · Karte "+(data.map+1));
        GUI.Label(new Rect(21,54,220,17),"Hülle  "+Mathf.CeilToInt(data.hull)+" / "+MaxHull);
        DrawBar(new Rect(21,71,218,7),data.hull,MaxHull,new Color(1,.16f,.25f));
        GUI.Label(new Rect(21,82,220,17),"Schild  "+Mathf.CeilToInt(data.shield)+" / "+MaxShield);
        DrawBar(new Rect(21,100,218,7),data.shield,MaxShield,new Color(.05f,.62f,1));
        float pilotX=HudWidth-186;
        GUI.Box(new Rect(pilotX,12,174,125),"PILOT");
        GUI.Label(new Rect(pilotX+9,33,157,17),"Credits: "+data.credits.ToString("N0"));
        GUI.Label(new Rect(pilotX+9,52,157,17),"Exoterium: "+data.exoterium.ToString("N0"));
        GUI.Label(new Rect(pilotX+9,71,157,17),"Level: "+data.level);
        GUI.Label(new Rect(pilotX+9,90,157,17),"EXP: "+data.xp+" / "+RequiredXP);
        GUI.Label(new Rect(pilotX+9,109,157,17),"Fehlen: "+Mathf.Max(0,RequiredXP-data.xp)+" EXP");
        if(selected!=null&&selected.go){
            GUI.Box(new Rect(HudWidth/2-120,12,240,68),selected.name+(attack?" · ANGRIFF":" · LOCK"));
            DrawBar(new Rect(HudWidth/2-110,42,220,9),selected.shield,selected.maxShield,new Color(.02f,.67f,1));
            DrawBar(new Rect(HudWidth/2-110,56,220,9),selected.hull,selected.maxHull,new Color(1,.15f,.26f));
            Vector3 screen=cam.WorldToScreenPoint(selected.go.transform.position+Vector3.up*9);
            if(screen.z>0){float x=screen.x-30,y=HudHeight-screen.y;DrawBar(new Rect(x,y,60,5),selected.shield,selected.maxShield,Color.cyan);DrawBar(new Rect(x,y+8,60,5),selected.hull,selected.maxHull,Color.red);}
        }
        // 2 x 5 Munitions-Slots; vier aktiv, sechs Platzhalter.
        float slotW=57f,slotH=31f,slotGap=5f;
        float ammoW=5*slotW+6*slotGap;
        float barX=(HudWidth-ammoW)*.5f,barY=HudHeight-106f;
        GUI.Box(new Rect(barX,barY,ammoW,101f),"MUNITION");
        string[] labels={"T1 · 1×","T2 · 2×","T3 · 3×","RAK"};
        int[] counts={data.ammo1,data.ammo2,data.ammo3,data.rockets};
        for(int i=0;i<10;i++){
            Rect slot=new Rect(barX+slotGap+(i%5)*(slotW+slotGap),barY+20+(i/5)*(slotH+slotGap),slotW,slotH);
            if(i<4){
                Color old=GUI.color;
                GUI.color=i==0?new Color(1,.5f,.55f):i==1?new Color(.5f,.8f,1):i==2?new Color(.5f,1,.6f):new Color(1,.75f,.3f);
                if(i<3&&data.ammoType==i+1)GUI.color=Color.white;
                if(GUI.Button(slot,labels[i]+"\n"+counts[i])){if(i<3)data.ammoType=i+1;else FireRocket();}
                GUI.color=old;
            }else{
                bool previous=GUI.enabled;GUI.enabled=false;
                GUI.Box(slot,"—");GUI.enabled=previous;
            }
        }
        GUI.Box(new Rect(HudWidth-254,HudHeight-225,242,210),"MINIMAP · 4×4");
        Rect mapRect=new Rect(HudWidth-246,HudHeight-197,225,165);
        DrawBar(mapRect,1,1,new Color(.035f,.09f,.15f));
        for(int r=0;r<4;r++)for(int c=0;c<4;c++){
            Rect cell=new Rect(mapRect.x+c*mapRect.width/4,mapRect.y+r*mapRect.height/4,mapRect.width/4,mapRect.height/4);
            GUI.Box(cell,((char)('A'+r)).ToString()+(c+1));
        }
        // Minimap: feste Ziele und alle lebenden NPCs (Positionen werden laufend aktualisiert).
        if(data.map==0) DrawMapMarker(mapRect,stationPoint,new Color(.2f,.8f,1f),"S",13);
        DrawMapMarker(mapRect,GatePoint,new Color(.7f,.35f,1f),"T",13);
        foreach(var enemy in enemies)if(enemy.go!=null)
            DrawMapMarker(mapRect,enemy.go.transform.position,Color.red,enemy.boss?"◆":"●",enemy.boss?12:9);
        DrawMapMarker(mapRect,player.transform.position,Color.cyan,"●",13);
        // Ereignislog oben, Menuefunktionen darunter. Kein Scrollen und keine
        // zusaetzlichen GameObjects/Updates pro Frame fuer die Loganzeige.
        float menuY=HudHeight-205f;
        GUI.Box(new Rect(12,menuY,292,193),"EREIGNISLOG / MENÜ");
        GUI.Label(new Rect(20,menuY+22,272,17),"Letzte Ereignisse:");
        for(int i=0, count=Mathf.Min(4,events.Count);i<count;i++){
            GUI.Label(new Rect(20,menuY+41+i*20,272,19),CompactEvent(events[i]));
        }
        for(int i=0;i<HudTabs.Length;i++){
            float bx=20+(i%3)*93f, by=menuY+127+(i/3)*29f;
            if(GUI.Button(new Rect(bx,by,88,25),HudTabs[i]))Toggle(HudTabs[i]);
        }
        if(Time.time<noticeUntil)GUI.Box(new Rect(HudWidth/2-150,90,300,28),notice);
        foreach(var n in numbers){
            Vector3 sp=cam.WorldToScreenPoint(n.pos+Vector3.up*(.8f-Time.time+n.expires)*2);
            if(sp.z>0){Color old=GUI.color;GUI.color=n.color;GUI.Label(new Rect(sp.x-25,HudHeight-sp.y-20,70,24),n.text);GUI.color=old;}
        }
        if(!paused){GUI.matrix=originalMatrix;return;}
        bool equipmentPanel=panel=="Inventar"||panel=="Ausrüstung";
        float panelWidth=equipmentPanel?770f:520f;
        float panelHeight=equipmentPanel?555f:535f;
        Rect area=new Rect(HudWidth/2-panelWidth/2,Mathf.Max(12f,(HudHeight-panelHeight)*.5f),panelWidth,Mathf.Min(panelHeight,HudHeight-24f));
        // Vollstaendig deckender Hintergrund: Weltraum und Schiffe scheinen nicht durch.
        Color oldPanelColor=GUI.color;
        if(white==null){white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();}
        GUI.color=new Color(.035f,.055f,.085f,1f);
        GUI.DrawTexture(area,white);
        GUI.color=oldPanelColor;
        GUI.Box(area,panel);GUILayout.BeginArea(new Rect(area.x+15,area.y+35,area.width-30,area.height-50));
        panelScroll=GUILayout.BeginScrollView(panelScroll);
        if(panel=="Hilfe"){GUILayout.Label("Linksklick: Ziel wählen oder fliegen · gedrückt halten: weiterfliegen");GUILayout.Label("STRG: Laserangriff an/aus · 6/7/8: Munition · 9/Leertaste: Rakete");GUILayout.Label("1–5: Schiff · G am Tor: Sprung · WASD: Fliegen");GUILayout.Label("I Inventar · E Ausrüstung · B Shop · P Profil · L Log");}
        if(panel=="Inventar"){GUILayout.Label("Credits: "+data.credits+" · Exoterium: "+data.exoterium+" · Erz: "+data.ore);GUILayout.Label("Munition: T1 "+data.ammo1+" · T2 "+data.ammo2+" · T3 "+data.ammo3+" · Raketen "+data.rockets);DrawEquipment();}
        if(panel=="Ausrüstung")DrawEquipment();
        if(panel=="Shop"){
            if(GUILayout.Button("100× T1 – 100 Credits"))BuyAmmo(1,100,100);
            if(GUILayout.Button("100× T2 – 300 Credits"))BuyAmmo(2,100,300);
            if(GUILayout.Button("100× T3 – 750 Credits"))BuyAmmo(3,100,750);
            if(GUILayout.Button("10× Raketen – 400 Credits")&&Spend(400))data.rockets+=10;
            if(GUILayout.Button("Laser T1 – 300 Credits")&&Spend(300))data.laser1++;
            if(GUILayout.Button("Laser T2 – 900 Credits")&&Spend(900))data.laser2++;
            if(GUILayout.Button("Laser T3 – 2600 Credits")&&Spend(2600))data.laser3++;
            if(GUILayout.Button("Schild T1 – 350 Credits")&&Spend(350))data.shield1++;
            if(GUILayout.Button("Schild T2 – 950 Credits")&&Spend(950))data.shield2++;
            if(GUILayout.Button("Schild T3 – 2900 Credits")&&Spend(2900))data.shield3++;
            if(GUILayout.Button("Speedgenerator – 800 Credits")&&Spend(800))data.speedGenerators++;
            if(GUILayout.Button("Reparaturroboter T2 – 1800 Credits")&&Spend(1800)){data.repairTier=2;data.extraSlots[0]=0;data.extraSlots[1]=0;}
            if(GUILayout.Button("Reparaturroboter T3 – 5000 Credits")&&Spend(5000)){data.repairTier=3;data.extraSlots[0]=0;data.extraSlots[1]=0;}
        }
        if(panel=="Profil"){GUILayout.Label("Level "+data.level+" · EXP "+data.xp+" / "+RequiredXP);GUILayout.Label("NPC-Abschüsse: "+data.kills);GUILayout.Label("Aktuelles Schiff: "+players[data.ship]);for(int i=0;i<5;i++)if(GUILayout.Button("Schiff wählen: "+players[i]))ChangeShip(i);}
        if(panel=="Ereignislog")foreach(string e in events)GUILayout.Label(e);
        if(GUILayout.Button("Schließen")){paused=false;panel="";Save();}
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix=originalMatrix;
    }
    bool Spend(int amount){if(data.credits<amount){Notice("Nicht genug Credits");return false;}data.credits-=amount;return true;}
    void BuyAmmo(int tier,int amount,int price){if(!Spend(price))return;if(tier==1)data.ammo1+=amount;else if(tier==2)data.ammo2+=amount;else data.ammo3+=amount;}
}
