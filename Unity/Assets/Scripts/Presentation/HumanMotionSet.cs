using UnityEngine;

namespace Vacancy
{
    [CreateAssetMenu(menuName = "Vacancy/Human Motion Set")]
    public sealed class HumanMotionSet : ScriptableObject
    {
        public GameObject malePrefab;
        public GameObject femalePrefab;
        public AnimationClip maleIdle;
        public AnimationClip maleWalk;
        public AnimationClip femaleIdle;
        public AnimationClip femaleWalk;

        public bool IsComplete =>
            malePrefab != null && femalePrefab != null &&
            maleIdle != null && maleWalk != null &&
            femaleIdle != null && femaleWalk != null;

        public GameObject Prefab(bool feminine) => feminine ? femalePrefab : malePrefab;
        public AnimationClip Idle(bool feminine) => feminine ? femaleIdle : maleIdle;
        public AnimationClip Walk(bool feminine) => feminine ? femaleWalk : maleWalk;
    }
}
