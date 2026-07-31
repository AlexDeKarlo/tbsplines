using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TBSplineS;

namespace TBSplineS.Editor
{
    public static class TbsExamplesBuilder
    {
        const string ScenePath = "Assets/TBSplineS/Examples/TBSplineS Examples.unity";
        const string MaterialsDir = "Assets/TBSplineS/Examples/Materials";
        const string TexturesDir = "Assets/TBSplineS/Examples/Textures";
        const float CellWidth = 26f;

        static TbsSplineComputer _computer;

        static Vector3 Cell(int index) => new Vector3(-104f + index * CellWidth, 0f, 0f);

        static Transform Section(Transform root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root);
            return go.transform;
        }

        [MenuItem("TBSplineS/Dev/Rebuild Examples Scene")]
        public static void Rebuild()
        {
            if (Application.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject previous = GameObject.Find("TBSplineS Examples");
            if (previous != null) Object.DestroyImmediate(previous);
            var root = new GameObject("TBSplineS Examples");

            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 55f, -70f);
                camera.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                camera.farClipPlane = 500f;
            }

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(24f, 1f, 6f);
            Paint(ground, GetMaterial("Ground", Color.white, "checker.png", new Vector2(48f, 12f)));

            var computerGo = new GameObject("Spline Computer");
            computerGo.transform.SetParent(root.transform);
            _computer = computerGo.AddComponent<TbsSplineComputer>();

            SectionRoad(Section(root.transform, "1 · ROAD"), 0);
            SectionRing(Section(root.transform, "2 · RING"), 1);
            SectionFork(Section(root.transform, "3 · FORK"), 2);
            SectionMesh(Section(root.transform, "4 · MESH"), 3);
            SectionFence(Section(root.transform, "5 · FENCE"), 4);
            SectionModifiers(Section(root.transform, "6 · MODIFIERS"), 5);
            SectionSurface(Section(root.transform, "7 · SURFACE"), 6);
            SectionPhysics(Section(root.transform, "8 · PHYSICS"), 7);
            SectionUtility(Section(root.transform, "9 · UTILITY"), 8);

            _computer.EnsureIds();
            _computer.Warmup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = computerGo;
            _computer = null;
        }

        static TbsSpline NewSpline(out int index)
        {
            var spline = new TbsSpline();
            _computer.AddSpline(spline);
            _computer.EnsureIds();
            index = _computer.SplineCount - 1;
            return spline;
        }

        static int SplineId(int index) => _computer[index].Id;

        static void SectionRoad(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline spline = _computer.Spline;
            spline.SetKnot(0, new TbsKnot(o + new Vector3(-11f, 0.5f, -9f)));
            spline.SetKnot(1, new TbsKnot(o + new Vector3(-4f, 2f, 3f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(4f, 0.5f, -6f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(11f, 1.5f, 7f)));
            spline.Type = TbsSplineType.CatmullRom;
            _computer.EnsureIds();
            TbsSplineEditorActions.SetKnotRoll(_computer, 0, 1, 28f);
            TbsSplineEditorActions.SetKnotRoll(_computer, 0, 2, -28f);
            float[] widths = { 1f, 1.7f, 0.7f, 1.3f };
            for (int i = 0; i < spline.Count && i < widths.Length; i++)
            {
                TbsKnot knot = spline[i];
                knot.Size = widths[i];
                spline.SetKnot(i, knot);
            }

            var roadGo = new GameObject("Road Mesh");
            roadGo.transform.SetParent(root);
            var roadGen = roadGo.AddComponent<TbsPathGenerator>();
            roadGen.Computer = _computer;
            roadGen.SplineId = SplineId(0);
            roadGen.Size = 3f;
            roadGen.GenerateMeshCollider = true;
            Paint(roadGo, GetMaterial("Road", Color.white, "asphalt.png", new Vector2(1f, 10f)));
            roadGen.RebuildImmediate();

            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Trigger Lamp";
            lamp.transform.SetParent(root);
            lamp.transform.position = o + new Vector3(0f, 4.5f, 0f);
            lamp.transform.localScale = Vector3.one * 1.2f;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            Paint(lamp, GetMaterial("Lamp", new Color(1f, 0.85f, 0.25f), null, null, true));
            lamp.SetActive(false);

            var triggerGo = new GameObject("Lamp Trigger (Spline Trigger)");
            triggerGo.transform.SetParent(root);
            triggerGo.transform.position = o + new Vector3(0f, 2.5f, 0f);
            var zone = triggerGo.AddComponent<TbsSplineTriggerZone>();
            zone.Computer = _computer;
            zone.SplineId = SplineId(0);
            zone.Position = 0.5f;
            zone.Direction = TbsTriggerType.Double;
            Component toggle = AddDemoComponent(triggerGo, "TbsDemoToggle");
            if (toggle != null)
            {
                toggle.GetType().GetField("Target").SetValue(toggle, lamp);
                var method = toggle.GetType().GetMethod("Toggle");
                var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), toggle, method);
                UnityEventTools.AddPersistentListener(zone.OnCrossed, action);
            }

            GameObject car = Prop(root, PrimitiveType.Cube, "Car (banking + speed zone)", new Vector3(1.4f, 0.6f, 2.4f), GetMaterial("Car", new Color(0.85f, 0.25f, 0.22f)));
            var follower = car.AddComponent<TbsSplineFollower>();
            follower.Computer = _computer;
            follower.SplineId = SplineId(0);
            follower.Speed = 7f;
            follower.EndMode = TbsFollowMode.PingPong;
            follower.SpeedRegions.Add(new TbsSpeedRegion { From = 0.4f, To = 0.6f, Value = 0.35f, Multiply = true });
            follower.Distance = 0f;
            Label(root, "ROAD · banking, speed zone, trigger, per-point width", cell);
        }

        static void SectionRing(Transform root, int cell)
        {
            Vector3 o = Cell(cell) + new Vector3(0f, 1.5f, 0f);
            TbsSpline spline = NewSpline(out int index);
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f;
                var knot = new TbsKnot(o + new Vector3(Mathf.Cos(angle) * 8f, Mathf.Sin(angle * 2f) * 0.8f, Mathf.Sin(angle) * 8f));
                knot.Color = Color.HSVToRGB(i / 6f, 0.65f, 1f);
                spline.AddKnot(knot);
            }
            spline.Closed = true;

            var tubeGo = new GameObject("Ring Tube");
            tubeGo.transform.SetParent(root);
            var tube = tubeGo.AddComponent<TbsTubeGenerator>();
            tube.Computer = _computer;
            tube.SplineId = SplineId(index);
            tube.Size = 0.8f;
            tube.Sides = 10;
            Paint(tubeGo, GetMaterial("Tube", Color.white, null, null, false, "Particles/Standard Unlit"));
            tube.RebuildImmediate();

            GameObject rider = Prop(root, PrimitiveType.Cube, "Ring Rider (loop)", new Vector3(0.7f, 0.7f, 1.4f), GetMaterial("Rider", new Color(0.95f, 0.62f, 0.2f)));
            var follower = rider.AddComponent<TbsSplineFollower>();
            follower.Computer = _computer;
            follower.SplineId = SplineId(index);
            follower.Speed = 6f;
            follower.EndMode = TbsFollowMode.Loop;
            follower.Distance = 0f;
            Label(root, "RING · closed tube, loop, per-point color", cell);
        }

        static void SectionFork(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline main = NewSpline(out int mainIndex);
            main.AddKnot(new TbsKnot(o + new Vector3(-11f, 0.5f, -6f)));
            main.AddKnot(new TbsKnot(o + new Vector3(0f, 0.5f, -6f)));
            main.AddKnot(new TbsKnot(o + new Vector3(11f, 0.5f, -6f)));
            TbsSpline branch = NewSpline(out int branchIndex);
            branch.AddKnot(new TbsKnot(o + new Vector3(0f, 0.5f, -6f)));
            branch.AddKnot(new TbsKnot(o + new Vector3(3f, 0.5f, 2f)));
            branch.AddKnot(new TbsKnot(o + new Vector3(9f, 0.5f, 8f)));
            _computer.EnsureIds();
            _computer.ConnectKnots(_computer.MakeRef(mainIndex, 1), _computer.MakeRef(branchIndex, 0));

            GameObject runner = Prop(root, PrimitiveType.Cube, "Fork Runner (junction switch, looped)", new Vector3(0.8f, 0.8f, 1.6f), GetMaterial("Runner", new Color(0.2f, 0.75f, 0.7f)));
            var follower = runner.AddComponent<TbsSplineFollower>();
            follower.Computer = _computer;
            follower.SplineId = SplineId(mainIndex);
            follower.Speed = 5f;
            follower.EndMode = TbsFollowMode.Stop;
            follower.Distance = 0f;
            AddDemoComponent(runner, "TbsDemoJunctionSwitcher");
            Label(root, "FORK · junction, auto branch switch", cell);
        }

        static void SectionMesh(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline spline = NewSpline(out int index);
            for (int i = 0; i < 5; i++)
                spline.AddKnot(new TbsKnot(o + new Vector3(-10f + i * 5f, 0.8f + i % 2 * 1.2f, Mathf.Sin(i * 1.6f) * 6f)));

            var meshGo = new GameObject("Spline Mesh (extruded cubes)");
            meshGo.transform.SetParent(root);
            var splineMesh = meshGo.AddComponent<TbsSplineMesh>();
            splineMesh.Computer = _computer;
            splineMesh.SplineId = SplineId(index);
            TbsSplineMeshChannel channel = splineMesh.Channels[0];
            channel.Type = TbsChannelType.Extrude;
            channel.Meshes = new[] { Resources.GetBuiltinResource<Mesh>("Cube.fbx") };
            channel.Count = 10;
            channel.Spacing = 0.25f;
            channel.MeshScale = new Vector3(2f, 0.5f, 1f);
            Paint(meshGo, GetMaterial("SplineMesh", new Color(0.9f, 0.5f, 0.25f)));
            splineMesh.RebuildImmediate();
            Label(root, "MESH · spline mesh, extrude channel", cell);
        }

        static void SectionFence(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline spline = NewSpline(out int index);
            spline.AddKnot(new TbsKnot(o + new Vector3(-11f, 0.4f, -7f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(-3f, 0.4f, 4f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(5f, 0.4f, -3f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(11f, 0.4f, 6f)));

            var postsGo = new GameObject("Fence Posts (place channel)");
            postsGo.transform.SetParent(root);
            var posts = postsGo.AddComponent<TbsSplineMesh>();
            posts.Computer = _computer;
            posts.SplineId = SplineId(index);
            TbsSplineMeshChannel channel = posts.Channels[0];
            channel.Type = TbsChannelType.Place;
            channel.Meshes = new[] { Resources.GetBuiltinResource<Mesh>("Cylinder.fbx") };
            channel.Count = 12;
            channel.MeshScale = new Vector3(0.18f, 0.8f, 0.18f);
            channel.MeshOffset = new Vector3(0f, 0.8f, 0f);
            Paint(postsGo, GetMaterial("FencePost", new Color(0.55f, 0.4f, 0.28f)));
            posts.RebuildImmediate();

            var railGo = new GameObject("Fence Rail (ribbon)");
            railGo.transform.SetParent(root);
            var rail = railGo.AddComponent<TbsPathGenerator>();
            rail.Computer = _computer;
            rail.SplineId = SplineId(index);
            rail.Size = 0.25f;
            var railOffset = rail.OffsetModifier.AddKey(new Vector2(0f, 1.5f), 0f, 1f);
            railOffset.CenterStart = 0f;
            railOffset.CenterEnd = 1f;
            Paint(railGo, GetMaterial("FenceRail", new Color(0.7f, 0.55f, 0.4f)));
            rail.RebuildImmediate();
            Label(root, "FENCE · place channel + offset modifier", cell);
        }

        static void SectionModifiers(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline spline = NewSpline(out int index);
            spline.AddKnot(new TbsKnot(o + new Vector3(-11f, 0.4f, 0f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(-4f, 0.4f, -4f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(4f, 0.4f, 4f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(11f, 0.4f, 0f)));

            var pathGo = new GameObject("Modifier Ribbon (size + offset keys)");
            pathGo.transform.SetParent(root);
            var path = pathGo.AddComponent<TbsPathGenerator>();
            path.Computer = _computer;
            path.SplineId = SplineId(index);
            path.Size = 1.2f;
            var size = path.SizeModifier.AddKey(2.4f, 0.25f, 0.75f);
            size.CenterStart = 0.4f;
            size.CenterEnd = 0.6f;
            var offset = path.OffsetModifier.AddKey(new Vector2(0f, 1.8f), 0.55f, 0.95f);
            offset.CenterStart = 0.4f;
            offset.CenterEnd = 0.6f;
            Paint(pathGo, GetMaterial("ModifierRibbon", new Color(0.62f, 0.4f, 0.85f)));
            path.RebuildImmediate();
            Label(root, "MODIFIERS · size + offset key regions", cell);
        }

        static void SectionSurface(Transform root, int cell)
        {
            Vector3 o = Cell(cell) + new Vector3(0f, 0.5f, 0f);
            TbsSpline spline = NewSpline(out int index);
            for (int i = 0; i < 5; i++)
            {
                float angle = i / 5f * Mathf.PI * 2f;
                spline.AddKnot(new TbsKnot(o + new Vector3(Mathf.Cos(angle) * 8f, 0f, Mathf.Sin(angle) * 8f)));
            }
            spline.Closed = true;

            var surfGo = new GameObject("Surface Platform");
            surfGo.transform.SetParent(root);
            var surface = surfGo.AddComponent<TbsSurfaceGenerator>();
            surface.Computer = _computer;
            surface.SplineId = SplineId(index);
            surface.Extrude = 1.5f;
            Paint(surfGo, GetMaterial("Surface", new Color(0.35f, 0.7f, 0.4f)));
            surface.RebuildImmediate();

            GameObject template = Prop(root, PrimitiveType.Capsule, "Prop Template", Vector3.one * 0.5f, GetMaterial("Prop", new Color(0.95f, 0.85f, 0.3f)));
            template.transform.position = o + new Vector3(0f, 1.6f, 0f);
            var propsGo = new GameObject("Props Along Outline");
            propsGo.transform.SetParent(root);
            var controller = propsGo.AddComponent<TbsObjectController>();
            controller.Computer = _computer;
            controller.SplineId = SplineId(index);
            controller.Objects = new[] { template };
            controller.SpawnCount = 8;
            controller.RebuildImmediate();
            Label(root, "SURFACE · fill + extrude, object controller", cell);
        }

        static void SectionPhysics(Transform root, int cell)
        {
            Vector3 o = Cell(cell);
            TbsSpline spline = NewSpline(out int index);
            spline.AddKnot(new TbsKnot(o + new Vector3(-11f, 6f, -4f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(-3f, 3.2f, 2f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(5f, 1.4f, -3f)));
            spline.AddKnot(new TbsKnot(o + new Vector3(11f, 0.4f, 3f)));

            var rampGo = new GameObject("Physics Ramp");
            rampGo.transform.SetParent(root);
            var ramp = rampGo.AddComponent<TbsPathGenerator>();
            ramp.Computer = _computer;
            ramp.SplineId = SplineId(index);
            ramp.Size = 3.5f;
            ramp.GenerateMeshCollider = true;
            Paint(rampGo, GetMaterial("Ramp", new Color(0.5f, 0.55f, 0.62f)));
            ramp.RebuildImmediate();

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Rolling Ball (respawns)";
            ball.transform.SetParent(root);
            ball.transform.position = o + new Vector3(-11f, 8f, -4f);
            ball.transform.localScale = Vector3.one * 1.1f;
            Paint(ball, GetMaterial("Ball", new Color(0.9f, 0.3f, 0.55f)));
            ball.AddComponent<Rigidbody>();
            AddDemoComponent(ball, "TbsDemoRespawn");
            Label(root, "PHYSICS · mesh collider ramp, rolling ball", cell);
        }

        static void SectionUtility(Transform root, int cell)
        {
            Vector3 o = Cell(cell) + new Vector3(0f, 1f, 0f);
            TbsSpline spline = NewSpline(out int index);
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f;
                spline.AddKnot(new TbsKnot(o + new Vector3(Mathf.Cos(angle) * 7f, 0f, Mathf.Sin(angle) * 7f)));
            }
            spline.Closed = true;

            GameObject marker = Prop(root, PrimitiveType.Sphere, "Positioner (30%)", Vector3.one * 0.9f, GetMaterial("Positioner", new Color(0.7f, 0.45f, 0.9f)));
            var positioner = marker.AddComponent<TbsSplinePositioner>();
            positioner.Computer = _computer;
            positioner.SplineId = SplineId(index);
            positioner.Position = 0.3f;
            positioner.RebuildImmediate();

            var orbitTarget = new GameObject("Orbit Target");
            orbitTarget.transform.SetParent(root);
            orbitTarget.transform.position = o + new Vector3(10f, 2f, 0f);
            Component orbit = AddDemoComponent(orbitTarget, "TbsDemoOrbit");
            if (orbit != null)
            {
                var type = orbit.GetType();
                type.GetField("Center").SetValue(orbit, o);
                type.GetField("Radius").SetValue(orbit, 10f);
                type.GetField("Height").SetValue(orbit, 2.5f);
            }

            GameObject shadow = Prop(root, PrimitiveType.Sphere, "Projector (follows orbit)", Vector3.one * 0.7f, GetMaterial("Projector", new Color(0.3f, 0.85f, 0.95f)));
            var projector = shadow.AddComponent<TbsSplineProjector>();
            projector.Computer = _computer;
            projector.SplineId = SplineId(index);
            projector.Source = orbitTarget.transform;

            var lengthGo = new GameObject("Length Calculator");
            lengthGo.transform.SetParent(root);
            var length = lengthGo.AddComponent<TbsLengthCalculator>();
            length.Computer = _computer;
            length.SplineId = SplineId(index);
            length.RebuildImmediate();
            Label(root, "UTILITY · positioner, projector, length", cell);
        }

        static GameObject Prop(Transform root, PrimitiveType type, string name, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root);
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Paint(go, material);
            return go;
        }

        static Component AddDemoComponent(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null)
            {
                Debug.LogWarning($"TBSplineS examples: demo script {typeName} not found");
                return null;
            }
            return go.AddComponent(type);
        }

        static Material GetMaterial(string name, Color color, string texture = null, Vector2? tiling = null, bool emission = false, string shader = "Standard")
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets/TBSplineS/Examples", "Materials");
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.shader == null || mat.shader.name != shader) mat.shader = Shader.Find(shader);
            mat.color = color;
            mat.mainTexture = texture != null
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + texture)
                : null;
            if (tiling.HasValue) mat.mainTextureScale = tiling.Value;
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * 1.6f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Paint(GameObject go, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        static void Label(Transform root, string text, int cell)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(root);
            go.transform.position = Cell(cell) + new Vector3(0f, 7.5f, 12f);
            AddDemoComponent(go, "TbsDemoLabel");

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Backdrop";
            back.transform.SetParent(go.transform, false);
            float width = Mathf.Max(6f, text.Length * 0.36f + 1.6f);
            back.transform.localScale = new Vector3(width, 1.7f, 0.06f);
            Object.DestroyImmediate(back.GetComponent<Collider>());
            Paint(back, GetMaterial("LabelBack", new Color(0.13f, 0.14f, 0.18f)));

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            var mesh = textGo.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = 0.13f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
        }
    }
}
