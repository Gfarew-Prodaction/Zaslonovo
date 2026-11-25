using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Zaslonovo.Editor
{
    [InitializeOnLoad]
    public static class CampPlayVerification
    {
        const string Pending="Zaslonovo.PlayVerification.Pending";
        static bool completed;
        static CampPlayVerification(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            EditorSceneManager.OpenScene(CampSceneBuilder.ScenePath);
            SessionState.SetBool(Pending,true);SessionState.SetFloat(Pending+"Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(completed||!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Pending+"Start",0)>120){Finish(false,"Play-mode timeout");return;}
            if(!EditorApplication.isPlaying)return;
            if(Time.frameCount<90)return;
            completed=true;
            var report=new List<string>();
            try
            {
                var player=Object.FindFirstObjectByType<CampFirstPersonController>();Check(player!=null,"Player starts in Play mode");
                Check(player.enabled&&player.viewCamera!=null&&player.viewCamera.enabled,"Controller and camera active");
                player.enabled=false;player.SetCapture(false);
                var controller=player.GetComponent<CharacterController>();
                void Step(Vector2 move,bool run=false,bool jump=false,int frames=60)
                {for(int i=0;i<frames;i++){Physics.SyncTransforms();player.SimulateFrame(move,Vector2.zero,run,jump&&i==0,1f/60);}}
                player.Respawn();Step(Vector2.zero,frames:90);
                var resting=player.transform.position;
                Check(player.Grounded,"Player settles on a walkable surface");report.Add("PASS: spawn rests on the terrain/path; gravity is active");
                Step(Vector2.up);float walking=Vector3.Distance(resting,player.transform.position);
                Check(walking>3.7f&&walking<4.6f,"WASD walk distance "+walking);report.Add("PASS: walking moves "+walking.ToString("F2")+" m/s");
                player.Respawn();Step(Vector2.zero,frames:60);resting=player.transform.position;Step(Vector2.up,true);float sprint=Vector3.Distance(resting,player.transform.position);
                Check(sprint>walking*1.5f,"Sprint increases speed");report.Add("PASS: sprint moves "+sprint.ToString("F2")+" m/s");
                player.Respawn();Step(Vector2.zero,frames:60);resting=player.transform.position;Step(Vector2.one);float diagonal=Vector3.Distance(resting,player.transform.position);
                Check(Mathf.Abs(diagonal-walking)<.3f,"Diagonal speed normalized");report.Add("PASS: diagonal movement does not increase speed");
                player.Respawn();Step(Vector2.zero,frames:60);resting=player.transform.position;
                float maxY=resting.y;for(int i=0;i<120;i++){Step(Vector2.zero,false,i==0,1);maxY=Mathf.Max(maxY,player.transform.position.y);}
                Check(maxY-resting.y>.65f&&maxY-resting.y<1.15f,"Jump height");Check(player.Grounded&&Mathf.Abs(player.transform.position.y-resting.y)<.10f,"Jump lands on ground");report.Add("PASS: jump rises and lands; height "+(maxY-resting.y).ToString("F2")+" m");
                player.Respawn();Step(Vector2.zero,frames:60);resting=player.transform.position;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary controller test wall";wall.transform.SetPositionAndRotation(resting+player.transform.forward*3+Vector3.up*1.5f,player.transform.rotation);wall.transform.localScale=new Vector3(4,3,.3f);Physics.SyncTransforms();
                try {Step(Vector2.up,true,frames:120);float moved=Vector3.Dot(player.transform.position-resting,wall.transform.forward);Check(moved>2&&moved<2.7f,"Walls block the player: "+moved);report.Add("PASS: sprinting into a wall stops the capsule");}
                finally {Object.DestroyImmediate(wall);}
                player.Respawn();player.SimulateFrame(Vector2.zero,new Vector2(500,3000),false,false,1f/60);
                Check(Mathf.Abs(player.Pitch+85)<.1f,"Vertical look lower clamp");Check(Quaternion.Angle(player.transform.rotation,Quaternion.Euler(0,player.spawnYaw,0))>20,"Mouse rotates player yaw");
                player.SimulateFrame(Vector2.zero,new Vector2(0,-6000),false,false,1f/60);Check(Mathf.Abs(player.Pitch-85)<.1f,"Vertical look upper clamp");report.Add("PASS: mouse turns the camera; pitch remains within +/-85 degrees");
                player.SetCapture(false);Check(Cursor.lockState==CursorLockMode.None,"Escape-style cursor release");
                player.transform.position=Vector3.down*30;player.Respawn();Check(Vector3.Distance(player.transform.position,player.spawnPosition)<.01f,"Respawn position");report.Add("PASS: cursor release and respawn");
                CampSceneBuilder.RenderPreviews();File.WriteAllLines("Library/ZaslonovoPlayVerification.txt",report);Finish(true,string.Join("\n",report));
            }
            catch(Exception exception){Finish(false,exception.ToString());}
        }
        static void Finish(bool success,string result)
        {
            completed=true;SessionState.SetBool(Pending,false);File.WriteAllText("Library/ZaslonovoPlayResult.txt",(success?"SUCCESS\n":"FAILED\n")+result);
            if(success)Debug.Log("ZASLONOVO PLAY VERIFIED\n"+result);else Debug.LogError(result);
            EditorApplication.isPlaying=false;
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
