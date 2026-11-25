using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ShootZ {
 public partial class ArcaneSurvival {
    class Dormant { public Vector3 position; public int faction,kind; public float health=50; public Vector3 target; public float alertUntil; }
    class Loot { public GameObject obj; public int kind,tier; }
    readonly List<Dormant> dormant=new List<Dormant>();
    readonly List<Loot> loot=new List<Loot>();
    readonly List<Transform> zoneMarkers=new List<Transform>();
    Vector3 zoneCenter;
    float zoneRadius=30*MapScale, nextSimulation, landingProgress, meleeReady, specialReady;
    bool extractionCalled, helicopterLanded, frostBook, lightningBook, meteorBook, deathWard, inventoryOpen, driving;
    int armorTier, crownTier, tomeTier, potions=2;
    GameObject helicopter, vehicle;
    AudioSource sound;
    string prompt="", notice="";
    float noticeUntil;
    bool night;
    int selectedSpell; float cooldownScale=1;
    public int DormantCount=>dormant.Count;
    public float ZoneRadius=>zoneRadius;
    public bool ExtractionCalled=>extractionCalled;
    void BuildSurvival() {
      night=Random.value<.5f;
      RenderSettings.ambientLight=night?new Color(.18f,.22f,.34f):new Color(.48f,.51f,.57f);
      RenderSettings.fog=true;RenderSettings.fogColor=night?new Color(.025f,.045f,.085f):new Color(.19f,.24f,.29f);
      RenderSettings.fogDensity=(night?.024f:.012f)/MapScale;
      sound=gameObject.AddComponent<AudioSource>();
      zoneCenter=new Vector3(Random.Range(-6f*MapScale,6f*MapScale),0,Random.Range(-6f*MapScale,6f*MapScale));
      for(int i=0;i<64*MapScale;i++){ var p=Shape("Rift perimeter",PrimitiveType.Cube,Vector3.zero,new Vector3(.16f,5,.16f),cyan,false);zoneMarkers.Add(p.transform); }
      for(int i=0;i<130;i++) { var p=new Vector3(Random.Range(-29f*MapScale,29f*MapScale),1,Random.Range(-29f*MapScale,29f*MapScale)); if(Vector3.Distance(p,controller.transform.position)<10)p.z=20;dormant.Add(new Dormant{position=p,faction=i%3,kind=i%4,health=45+(i%4)*6,target=p}); }
      for(int i=0;i<3;i++){
        var p=new Vector3(Mathf.Sin(i*2.094f)*23*MapScale,1,Mathf.Cos(i*2.094f)*23*MapScale);
        var boss=Shape(new[]{"Resonant","Ironhide","Broodmother"}[i],PrimitiveType.Capsule,p,new Vector3(2,2.5f,2),enemyMaterial);
        DressEnemy(boss,i,true);
        enemies.Add(new Enemy{obj=boss,health=450+i*100,speed=1.25f,boss=i,faction=i,target=p});
        for(int j=0;j<3;j++)Shape("Military barricade",PrimitiveType.Cube,p+new Vector3((j-1)*3,-.5f,5),new Vector3(2,1,1),stone);
      }
      for(int i=0;i<20;i++)DropLoot(new Vector3(Random.Range(-25f*MapScale,25f*MapScale),.65f,Random.Range(-25f*MapScale,25f*MapScale)),i%7,Random.Range(1,4));
      DropLoot(new Vector3(-2,.65f,-17),0,1);DropLoot(new Vector3(0,.65f,-17),1,1);DropLoot(new Vector3(2,.65f,-17),2,1);
      Shape("Landing pad",PrimitiveType.Cylinder,zoneCenter+Vector3.up*.02f,new Vector3(9,.04f,9),stone);
      Shape("Extraction beacon",PrimitiveType.Cylinder,zoneCenter+Vector3.up*.6f,new Vector3(.5f,.6f,.5f),cyan);
      helicopter=new GameObject("Rescue helicopter");helicopter.transform.SetParent(transform);helicopter.transform.position=zoneCenter+Vector3.up*30;
      var body=Shape("Helicopter fuselage",PrimitiveType.Capsule,helicopter.transform.position,new Vector3(2,1.3f,4),stone,false);body.transform.SetParent(helicopter.transform,true);
      var rotor=Shape("Rotor",PrimitiveType.Cube,helicopter.transform.position+Vector3.up*1.8f,new Vector3(10,.1f,.25f),stone,false);rotor.transform.SetParent(helicopter.transform,true);
      helicopter.SetActive(false);
      vehicle=Shape("Rover [E]",PrimitiveType.Cube,new Vector3(5,.7f,-19),new Vector3(2,1.1f,3.5f),stone,false);
      for(int i=0;i<4;i++){var wheel=Shape("Wheel",PrimitiveType.Cylinder,vehicle.transform.position+new Vector3(i%2==0?-1.1f:1.1f,-.4f,i<2?-1:1),new Vector3(.7f,.2f,.7f),stone,false);wheel.transform.rotation=Quaternion.Euler(0,0,90);wheel.transform.SetParent(vehicle.transform,true);}
      Notice("Loot nearby books with E. Survive the rift, then call extraction at the beacon.");
    }
    void DressEnemy(GameObject enemy,int faction,bool boss) {
      enemy.GetComponent<Renderer>().enabled=false;
      var body=Shape("Torn uniform",PrimitiveType.Cube,Vector3.zero,new Vector3(.7f,.9f,.42f),stone,false).transform;
      body.SetParent(enemy.transform,false);body.localPosition=new Vector3(0,.05f,0);
      var head=Shape("Infected head",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.42f,enemyMaterial,false).transform;
      head.SetParent(enemy.transform,false);head.localPosition=new Vector3(0,.72f,0);
      for(int j=0;j<2;j++){
        var arm=Shape("Reaching arm",PrimitiveType.Cube,Vector3.zero,new Vector3(.18f,.18f,.72f),enemyMaterial,false).transform;
        arm.SetParent(enemy.transform,false);arm.localPosition=new Vector3(j==0?-.44f:.44f,.25f,.28f);
        var leg=Shape("Leg",PrimitiveType.Cube,Vector3.zero,new Vector3(.23f,.7f,.25f),stone,false).transform;
        leg.SetParent(enemy.transform,false);leg.localPosition=new Vector3(j==0?-.2f:.2f,-.67f,0);
        var eye=Shape("Resonance organ",PrimitiveType.Sphere,Vector3.zero,Vector3.one*(boss?.18f:.09f),faction==0?ember:faction==1?cyan:gemMaterial,false).transform;
        eye.SetParent(enemy.transform,false);eye.localPosition=new Vector3(j==0?-.1f:.1f,.75f,.19f);
      }
    }
    void DropLoot(Vector3 position,int kind,int tier){
      var obj=Shape("Loot "+kind+" T"+tier,PrimitiveType.Cube,new Vector3(position.x,.65f,position.z),new Vector3(.55f,.15f,.7f),kind>=3&&kind<=5?gemMaterial:cyan,false);
      loot.Add(new Loot{obj=obj,kind=kind,tier=tier});
    }
    string LootName(Loot l) {
      string[] labels={"Frost nova book [Q]","Chain lightning book [F]","Meteor book [F]","Crown","Robe","Grimoire","Health potion"};
      return labels[l.kind]+" / T"+l.tier;
    }
    void TickSurvival(float dt) {
      zoneRadius=Mathf.Lerp(30*MapScale,7,Mathf.Clamp01((Elapsed-30)/Mathf.Max(1,survivalSeconds-30)));
      for(int i=0;i<zoneMarkers.Count;i++) {float a=i*Mathf.PI*2/zoneMarkers.Count;zoneMarkers[i].position=zoneCenter+new Vector3(Mathf.Sin(a)*zoneRadius,2.5f,Mathf.Cos(a)*zoneRadius);}
      if(Vector2.Distance(new Vector2(controller.transform.position.x,controller.transform.position.z),new Vector2(zoneCenter.x,zoneCenter.z))>zoneRadius)TakeDamage(6*dt);
      if(Time.time>=nextSimulation){nextSimulation=Time.time+.35f;SimulatePopulation();}
      prompt="";
      foreach(var item in loot) {item.obj.transform.Rotate(0,25*dt,0); if(Vector3.Distance(controller.transform.position,item.obj.transform.position)<2.5f){prompt="E  Pick up "+LootName(item);break;}}
      if(driving){vehicle.transform.position=controller.transform.position+Vector3.up*.3f;vehicle.transform.rotation=controller.transform.rotation; EmitNoise(controller.transform.position,22);foreach(var e in enemies.ToArray())if(Vector3.Distance(e.obj.transform.position,controller.transform.position)<2.5f)HurtEnemy(e,90*dt);prompt="E  Leave rover";}
      else if(Vector3.Distance(controller.transform.position,vehicle.transform.position)<3)prompt="E  Drive rover";
      float beaconDistance=Vector3.Distance(controller.transform.position,zoneCenter);
      if(beaconDistance<4 && !extractionCalled)prompt=Elapsed<survivalSeconds?"Beacon unlocks when the rift finishes shrinking":"E  Call helicopter";
      if(extractionCalled) {
         bool contested=false;
         foreach(var e in enemies)if(Vector3.Distance(e.obj.transform.position,zoneCenter)<13){contested=true;break;}
         if(!contested)foreach(var d in dormant)if(Vector3.Distance(d.position,zoneCenter)<18){contested=true;break;}
         if(!contested)landingProgress+=dt;else landingProgress=Mathf.Max(0,landingProgress-dt*.5f);
         helicopter.SetActive(true);
         helicopter.transform.position=zoneCenter+Vector3.up*Mathf.Lerp(25,2,landingProgress/12);
         if(helicopter.transform.childCount>1)helicopter.transform.GetChild(1).Rotate(0,1000*dt,0);
         helicopterLanded=landingProgress>=12;
         if(helicopterLanded && beaconDistance<5)prompt="E  Board helicopter and extract";
      }
    }
    void SimulatePopulation() {
      for(int i=dormant.Count-1;i>=0;i--) {
         var d=dormant[i]; var planar=d.position-zoneCenter;planar.y=0;
         if(Time.time<d.alertUntil)d.position=Vector3.MoveTowards(d.position,d.target,.85f);
         else if(planar.magnitude>zoneRadius-2 || extractionCalled)d.position=Vector3.MoveTowards(d.position,zoneCenter+Vector3.up, .85f);
         if(enemies.Count<45 && Vector3.Distance(d.position,controller.transform.position)<18){
           int kind=d.kind;
           var obj=Shape(new[]{"Infected","Runner","Listener","Screamer"}[kind],PrimitiveType.Capsule,d.position,Vector3.one*(kind==1?.65f:.85f),enemyMaterial);
           DressEnemy(obj,d.faction,false);
           enemies.Add(new Enemy{obj=obj,health=d.health,speed=kind==1?4:2.1f,faction=d.faction,kind=d.kind,target=d.target,alertUntil=d.alertUntil});dormant.RemoveAt(i);
         }
      }
      foreach(var e in enemies.ToArray()) {
         if(!enemies.Contains(e))continue;
         if(e.boss<0 && Vector3.Distance(e.obj.transform.position,controller.transform.position)>26 && Time.time>e.alertUntil){
           dormant.Add(new Dormant{position=e.obj.transform.position,faction=e.faction,kind=e.kind,health=e.health,target=e.target,alertUntil=e.alertUntil});Destroy(e.obj);enemies.Remove(e);continue;
         }
         foreach(var other in enemies.ToArray()) if(e!=other && e.faction!=other.faction && Vector3.Distance(e.obj.transform.position,other.obj.transform.position)<2.2f){HurtEnemy(other,4);break;}
      }
    }
    void EmitNoise(Vector3 position,float radius){
      foreach(var e in enemies)if(Vector3.Distance(e.obj.transform.position,position)<radius*(e.kind==2?1.6f:1)){e.target=position;e.alertUntil=Time.time+8;}
      foreach(var d in dormant)if(Vector3.Distance(d.position,position)<radius){d.target=position;d.alertUntil=Time.time+8;}
    }
    void Interact(){
      if(helicopterLanded && Vector3.Distance(controller.transform.position,zoneCenter)<5){EndRun();return;}
      if(!extractionCalled && Elapsed>=survivalSeconds && Vector3.Distance(controller.transform.position,zoneCenter)<4){extractionCalled=true;EmitNoise(zoneCenter,100);Notice("Helicopter inbound. Clear the landing zone for 12 seconds.");return;}
      if(driving){driving=false;return;}
      if(Vector3.Distance(controller.transform.position,vehicle.transform.position)<3){driving=true;return;}
      for(int i=0;i<loot.Count;i++){var item=loot[i];if(Vector3.Distance(controller.transform.position,item.obj.transform.position)>2.5f)continue;
        switch(item.kind){case 0:frostBook=true;break;case 1:lightningBook=true;selectedSpell=0;break;case 2:meteorBook=true;selectedSpell=1;break;
        case 3:damage+=Mathf.Max(0,item.tier-crownTier)*2;crownTier=Mathf.Max(crownTier,item.tier);break;
        case 4:armorTier=Mathf.Max(armorTier,item.tier);if(item.tier==4)deathWard=true;break;
        case 5:if(item.tier>tomeTier){interval=Mathf.Max(.08f,interval*Mathf.Pow(.9f,item.tier-tomeTier));tomeTier=item.tier;}break;case 6:potions++;break;}
        Notice("Acquired "+LootName(item));Destroy(item.obj);loot.RemoveAt(i);return;
      }
    }
    void Melee(){if(Time.time<meleeReady)return;meleeReady=Time.time+.7f;EmitNoise(controller.transform.position,3);
      foreach(var e in enemies.ToArray()){var delta=e.obj.transform.position-view.transform.position;if(delta.magnitude<3 && Vector3.Dot(view.transform.forward,delta.normalized)>.25f)HurtEnemy(e,55);}
      PlayTone(100,.1f,.12f);
    }
    void CastSpecial(){
      if(Time.time<specialReady)return;
      if(meteorBook && (selectedSpell==1 || !lightningBook)){
        specialReady=Time.time+12*cooldownScale;var point=view.transform.position+view.transform.forward*15;
        if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,40))point=hit.point;
        point.y=1;EmitNoise(point,70);
        foreach(var e in enemies.ToArray())if(Vector3.Distance(e.obj.transform.position,point)<8)HurtEnemy(e,140+damage);
        var burst=Shape("Meteor blast",PrimitiveType.Sphere,point,Vector3.one*5,ember,false);Destroy(burst,.25f);PlayTone(65,.3f,.2f);
      } else if(lightningBook){
        specialReady=Time.time+4*cooldownScale;Vector3 origin=view.transform.position;int count=0;
        foreach(var e in enemies.ToArray())if(Vector3.Distance(e.obj.transform.position,origin)<16 && count++<5){
          var lineObj=new GameObject("Chain lightning");lineObj.transform.SetParent(transform);
          var line=lineObj.AddComponent<LineRenderer>();line.sharedMaterial=cyan;line.startWidth=.08f;line.endWidth=.025f;line.positionCount=2;line.SetPosition(0,origin);line.SetPosition(1,e.obj.transform.position);
          origin=e.obj.transform.position;HurtEnemy(e,damage*1.8f);Destroy(lineObj,.18f);
        }EmitNoise(controller.transform.position,30);PlayTone(850,.15f,.12f);
      }
    }
    void PlayTone(float frequency,float seconds,float volume){
      int count=(int)(22050*seconds);var data=new float[count];
      for(int i=0;i<count;i++)data[i]=Mathf.Sin(i*frequency*Mathf.PI*2/22050)*Mathf.Pow(1-i/(float)count,2);
      var clip=AudioClip.Create("Arcane pulse",count,1,22050,false);clip.SetData(data,0);sound.PlayOneShot(clip,volume);Destroy(clip,seconds+1);
    }
    void Notice(string value){notice=value;noticeUntil=Time.time+7;}
    void DrawSurvivalHUD(){
      GUI.Label(new Rect(410,24,540,35),"RIFT "+Mathf.CeilToInt(zoneRadius)+"m  |  INFECTED "+(enemies.Count+dormant.Count)+"  |  "+(night?"NIGHT":"DAY"),smallStyle);
      GUI.Label(new Rect(405,62,540,35),extractionCalled?"LANDING "+Mathf.FloorToInt(landingProgress)+"/12s — keep zone clear":Elapsed>=survivalSeconds?"GO TO THE CENTRAL BEACON":"Loot, grow stronger, follow the shrinking rift",smallStyle);
      GUI.Label(new Rect(380,575,800,35),prompt,textStyle);
      if(Time.time<noticeUntil)GUI.Label(new Rect(300,530,900,35),notice,smallStyle);
      GUI.Label(new Rect(25,603,1100,30),"E interact   •   RMB magic blade   •   F "+((meteorBook && (selectedSpell==1 || !lightningBook))?"Meteor":lightningBook?"Chain lightning":"Find spellbook")+"   •   H heal ("+potions+")   •   Ctrl sneak   •   Tab gear   •   X switch spell",smallStyle);
      if(inventoryOpen){GUI.Box(new Rect(920,150,340,270),"EQUIPMENT");GUI.Label(new Rect(940,185,300,225),"Crown T"+crownTier+"\nRobe T"+armorTier+"\nGrimoire T"+tomeTier+"\nFrost "+frostBook+"\nLightning "+lightningBook+"\nMeteor "+meteorBook+"\nAugment at levels 5, 10, 15...",smallStyle);}
    }
 }
}