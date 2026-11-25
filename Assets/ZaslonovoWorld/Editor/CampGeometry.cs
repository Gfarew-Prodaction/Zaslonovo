using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Zaslonovo.Editor
{
    internal sealed class CampMesh
    {
        readonly List<Vector3> v = new(); readonly List<Vector2> uv = new(); readonly List<int> triangles = new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect? atlas=null)
        {
            int n=v.Count;v.AddRange(new[]{a,b,c,d});var r=atlas??new Rect(0,0,1,1);
            uv.AddRange(new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)});
            triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        public void Append(Mesh source){int start=v.Count;v.AddRange(source.vertices);uv.AddRange(source.uv);triangles.AddRange(source.triangles.Select(i=>i+start));}
        public void Triangle(Vector3 a,Vector3 b,Vector3 c)
        {int n=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up});triangles.AddRange(new[]{n,n+1,n+2});}
        public void Box(Vector3 c,Vector3 s,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;var p=new Vector3[8];
            for(int i=0;i<8;i++)p[i]=c+q*Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),s);
            foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3}})Quad(p[f[0]],p[f[1]],p[f[2]],p[f[3]]);
        }
        public void Tube(Vector3 a,Vector3 b,float ra,float rb,int sides=10,float uvScale=1)
        {
            Vector3 axis=(b-a).normalized;var u=Vector3.Cross(axis,Vector3.forward).normalized;if(u.sqrMagnitude<.1f)u=Vector3.Cross(axis,Vector3.right).normalized;var w=Vector3.Cross(axis,u);
            for(int i=0;i<sides;i++)
            {
                float t=i*2*Mathf.PI/sides,tt=(i+1)*2*Mathf.PI/sides;
                Vector3 r=u*Mathf.Cos(t)+w*Mathf.Sin(t),s=u*Mathf.Cos(tt)+w*Mathf.Sin(tt);
                Quad(a+r*ra,a+s*ra,b+s*rb,b+r*rb,new Rect(i/(float)sides,0,1f/sides,(b-a).magnitude*uvScale));
            }
        }
        public void Sphere(Vector3 center,float radius,int segments=12,int rings=8)
        {
            Vector3 P(int i,int j){float a=i*2*Mathf.PI/segments,b=j*Mathf.PI/rings;return center+new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*radius;}
            for(int j=0;j<rings;j++)for(int i=0;i<segments;i++)Quad(P(i,j),P(i+1,j),P(i+1,j+1),P(i,j+1));
        }
        public Mesh Finish(string name,bool smooth=false)
        {
            var mesh=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
    internal static class CampGeometry
    {
        public const string Generated="Assets/ZaslonovoWorld/Generated";
        public static T Save<T>(T obj,string path) where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(obj,path);return obj;}
            EditorUtility.CopySerialized(obj,old);Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);return old;
        }
        public static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material mat,bool collision=false)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        public static Mesh AssetMesh(string name,CampMesh mesh)=>Save(mesh.Finish(name),Generated+"/Meshes/"+name+".asset");
        static float F(System.Random r,float a,float b)=>(float)(a+r.NextDouble()*(b-a));
        public static Mesh[] Pine(int seed,int level)
        {
            var r=new System.Random(seed);var bark=new CampMesh();var foliage=new CampMesh();float height=F(r,20,24),radius=F(r,.23f,.34f);
            Vector3 Trunk(float t)=>new Vector3(Mathf.Sin(t*3+seed)*.35f*t,height*t,Mathf.Sin(t*4+seed*2)*.3f*t);
            int trunkSteps=level==0?24:level==1?16:9,sides=level==0?14:level==1?10:7;
            for(int j=0;j<trunkSteps;j++){float a=j/(float)trunkSteps,b=(j+1f)/trunkSteps;bark.Tube(Trunk(a),Trunk(b),radius*Mathf.Pow(1-a,.67f)+.016f,radius*Mathf.Pow(1-b,.67f)+.016f,sides,1.2f);}
            int branches=level==0?38:level==1?25:12;
            for(int branch=0;branch<branches;branch++)
            {
                float t=(branch+.3f)/branches,y=.45f+.54f*t,angle=branch*2.39996f+seed;
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(radial,Vector3.up);
                float length=(1.4f+Mathf.Pow(Mathf.Sin(t*Mathf.PI),.7f)*4.4f)*F(r,.7f,1.12f);
                Vector3 a=Trunk(y),b=a+radial*length+Vector3.up*F(r,.1f,1.4f),middle=Vector3.Lerp(a,b,.55f)-Vector3.up*.22f;
                bark.Tube(a,middle,F(r,.045f,.09f),.024f,level==2?5:7);bark.Tube(middle,b,.024f,.006f,level==2?5:6);
                int twigs=level==0?20:level==1?11:5;
                for(int k=0;k<twigs;k++)
                {
                    float u=F(r,.28f,1.02f);float spread=F(r,-1,1)*(1-u*.5f)*length*.27f;
                    Vector3 origin=Vector3.Lerp(a,b,u)+side*spread+Vector3.up*F(r,-.2f,.6f);
                    Vector3 dir=(radial*F(r,.4f,1.1f)+side*F(r,-.8f,.8f)+Vector3.up*F(r,.05f,.9f)).normalized;
                    float len=F(r,.65f,1.25f),width=len*F(r,.44f,.63f);
                    Vector3 across=Vector3.Cross(dir,(k%2==0?Vector3.up:side)).normalized*width*.5f;
                    if(across.sqrMagnitude<.001f)across=side*width*.5f;
                    foliage.Quad(origin-across,origin+across,origin+dir*len+across,origin+dir*len-across,new Rect(.012f,.559f,.22f,.44f));
                }
            }
            // Dead lower branches preserve the long, clean trunks seen in the camp photographs.
            if(level<2)for(int i=0;i<9;i++){float t=F(r,.23f,.46f),a=F(r,0,Mathf.PI*2);var p=Trunk(t);bark.Tube(p,p+new Vector3(Mathf.Cos(a),-.32f,Mathf.Sin(a))*F(r,.25f,.65f),.028f,.004f,6);}
            return new[]{AssetMesh("Pine_"+seed+"_"+level+"_Bark",bark),AssetMesh("Pine_"+seed+"_"+level+"_Foliage",foliage)};
        }
        public static void GardenLeaves(CampMesh m,Vector3 a,Vector3 b,int seed)
        {
            for(int i=0;i<6;i++)
            {
                float angle=i*2.39996f+seed*.61f;var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var across=new Vector3(-dir.z,0,dir.x)*.035f;
                float length=.12f+.04f*Mathf.Sin(seed+i);var origin=Vector3.Lerp(a,b,.25f+i*.08f);var middle=origin+dir*length*.55f+Vector3.up*.035f;
                m.Quad(origin,middle+across,origin+dir*length+Vector3.up*.015f,middle-across);
            }
        }
        public static Mesh BenchWood()
        {
            var m=new CampMesh();for(int i=0;i<5;i++)m.Box(new Vector3(0,.45f,-.22f+i*.10f),new Vector3(1.8f,.055f,.085f));
            for(int i=0;i<4;i++)m.Box(new Vector3(0,.61f+i*.105f,.235f+i*.012f),new Vector3(1.8f,.085f,.045f),Quaternion.Euler(7,0,0));return AssetMesh("BenchWood",m);
        }
        public static Mesh BenchMetal()
        {
            var m=new CampMesh();foreach(float x in new[]{-.66f,.66f})
            {foreach(float z in new[]{-.16f,.17f})m.Tube(new Vector3(x,.015f,z),new Vector3(x,.45f,z),.027f,.027f,8);m.Tube(new Vector3(x,.35f,.19f),new Vector3(x,.98f,.28f),.028f,.024f,8);m.Box(new Vector3(x,.40f,0),new Vector3(.065f,.07f,.5f));m.Tube(new Vector3(x,.68f,-.16f),new Vector3(x,.68f,.26f),.025f,.025f,8);}
            return AssetMesh("BenchMetal",m);
        }
        public static Mesh LampPole()
        {
            var m=new CampMesh();m.Tube(Vector3.zero,Vector3.up*.25f,.13f,.09f,12);m.Tube(Vector3.up*.2f,Vector3.up*3.5f,.055f,.035f,12);m.Tube(Vector3.up*3.47f,Vector3.up*3.60f,.11f,.07f,12);return AssetMesh("LampPole",m);
        }
        public static Mesh LampGlobe(){var m=new CampMesh();m.Sphere(Vector3.up*3.78f,.21f,16,12);return AssetMesh("LampGlobe",m);}
        public static Mesh[] Fountain()
        {
            var stone=new CampMesh();var water=new CampMesh();int seg=64;
            void Lathe(CampMesh m,Vector2[] profile)
            {
                for(int j=0;j<profile.Length-1;j++)for(int i=0;i<seg;i++)
                {Vector3 P(int n,Vector2 rz)=>new Vector3(Mathf.Cos(n*2*Mathf.PI/seg)*rz.x,rz.y,Mathf.Sin(n*2*Mathf.PI/seg)*rz.x);m.Quad(P(i,profile[j]),P(i,profile[j+1]),P(i+1,profile[j+1]),P(i+1,profile[j]));}
            }
            Lathe(stone,new[]{new Vector2(2.65f,.02f),new Vector2(2.68f,.22f),new Vector2(2.63f,.52f),new Vector2(2.49f,.56f),new Vector2(2.40f,.42f),new Vector2(2.40f,.15f)});
            Lathe(stone,new[]{new Vector2(.65f,.2f),new Vector2(.45f,.5f),new Vector2(.29f,1.0f),new Vector2(.36f,1.45f),new Vector2(1.40f,1.6f),new Vector2(1.44f,1.72f),new Vector2(1.32f,1.77f),new Vector2(.28f,1.52f),new Vector2(.18f,2.25f),new Vector2(.84f,2.42f),new Vector2(.88f,2.52f),new Vector2(.73f,2.57f),new Vector2(.14f,2.40f),new Vector2(.10f,2.96f)});
            for(int i=0;i<seg;i++){float a=i*2*Mathf.PI/seg,b=(i+1)*2*Mathf.PI/seg;water.Triangle(new Vector3(0,.41f,0),new Vector3(Mathf.Cos(b)*2.4f,.41f,Mathf.Sin(b)*2.4f),new Vector3(Mathf.Cos(a)*2.4f,.41f,Mathf.Sin(a)*2.4f));}
            return new[]{AssetMesh("FountainStone",stone),AssetMesh("FountainWater",water)};
        }
    }
}
