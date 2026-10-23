// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace SynthEnvBuilder
{
    [ExecuteInEditMode]
    public class Bounded3DEntity : SpatialEntity
    {
        public Vector3 Center;
        public Vector3 Size;
        [HideInInspector]
        public Vector3 GlobalBoundingBoxSize;
        [HideInInspector, SerializeField]
        private bool Initialized = false;

        // for scene gizmo purposes
        private Mesh mesh;
        // for future: create the bounds from another (meaningful) GameObject

        protected override void Awake()
        {
            base.Awake();
            mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (!Initialized)
            {
                CalculateBoundingBox();
                Initialized = true;
            }
        }

        public void OnDrawGizmos()
        {
            if (Center != null && Size != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f); // translucent red
                GlobalBoundingBoxSize = UnityEngine.Vector3.Scale(transform.lossyScale, Size);
                Gizmos.DrawWireMesh(mesh, Center, transform.rotation, GlobalBoundingBoxSize);
            }
        }

        public void CalculateBoundingBox()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning("Attempting to designate an invisible object as a Bounded3D entity.");
                return;
            }
            Center = meshRenderer.bounds.center;
            Size = meshRenderer.localBounds.size;
        }
    }
}
