using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Zaslonovo.Editor
{
    public static class CampSceneBuilder
    {
        public const string ScenePath="Assets/Scenes/Zaslonovo_Camp.unity";
        const string Root="Assets/ZaslonovoWorld";
        const string Gen=Root+"/Generated";
        [Serializable] public class Point {public float x,z; public Vector3 V=>new Vector3(x,0,z);}
        [Serializable] public class Feature {public string id;public Point[] points;}
        [Serializable] public class Geometry {public Feature[] features;}
        sealed class Building {public string name,path,osm;public Vector3 entrance;public GameObject obj;public Bounds bounds;}
        sealed class PathDef {public string name;public Vector3[] points;public float width;}
        static readonly List<Building> buildings=new();
        static readonly List<PathDef> paths=new();
        static readonly List<Vector3> treePositions=new();
        static Transform world,landscape,architecture,vegetation,furniture;
        static Material paving,concrete,bark,foliage,metal,wood,lamp,white,water,grass,worldText;
        static Terrain terrain;
        static readonly System.Random random=new(2305);
        static float F(float a,float b)=>(float)(a+random.NextDouble()*(b-a));
        static Vector3 V(float x,float z)=>new(x,0,z);
        static Mesh MeshSave(string name,CampMesh mesh)=>CampGeometry.AssetMesh(name,mesh);

        public static void FinishScenery()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var mat=CampGeometry.Save(new Material(Shader.Find("Zaslonovo/World Text")){name="Надписи URP"},Gen+"/Materials/WorldText.mat");
            foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {label.GetComponent<MeshRenderer>().sharedMaterial=mat;if(label.GetComponent<CampWorldLabel>()==null)label.gameObject.AddComponent<CampWorldLabel>();}
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(Gen+"/Meshes/FlowerStems.asset");
            if(source.vertexCount==10240)
            {
                var points=source.vertices;var mesh=new CampMesh();mesh.Append(source);
                for(int j=0;j<640;j++)
                {int n=j*16;var a=(points[n]+points[n+4]+points[n+8]+points[n+12])/4;var b=(points[n+3]+points[n+7]+points[n+11]+points[n+15])/4;CampGeometry.GardenLeaves(mesh,a,b,j);}
                MeshSave("FlowerStems",mesh);
            }
            var stone=AssetDatabase.LoadAssetAtPath<Mesh>(Gen+"/Meshes/FountainStone.asset");
            if(Vector3.Dot(new Vector3(stone.vertices[0].x,0,stone.vertices[0].z),stone.normals[0])<0)
            {var indices=stone.triangles;for(int j=0;j<indices.Length;j+=3)(indices[j],indices[j+2])=(indices[j+2],indices[j]);stone.triangles=indices;stone.RecalculateNormals();stone.RecalculateTangents();EditorUtility.SetDirty(stone);}
            foreach(var path in Directory.GetFiles(Gen+"/Meshes","Curbs_*.asset"))
            {
                var curb=AssetDatabase.LoadAssetAtPath<Mesh>(path);var vv=curb.vertices;var uu=curb.uv;var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
                for(int j=0;j<vv.Length;j+=24)
                {
                    var center=Vector3.zero;for(int k=0;k<24;k++)center+=vv[j+k]/24;
                    if(Vector3.Distance(center,V(-42,22))<7.3f)continue;
                    int n=vertices.Count;for(int k=0;k<24;k++){vertices.Add(vv[j+k]);uv.Add(uu[j+k]);}
                    for(int face=0;face<6;face++){int a=n+face*4;indices.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});}
                }
                curb.Clear();curb.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;curb.SetVertices(vertices);curb.SetUVs(0,uv);curb.SetTriangles(indices,0);curb.RecalculateNormals();curb.RecalculateTangents();curb.RecalculateBounds();EditorUtility.SetDirty(curb);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);Validate();RenderPreviews();
        }
        public static void FinalizeAndVerify()
        {
            EditorSceneManager.OpenScene(ScenePath);
            RenderSettings.skybox.SetColor("_SkyTint",new Color(.5f,.5f,.5f));EditorUtility.SetDirty(RenderSettings.skybox);AssetDatabase.SaveAssets();
            Validate();CampPlayVerification.Run();
        }
        public static void BuildAndPreview(){Build();RenderPreviews();}
        [MenuItem("Tools/Zaslonovo/Build camp scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Gen+"/Meshes");Directory.CreateDirectory(Gen+"/Materials");Directory.CreateDirectory(Gen+"/Terrain");Directory.CreateDirectory("Assets/Scenes");AssetDatabase.Refresh();
            PrepareTextures();ApplyUrp();Materials();
            var open=SceneManager.GetSceneByPath(ScenePath);
            if(open.IsValid())
            {
                if(open.isDirty&&!Application.isBatchMode)throw new InvalidOperationException("Save the camp scene before rebuilding it.");
                EditorSceneManager.CloseScene(open,true);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode||!SceneManager.GetActiveScene().isDirty?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            world=new GameObject("Заслоново — территория").transform;
            landscape=Group("01 • Рельеф и дорожки");architecture=Group("02 • Корпуса — URP");vegetation=Group("03 • Сосновый бор");furniture=Group("04 • Благоустройство");
            buildings.Clear();paths.Clear();treePositions.Clear();
            Lighting();Ground();PlaceBuildings();PlanPaths();BuildPaths();Decorate();PlantForest();PlantFlowers();Player();
            var view=SceneView.lastActiveSceneView;
            if(view!=null){view.pivot=V(-15,75)+Vector3.up*12;view.rotation=Quaternion.Euler(38,-24,0);view.size=155;view.Repaint();}
            EditorSceneManager.SaveScene(scene,ScenePath);
            var other=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToArray();EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(other).ToArray();
            AssetDatabase.SaveAssets();Validate();
            File.WriteAllText("Library/ZaslonovoWorldBuild.txt","SUCCESS\n"+ScenePath+"\nBuildings: "+buildings.Count+"\nTrees: "+treePositions.Count+"\nPaths: "+paths.Count);
            Debug.Log("ZASLONOVO CAMP BUILT: "+treePositions.Count+" pines, "+paths.Count+" paths, five URP buildings and first-person player");
        }
        static Transform Group(string name){var t=new GameObject(name).transform;t.SetParent(world,false);return t;}
        static void PrepareTextures()
        {
            foreach(var path in Directory.GetFiles(Root+"/Textures").Where(p=>!p.EndsWith(".meta")))
            {
                if(AssetImporter.GetAtPath(path) is not TextureImporter imp)continue;
                imp.textureType=path.Contains("nor_gl")?TextureImporterType.NormalMap:TextureImporterType.Default;
                imp.sRGBTexture=!path.Contains("nor_gl")&&!path.Contains("alpha");imp.alphaIsTransparency=path.EndsWith("PineFoliage.png");
                imp.alphaSource=path.EndsWith("PineFoliage.png")?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
                imp.mipmapEnabled=true;imp.maxTextureSize=2048;imp.wrapMode=path.Contains("PineFoliage")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;imp.anisoLevel=8;imp.textureCompression=TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
        }
        static void ApplyUrp()
        {
            var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if(rp==null)throw new InvalidOperationException("Project URP asset not found");
            GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;
            rp.shadowDistance=145;rp.shadowCascadeCount=4;rp.msaaSampleCount=4;EditorUtility.SetDirty(rp);
            var roots=new[]{"Assets/Zaslonovo","Assets/Zaslonovo_Corpus","Assets/Zaslonovo_Three"};
            var mats=AssetDatabase.FindAssets("t:Material",roots).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("/URP/")).Select(AssetDatabase.LoadAssetAtPath<Material>).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var path in AssetDatabase.FindAssets("t:Model",roots).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx")))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                foreach(var pair in importer.GetExternalObjectMap())
                    if(pair.Key.type==typeof(Material)&&mats.TryGetValue(pair.Key.name,out var urp))importer.AddRemap(pair.Key,urp);
                importer.SaveAndReimport();
            }
        }
        static Material Mat(string name,Color color,string texture=null,float smooth=.3f,float metallic=0)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);m.enableInstancing=true;
            if(texture!=null)
            {
                var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+"_diff.jpg");if(t==null)throw new InvalidOperationException("Texture missing: "+texture);
                m.SetTexture("_BaseMap",t);var n=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+"_nor_gl.jpg");if(n!=null){m.SetTexture("_BumpMap",n);m.SetFloat("_BumpScale",.65f);m.EnableKeyword("_NORMALMAP");}
            }
            return CampGeometry.Save(m,Gen+"/Materials/"+name+".mat");
        }
        static void Materials()
        {
            worldText=CampGeometry.Save(new Material(Shader.Find("Zaslonovo/World Text")){name="Надписи URP"},Gen+"/Materials/WorldText.mat");
            paving=Mat("Плитка дорожек",Color.white,"pavement_06",.16f);
            concrete=Mat("Светлый бетон",new Color(.67f,.65f,.59f),null,.24f);
            bark=Mat("Кора сосны",Color.white,"pine_bark",.14f);
            foliage=Mat("Хвоя сосны",new Color(.72f,.88f,.62f),null,.12f);
            foliage.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/PineFoliage.png"));foliage.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/pine_twig_nor_gl.jpg"));foliage.EnableKeyword("_NORMALMAP");foliage.SetFloat("_BumpScale",.35f);
            foliage.SetFloat("_AlphaClip",1);foliage.SetFloat("_Cutoff",.42f);foliage.EnableKeyword("_ALPHATEST_ON");foliage.SetFloat("_Cull",0);foliage.SetOverrideTag("RenderType","TransparentCutout");foliage.renderQueue=2450;EditorUtility.SetDirty(foliage);
            metal=Mat("Парковый металл",new Color(.14f,.23f,.19f),null,.42f,.55f);
            wood=Mat("Доски скамеек",new Color(.31f,.17f,.075f),null,.26f);
            lamp=Mat("Молочные плафоны",new Color(.93f,.93f,.82f),null,.50f);lamp.SetColor("_EmissionColor",new Color(.3f,.28f,.19f));lamp.EnableKeyword("_EMISSION");
            white=Mat("Камень фонтана",new Color(.78f,.75f,.65f),null,.3f);
            water=Mat("Вода фонтана",new Color(.16f,.34f,.29f),null,.92f,.22f);
            grass=Mat("Трава",new Color(.23f,.32f,.11f),null,.10f);grass.SetFloat("_Cull",0);
        }
        static void Lighting()
        {
            var rp=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;QualitySettings.vSyncCount=1;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.57f,.69f,.79f);RenderSettings.ambientEquatorColor=new Color(.39f,.43f,.33f);RenderSettings.ambientGroundColor=new Color(.22f,.24f,.17f);RenderSettings.ambientIntensity=1.1f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.66f,.74f,.75f);RenderSettings.fogDensity=.0018f;
            var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.9f);sky.SetColor("_SkyTint",new Color(.5f,.5f,.5f));sky.SetFloat("_Exposure",1.1f);RenderSettings.skybox=CampGeometry.Save(sky,Gen+"/Materials/Дневное небо.mat");
            var sun=new GameObject("Солнечный свет").AddComponent<Light>();sun.transform.SetParent(world,false);sun.type=LightType.Directional;sun.intensity=1.7f;sun.color=new Color(1,.94f,.82f);sun.transform.rotation=Quaternion.Euler(48,56,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.85f;sun.shadowBias=.06f;sun.shadowNormalBias=.32f;RenderSettings.sun=sun;
            var volume=new GameObject("Свет и цвет").AddComponent<Volume>();volume.transform.SetParent(world,false);volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);var ca=profile.Add<ColorAdjustments>();ca.postExposure.Override(.40f);ca.contrast.Override(6);ca.saturation.Override(4);var bloom=profile.Add<Bloom>();bloom.intensity.Override(.12f);bloom.threshold.Override(1.3f);volume.sharedProfile=CampGeometry.Save(profile,Gen+"/Daylight.asset");foreach(var component in volume.sharedProfile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,volume.sharedProfile);
        }
        static void Ground()
        {
            var td=new TerrainData{name="Заельцовский бор",heightmapResolution=513,alphamapResolution=512,size=new Vector3(1200,12,1400)};
            var heights=new float[513,513];for(int z=0;z<513;z++)for(int x=0;x<513;x++)
            {float wx=-600+x/512f*1200,wz=-600+z/512f*1400;float edge=Mathf.Clamp01(Mathf.Max(Mathf.Abs(wx)-170,Mathf.Abs(wz-80)-225)/90);heights[z,x]=.15f+edge*(Mathf.PerlinNoise(wx*.013f+45,wz*.013f+32)-.4f)*.14f;}
            td.SetHeights(0,0,heights);
            TerrainLayer Layer(string n,float tile)
            {var l=new TerrainLayer{diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+n+"_diff.jpg"),normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+n+"_nor_gl.jpg"),tileSize=Vector2.one*tile,normalScale=.45f,smoothness=.06f,smoothnessSource=TerrainLayerSmoothnessSource.ConstantMultipliedByDiffuseAlpha,diffuseRemapMax=n=="grass_ground"?new Vector4(.88f,1.12f,.86f,1):Vector4.one};return CampGeometry.Save(l,Gen+"/Terrain/"+n+".terrainlayer");}
            td.terrainLayers=new[]{Layer("grass_ground",5),Layer("forest_ground_04",6)};
            var alpha=new float[512,512,2];for(int z=0;z<512;z++)for(int x=0;x<512;x++){float wx=-600+x/511f*1200,wz=-600+z/511f*1400;float n=Mathf.PerlinNoise(wx*.035f+20,wz*.035f+30);float lawn=Mathf.Clamp01(.35f+n*.7f-(Mathf.Abs(wx)>150?.3f:0));alpha[z,x,0]=lawn;alpha[z,x,1]=1-lawn;}td.SetAlphamaps(0,0,alpha);
            td=CampGeometry.Save(td,Gen+"/Terrain/CampTerrain.asset");var go=Terrain.CreateTerrainGameObject(td);go.name="Рельеф — трава и хвойная подстилка";go.transform.SetParent(landscape,false);go.transform.position=new Vector3(-600,-1.8f,-600);terrain=go.GetComponent<Terrain>();terrain.materialTemplate=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));terrain.materialTemplate=CampGeometry.Save(terrain.materialTemplate,Gen+"/Materials/Рельеф URP.mat");terrain.heightmapPixelError=4;terrain.basemapDistance=190;
        }
        static Geometry ReadGeometry()=>JsonUtility.FromJson<Geometry>(File.ReadAllText(Root+"/References/SiteGeometry.json"));
        static Bounds RendererBounds(GameObject go)
        {
            var lod=go.GetComponent<LODGroup>();var rr=lod!=null?lod.GetLODs()[0].renderers:go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static void PlaceBuildings()
        {
            var source=ReadGeometry();
            var entries=new[]{
                new[]{"Центр детского творчества","Assets/Zaslonovo/Zaslonovo_URP.prefab","538191838"},
                new[]{"Корпус с наружной лестницей","Assets/Zaslonovo_Corpus/Zaslonovo_Corpus_URP.prefab","243048722"},
                new[]{"Бежевый жилой корпус","Assets/Zaslonovo_Three/Beige/Zaslonovo_Beige_URP.prefab","243048721"},
                new[]{"Одноэтажный корпус","Assets/Zaslonovo_Three/Mural/Zaslonovo_Mural_URP.prefab","538191833"},
                new[]{"Крытый бассейн","Assets/Zaslonovo_Three/Pool/Zaslonovo_Pool_URP.prefab","538191831"}};
            foreach(var entry in entries)
            {
                var feature=source.features.Single(f=>f.id==entry[2]);var p=feature.points.Select(p=>p.V).ToArray();var center=(p.Aggregate(Vector3.zero,(a,b)=>a+b)/p.Length);
                float bestArea=float.MaxValue,angle=0;
                for(int i=0;i<p.Length-1;i++)
                {
                    Vector3 dir=(p[i+1]-p[i]).normalized;if(dir.sqrMagnitude<.1f)continue;Vector3 side=new(-dir.z,0,dir.x);var xs=p.Select(v=>Vector3.Dot(v,dir)).ToArray();var ys=p.Select(v=>Vector3.Dot(v,side)).ToArray();float w=xs.Max()-xs.Min(),d=ys.Max()-ys.Min();
                    if(w*d<bestArea){bestArea=w*d;angle=Mathf.Atan2(dir.z,dir.x)*Mathf.Rad2Deg;if(d>w)angle+=90;}
                }
                var rotation=Quaternion.Euler(0,-angle,0);Vector3 toCenter=V(-20,65)-center;if(Vector3.Dot(rotation*Vector3.forward,toCenter)<0)rotation*=Quaternion.Euler(0,180,0);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry[1]);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,architecture);go.name=entry[0];go.transform.rotation=rotation;
                var bb=RendererBounds(go);go.transform.position+=center-new Vector3(bb.center.x,bb.min.y,bb.center.z)+Vector3.up*.025f;
                bb=RendererBounds(go);var front=go.transform.forward;var anchor=bb.center+front*(Mathf.Abs(front.x)*bb.extents.x+Mathf.Abs(front.z)*bb.extents.z+2.3f);anchor.y=.05f;
                if(entry[2]=="538191838")anchor=go.transform.TransformPoint(new Vector3(-.65f,0,8.0f));
                // Doors use the model's authoring coordinates, retained by the URP prefab.
                if(entry[2]=="243048721")anchor=go.transform.TransformPoint(new Vector3(0,0,5.0f));
                if(entry[2]=="538191833")anchor=go.transform.TransformPoint(new Vector3(-4.78f,0,3.45f));
                if(entry[2]=="538191831")anchor=go.transform.TransformPoint(new Vector3(-1.68f,0,6.0f));
                if(entry[2]=="243048722")anchor=go.transform.TransformPoint(new Vector3(8.10f,0,-5.20f));
                anchor.y=.05f;
                buildings.Add(new Building{name=entry[0],path=entry[1],osm=entry[2],entrance=anchor,obj=go,bounds=bb});
                foreach(var r in go.GetComponentsInChildren<MeshRenderer>())r.gameObject.isStatic=true;
            }
        }
        static Vector3[] Smooth(Vector3[] control)
        {
            var output=new List<Vector3>();
            for(int j=0;j<control.Length-1;j++)
            {
                var a=control[Mathf.Max(0,j-1)];var b=control[j];var c=control[j+1];var d=control[Mathf.Min(control.Length-1,j+2)];int count=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(b,c)/.7f));
                for(int i=0;i<count;i++){float t=i/(float)count;var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);p.y=.04f;output.Add(p);}
            }
            var end=control[^1];end.y=.04f;output.Add(end);return output.ToArray();
        }
        static void Path(string name,float width,params Vector3[] points){paths.Add(new PathDef{name=name,width=width,points=Smooth(points)});}
        static void PlanPaths()
        {
            var geo=ReadGeometry();foreach(var id in new[]{"437056489","695991110"})Path("Дорожка OSM "+id,3.1f,geo.features.Single(f=>f.id==id).points.Select(p=>p.V).ToArray());
            var admin=buildings[0];var stairs=buildings[1];var beige=buildings[2];var mural=buildings[3];var pool=buildings[4];
            Path("Главная аллея",4.0f,V(-42,22),V(-22,53),V(0,86),V(10,125),V(22,153),admin.entrance+admin.obj.transform.forward*22,admin.entrance);
            Path("Восточная аллея",3.2f,V(-22,53),V(40,66),V(74,95),V(94,143),mural.entrance);
            Path("Южная аллея",3.5f,V(-42,22),V(-26,-21),V(24,-30),V(72,-25),beige.entrance);
            Path("Связь северных корпусов",3.0f,admin.entrance,admin.entrance+admin.obj.transform.forward*13,V(-8,156),V(50,156),mural.entrance+mural.obj.transform.forward*14,mural.entrance);
            Path("Вход в бассейн",3.5f,pool.entrance,V(-10,18));
            Path("Боковой вход корпуса",2.4f,stairs.entrance,V(62,38),V(64,58),V(40,66));
            Path("Прогулочная петля",2.7f,V(-42,22),V(-77,67),V(-72,111),V(-35,152),admin.entrance);
            Path("Восточная прогулочная петля",2.8f,beige.entrance,beige.entrance+beige.obj.transform.forward*12,V(88,30),V(131,30),V(132,92),V(122,159),mural.entrance);
            Path("Подход к фонтану",4.0f,V(-49,5),V(-42,22));
        }
        static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {p.y=a.y=b.y=0;var ab=b-a;float u=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.001f,ab.sqrMagnitude));return Vector3.Distance(p,a+u*ab);}
        static float PathDistance(Vector3 point,PathDef path)
        {float d=float.MaxValue;for(int i=0;i<path.points.Length-1;i++)d=Mathf.Min(d,SegmentDistance(point,path.points[i],path.points[i+1]));return d;}
        static void BuildPaths()
        {
            for(int k=0;k<paths.Count;k++)
            {
                var path=paths[k];var m=new Mesh{ name=path.name,indexFormat=IndexFormat.UInt32};var vertices=new List<Vector3>();var uv=new List<Vector2>();var index=new List<int>();var curb=new CampMesh();
                for(int i=0;i<path.points.Length;i++)
                {
                    var p=path.points[i];var tangent=(path.points[Mathf.Min(i+1,path.points.Length-1)]-path.points[Mathf.Max(i-1,0)]).normalized;var side=new Vector3(-tangent.z,0,tangent.x);
                    foreach(float sign in new[]{-1f,1f}){var q=p+side*path.width/2*sign;vertices.Add(q);uv.Add(new Vector2(q.x,q.z)/2.6f);}
                    if(i==path.points.Length-1)continue;
                    int n=i*2;index.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
                    var next=path.points[i+1];foreach(float sign in new[]{-1f,1f})
                    {
                        var a=p+side*(path.width/2+.065f)*sign;var b=next+side*(path.width/2+.065f)*sign;
                        bool atJunction=Vector3.Distance((a+b)/2,V(-42,22))<7.3f||paths.Where((v,j)=>j!=k).Any(other=>PathDistance((a+b)/2,other)<other.width/2+.20f);
                        if(!atJunction)curb.Box((a+b)/2+Vector3.up*.018f,new Vector3(.12f,.12f,(b-a).magnitude+.025f),Quaternion.LookRotation(b-a));
                    }
                }
                m.SetVertices(vertices);m.SetUVs(0,uv);m.SetTriangles(index,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();m=CampGeometry.Save(m,Gen+"/Meshes/Path_"+k+".asset");
                CampGeometry.MeshObject(path.name,landscape,m,paving,true).isStatic=true;CampGeometry.MeshObject(path.name+" — бордюры",landscape,MeshSave("Curbs_"+k,curb),concrete).isStatic=true;
            }
            foreach(var b in buildings)
            {
                var m=new CampMesh();m.Quad(new Vector3(-4,0,-2.6f),new Vector3(-4,0,2.6f),new Vector3(4,0,2.6f),new Vector3(4,0,-2.6f),new Rect(0,0,8/2.6f,2));var apron=CampGeometry.MeshObject("Площадка у входа — "+b.name,landscape,MeshSave("Apron_"+b.osm,m),paving,true);apron.transform.SetPositionAndRotation(b.entrance+b.obj.transform.forward*1.25f+Vector3.up*.015f,b.obj.transform.rotation);apron.isStatic=true;
            }
            var square=new CampMesh();int segments=72;var center=V(-42,22)+Vector3.up*.038f;
            for(int i=0;i<segments;i++){float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;square.Triangle(center,center+V(Mathf.Cos(b)*7,Mathf.Sin(b)*7),center+V(Mathf.Cos(a)*7,Mathf.Sin(a)*7));}
            CampGeometry.MeshObject("Площадь у фонтана — OSM",landscape,MeshSave("FountainSquare",square),paving,true).isStatic=true;
        }
        static void Decorate()
        {
            var benchW=CampGeometry.BenchWood();var benchM=CampGeometry.BenchMetal();var pole=CampGeometry.LampPole();var globe=CampGeometry.LampGlobe();
            int benchCount=0,lampCount=0;
            foreach(var path in paths)
            {
                float nextBench=16,nextLamp=7,total=0;
                for(int i=1;i<path.points.Length;i++)
                {
                    var a=path.points[i-1];var b=path.points[i];var tangent=(b-a).normalized;var side=new Vector3(-tangent.z,0,tangent.x);total+=Vector3.Distance(a,b);
                    if(total>=nextBench)
                    {
                        var pos=b+side*(path.width/2+1.15f);pos.y=0;
                        if(!InsideBuilding(pos,1.4f)&&paths.All(p=>p==path||PathDistance(pos,p)>p.width/2+1))
                        {var parent=new GameObject("Скамейка "+(++benchCount)).transform;parent.SetParent(furniture,false);parent.SetPositionAndRotation(pos,Quaternion.LookRotation(side));CampGeometry.MeshObject("Деревянные планки",parent,benchW,wood);CampGeometry.MeshObject("Металлическая рама",parent,benchM,metal);var col=parent.gameObject.AddComponent<BoxCollider>();col.center=new Vector3(0,.45f,0);col.size=new Vector3(1.8f,.9f,.5f);}
                        nextBench+=31;
                    }
                    if(total>=nextLamp)
                    {
                        float sign=lampCount%2==0?1:-1;var pos=b+side*(path.width/2+.65f)*sign;pos.y=0;
                        if(!InsideBuilding(pos,.6f)){var parent=new GameObject("Парковый фонарь "+(++lampCount)).transform;parent.SetParent(furniture,false);parent.position=pos;CampGeometry.MeshObject("Стойка",parent,pole,metal);CampGeometry.MeshObject("Плафон",parent,globe,lamp);var col=parent.gameObject.AddComponent<CapsuleCollider>();col.center=Vector3.up*1.6f;col.height=3.2f;col.radius=.1f;}
                        nextLamp+=19;
                    }
                }
            }
            var fountain=CampGeometry.Fountain();var root=new GameObject("Фонтан — точка OpenStreetMap").transform;root.SetParent(furniture,false);root.position=V(-42,22);CampGeometry.MeshObject("Каменные чаши",root,fountain[0],white);CampGeometry.MeshObject("Вода",root,fountain[1],water);var fc=root.gameObject.AddComponent<CapsuleCollider>();fc.center=Vector3.up*.3f;fc.height=.6f;fc.radius=2.65f;fc.direction=1;
            var jets=new CampMesh();for(int i=0;i<16;i++){float a=i*2*Mathf.PI/16;Vector3 before=new(Mathf.Cos(a)*.8f,2.44f,Mathf.Sin(a)*.8f);for(int j=1;j<=16;j++){float t=j/16f;var p=new Vector3(Mathf.Cos(a)*(.8f+t*.7f),2.44f+1.4f*t-3.4f*t*t,Mathf.Sin(a)*(.8f+t*.7f));jets.Tube(before,p,.009f,.009f,4);before=p;}}CampGeometry.MeshObject("Струи фонтана",root,MeshSave("FountainJets",jets),lamp);
            Sign(V(-24,51),"КОРПУСА  →\nБАССЕЙН  ↓",28);
            Sign(V(40,67),"ЦЕНТР ТВОРЧЕСТВА  ↑\nЖИЛЫЕ КОРПУСА  →",-15);
            Sign(V(66,19),"ЖИЛОЙ КОРПУС  →\nБАССЕЙН  ←",-20);
            var gate=new GameObject("Входная аллея").transform;gate.SetParent(furniture,false);gate.position=V(-92,-17);var frame=new CampMesh();frame.Box(new Vector3(-2.5f,1.8f,0),new Vector3(.18f,3.6f,.18f));frame.Box(new Vector3(2.5f,1.8f,0),new Vector3(.18f,3.6f,.18f));frame.Box(new Vector3(0,3.42f,0),new Vector3(5.2f,.46f,.12f));CampGeometry.MeshObject("Входная арка",gate,MeshSave("EntranceGate",frame),metal);Label(gate,"ЗАСЛОНОВО",new Vector3(0,3.43f,-.073f),.25f);
        }
        static void Sign(Vector3 point,string text,float yaw)
        {
            var t=new GameObject("Указатель — "+text.Replace('\n',' ')).transform;t.SetParent(furniture,false);t.SetPositionAndRotation(point,Quaternion.Euler(0,yaw,0));var frame=new CampMesh();frame.Box(new Vector3(0,.92f,0),new Vector3(.09f,1.84f,.09f));frame.Box(new Vector3(0,1.88f,0),new Vector3(2.05f,.62f,.09f));CampGeometry.MeshObject("Стойка и табличка",t,MeshSave("Sign_"+Mathf.RoundToInt(point.x)+"_"+Mathf.RoundToInt(point.z),frame),metal);Label(t,text,new Vector3(0,1.88f,-.052f),.10f);
        }
        static void Label(Transform parent,string text,Vector3 local,float size)
        {
            var go=new GameObject("Надпись");go.transform.SetParent(parent,false);go.transform.localPosition=local;var tm=go.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.characterSize=size;tm.fontSize=64;tm.color=new Color(.93f,.94f,.86f);tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=worldText;go.AddComponent<CampWorldLabel>();
        }
        static bool InsideBuilding(Vector3 point,float margin)
        {return buildings.Any(b=>point.x>b.bounds.min.x-margin&&point.x<b.bounds.max.x+margin&&point.z>b.bounds.min.z-margin&&point.z<b.bounds.max.z+margin);}
        static void PlantForest()
        {
            var meshVariants=new Mesh[4][][];for(int i=0;i<4;i++){meshVariants[i]=new Mesh[3][];for(int level=0;level<3;level++)meshVariants[i][level]=CampGeometry.Pine(51+i*7,level);}
            int count=0;
            for(float z=-77;z<288;z+=7.8f)for(float x=-164;x<164;x+=7.8f)
            {
                var p=V(x+F(-2.9f,2.9f),z+F(-2.9f,2.9f));
                if(random.NextDouble()>.70||InsideBuilding(p,4.2f)||paths.Any(path=>PathDistance(p,path)<path.width/2+1.9f)||Vector3.Distance(p,V(-42,22))<9.4f)continue;
                if(buildings.Any(b=>Vector3.Distance(p,b.entrance)<8))continue;
                int variant=count%4;float scale=F(.78f,1.22f);var tree=new GameObject("Сосна "+(++count));tree.transform.SetParent(vegetation,false);tree.transform.SetPositionAndRotation(p+Vector3.up*(terrain.SampleHeight(p)+terrain.transform.position.y),Quaternion.Euler(0,F(0,360),0));tree.transform.localScale=Vector3.one*scale;
                var levels=new LOD[3];for(int level=0;level<3;level++)
                {
                    var parent=new GameObject("Уровень "+level).transform;parent.SetParent(tree.transform,false);var a=CampGeometry.MeshObject("Ствол и ветви",parent,meshVariants[variant][level][0],bark);var b=CampGeometry.MeshObject("Хвоя",parent,meshVariants[variant][level][1],foliage);var rr=new[]{a.GetComponent<Renderer>(),b.GetComponent<Renderer>()};
                    if(level==2)foreach(var r in rr)r.shadowCastingMode=ShadowCastingMode.Off;levels[level]=new LOD(level==0?.16f:level==1?.055f:.012f,rr);
                }
                var lod=tree.AddComponent<LODGroup>();lod.SetLODs(levels);lod.RecalculateBounds();var collider=tree.AddComponent<CapsuleCollider>();collider.center=Vector3.up*6;collider.height=12;collider.radius=.32f;treePositions.Add(p);
            }
            File.WriteAllText("Library/ZaslonovoTreeCount.txt",count.ToString());
        }
        static void PlantFlowers()
        {
            var colors=new[]{new Color(.94f,.32f,.10f),new Color(.96f,.69f,.08f),new Color(.9f,.12f,.39f)};var flowers=new[]{new CampMesh(),new CampMesh(),new CampMesh()};var stems=new CampMesh();var grassMesh=new CampMesh();
            var admin=buildings[0];var right=admin.obj.transform.right;var forward=admin.obj.transform.forward;
            for(int side=-1;side<=1;side+=2)
            {
                var center=admin.entrance+forward*5.2f+right*side*7.2f;
                var bed=new CampMesh();bed.Box(center+Vector3.up*.02f,new Vector3(4.2f,.09f,8.0f),admin.obj.transform.rotation);CampGeometry.MeshObject("Цветник у корпуса "+side,landscape,MeshSave("FlowerBed_"+side,bed),Mat("Почва",new Color(.13f,.10f,.06f),null,.1f));
                for(int i=0;i<320;i++)
                {
                    var p=center+right*F(-1.9f,1.9f)+forward*F(-3.8f,3.8f);float h=F(.21f,.49f);stems.Tube(p+Vector3.up*.07f,p+Vector3.up*h,.008f,.005f,4);CampGeometry.GardenLeaves(stems,p+Vector3.up*.07f,p+Vector3.up*h,i+(side<0?0:320));var top=p+Vector3.up*h;int c=i%3;float radius=F(.04f,.075f);
                    for(int petal=0;petal<7;petal++){float a=petal*2*Mathf.PI/7;var dir=V(Mathf.Cos(a),Mathf.Sin(a));var cross=V(-dir.z,dir.x)*radius*.42f;flowers[c].Quad(top,top+dir*radius+cross+Vector3.up*.025f,top+dir*radius*1.5f+Vector3.up*.018f,top+dir*radius-cross+Vector3.up*.025f);}
                }
            }
            for(int j=0;j<16000;j++)
            {
                var p=V(F(-150,150),F(-55,270));if(InsideBuilding(p,2)||Vector3.Distance(p,V(-42,22))<8)continue;float dist=paths.Min(path=>PathDistance(p,path)-path.width/2);if(dist<.6f||dist>7||random.NextDouble()>.45)continue;
                float h=F(.10f,.28f);for(int i=0;i<3;i++){float a=F(0,Mathf.PI*2);Vector3 side=V(Mathf.Cos(a),Mathf.Sin(a))*.025f;grassMesh.Triangle(p-side,p+side,p+Vector3.up*h+side*.8f);}
            }
            CampGeometry.MeshObject("Стебли цветников",vegetation,MeshSave("FlowerStems",stems),grass);
            for(int i=0;i<3;i++){var mat=Mat("Цветы "+i,colors[i],null,.3f);mat.SetFloat("_Cull",0);CampGeometry.MeshObject("Цветы "+i,vegetation,MeshSave("Flowers_"+i,flowers[i]),mat);}
            CampGeometry.MeshObject("Трава вдоль аллей",vegetation,MeshSave("PathsideGrass",grassMesh),grass);
        }
        static void Player()
        {
            var admin=buildings[0];Vector3 spawn=admin.entrance+admin.obj.transform.forward*13;spawn.y=.13f;
            var p=new GameObject("Игрок — WASD и мышь");p.transform.SetParent(world,false);var yaw=Quaternion.LookRotation(admin.entrance-spawn).eulerAngles.y;p.transform.SetPositionAndRotation(spawn,Quaternion.Euler(0,yaw,0));
            var controller=p.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.30f;controller.center=Vector3.up*.9f;controller.stepOffset=.32f;controller.slopeLimit=48;controller.skinWidth=.035f;controller.minMoveDistance=0;
            var eye=new GameObject("Main Camera");eye.tag="MainCamera";eye.transform.SetParent(p.transform,false);eye.transform.localPosition=Vector3.up*1.67f;var camera=eye.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.045f;camera.farClipPlane=430;camera.allowHDR=true;eye.AddComponent<AudioListener>();var urp=eye.AddComponent<UniversalAdditionalCameraData>();urp.renderPostProcessing=true;urp.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;urp.antialiasingQuality=AntialiasingQuality.High;
            var fps=p.AddComponent<CampFirstPersonController>();fps.viewCamera=camera;fps.spawnPosition=spawn;fps.spawnYaw=yaw;
            var prefab=PrefabUtility.SaveAsPrefabAsset(p,Gen+"/FirstPersonPlayer.prefab");
        }
        [MenuItem("Tools/Zaslonovo/Validate camp scene")]
        public static void Validate()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()){if(!Application.isBatchMode)throw new InvalidOperationException("Open the Zaslonovo_Camp scene before validating it.");scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);}SceneManager.SetActiveScene(scene);
            var roots=scene.GetRootGameObjects();var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var fps=all.Select(t=>t.GetComponent<CampFirstPersonController>()).FirstOrDefault(c=>c!=null);Check(fps!=null&&fps.viewCamera!=null,"Player and camera");Check(roots.SelectMany(r=>r.GetComponentsInChildren<AudioListener>()).Count()==1,"Single audio listener");
            Check(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset,"URP enabled");
            var buildingRoot=all.Single(t=>t.name=="02 • Корпуса — URP");Check(buildingRoot.childCount==5,"All five buildings");
            foreach(Transform b in buildingRoot)
            {
                Check(b.GetComponentsInChildren<Collider>().Length>0,b.name+" colliders");foreach(var renderer in b.GetComponentsInChildren<MeshRenderer>(true))foreach(var mat in renderer.sharedMaterials)Check(mat!=null&&mat.shader.name=="Universal Render Pipeline/Lit",b.name+" URP materials");
            }
            var trees=all.Single(t=>t.name=="03 • Сосновый бор").GetComponentsInChildren<LODGroup>();Check(trees.Length>500,"Forest density and LODs");
            Physics.SyncTransforms();var groundHits=Physics.RaycastAll(fps.spawnPosition+Vector3.up*2,Vector3.down,5).Where(h=>!h.collider.transform.IsChildOf(fps.transform)).OrderBy(h=>h.distance).ToArray();Check(groundHits.Length>0,"Walkable spawn ground");var hit=groundHits[0];Check(Mathf.Abs(hit.point.y-fps.spawnPosition.y)<.25f,"Spawn is close to the ground");
            var playerCollider=fps.GetComponent<CharacterController>();Check(playerCollider.stepOffset>=.3f&&playerCollider.height>1.7f,"Controller scale");
            var blocked=new List<string>();
            foreach(var pathCollider in roots.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Where(c=>c.sharedMesh!=null&&AssetDatabase.GetAssetPath(c.sharedMesh).Contains("/Path_")))
            {
                var points=pathCollider.sharedMesh.vertices;
                for(int j=12;j<points.Length-12;j+=16)
                {
                    Vector3 p=pathCollider.transform.TransformPoint((points[j]+points[j+1])/2);
                    foreach(var obstacle in Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.48f,.27f))
                        if(obstacle.transform.IsChildOf(buildingRoot))blocked.Add(pathCollider.name+" at "+p.ToString("F1")+" / "+obstacle.name);
                }
            }
            File.WriteAllLines("Library/ZaslonovoPathObstructions.txt",blocked);
            Check(blocked.Count==0,"Walkable alleys cross building collision: "+string.Join("; ",blocked.Take(4)));
            File.WriteAllText("Library/ZaslonovoWorldValidation.txt","SUCCESS\n5 URP buildings\n"+trees.Length+" pine LODGroups\nPlayer, terrain, path collisions and dependencies verified\nSpawn ground: "+hit.point.ToString("F3"));
        }
        static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
        public static void DiagnoseAndPreview(){Diagnose();RenderPreviews();}
        public static void Diagnose()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var lines=new List<string>();
            var root=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="02 • Корпуса — URP");
            foreach(Transform building in root)lines.Add("BOUNDS "+building.name+" "+RendererBounds(building.gameObject).ToString("F2")+" yaw="+building.eulerAngles.y);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                lines.Add(r.transform.parent.name+" / "+r.name+" -> "+string.Join(" | ",r.sharedMaterials.Select(m=>m.name+" / "+m.GetTexture("_BaseMap")?.name)));
            File.WriteAllLines("Library/ZaslonovoMaterialDiagnostics.txt",lines);
        }
        public static void RenderPreviews()
        {
            if(!SceneManager.GetSceneByPath(ScenePath).IsValid())EditorSceneManager.OpenScene(ScenePath);
            var fps=Object.FindFirstObjectByType<CampFirstPersonController>();var cam=fps.viewCamera;Directory.CreateDirectory("CampPreviews");
            void Capture(string file,Vector3 position,Vector3 target,int width=1920,int height=1080)
            {
                var go=new GameObject("Preview camera");var c=go.AddComponent<Camera>();c.CopyFrom(cam);c.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));c.fieldOfView=65;c.nearClipPlane=.1f;c.farClipPlane=800;var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=1};rt.Create();var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};RenderPipeline.SubmitRenderRequest(c,request);RenderPipeline.SubmitRenderRequest(c,request);
                var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes("CampPreviews/"+file+".png",tex.EncodeToPNG());RenderTexture.active=prior;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);
            }
            Capture("FirstPerson",cam.transform.position,cam.transform.position+cam.transform.forward*20);
            Capture("Overview",new Vector3(-100,115,-70),new Vector3(24,0,108),2400,1500);
            Capture("Fountain",new Vector3(-47,2.1f,16),new Vector3(-42,1.3f,22));
            File.WriteAllText("Library/ZaslonovoRenderResult.txt","SUCCESS");
        }
    }
}
