using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShootZ
{
    public partial class ArcaneSurvival : MonoBehaviour
    {
        public float survivalSeconds = 300;
        const float MapScale = 5f;
        public float Health { get; private set; } = 100;
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int EnemyCount => enemies.Count;
        public bool Choosing { get; private set; }
        public bool Finished { get; private set; }
        public bool Paused { get; private set; }
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly List<Transform> gems = new List<Transform>();
        readonly List<Material> materials = new List<Material>();
        readonly int[] choices = new int[3];
        readonly string[] names = { "Empowered flame", "Rapid casting", "Twin embers", "Vital renewal", "Swift feet", "Frost mastery" };
        readonly string[] descriptions = { "+25% spell damage", "15% shorter casting interval", "+1 projectile (max 5)", "+25 max HP and heal 45", "+12% movement speed", "Frost nova damage +20" };
        Camera view;
        CharacterController controller;
        Material stone, cyan, ember, enemyMaterial, gemMaterial;
        float pitch, nextShot, nextNova, hurtUntil, vertical;
        float damage = 32, interval = .32f, speed = 7, maxHealth = 100, novaDamage = 35;
        int projectiles = 1;
        Transform hand;
        GUIStyle titleStyle, textStyle, smallStyle;
        class Enemy { public GameObject obj; public float health, speed, nextAttack, frozenUntil, alertUntil; public int faction, kind, boss=-1; public Vector3 target; }
        class Bolt { public GameObject obj; public Vector3 direction; public float life; }
        void Start()
        {
            Time.timeScale = 1;
            stone = MakeMaterial(new Color(.085f,.12f,.17f));
            cyan = MakeMaterial(new Color(.08f,.45f,.55f), true);
            ember = MakeMaterial(new Color(1,.29f,.055f), true);
            enemyMaterial = MakeMaterial(new Color(.26f,.31f,.23f));
            gemMaterial = MakeMaterial(new Color(.25f,1,.65f), true);
            BuildArena();
            BuildSurvival();
            LockCursor(true);
        }
        Material MakeMaterial(Color color, bool glow = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var mat = new Material(shader); mat.color = color;
            if(glow) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * .65f); }
            materials.Add(mat); return mat;
        }
        GameObject Shape(string label, PrimitiveType kind, Vector3 pos, Vector3 scale, Material mat, bool solid = true)
        {
            var obj = GameObject.CreatePrimitive(kind); obj.name = label;
            obj.transform.SetParent(transform); obj.transform.position = pos; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            if(!solid) { var col = obj.GetComponent<Collider>(); col.enabled = false; Destroy(col); }
            return obj;
        }
        void BuildArena()
        {
            Shape("Obsidian arena", PrimitiveType.Cube, new Vector3(0,-.5f,0),new Vector3(64*MapScale,1,64*MapScale),stone);
            for(int i=0;i<4;i++) {
                float a=i*Mathf.PI*.5f; var p=new Vector3(Mathf.Sin(a)*32*MapScale,2,Mathf.Cos(a)*32*MapScale);
                Shape("Arena boundary",PrimitiveType.Cube,p,i%2==0?new Vector3(64*MapScale+1,4,1):new Vector3(1,4,64*MapScale+1),stone);
            }
            for(int i=0;i<16;i++) {
                float a=i*Mathf.PI/8; var p=new Vector3(Mathf.Sin(a)*29*MapScale,2.5f,Mathf.Cos(a)*29*MapScale);
                Shape("Runic pillar",PrimitiveType.Cube,p,new Vector3(1.2f,5,1.2f),stone);
                Shape("Rune lantern",PrimitiveType.Sphere,p+Vector3.up*2.8f,Vector3.one*.55f,cyan,false);
            }
            for(int i=-3;i<=3;i++) {
                Shape("Inlaid rune",PrimitiveType.Cube,new Vector3(i*8*MapScale,.012f,0),new Vector3(.035f,.02f,62*MapScale),cyan,false);
                Shape("Inlaid rune",PrimitiveType.Cube,new Vector3(0,.013f,i*8*MapScale),new Vector3(62*MapScale,.02f,.035f),cyan,false);
            }
            var player = new GameObject("Mage"); player.transform.SetParent(transform); player.transform.position=new Vector3(0,.1f,-20);
            controller=player.AddComponent<CharacterController>(); controller.height=1.8f; controller.radius=.35f; controller.center=Vector3.up*.9f;
            var cameraObj=new GameObject("Mage Camera"); cameraObj.tag="MainCamera"; cameraObj.transform.SetParent(player.transform,false); cameraObj.transform.localPosition=Vector3.up*1.65f;
            view=cameraObj.AddComponent<Camera>(); view.fieldOfView=78; view.nearClipPlane=.05f; view.farClipPlane=100*MapScale;
            view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.018f,.025f,.055f);
            cameraObj.AddComponent<AudioListener>();
            hand=Shape("Held ember",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.16f,ember,false).transform;
            hand.SetParent(view.transform,false); hand.localPosition=new Vector3(.36f,-.25f,.6f);
            var light=hand.gameObject.AddComponent<Light>(); light.color=new Color(1,.3f,.1f); light.intensity=2; light.range=5;
        }
        void Update()
        {
            var kb=Keyboard.current; var mouse=Mouse.current;
            if(kb!=null && kb.escapeKey.wasPressedThisFrame && !Choosing && !Finished) { Paused=!Paused; Time.timeScale=Paused?0:1; LockCursor(!Paused); }
            if(Finished) { if(kb!=null && kb.rKey.wasPressedThisFrame) Restart(); return; }
            if(Choosing) {
                if(kb!=null) { if(kb.digit1Key.wasPressedThisFrame) SelectUpgrade(0); else if(kb.digit2Key.wasPressedThisFrame) SelectUpgrade(1); else if(kb.digit3Key.wasPressedThisFrame) SelectUpgrade(2); }
                return;
            }
            if(Paused) return;
            float dt=Time.deltaTime; Elapsed+=dt;
            TickSurvival(dt); if(Finished) return;
            if(mouse!=null && Cursor.lockState==CursorLockMode.Locked) {
                Vector2 delta=mouse.delta.ReadValue()*.09f;
                controller.transform.Rotate(0,delta.x,0); pitch=Mathf.Clamp(pitch-delta.y,-85,85); view.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            if(kb!=null) {
                Vector2 move=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));
                if(controller.isGrounded) { vertical=-2; if(kb.spaceKey.wasPressedThisFrame) vertical=7; } else vertical-=20*dt;
                var direction=controller.transform.right*move.x+controller.transform.forward*move.y;
                controller.Move((Vector3.ClampMagnitude(direction,1)*speed*(driving?2.6f:(kb.leftCtrlKey.isPressed?.5f:1))+Vector3.up*vertical)*dt);
                if(kb.qKey.wasPressedThisFrame && frostBook && !driving) CastNova();
                if(kb.eKey.wasPressedThisFrame) Interact();
                if(kb.fKey.wasPressedThisFrame && !driving) CastSpecial();
                if(kb.xKey.wasPressedThisFrame) selectedSpell=1-selectedSpell;
                if(kb.hKey.wasPressedThisFrame && potions>0) { potions--;Health=Mathf.Min(maxHealth,Health+50); }
                if(kb.tabKey.wasPressedThisFrame) inventoryOpen=!inventoryOpen;
            }
            if(mouse!=null && !driving && mouse.rightButton.wasPressedThisFrame) Melee();
            if(mouse!=null && !driving && mouse.leftButton.isPressed && Time.time>=nextShot) Cast();
            
            TickBolts(dt); TickEnemies(dt); TickGems(dt);
            if(hand) hand.localPosition=new Vector3(.36f,-.25f+Mathf.Sin(Time.time*3)*.015f,.6f);
        }
        public void Cast()
        {
            if(Finished || Choosing || Paused) return;
            nextShot=Time.time+interval; EmitNoise(controller.transform.position,18); PlayTone(520,.08f,.1f);
            for(int i=0;i<projectiles;i++) {
                var dir=Quaternion.AngleAxis((i-(projectiles-1)*.5f)*5,view.transform.up)*view.transform.forward;
                var obj=Shape("Firebolt",PrimitiveType.Sphere,view.transform.position+view.transform.forward*.7f,Vector3.one*.2f,ember,false);
                bolts.Add(new Bolt { obj=obj, direction=dir, life=2 });
            }
        }
        void TickBolts(float dt)
        {
            for(int i=bolts.Count-1;i>=0;i--) {
                var b=bolts[i]; var old=b.obj.transform.position; var step=b.direction*32*dt; bool hit=false;
                if(Physics.SphereCast(old,.15f,b.direction,out var info,step.magnitude)) {
                    var enemy=enemies.Find(e=>e.obj==info.collider.gameObject);
                    if(enemy!=null) HurtEnemy(enemy,damage);
                    hit=true;
                }
                b.obj.transform.position+=step; b.life-=dt;
                if(hit || b.life<=0) { Destroy(b.obj); bolts.RemoveAt(i); }
            }
        }
        public void SpawnEnemy()
        {
            var center=controller.transform.position;
            var pos=center+Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward*Random.Range(15f,22f);
            pos.x=Mathf.Clamp(pos.x,-29*MapScale,29*MapScale); pos.z=Mathf.Clamp(pos.z,-29*MapScale,29*MapScale); pos.y=1;
            bool elite=Random.value<.15f && Elapsed>40;
            var obj=Shape(elite?"Elite wraith":"Wraith",PrimitiveType.Capsule,pos,Vector3.one*(elite?1.3f:.85f),enemyMaterial);
            enemies.Add(new Enemy{obj=obj,health=(elite?100:45)+Elapsed*.12f,speed=elite?1.8f:2.3f+Elapsed/180, faction=0, target=pos});
        }
        void TickEnemies(float dt)
        {
            foreach(var e in enemies) {
                var playerDistance=Vector3.Distance(controller.transform.position,e.obj.transform.position);
                if(playerDistance< (Keyboard.current!=null && Keyboard.current.leftCtrlKey.isPressed?3:(night?7:10))) { e.alertUntil=Time.time+7; e.target=controller.transform.position; }
                
                else if(new Vector2(e.obj.transform.position.x-zoneCenter.x,e.obj.transform.position.z-zoneCenter.z).magnitude>zoneRadius || extractionCalled) e.target=zoneCenter;
                var diff=e.target-e.obj.transform.position; diff.y=0;
                float dist=diff.magnitude;
                if(dist>1.25f) {
                    var step=diff.normalized*e.speed*(Time.time<e.frozenUntil?.25f:1)*dt;
                    if(Physics.Raycast(e.obj.transform.position,step.normalized,out var obstacle,.9f) && obstacle.collider.gameObject.name=="Military barricade")
                       step=Vector3.Cross(Vector3.up,diff.normalized)*e.speed*dt;
                    e.obj.transform.position+=step;
                }
                if(diff.sqrMagnitude>.01f)e.obj.transform.rotation=Quaternion.LookRotation(diff);
                if(e.kind==3 && playerDistance<10) EmitNoise(e.obj.transform.position,16);
                if(e.boss==0 && playerDistance<14) EmitNoise(controller.transform.position,25);
                if(playerDistance<1.8f && Time.time>=e.nextAttack) { e.nextAttack=Time.time+1; TakeDamage(e.boss>=0?22:8); if(Finished) break; }
            }
        }
        public void TakeDamage(float amount)
        {
            if(Finished) return;
            amount*=1-Mathf.Clamp(armorTier*.12f,0,.55f);
            if(Health-amount<=0 && deathWard){deathWard=false;Health=50;return;}
            Health=Mathf.Max(0,Health-amount); hurtUntil=Time.time+.18f;
            if(Health<=0) EndRun();
        }
        void HurtEnemy(Enemy enemy,float amount)
        {
            if(!enemies.Contains(enemy) || enemy.health<=0) return;
            enemy.health-=amount;
            if(enemy.health>0) return;
            var gem=Shape("Aether shard",PrimitiveType.Cube,enemy.obj.transform.position,Vector3.one*.3f,gemMaterial,false);
            gems.Add(gem.transform); if(enemy.boss>=0) DropLoot(enemy.obj.transform.position,3+enemy.boss,4); Destroy(enemy.obj); enemies.Remove(enemy); Kills++;
        }
        public void CastNova()
        {
            if(Time.time<nextNova || Finished || Choosing || Paused) return;
            nextNova=Time.time+8*cooldownScale;EmitNoise(controller.transform.position,32);PlayTone(180,.18f,.15f);
            foreach(var e in enemies.ToArray()) if(Vector3.Distance(e.obj.transform.position,controller.transform.position)<9) { e.frozenUntil=Time.time+3; HurtEnemy(e,novaDamage); }
            var ring=Shape("Frost nova",PrimitiveType.Sphere,controller.transform.position,Vector3.one*16,cyan,false);
            var renderer=ring.GetComponent<Renderer>(); renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(ring,.08f);
        }
        void TickGems(float dt)
        {
            for(int i=gems.Count-1;i>=0;i--) {
                var g=gems[i]; g.Rotate(40*dt,90*dt,0);
                float distance=Vector3.Distance(g.position,controller.transform.position+Vector3.up);
                if(distance<6) g.position=Vector3.MoveTowards(g.position,controller.transform.position+Vector3.up,12*dt);
                if(distance<1.1f) { Destroy(g.gameObject); gems.RemoveAt(i); Experience+=5; }
            }
            while(Experience>=Level*8 && !Choosing) { Experience-=Level*8;Level++; damage+=1.5f; if(Level%5!=0) continue; Choosing=true; Time.timeScale=0; LockCursor(false);
                var pool=new List<int>{0,1,2,3,4,5}; if(projectiles>=5)pool.Remove(2);
                for(int i=0;i<3;i++){ int index=Random.Range(0,pool.Count);choices[i]=pool[index];pool.RemoveAt(index); }
            }
        }
        public void SelectUpgrade(int slot)
        {
            if(!Choosing || slot<0 || slot>=3) return;
            switch(choices[slot]) {
                case 0:damage*=1.25f;break; case 1:interval=Mathf.Max(.07f,interval*.85f);cooldownScale=Mathf.Max(.3f,cooldownScale*.85f);break;
                case 2:projectiles=Mathf.Min(5,projectiles+1);break;
                case 3:maxHealth+=25;Health=Mathf.Min(maxHealth,Health+45);break;
                case 4:speed*=1.12f;break; case 5:novaDamage+=20;break;
            }
            Choosing=false;Time.timeScale=1;LockCursor(true);
        }
        void EndRun()
        {
            Finished=true; Time.timeScale=0; LockCursor(false);
            PlayerPrefs.SetInt("ShootZ.BestKills",Mathf.Max(Kills,PlayerPrefs.GetInt("ShootZ.BestKills",0)));
            PlayerPrefs.Save();
        }
        public void Restart() { Time.timeScale=1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        void LockCursor(bool locked) { Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked; }
        void OnDestroy() { Time.timeScale=1;LockCursor(false);foreach(var mat in materials)if(mat)Destroy(mat); }
        void OnGUI()
        {
            if(titleStyle==null) {
                titleStyle=new GUIStyle(GUI.skin.label){fontSize=32,fontStyle=FontStyle.Bold};
                textStyle=new GUIStyle(GUI.skin.label){fontSize=20};
                smallStyle=new GUIStyle(GUI.skin.label){fontSize=15};
            }
            var previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            GUI.color=new Color(.04f,.07f,.12f,.92f);GUI.DrawTexture(new Rect(20,20,360,145),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(38,30,330,45),"SHOOTZ / ARCANE SURVIVAL",textStyle);
            GUI.Label(new Rect(38,67,320,35),"HP "+Mathf.CeilToInt(Health)+" / "+maxHealth+"   |   LV "+Level,textStyle);
            GUI.color=new Color(.2f,.9f,.72f);GUI.DrawTexture(new Rect(38,104,300*Health/maxHealth,8),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(38,125,320,25),"AETHER "+Experience+" / "+(Level*8)+"   •   KILLS "+Kills,smallStyle);
            GUI.Label(new Rect(970,30,290,35),Mathf.FloorToInt(Elapsed/60).ToString("00")+":"+Mathf.FloorToInt(Elapsed%60).ToString("00")+" / RIFT",titleStyle);
            GUI.Label(new Rect(25,667,1100,35),"WASD move   •   Mouse aim   •   Hold LMB fire   •   Space jump   •   Q frost nova   •   Esc pause",smallStyle);
            GUI.Label(new Rect(25,635,700,30),"FROST NOVA  "+(!frostBook?"Find skillbook":Time.time>=nextNova?"READY":Mathf.CeilToInt(nextNova-Time.time)+"s"),textStyle);
            if(!Choosing && !Finished && !Paused) { GUI.Label(new Rect(632,345,30,35),"+",textStyle); }
            if(Time.time<hurtUntil) { GUI.color=new Color(1,0,0,.14f);GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture);GUI.color=Color.white; }
            if(Choosing || Finished || Paused) {
                GUI.color=new Color(.015f,.025f,.06f,.96f);GUI.DrawTexture(new Rect(230,190,820,360),Texture2D.whiteTexture);GUI.color=Color.white;
                if(Choosing) {
                    GUI.Label(new Rect(270,215,740,55),"CHOOSE YOUR ARCANE GIFT",titleStyle);
                    for(int i=0;i<3;i++) if(GUI.Button(new Rect(270+i*245,300,225,150),(i+1)+". "+names[choices[i]]+"\n\n"+descriptions[choices[i]])) SelectUpgrade(i);
                } else if(Finished) {
                    GUI.Label(new Rect(270,220,750,55),Health>0?"EXTRACTION COMPLETE":"THE RIFT CLAIMED YOU",titleStyle);
                    GUI.Label(new Rect(270,295,730,80),"Kills: "+Kills+"    Level: "+Level+"    Best kills: "+PlayerPrefs.GetInt("ShootZ.BestKills",0),textStyle);
                    if(GUI.Button(new Rect(450,405,380,65),"RETRY  [R]")) Restart();
                } else {
                    GUI.Label(new Rect(500,240,400,70),"PAUSED",titleStyle);
                    if(GUI.Button(new Rect(450,360,380,65),"RESUME")) { Paused=false;Time.timeScale=1;LockCursor(true); }
                }
            }
            DrawSurvivalHUD();
            GUI.matrix=previous;
        }
    }
}
