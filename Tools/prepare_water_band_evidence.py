"""Prepare an isolated RiverWater diagnostic package for Run-WaterBandEvidence.ps1.

Development fixture only: never changes canonical module packages. The runner
owns backup/restoration of the ignored Integration manifest/lock/capture script.
Baseline disables candidate ripple normals; all variants share water shader time4.
"""
import pathlib, shutil, json, hashlib, subprocess
r=pathlib.Path(__file__).resolve().parents[2]; out=r/'.bootstrap-tools/waterband'; out.mkdir(parents=True,exist_ok=True)
source=r/'Rivers/Packages/com.terraloom.rivers'; candidate=out/'com.terraloom.rivers'
shutil.copytree(source,candidate,dirs_exist_ok=True)
shader=candidate/'Unity/RiverWater.shader'; text=shader.read_text()
assert '_RippleHeight' not in text, 'Diagnostic preparer targets the published flat-normal baseline only'
assert '_Time.y * speed' in text and 'surface.smoothness = lerp(0.88h,' in text, 'Water shader baseline changed; review diagnostics first'
text=text.replace('_BaseColor("Water", Color)', '_EvidenceTime("Evidence time", Float) = 4\n        _EvidenceSmoothness("Evidence roughness control", Float) = 0.88\n        _EvidenceNormals("Evidence normals", Float) = 0\n        _BaseColor("Water", Color)')
text=text.replace('float _FlowSpeed,', 'float _EvidenceTime, _EvidenceSmoothness, _EvidenceNormals;\n            float _FlowSpeed,')
text=text.replace('_Time.y * speed', '_EvidenceTime * speed')
text=text.replace('_EvidenceTime("Evidence time", Float) = 4', '_RippleHeight("Visual ripple height (metres, no displacement)", Range(0,0.1)) = 0.025\n        _EvidenceTime("Evidence time", Float) = 4')
text=text.replace('float _EvidenceTime,', 'float _RippleHeight;\n            float _EvidenceTime,')
text=text.replace('half3 normal = NormalizeNormalPerPixel(input.normalWS);\n            half3 view', '''half3 normal = NormalizeNormalPerPixel(input.normalWS);
            // Differentiate the existing metre-UV wave on the actual surface, so light
            // reflects from ripples instead of one mirror-flat strip. Shading only:
            // planned water heights, clipping, normals/depth passes and colliders stay exact.
            float height = wave * _RippleHeight * liquid;
            float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
            float3 rx = cross(dy, normal), ry = cross(normal, dx);
            float determinant = dot(dx, rx);
            if (abs(determinant) > 1e-10)
                normal = normalize(normal - (ddx(height) * rx + ddy(height) * ry) / determinant);
            half3 view''')
text=text.replace('surface.smoothness = lerp(0.88h,', 'surface.smoothness = lerp(_EvidenceSmoothness,')
text=text.replace('half3 view = GetWorldSpaceNormalizeViewDir', 'if (_EvidenceNormals > 0.5) return half4(normal * 0.5 + 0.5, 1);\n            half3 view = GetWorldSpaceNormalizeViewDir')
text=text.replace('#pragma fragment Frag', '#pragma fragment Frag\n            #pragma multi_compile_local_fragment _ _SPECULARHIGHLIGHTS_OFF\n            #pragma multi_compile_local_fragment _ _ENVIRONMENTREFLECTIONS_OFF')
shader.write_text(text,encoding='utf-8')
manifest=r/'Integration/Packages/manifest.json'; lock=r/'Integration/Packages/packages-lock.json'
for name,p in [('manifest.json',manifest),('packages-lock.json',lock)]:
    if (out/name).exists():
        assert p.read_bytes()==(out/name).read_bytes(), 'Cannot retry over changed consumer settings'
    else: shutil.copy2(p,out/name)
data=json.loads(manifest.read_text()); data['dependencies']['com.terraloom.rivers']='file:../../.bootstrap-tools/waterband/com.terraloom.rivers'
manifest.write_text(json.dumps(data,indent=2)+'\n')
fixture=r/'Integration/Assets/TerraLoom/Integration/FrozenCrossingEvidence.cs'; shutil.copy2(fixture,out/fixture.name)
code=fixture.read_text()
old='RuntimeVisualCapture.Save(Camera.main,Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+suffix+".png"));'
new='''var waterRenderers=c.Rivers.GeneratedRoot.GetComponentsInChildren<MeshRenderer>();
                    var materials=waterRenderers.SelectMany(x=>x.sharedMaterials).Where(x=>x&&x.shader.name=="TerraLoom/RiverWater").Distinct().ToArray();
                    if(materials.Length==0||materials.Any(x=>!x.HasProperty("_EvidenceTime")))throw new InvalidOperationException("Diagnostic river shader required.");
                    foreach(string mode in new[]{"baseline","foam-off","specular-off","environment-off","all-specular-off","roughness","normals","ripple-normals"})
                    {
                        var oldMaterials=waterRenderers.Select(x=>x.sharedMaterials).ToArray();
                        var clones=materials.ToDictionary(x=>x,x=>new Material(x));
                        try
                        {
                            foreach(var clone in clones.Values)
                            {
                                clone.SetFloat("_EvidenceTime",4);clone.SetFloat("_EvidenceSmoothness",mode=="roughness"?.65f:.88f);clone.SetFloat("_EvidenceNormals",mode=="normals"?1:0);
                                clone.SetFloat("_RippleHeight",mode=="ripple-normals"?.025f:0);
                                if(mode=="foam-off")clone.SetFloat("_FoamStrength",0);
                                if(mode=="specular-off"||mode=="all-specular-off")clone.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");else clone.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                                if(mode=="environment-off"||mode=="all-specular-off")clone.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");else clone.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                            }
                            foreach(var renderer in waterRenderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(x=>clones.TryGetValue(x,out var clone)?clone:x).ToArray();
                            string capture=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+suffix+"-"+mode+".png");
                            RuntimeVisualCapture.Save(Camera.main,capture);
                            File.WriteAllText(Path.ChangeExtension(capture,".state.txt"),WaterBandMetadata(Camera.main,clones.Values.ToArray(),waterRenderers,c.World.TerrainContentFingerprint,mode));
                        }
                        finally{for(int n=0;n<waterRenderers.Length;n++)waterRenderers[n].sharedMaterials=oldMaterials[n];foreach(var clone in clones.Values)Destroy(clone);}
                    }'''
assert old in code; code=code.replace(old,new)
idx=code.index('        private IEnumerator Start()')
helper='''        private static string WaterBandMetadata(Camera camera,Material[] materials,MeshRenderer[] renderers,string terrain,string mode)
        {
            var s=new System.Text.StringBuilder();s.AppendLine("mode="+mode+"; fixedWaterShaderTime=4; terrain="+terrain);
            s.AppendLine("camera="+camera.transform.position.ToString("R")+"; rotation="+camera.transform.rotation.ToString("R")+"; fov="+camera.fieldOfView+"; near="+camera.nearClipPlane+"; far="+camera.farClipPlane+"; HDR="+camera.allowHDR+"; MSAA="+camera.allowMSAA+"; clear="+camera.clearFlags+"; quality="+QualitySettings.GetQualityLevel()+"; color="+QualitySettings.activeColorSpace+"; pipeline="+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name);
            s.AppendLine("ambientMode="+RenderSettings.ambientMode+"; ambient="+RenderSettings.ambientLight.ToString("R")+"; ambientIntensity="+RenderSettings.ambientIntensity+"; sky="+RenderSettings.ambientSkyColor.ToString("R")+"; equator="+RenderSettings.ambientEquatorColor.ToString("R")+"; ground="+RenderSettings.ambientGroundColor.ToString("R")+"; reflectionIntensity="+RenderSettings.reflectionIntensity+"; fog="+RenderSettings.fog+"; fogColor="+RenderSettings.fogColor.ToString("R")+"; skybox="+(RenderSettings.skybox?RenderSettings.skybox.name:"none")+"; volumes="+FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length);
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))s.AppendLine("light="+light.name+"; type="+light.type+"; intensity="+light.intensity+"; color="+light.color.ToString("R")+"; shadows="+light.shadows+"; strength="+light.shadowStrength+"; position="+light.transform.position.ToString("R")+"; rotation="+light.transform.rotation.ToString("R"));
            foreach(var material in materials)
            {
                s.AppendLine("shader="+material.shader.name+"; keywords="+string.Join(",",material.shaderKeywords)+"; queue="+material.renderQueue+"; instancing="+material.enableInstancing);
                for(int n=0;n<material.shader.GetPropertyCount();n++)
                {
                    var name=material.shader.GetPropertyName(n);var type=material.shader.GetPropertyType(n);
                    if(type==UnityEngine.Rendering.ShaderPropertyType.Color)s.AppendLine(name+"="+material.GetColor(name).ToString("R"));
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)s.AppendLine(name+"="+material.GetVector(name).ToString("R"));
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Float||type==UnityEngine.Rendering.ShaderPropertyType.Range)s.AppendLine(name+"="+material.GetFloat(name).ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            foreach(var renderer in renderers)
            {
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)continue;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    foreach(var v in mesh.vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}foreach(var n in mesh.normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}foreach(var uv in mesh.uv){writer.Write(uv.x);writer.Write(uv.y);}foreach(var i in mesh.triangles)writer.Write(i);
                    writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())s.AppendLine("mesh="+mesh.name+"; SHA256="+BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","")+"; position="+renderer.transform.position.ToString("R")+"; rotation="+renderer.transform.rotation.ToString("R")+"; scale="+renderer.transform.lossyScale.ToString("R"));
                }
            }
            return s.ToString();
        }
'''
code=code[:idx]+helper+code[idx:];fixture.write_text(code,encoding='utf-8')
evidence=r/'Integration/.artifacts/WaterBand-20261010'; evidence.mkdir(parents=True,exist_ok=True)
settings=[*sorted((r/'Integration/Assets/Settings').rglob('*.asset')),*sorted((r/'Integration/ProjectSettings').glob('*.asset'))]
(evidence/'candidate-provenance.json').write_text(json.dumps({'originalShaderSHA256':hashlib.sha256((source/'Unity/RiverWater.shader').read_bytes()).hexdigest(),'diagnosticShaderSHA256':hashlib.sha256(shader.read_bytes()).hexdigest(),'settingsSHA256':{p.relative_to(r/'Integration').as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in settings},'RiversCommit':subprocess.check_output(['git','-C',str(r/'Rivers'),'rev-parse','HEAD'],text=True).strip(),'waterTime':4,'modes':['baseline','foam-off','specular-off','environment-off','all-specular-off','roughness','normals','ripple-normals'],'sharedPackagesUnmodified':True},indent=2))
print('Prepared isolated diagnostic package and ignored consumer capture fixture; canonical files backed up.')
