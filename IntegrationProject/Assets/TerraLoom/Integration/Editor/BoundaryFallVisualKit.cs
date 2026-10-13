using System;
using UnityEditor;
using UnityEngine;

namespace TerraLoom.Integration.Editor
{
    /// <summary>Original deterministic fixture art. No external textures or model licences.</summary>
    public static class BoundaryFallVisualKit
    {
        public sealed class Assets
        {
            public Material Ground,Bridge,Water,Rock;
            public TerrainLayer[] TerrainLayers;
        }
        public static Assets Build(string root,TerrainData terrain)
        {
            if(!AssetDatabase.IsValidFolder(root))throw new ArgumentException("Create the fixture art folder first.");
            var soil=Texture(root,"Soil",new Color(.43f,.36f,.27f),new Color(.57f,.47f,.33f),31);
            var grass=Texture(root,"Grass",new Color(.22f,.34f,.17f),new Color(.41f,.51f,.25f),43);
            var stone=Texture(root,"Stone",new Color(.27f,.31f,.33f),new Color(.5f,.53f,.52f),57);
            var timber=Texture(root,"Timber",new Color(.34f,.2f,.11f),new Color(.57f,.38f,.19f),71);
            var result=new Assets
            {
                Ground=Material(root,"Ground",soil,.08f),
                Bridge=Material(root,"Timber",timber,.13f),
                Rock=Material(root,"Rock",stone,.12f),
                Water=Water(root),
                TerrainLayers=new[]{Layer(root,"Grass",grass),Layer(root,"Soil",soil),Layer(root,"Stone",stone)}
            };
            terrain.terrainLayers=result.TerrainLayers;
            terrain.alphamapResolution=128;
            var maps=new float[128,128,3];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++)
            {
                float metresX=x*32f/127,metresZ=z*32f/127;
                float bank=Mathf.Abs(metresZ-16);
                float rock=Mathf.Clamp01((metresX-4.8f)/1.7f)*Mathf.Clamp01((7.6f-metresX)/1.7f);
                float grassWeight=metresX<6?Mathf.Lerp(.7f,.9f,Hash(x,z,13)):
                    bank>10?Mathf.Lerp(.3f,.55f,Hash(x,z,23)):.08f;
                float soilWeight=1-grassWeight-rock;
                maps[z,x,0]=grassWeight;maps[z,x,1]=Mathf.Max(0,soilWeight);maps[z,x,2]=rock;
            }
            terrain.SetAlphamaps(0,0,maps);
            AssetDatabase.SaveAssets();
            return result;
        }
        /// <summary>Two seasonal tree palettes and scattered original low-poly stones.
        /// Fixture dressing only; RegionDecoration ownership is tested separately.</summary>
        public static void PlaceProps(Transform parent,Terrain terrain,string root)
        {
            var wood=Material(root,"TreeBark",null,.08f);wood.SetColor("_BaseColor",new Color(.34f,.22f,.12f));
            var summer=Material(root,"SummerLeaves",null,.05f);
            summer.SetColor("_BaseColor",new Color(.18f,.38f,.14f));
            var winter=Material(root,"WinterNeedles",null,.06f);
            winter.SetColor("_BaseColor",new Color(.55f,.64f,.61f));
            var trunk=MeshAsset(root,"TreeTrunk",TreeTrunk);
            var crown=MeshAsset(root,"TreeCrown",TreeCrown);
            var stone=MeshAsset(root,"FieldStone",FieldStone);
            var rock=AssetDatabase.LoadAssetAtPath<Material>(root+"/Rock.mat");
            var group=new GameObject("Seasonal trees and stones (fixture)");group.transform.SetParent(parent,false);
            void Part(Transform tree,string name,Mesh mesh,Material material)
            {
                var go=new GameObject(name);go.transform.SetParent(tree,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            void Tree(string name,float x,float z,Material leaves,float size)
            {
                var go=new GameObject(name);go.transform.SetParent(group.transform,false);
                go.transform.position=new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y,z);
                go.transform.localScale=Vector3.one*size;
                Part(go.transform,"Original trunk",trunk,wood);
                Part(go.transform,"Seasonal crown",crown,leaves);
            }
            Tree("Summer pine A",2.5f,3,summer,1.05f);
            Tree("Summer pine B",3.4f,28,summer,.82f);
            Tree("Summer pine C",1.8f,23,summer,1.12f);
            Tree("Winter pine A",15,4,winter,.88f);
            Tree("Winter pine B",19,27,winter,1.03f);
            Tree("Winter pine C",23,29,winter,.73f);
            var locations=new[]{new Vector2(12,5),new Vector2(21,6),new Vector2(17,25),new Vector2(3,27)};
            for(int i=0;i<locations.Length;i++)
            {
                var p=locations[i];var go=new GameObject("Original fieldstone "+(i+1));
                go.transform.SetParent(group.transform,false);
                go.transform.position=new Vector3(p.x,terrain.SampleHeight(new Vector3(p.x,0,p.y))+
                    terrain.transform.position.y,p.y);
                go.transform.localScale=new Vector3(.8f+i*.17f,.5f+i*.06f,.65f+i*.12f);
                Part(go.transform,"Stone mesh",stone,rock);
            }
            AssetDatabase.SaveAssets();
        }
        private static Mesh MeshAsset(string root,string name,Func<Mesh> create)
        {
            string path=root+"/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)return mesh;
            mesh=create();mesh.name="TerraLoom original "+name;
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Mesh TreeTrunk()
        {
            const int sides=8;var vertices=new Vector3[sides*2];var triangles=new int[sides*6];
            for(int i=0;i<sides;i++)
            {
                float angle=2*Mathf.PI*i/sides;
                vertices[2*i]=new Vector3(Mathf.Cos(angle)*.18f,0,Mathf.Sin(angle)*.18f);
                vertices[2*i+1]=new Vector3(Mathf.Cos(angle)*.12f,1.85f,Mathf.Sin(angle)*.12f);
                int next=(i+1)%sides,t=i*6;
                triangles[t]=2*i;triangles[t+1]=2*i+1;triangles[t+2]=2*next;
                triangles[t+3]=2*next;triangles[t+4]=2*i+1;triangles[t+5]=2*next+1;
            }
            var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();return mesh;
        }
        private static Mesh TreeCrown()
        {
            const int sides=9;var vertices=new Vector3[sides*2+2];var triangles=new int[sides*6];
            for(int i=0;i<sides;i++)
            {
                float angle=2*Mathf.PI*i/sides;
                vertices[i]=new Vector3(Mathf.Cos(angle)*.9f,1.15f,Mathf.Sin(angle)*.9f);
                vertices[sides+i]=new Vector3(Mathf.Cos(angle)*.65f,1.8f,Mathf.Sin(angle)*.65f);
                int next=(i+1)%sides,t=i*6;
                triangles[t]=i;triangles[t+1]=sides+i;triangles[t+2]=next;
                triangles[t+3]=next;triangles[t+4]=sides+i;triangles[t+5]=sides+next;
            }
            vertices[sides*2]=new Vector3(0,2.95f,0);
            var list=new System.Collections.Generic.List<int>(triangles);
            for(int i=0;i<sides;i++){list.Add(sides+i);list.Add(sides*2);list.Add(sides+(i+1)%sides);}
            var mesh=new Mesh{vertices=vertices,triangles=list.ToArray()};mesh.RecalculateNormals();return mesh;
        }
        private static Mesh FieldStone()
        {
            var vertices=new[]{new Vector3(0,.8f,0),new Vector3(1,.22f,0),new Vector3(0,.18f,1),
                new Vector3(-.9f,.25f,0),new Vector3(0,.18f,-.8f),new Vector3(0,-.15f,0)};
            var triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,1,5,2,1,5,3,2,5,4,3,5,1,4};
            var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();return mesh;
        }
        private static Texture2D Texture(string root,string name,Color dark,Color light,int seed)
        {
            string path=root+"/"+name+"Texture.asset";
            var result=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(result)return result;
            const int n=128;
            result=new Texture2D(n,n,TextureFormat.RGBA32,false,true){name="TerraLoom "+name,
                wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,anisoLevel=4};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float broad=Hash(x/8,y/8,seed),fine=Hash(x,y,seed+1);
                float bands=(Mathf.Sin(y*.31f+broad*2)+1)*.5f;
                float mix=Mathf.Clamp01(.2f+.43f*broad+.23f*fine+.14f*bands);
                pixels[y*n+x]=Color.Lerp(dark,light,mix);
            }
            result.SetPixels(pixels);result.Apply(false,false);
            AssetDatabase.CreateAsset(result,path);return result;
        }
        private static float Hash(int x,int y,int seed)
        {
            unchecked
            {
                uint v=(uint)(x*73856093^y*19349663^seed*83492791);
                v^=v>>16;v*=0x7feb352d;v^=v>>15;v*=0x846ca68b;v^=v>>16;
                return (v&0xffff)/65535f;
            }
        }
        private static Material Material(string root,string name,Texture texture,float smoothness)
        {
            var path=root+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="TerraLoom "+name};
                AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(material);return material;
        }
        private static Material Water(string root)
        {
            var material=Material(root,"Water",null,.85f);
            material.SetColor("_BaseColor",new Color(.12f,.38f,.45f,.75f));
            material.SetFloat("_Surface",1);
            material.SetFloat("_Blend",0);
            material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
            EditorUtility.SetDirty(material);return material;
        }
        private static TerrainLayer Layer(string root,string name,Texture2D diffuse)
        {
            var path=root+"/"+name+"TerrainLayer.asset";
            var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if(!layer){layer=new TerrainLayer{name="TerraLoom "+name};AssetDatabase.CreateAsset(layer,path);}
            layer.diffuseTexture=diffuse;layer.tileSize=new Vector2(8,8);EditorUtility.SetDirty(layer);return layer;
        }
    }
}
