
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
#endif

namespace UdonSharp.Examples.Inspectors
{
    [AddComponentMenu("")]
    public class CustomInspectorChildBehaviour : UdonSharpBehaviour
    {
        public CustomInspectorBehaviour parentBehaviour;
        public int myIndex;

        float sinHeight;

        private void Update()
        {
            int totalChildCount = parentBehaviour.childBehaviours.Length;

            float orbitAmount = (myIndex / (float)totalChildCount) * Mathf.PI * 2 + Time.time * 0.4f; // 0.4 rotations per second

            transform.position = parentBehaviour.orbitOrigin + new Vector3(Mathf.Sin(orbitAmount), 0f, Mathf.Cos(orbitAmount));

            sinHeight = Mathf.Sin(orbitAmount);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // RootOnly update will only copy the data for this behaviour from Udon to the proxy
            this.UpdateProxy(ProxySerializationPolicy.RootOnly);

            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * sinHeight * 0.6f);
            Gizmos.DrawCube(transform.position, Vector3.one * 0.2f);
        }
#endif
    }
}
