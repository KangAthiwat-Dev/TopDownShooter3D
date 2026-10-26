#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public sealed class WindingMapRegressionDriver:MonoBehaviour
{
    private readonly StringBuilder report=new StringBuilder();
    private PlayerMovement movement;private PlayerInput input;private CharacterController controller;private Keyboard keyboard;private ReadOnlyArray<InputDevice>? oldDevices;
    private bool passed=true,finished,oldBackground;private int oldFps,oldVsync;private float oldScale;private string runtimeError;private GameObject testEnemy;
    private IEnumerator Start()
    {
        oldBackground=Application.runInBackground;oldFps=Application.targetFrameRate;oldVsync=QualitySettings.vSyncCount;oldScale=Time.timeScale;
        Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.timeScale=1;Application.logMessageReceived+=OnLog;
        report.AppendLine("Real full-map Play Mode regression "+DateTime.Now.ToString("s"));
        var stack=new Stack<IEnumerator>();stack.Push(Checks());
        while(stack.Count>0){object next=null;Exception failure=null;try{if(!stack.Peek().MoveNext())stack.Pop();else next=stack.Peek().Current;}catch(Exception e){failure=e;}if(failure!=null){Finish(failure);yield break;}if(next is IEnumerator nested)stack.Push(nested);else if(next!=null||stack.Count>0)yield return next;}
        Finish(null);
    }
    private IEnumerator Checks()
    {
        var map=FindAnyObjectByType<ForestMapLayout>();movement=FindAnyObjectByType<PlayerMovement>();input=movement.GetComponent<PlayerInput>();controller=movement.GetComponent<CharacterController>();
        var spawner=FindAnyObjectByType<EnemyWaveSpawner>();spawner.StopAllCoroutines();spawner.enabled=false;foreach(var ai in FindObjectsByType<EnemyAI>())ai.gameObject.SetActive(false);
        keyboard=InputSystem.AddDevice<Keyboard>("FullMapTestKeyboard");var devices=input.actions.devices;if(devices.HasValue)oldDevices=new ReadOnlyArray<InputDevice>(devices.Value.ToArray());input.actions.devices=new InputDevice[]{keyboard};
        yield return Wait(0.4f);Check(map!=null&&FindObjectsByType<PlayerMovement>().Length==1,"One player and full map loaded; no duplicate gameplay setup");
        var original=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/World/ForestDemo/ForestTerrain.asset");var current=map.terrain.terrainData;
        var before=original.GetHeights(0,0,original.heightmapResolution,original.heightmapResolution);var after=current.GetHeights(0,0,current.heightmapResolution,current.heightmapResolution);bool preserved=true;int samples=0;
        for(int z=0;z<current.heightmapResolution;z++)for(int x=0;x<current.heightmapResolution;x++)
        {
            Vector3 p=map.terrain.transform.position+new Vector3((float)x/(current.heightmapResolution-1)*current.size.x,0,(float)z/(current.heightmapResolution-1)*current.size.z);
            if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(-60,-60))>30&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(-82.63f,-82.18f))>19)continue;
            preserved&=Mathf.Abs(before[z,x]-after[z,x])<0.000001f;samples++;
        }
        Check(preserved&&samples>1000,"Original sanctuary/forest sculpt preserved inside the protected area ("+samples+" samples)");
        var oldPaint=original.GetAlphamaps(0,0,original.alphamapWidth,original.alphamapHeight);var newPaint=current.GetAlphamaps(0,0,current.alphamapWidth,current.alphamapHeight);bool samePaint=true;
        for(int z=0;z<current.alphamapHeight;z++)for(int x=0;x<current.alphamapWidth;x++)
        {
            Vector2 p=new Vector2(map.terrain.transform.position.x+(float)x/(current.alphamapWidth-1)*current.size.x,map.terrain.transform.position.z+(float)z/(current.alphamapHeight-1)*current.size.z);
            if(!Protected(p))continue;for(int l=0;l<current.alphamapLayers;l++)samePaint&=Mathf.Abs(oldPaint[z,x,l]-newPaint[z,x,l])<0.000001f;
        }
        Check(samePaint,"Original terrain painting preserved inside the protected start area");
        bool sameGrass=true;for(int l=0;l<current.detailPrototypes.Length;l++)
        {
            int[,] a=original.GetDetailLayer(0,0,original.detailWidth,original.detailHeight,l),b=current.GetDetailLayer(0,0,current.detailWidth,current.detailHeight,l);
            for(int z=0;z<current.detailHeight;z++)for(int x=0;x<current.detailWidth;x++)
            {
                Vector2 p=new Vector2(map.terrain.transform.position.x+(x+0.5f)/current.detailWidth*current.size.x,map.terrain.transform.position.z+(z+0.5f)/current.detailHeight*current.size.z);
                if(Protected(p))sameGrass&=a[z,x]==b[z,x];
            }
        }
        Check(sameGrass,"Original grass-brush edits preserved inside the protected start area");
        foreach(Transform arrival in map.arrivals)
        {
            Check(NavMesh.SamplePosition(arrival.position,out NavMeshHit point,3,NavMesh.AllAreas),arrival.name+" has walkable navigation");Warp(point.position+Vector3.up*0.08f);yield return Wait(0.2f);
            Vector3 start=movement.transform.position;Keys(Key.D);yield return Wait(0.45f);Keys();yield return Wait(0.15f);
            Check(Vector3.Distance(start,movement.transform.position)>0.8f&&Grounded(),"Actual Player input walks and stays grounded at "+arrival.name);
            yield return Capture(arrival.name);
        }
        // A real Bear follows the new map's own navigation, not an isolated test floor.
        NavMesh.SamplePosition(map.camp.position,out NavMeshHit camp,4,NavMesh.AllAreas);Warp(camp.position+Vector3.up*0.08f);
        NavMesh.SamplePosition(camp.position+Vector3.forward*9,out NavMeshHit enemyPoint,4,NavMesh.AllAreas);
        testEnemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy_Bear.prefab"),enemyPoint.position,Quaternion.LookRotation(movement.transform.position-enemyPoint.position));
        float distance=Vector3.Distance(testEnemy.transform.position,movement.transform.position);yield return Wait(1.1f);var agent=testEnemy.GetComponent<NavMeshAgent>();
        Check(agent.isOnNavMesh&&agent.hasPath&&agent.pathStatus==NavMeshPathStatus.PathComplete&&Vector3.Distance(testEnemy.transform.position,movement.transform.position)<distance-1,"Actual Bear chases on the finished forest NavMesh");Destroy(testEnemy);yield return null;
        // Test all continuous perimeter colliders with the real CharacterController, including PlayerShadow layer.
        movement.enabled=false;
        foreach(BoxCollider wall in map.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.EndsWith("SafetyWall")))
        {
            Vector3 inward=wall.name.StartsWith("West")?Vector3.right:wall.name.StartsWith("East")?Vector3.left:wall.name.StartsWith("South")?Vector3.forward:Vector3.back;
            Vector3 surface=wall.bounds.center;surface.y=12;Vector3 start=surface+inward*3;int originalLayer=movement.gameObject.layer;
            foreach(int layer in new[]{originalLayer,LayerMask.NameToLayer("PlayerShadow")})
            {
                if(layer<0)continue;Warp(start);movement.gameObject.layer=layer;Physics.SyncTransforms();
                for(int i=0;i<20;i++){controller.Move(-inward*0.8f);yield return null;}
                Check(Vector3.Dot(movement.transform.position-surface,inward)>0.4f,wall.name+" contains actual player on layer "+LayerMask.LayerToName(layer));
            }
            movement.gameObject.layer=originalLayer;
        }
        movement.enabled=true;FindAnyObjectByType<PlayerSceneSpawnPoint>().PlacePlayer();yield return Wait(0.2f);
        var panel=FindAnyObjectByType<InventoryPanelUI>();Check(panel.TryOpen(),"Inventory opens on the completed map");Vector3 paused=movement.transform.position;yield return Wait(0.2f);Check(Time.timeScale==0&&movement.transform.position==paused,"Inventory still pauses the player/world");panel.Close();yield return Wait(0.1f);
        Check(runtimeError==null,"No runtime errors in map travel, enemy navigation, containment or pause");
    }
    private void Warp(Vector3 p){controller.enabled=false;movement.transform.position=p;controller.enabled=true;Physics.SyncTransforms();}
    private bool Grounded()=>Physics.Raycast(movement.transform.position+Vector3.up,Vector3.down,out RaycastHit hit,3,1<<7,QueryTriggerInteraction.Ignore)&&Mathf.Abs(movement.transform.position.y-hit.point.y)<0.3f;
    private static bool Protected(Vector2 p)=>Vector2.Distance(p,new Vector2(-60,-60))<30||Vector2.Distance(p,new Vector2(-82.63f,-82.18f))<19;
    private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    private static IEnumerator Wait(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
    private static IEnumerator Capture(string label){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/WindingMap_"+label+".png");yield return null;}
    private void Check(bool ok,string text){passed&=ok;report.AppendLine((ok?"PASS ":"FAIL ")+text);}
    private void OnLog(string condition,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=condition;}
    private void Finish(Exception failure)
    {
        if(finished)return;finished=true;if(failure!=null){passed=false;report.AppendLine("EXCEPTION "+failure);}if(runtimeError!=null){passed=false;report.AppendLine("RUNTIME ERROR "+runtimeError);}
        if(input!=null)input.actions.devices=oldDevices;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(testEnemy!=null)Destroy(testEnemy);
        Application.logMessageReceived-=OnLog;Application.runInBackground=oldBackground;Application.targetFrameRate=oldFps;QualitySettings.vSyncCount=oldVsync;Time.timeScale=oldScale;
        report.AppendLine(passed?"ALL CHECKS PASSED":"CHECKS FAILED");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WindingMapPlaytest.txt",report.ToString());Debug.Log(report.ToString());EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    private void OnDestroy(){if(!finished)Finish(new InvalidOperationException("Map playtest interrupted"));}
}
#endif
