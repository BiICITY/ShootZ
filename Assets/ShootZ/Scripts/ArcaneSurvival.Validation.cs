#if UNITY_EDITOR
using UnityEngine;
namespace ShootZ {
 public partial class ArcaneSurvival {
  public string ValidateCoreRules() {
   var report=new System.Text.StringBuilder();
   System.Action<bool,string> check=(ok,label)=>{if(!ok)throw new System.Exception("FAIL: "+label);report.AppendLine("PASS: "+label);};
   check(EnemyCount<=45 && DormantCount>0,"bounded active population");
   controller.enabled=false;controller.transform.position=new Vector3(-2,.1f,-17);controller.enabled=true;
   Interact();check(frostBook,"ground loot unlocks frost spell");
   Experience=80;TickGems(0);check(Level==5 && Choosing && Time.timeScale==0,"5-level augment cadence and pause");
   SelectUpgrade(0);check(!Choosing && Time.timeScale==1,"augment resumes combat");
   var before=Health;TakeDamage(10);check(Health<before,"player damage");
   Cast();check(bolts.Count>0,"firebolt creation");
   var enemy=enemies[0];int count=Kills;HurtEnemy(enemy,10000);check(Kills==count+1 && gems.Count>0,"kill and experience drop");
   controller.enabled=false;controller.transform.position=zoneCenter+Vector3.up*.1f;controller.enabled=true;
   Elapsed=survivalSeconds;Interact();check(extractionCalled,"beacon calls helicopter");
   foreach(var e in enemies)e.obj.transform.position=zoneCenter+Vector3.up;
   var start=landingProgress;TickSurvival(1);check(landingProgress<=start,"contested zone blocks landing");
   foreach(var e in enemies)Destroy(e.obj);enemies.Clear();dormant.Clear();
   TickSurvival(12.1f);check(helicopterLanded,"clear zone permits landing");
   Interact();check(Finished && Health>0,"boarding wins");
   Finished=false;Time.timeScale=1;deathWard=false;TakeDamage(10000);check(Finished && Health==0,"death ends run");
   return report.ToString();
  }
 }
}
#endif