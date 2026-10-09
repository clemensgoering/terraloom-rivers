using System;
using System.Collections.Generic;
using System.Linq;
using TerraLoom.Core.Editor;
using TerraLoom.Core.Unity;
using TerraLoom.Rivers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TerraLoom.Rivers.Editor
{
    /// <summary>Optional workbench provider. Reads live plans or saved preview data without replanning.</summary>
    public sealed class RiversWorkbenchExtension : WorldModuleEditorExtension
    {
        public override string Id => "rivers";
        public override string Title => "Rivers";
        public override string Icon => "Rivers";
        public override int Order => 200;
        public override Color Color => new Color(.12f, .72f, .87f);
        private readonly Dictionary<int, Preview> previews = new Dictionary<int, Preview>();
        private string lastSceneError;
        private const int MaxLines = 2048;

        public override Component[] Find(TerraLoomWorld world) => !world ? Array.Empty<Component>() :
            Object.FindObjectsByType<TerraLoomRivers>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .Where(x => x.World == world).Cast<Component>().ToArray();

        public override Component Add(TerraLoomWorld world, bool separateObject)
        {
            if (!world) throw new ArgumentNullException(nameof(world));
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add TerraLoom Rivers");
            try
            {
                var host = world.gameObject;
                if (separateObject)
                {
                    host = new GameObject("Rivers");
                    SceneManager.MoveGameObjectToScene(host, world.gameObject.scene);
                    Undo.RegisterCreatedObjectUndo(host, "Create Rivers module");
                    Undo.SetTransformParent(host.transform, world.transform, "Parent Rivers module");
                    host.transform.localPosition = Vector3.zero; host.transform.localRotation = Quaternion.identity; host.transform.localScale = Vector3.one;
                }
                var rivers = host.GetComponent<TerraLoomRivers>();
                if (rivers && rivers.World && rivers.World != world) throw new InvalidOperationException("Existing Rivers belongs to another world. Add a separate module object.");
                if (!rivers) rivers = Undo.AddComponent<TerraLoomRivers>(host);
                Undo.RecordObject(rivers, "Bind Rivers world"); rivers.World = world;
                Undo.RecordObject(host, "Set Rivers icon"); TerraLoomEditorIcons.Apply(host, Icon);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rivers);
                EditorUtility.SetDirty(rivers); EditorSceneManager.MarkSceneDirty(host.scene);
                Undo.CollapseUndoOperations(group); return rivers;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        public override void DrawInspector(Component component)
        { if (component is TerraLoomRivers) WorldWorkbenchGUI.DrawComponentInspector(component); }

        public override void DrawScene(TerraLoomWorld world)
        {
            if (!world || !TerraLoomSceneSettings.Rivers || Event.current == null || Event.current.type != EventType.Repaint) return;
            try
            {
                int remaining = MaxLines;
                foreach (int key in previews.Where(p => !p.Value.Owner).Select(p => p.Key).ToArray()) previews.Remove(key);
                using (new Handles.DrawingScope(Color, Matrix4x4.identity))
                foreach (var component in Find(world))
                {
                    if (remaining <= 0) break;
                    var rivers = (TerraLoomRivers)component;
                    if (rivers.LastPlan != null && rivers.LastPlan.Routes.Count > 0)
                    {
                        foreach (var route in rivers.LastPlan.Routes)
                        {
                            if (remaining <= 0) break;
                            DrawRoute(route.WaterPolyline.Count, i => Point(route.WaterPolyline[i]), rivers.Width, 0,
                                route.Request.SourceId, route.Request.MouthId, ref remaining);
                            DrawBanks(route.LeftBankPolyline.Count, i=>Point(route.LeftBankPolyline[i]),
                                i=>Point(route.RightBankPolyline[i]), ref remaining);
                        }
                    }
                    else
                    {
                        var preview = GetPreview(rivers);
                        if (preview.Routes != null && preview.Routes.Length > 0)
                        {
                            foreach (var route in preview.Routes)
                            {
                                if (remaining <= 0) break;
                                DrawRoute(route.Points.Length, i => route.Points[i], rivers.Width, 0,
                                    route.Id + " source (saved)", route.Id + " mouth (saved)", ref remaining);
                                DrawBanks(route.Points.Length, i=>route.Left[i], i=>route.Right[i], ref remaining);
                            }
                        }
                        else DrawBounds(rivers, ref remaining);
                    }
                }
                lastSceneError = null;
            }
            catch (Exception ex)
            {
                if (lastSceneError != ex.Message) Debug.LogWarning("Rivers scene preview skipped: " + ex.Message, world);
                lastSceneError = ex.Message;
            }
        }
        private void DrawRoute(int count, Func<int, Vector3> point, float width, float bankWidth, string source, string mouth, ref int remaining)
        {
            if (count < 2) return;
            Label(point(0), source + " →"); Label(point(count - 1), "→ " + mouth);
            float half = Finite(width) ? Mathf.Clamp(width / 2, 0, 10000) : 0;
            float outer = half + (Finite(bankWidth) ? Mathf.Clamp(bankWidth, 0, 10000) : 0);
            float distanceToArrow = 0;
            for (int i = 0; i < count - 1 && remaining > 0; i++)
            {
                var a = point(i); var b = point(i + 1);
                if (!Finite(a) || !Finite(b)) continue;
                var direction = b - a; if (direction.sqrMagnitude < .000001f) continue;
                Handles.color = Color; Line(a, b, ref remaining);
                var horizontal = new Vector3(direction.x, 0, direction.z);
                var side = horizontal.sqrMagnitude > .000001f ? new Vector3(horizontal.z, 0, -horizontal.x).normalized : Vector3.right;
                if (TerraLoomSceneSettings.Footprints && half > 0)
                {
                    Line(a - side * half, b - side * half, ref remaining); Line(a + side * half, b + side * half, ref remaining);
                    Line(a - side * half, a + side * half, ref remaining); Line(b - side * half, b + side * half, ref remaining);
                    if (outer > half)
                    {
                        Handles.color = new Color(.72f, .5f, .27f);
                        Line(a - side * outer, b - side * outer, ref remaining); Line(a + side * outer, b + side * outer, ref remaining);
                        Line(a - side * outer, a + side * outer, ref remaining); Line(b - side * outer, b + side * outer, ref remaining);
                    }
                }
                distanceToArrow += direction.magnitude;
                if (TerraLoomSceneSettings.Flows && (i == 0 || distanceToArrow >= 8))
                {
                    Handles.color = Color;
                    float size = Mathf.Clamp(half, .4f, 1.5f);
                    var tip = (a + b) * .5f; var tail = tip - direction.normalized * size;
                    Line(tip, tail + side * size * .5f, ref remaining); Line(tip, tail - side * size * .5f, ref remaining);
                    distanceToArrow = 0;
                }
            }
        }
        private void DrawBounds(TerraLoomRivers rivers, ref int remaining)
        {
            if (!rivers.GeneratedRoot) return;
            foreach (var marker in rivers.GeneratedRoot.GetComponentsInChildren<RiverGeneratedGeometry>(true))
            {
                if (remaining <= 0) break;
                var filter = marker.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                // Bounds do not encode source/mouth order: do not invent flow arrows after plan loss.
                var bounds = filter.sharedMesh.bounds; var t = filter.transform;
                Handles.color = marker.Role == RiverGeometryRole.Water ? Color : new Color(.72f, .5f, .27f);
                Label(t.TransformPoint(bounds.center), marker.name + " (baked bounds)");
                if (TerraLoomSceneSettings.Footprints) Box(bounds, t, ref remaining);
                else if (marker.Role == RiverGeometryRole.Water)
                {
                    var axis = bounds.size.x > bounds.size.z ? Vector3.right * bounds.extents.x : Vector3.forward * bounds.extents.z;
                    Line(t.TransformPoint(bounds.center - axis), t.TransformPoint(bounds.center + axis), ref remaining);
                }
            }
        }
        private static void DrawBanks(int count, Func<int,Vector3> left, Func<int,Vector3> right, ref int remaining)
        {
            if (!TerraLoomSceneSettings.Footprints || count<2) return;
            Handles.color = new Color(.72f,.5f,.27f);
            for (int i=1;i<count && remaining>0;i++)
            { Line(left(i-1),left(i),ref remaining); Line(right(i-1),right(i),ref remaining); }
            Label(left(0),"Local bank seam (captured terrain)");
        }
        private Preview GetPreview(TerraLoomRivers owner)
        {
            int key = owner.GetInstanceID();
            if (previews.TryGetValue(key, out var cached) && cached.Owner == owner && cached.Json == owner.PlanJson) return cached;
            var preview = new Preview { Owner = owner, Json = owner.PlanJson }; previews[key] = preview;
            if (string.IsNullOrWhiteSpace(preview.Json) || preview.Json.Length > 32000000) return preview;
            try
            {
                var document = JsonUtility.FromJson<Document>(preview.Json);
                if (document == null || document.format != 2 || document.routes == null || document.routes.Length > 1024) return preview;
                var routes = new List<PreviewRoute>(); int points = 0;
                foreach (var route in document.routes)
                {
                    if (route == null || route.water == null || route.water.Length < 2
                        || route.leftBank == null || route.rightBank == null
                        || route.leftBank.Length != route.water.Length || route.rightBank.Length != route.water.Length) return preview;
                    points += route.water.Length*3; if (points > 200000) return preview;
                    var positions = new Vector3[route.water.Length];
                    var left = new Vector3[positions.Length]; var right = new Vector3[positions.Length];
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var p = route.water[i]; if (p == null) return preview;
                        positions[i] = new Vector3((float)p.x, (float)p.y, (float)p.z); if (!Finite(positions[i])) return preview;
                        var l=route.leftBank[i];var r=route.rightBank[i];if(l==null||r==null)return preview;
                        left[i]=new Vector3((float)l.x,(float)l.y,(float)l.z);
                        right[i]=new Vector3((float)r.x,(float)r.y,(float)r.z);
                        if(!Finite(left[i])||!Finite(right[i]))return preview;
                    }
                    routes.Add(new PreviewRoute { Points = positions, Left = left, Right = right, Id = route.id });
                }
                preview.Routes = routes.ToArray();
            }
            catch (Exception) { /* Saved preview is optional; use mesh bounds on malformed JSON. */ }
            return preview;
        }
        private sealed class Preview { public TerraLoomRivers Owner; public string Json; public PreviewRoute[] Routes; }
        private sealed class PreviewRoute { public Vector3[] Points, Left, Right; public string Id; }
        [Serializable] private sealed class Document { public int format; public SavedRoute[] routes; }
        [Serializable] private sealed class SavedRoute { public string id; public SavedPoint[] water, leftBank, rightBank; }
        [Serializable] private sealed class SavedPoint { public double x, y, z; }
        private static Vector3 Point(TerraLoom.Core.WorldPoint point) => new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        private static void Label(Vector3 p, string text)
        { if (TerraLoomSceneSettings.Labels && Finite(p)) Handles.Label(p + Vector3.up * .12f, text, EditorStyles.miniBoldLabel); }
        private static void Line(Vector3 a, Vector3 b, ref int remaining)
        { if (remaining > 0 && Finite(a) && Finite(b)) { Handles.DrawLine(a, b); remaining--; } }
        private static void Box(Bounds bounds, Transform transform, ref int remaining)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++) corners[i] = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            for (int i = 0; i < 8; i++) for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) Line(corners[i], corners[i | bit], ref remaining);
        }
    }
}
