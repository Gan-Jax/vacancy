using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Vacancy
{
    public sealed class HumanMotionDriver : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        float walkWeight;

        public void Bind(Animator animator, AnimationClip idle, AnimationClip walk)
        {
            if (idle == null || walk == null || animator == null) return;

            if (graph.IsValid()) graph.Destroy();

            graph = PlayableGraph.Create(name + "-Motions");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            mixer = AnimationMixerPlayable.Create(graph, 2);
            var idlePlayable = AnimationClipPlayable.Create(graph, idle);
            var walkPlayable = AnimationClipPlayable.Create(graph, walk);
            idlePlayable.SetApplyFootIK(true);
            walkPlayable.SetApplyFootIK(true);
            graph.Connect(idlePlayable, 0, mixer, 0);
            graph.Connect(walkPlayable, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);

            var output = AnimationPlayableOutput.Create(graph, "Human", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public void SetWalking(bool walking, float dt)
        {
            if (!mixer.IsValid()) return;
            walkWeight = Mathf.MoveTowards(walkWeight, walking ? 1f : 0f, dt * 6f);
            mixer.SetInputWeight(0, 1f - walkWeight);
            mixer.SetInputWeight(1, walkWeight);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
