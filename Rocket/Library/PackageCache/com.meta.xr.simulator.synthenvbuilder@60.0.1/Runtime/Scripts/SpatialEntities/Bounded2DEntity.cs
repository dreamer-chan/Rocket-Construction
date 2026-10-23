// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace SynthEnvBuilder
{
    [ExecuteInEditMode]
    public class Bounded2DEntity : SpatialEntity
    {
        [HideInInspector]
        public Vector3 Center;
        [HideInInspector]
        public Bounds LocalBounds; // will be serialized and available to SpatialEntityManager

        // for scene gizmo purposes
        private Mesh mesh;

        protected override void Awake()
        {
            base.Awake();
            mesh = Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
        }

        void OnDrawGizmos()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning("Attempting to designate an invisible object as a Bounded2D entity.");
                return;
            }
            Center = meshRenderer.bounds.center; // center in global coordinates
            LocalBounds = meshRenderer.localBounds; // not used for Scene Gizmos; only for return data

            Gizmos.color = new Color(1.0f, 0.92f, 0.016f, 0.1f); // translucent yellow
            Gizmos.DrawWireMesh(mesh, transform.position, transform.rotation, transform.lossyScale);
        }
    }
}
