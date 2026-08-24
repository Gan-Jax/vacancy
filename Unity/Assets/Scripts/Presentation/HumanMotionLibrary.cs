using UnityEngine;

namespace Vacancy
{
    /// <summary>
    /// Loads Kevin Iglesias Human Basic Motions dummies and in-place clips.
    /// Prefers a Resources asset so builds work; falls back to project paths in the Editor.
    /// </summary>
    public static class HumanMotionLibrary
    {
        const string MalePrefabPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_M.prefab";
        const string FemalePrefabPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Prefabs/Human_BasicMotionsDummy_F.prefab";
        const string MaleIdlePath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
        const string MaleWalkPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        const string FemaleIdlePath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
        const string FemaleWalkPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/HumanF@Walk01_Forward.fbx";

        static bool attempted;
        static HumanMotionSet loaded;

        public static bool TryGet(out HumanMotionSet set)
        {
            if (!attempted)
            {
                attempted = true;
                loaded = Resources.Load<HumanMotionSet>("HumanMotions");
                if (loaded == null || !loaded.IsComplete)
                {
                    loaded = LoadFromProject();
                }
            }

            set = loaded;
            return set != null && set.IsComplete;
        }

        static HumanMotionSet LoadFromProject()
        {
#if UNITY_EDITOR
            var set = ScriptableObject.CreateInstance<HumanMotionSet>();
            set.malePrefab = Load<GameObject>(MalePrefabPath);
            set.femalePrefab = Load<GameObject>(FemalePrefabPath);
            set.maleIdle = LoadClip(MaleIdlePath);
            set.maleWalk = LoadClip(MaleWalkPath);
            set.femaleIdle = LoadClip(FemaleIdlePath);
            set.femaleWalk = LoadClip(FemaleWalkPath);
            return set.IsComplete ? set : null;
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        static T Load<T>(string path) where T : Object
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static AnimationClip LoadClip(string path)
        {
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview", System.StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }
#endif
    }
}
