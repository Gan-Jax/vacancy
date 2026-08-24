using System;
using UnityEngine;

namespace Vacancy
{
    /// <summary>
    /// Guest, staff, and first-person body. Prefers Kevin Iglesias Human Basic
    /// Motions dummies; falls back to runtime primitives if the pack is missing.
    /// </summary>
    public sealed class CharacterModel
    {
        const float FirstPersonBack = 0.22f;

        public Transform Root { get; private set; }
        public GameObject GameObject => Root.gameObject;

        readonly Func<Color, Material> material;
        readonly Renderer[] clothes;
        readonly Material[] clothingMats;
        readonly Transform leftArm;
        readonly Transform rightArm;
        readonly Transform leftLeg;
        readonly Transform rightLeg;
        readonly HumanMotionDriver motions;
        readonly Transform visual;
        readonly bool firstPerson;
        readonly bool humanoid;
        Vector3 lastPos;
        float walkPhase;

        CharacterModel(
            Transform root,
            Func<Color, Material> material,
            Renderer[] clothes,
            Material[] clothingMats,
            Transform leftArm,
            Transform rightArm,
            Transform leftLeg,
            Transform rightLeg,
            HumanMotionDriver motions,
            bool firstPerson,
            bool humanoid,
            Transform visual = null)
        {
            Root = root;
            this.material = material;
            this.clothes = clothes;
            this.clothingMats = clothingMats;
            this.leftArm = leftArm;
            this.rightArm = rightArm;
            this.leftLeg = leftLeg;
            this.rightLeg = rightLeg;
            this.motions = motions;
            this.visual = visual != null && visual != root ? visual : null;
            this.firstPerson = firstPerson;
            this.humanoid = humanoid;
            lastPos = root.position;
        }

        public static CharacterModel BuildNpc(Transform parent, string name, Func<Color, Material> material, bool feminine = false)
        {
            return Build(parent, name, material, firstPerson: false, feminine);
        }

        public static CharacterModel BuildFirstPerson(Transform parent, Camera camera, Func<Color, Material> material)
        {
            var model = Build(parent, "Player", material, firstPerson: true, feminine: false);
            if (!model.humanoid && camera != null)
            {
                model.leftArm.SetParent(camera.transform, false);
                model.rightArm.SetParent(camera.transform, false);
                model.leftArm.localPosition = new Vector3(-0.28f, -0.32f, 0.42f);
                model.rightArm.localPosition = new Vector3(0.28f, -0.32f, 0.42f);
                model.leftArm.localRotation = Quaternion.Euler(18f, 8f, 12f);
                model.rightArm.localRotation = Quaternion.Euler(18f, -8f, -12f);
            }

            return model;
        }

        static CharacterModel Build(Transform parent, string name, Func<Color, Material> material, bool firstPerson, bool feminine)
        {
            if (HumanMotionLibrary.TryGet(out var motions) &&
                TryBuildHumanoid(parent, name, motions, firstPerson, feminine, out var humanoid))
            {
                return humanoid;
            }

            return BuildPrimitive(parent, name, material, firstPerson);
        }

        static bool TryBuildHumanoid(
            Transform parent,
            string name,
            HumanMotionSet set,
            bool firstPerson,
            bool feminine,
            out CharacterModel model)
        {
            model = null;
            var prefab = set.Prefab(feminine);
            var idle = set.Idle(feminine);
            var walk = set.Walk(feminine);
            if (prefab == null || idle == null || walk == null) return false;

            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);

            var instance = UnityEngine.Object.Instantiate(prefab, pivot);
            instance.name = "Body";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            foreach (var proxy in instance.GetComponentsInChildren<KevinIglesias.SpineProxy>(true))
            {
                proxy.enabled = false;
            }

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;

            var driver = instance.GetComponent<HumanMotionDriver>();
            if (driver == null) driver = instance.AddComponent<HumanMotionDriver>();
            driver.Bind(animator, idle, walk);

            var clothes = instance.GetComponentsInChildren<Renderer>(true);

            var clothingMats = InstanceMaterials(clothes);
            if (firstPerson) HideFirstPersonHead(instance.transform);

            model = new CharacterModel(
                pivot,
                null,
                clothes,
                clothingMats,
                null,
                null,
                null,
                null,
                driver,
                firstPerson,
                humanoid: true,
                instance.transform);
            return true;
        }

        static CharacterModel BuildPrimitive(Transform parent, string name, Func<Color, Material> material, bool firstPerson)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);

            Color skin = Palette.Hex("#d8b49a");
            Color shoe = Palette.Hex("#2a2430");
            Color hair = Palette.Hex("#3a2a22");
            Color clothes = Palette.Player;

            var hips = Part(root, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), new Vector3(0.34f, 0.18f, 0.2f), clothes, material);
            var torso = Part(root, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.12f, 0f), new Vector3(0.38f, 0.48f, 0.22f), clothes, material);

            var leftLeg = Part(root, "LeftLeg", PrimitiveType.Capsule, new Vector3(-0.1f, 0.38f, 0f), new Vector3(0.14f, 0.36f, 0.14f), clothes, material).transform;
            var rightLeg = Part(root, "RightLeg", PrimitiveType.Capsule, new Vector3(0.1f, 0.38f, 0f), new Vector3(0.14f, 0.36f, 0.14f), clothes, material).transform;
            Part(leftLeg, "LeftShoe", PrimitiveType.Cube, new Vector3(0f, -0.42f, 0.04f), new Vector3(0.16f, 0.1f, 0.22f), shoe, material);
            Part(rightLeg, "RightShoe", PrimitiveType.Cube, new Vector3(0f, -0.42f, 0.04f), new Vector3(0.16f, 0.1f, 0.22f), shoe, material);

            var leftArm = Part(torso.transform, "LeftArm", PrimitiveType.Capsule, new Vector3(-0.28f, 0.04f, 0f), new Vector3(0.11f, 0.28f, 0.11f), skin, material).transform;
            var rightArm = Part(torso.transform, "RightArm", PrimitiveType.Capsule, new Vector3(0.28f, 0.04f, 0f), new Vector3(0.11f, 0.28f, 0.11f), skin, material).transform;

            if (!firstPerson)
            {
                var head = Part(torso.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(0.26f, 0.26f, 0.26f), skin, material);
                Part(head.transform, "Hair", PrimitiveType.Sphere, new Vector3(0f, 0.08f, -0.01f), new Vector3(0.28f, 0.16f, 0.26f), hair, material);
                Part(head.transform, "EyeL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.02f, 0.11f), new Vector3(0.05f, 0.05f, 0.04f), Color.white, material);
                Part(head.transform, "EyeR", PrimitiveType.Sphere, new Vector3(0.06f, 0.02f, 0.11f), new Vector3(0.05f, 0.05f, 0.04f), Color.white, material);
                Part(head.transform, "PupilL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.02f, 0.13f), new Vector3(0.025f, 0.025f, 0.02f), Color.black, material);
                Part(head.transform, "PupilR", PrimitiveType.Sphere, new Vector3(0.06f, 0.02f, 0.13f), new Vector3(0.025f, 0.025f, 0.02f), Color.black, material);
            }

            var clothesRenderers = new[]
            {
                hips.GetComponent<Renderer>(),
                torso.GetComponent<Renderer>()
            };

            return new CharacterModel(
                root,
                material,
                clothesRenderers,
                null,
                leftArm,
                rightArm,
                leftLeg,
                rightLeg,
                null,
                firstPerson,
                humanoid: false);
        }

        public void Recolor(Color outfit)
        {
            if (clothingMats != null)
            {
                foreach (var mat in clothingMats)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", outfit);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", outfit);
                }

                return;
            }

            if (material == null || clothes == null) return;
            var matShared = material(outfit);
            foreach (var renderer in clothes)
            {
                if (renderer != null) renderer.sharedMaterial = matShared;
            }
        }

        public void Place(float layoutX, float layoutY, float yawDegrees, float dt, float footY = 0f)
        {
            var pos = WorldScale.ToWorld(layoutX, layoutY, footY);
            Root.position = pos;
            if (!firstPerson) Root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            AlignVisual();
            Animate(pos, dt);
        }

        public void SyncFirstPerson(PlayerActor player, float dt)
        {
            if (!firstPerson || player == null) return;
            var pos = WorldScale.ToWorld(player.X, player.Y, player.FootY);
            var yaw = Quaternion.Euler(0f, player.Yaw, 0f);
            Root.position = humanoid ? pos - yaw * Vector3.forward * FirstPersonBack : pos;
            Root.rotation = yaw;
            AlignVisual();
            Animate(pos, dt);
        }

        void AlignVisual()
        {
            if (visual == null) return;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        void Animate(Vector3 pos, float dt)
        {
            float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z));
            lastPos = pos;
            bool walking = moved > 0.002f;

            if (motions != null)
            {
                motions.SetWalking(walking, dt);
                return;
            }

            walkPhase = walking ? walkPhase + dt * 8f : Mathf.MoveTowards(walkPhase, 0f, dt * 6f);
            float swing = walking ? Mathf.Sin(walkPhase) * 22f : 0f;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            if (firstPerson) return;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.7f, 0f, 8f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(swing * 0.7f, 0f, -8f);
        }

        static Material[] InstanceMaterials(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return Array.Empty<Material>();
            var list = new System.Collections.Generic.List<Material>();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mats = renderer.materials;
                renderer.materials = mats;
                foreach (var mat in mats)
                {
                    if (mat != null) list.Add(mat);
                }
            }

            return list.ToArray();
        }

        static void HideFirstPersonHead(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.gameObject.name;
                if (n.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    renderer.enabled = false;
                }
            }
        }

        static GameObject Part(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 scale,
            Color color,
            Func<Color, Material> material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
