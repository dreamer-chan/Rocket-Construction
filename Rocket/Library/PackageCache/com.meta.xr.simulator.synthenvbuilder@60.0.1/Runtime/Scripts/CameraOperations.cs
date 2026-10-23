/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Google.Protobuf;
using Synthenv;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Experimental.Rendering;

namespace SynthEnvBuilder
{
    public class CameraOperations
    {
        public static PostProcessResources postProcessResources = Resources.Load<PostProcessResources>("PostProcessResources");

        private static Material depthCopyMaterial_ = null;

        public static void CreateCameraPair(
            PassthroughFrameResolution resolution,
            PassthroughFrameColorMode colorMode,
            out Camera[] cameras)
        {
            // initialize the material for blitting
            if (depthCopyMaterial_ == null)
            {
                // NOTE: This MUST be referenced properly in the ProjectSettings or standalone will break
                depthCopyMaterial_ = new Material(Shader.Find("Custom/CopyDepthShader"));
                if (depthCopyMaterial_ == null)
                {
                    Debug.LogError("Custom/CopyDepthShader is NOT found!");
                }
            }

            cameras = new Camera[2];

            RenderTextureDescriptor desc = new RenderTextureDescriptor((int)resolution.Width, (int)resolution.Height, RenderTextureFormat.ARGB32, 32);
            desc.sRGB = true;
            desc.depthStencilFormat = GraphicsFormat.D16_UNorm;
            desc.mipCount = 1;

            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i] = new GameObject().AddComponent<Camera>();
                RenderTexture renderTexture = new RenderTexture(desc);
                renderTexture.Create();
                cameras[i].targetTexture = renderTexture;
                cameras[i].depthTextureMode = DepthTextureMode.Depth; // enabled depth rendering
                cameras[i].cullingMask &= ~(1 << LayerMask.NameToLayer("Position Marks")); // hide position marks
                if (colorMode == PassthroughFrameColorMode.Grayscale)
                {
                    AddGrayscalePostEffect(cameras[i]);
                }
                cameras[i].enabled = false;
            }
        }

        public static void DestroyCameraPair(Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                Object.Destroy(camera.gameObject);
            }
        }

        public static void AddGrayscalePostEffect(Camera camera)
        {
            var postProcessLayer = camera.gameObject.AddComponent<PostProcessLayer>();
            postProcessLayer.Init(postProcessResources);
            postProcessLayer.volumeLayer = LayerMask.GetMask("Default");
            postProcessLayer.enabled = true;
        }

        public static void UpdateCameraPose(Camera camera, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
        }

        public static void UpdateCameraFov(Camera camera, Synthenv.Fov fov)
        {
            // TODO: Update only when fov is actually changed. This could save a lot of unnecessary computation after stream is introduced.
            float leftDistance = Mathf.Tan(fov.AngleLeft) * camera.nearClipPlane;
            float rightDistance = Mathf.Tan(fov.AngleRight) * camera.nearClipPlane;
            float upDistance = Mathf.Tan(fov.AngleUp) * camera.nearClipPlane;
            float downDistance = Mathf.Tan(fov.AngleDown) * camera.nearClipPlane;

            Matrix4x4 projectionMatrix = Utils.PerspectiveOffCenter(leftDistance, rightDistance, downDistance, upDistance, camera.nearClipPlane, camera.farClipPlane);
            camera.projectionMatrix = projectionMatrix;
        }

        public static void UpdateCameraView(Camera camera, View view)
        {
            UnityEngine.Vector3 position = Utils.FromOpenXRVector3f(view.Pose.Position);
            UnityEngine.Quaternion orientation = Utils.FromOpenXRQuaternion(view.Pose.Orientation);
            Fov fov = view.Fov;
            UpdateCameraPose(camera, position, orientation);
            UpdateCameraFov(camera, fov);
        }

        public static ByteString[] RenderViewsIntoImages(Camera[] cameras, Texture2D jpgTexture, Texture2D depthTexture = null)
        {
            ByteString[] encodedImgs = new ByteString[SyntheticEnvironmentServer.kNumEyes * 2];
            var leftEyeCamera = cameras[0];

            RenderTexture.active = leftEyeCamera.targetTexture;
            leftEyeCamera.Render();
            jpgTexture.ReadPixels(new Rect(0, 0, leftEyeCamera.targetTexture.width, leftEyeCamera.targetTexture.height), 0, 0);
            jpgTexture.Apply();
            encodedImgs[0] = ByteString.CopyFrom(jpgTexture.EncodeToJPG());

            // TODO: determine what is rendered by XrSim's debug window selection
            encodedImgs[1] = ByteString.Empty;
            encodedImgs[3] = ByteString.Empty;

            if (depthTexture != null)
            {
                // make temporary RenderTexture for blitting D16_UNorm depth (Unity takes care of mem management)
                RenderTexture tmpDepthTexture = RenderTexture.GetTemporary(leftEyeCamera.targetTexture.width, leftEyeCamera.targetTexture.height, 0, RenderTextureFormat.R16);

                // Blit the depth buffer from the camera's targetTexture to the temporary texture
                Graphics.Blit(leftEyeCamera.targetTexture, tmpDepthTexture, depthCopyMaterial_);

                depthTexture.ReadPixels(new Rect(0, 0, leftEyeCamera.targetTexture.width, leftEyeCamera.targetTexture.height), 0, 0);
                depthTexture.Apply();
                encodedImgs[2] = ByteString.CopyFrom(depthTexture.EncodeToEXR());
            }

            return encodedImgs;
        }

        public static void RenderViewsIntoGpuHandles(Camera[] cameras, Texture2D[] textures, Texture2D[] depthTextures = null)
        {
            for (int eye = 0; eye < SyntheticEnvironmentServer.kNumEyes; eye++)
            {
                RenderTexture.active = cameras[eye].targetTexture;
                cameras[eye].Render();
                SharedTexture.VerticallyFlipRenderTexture(cameras[eye].targetTexture);
                Graphics.CopyTexture(cameras[eye].targetTexture, textures[eye]);

                if (depthTextures == null)
                {
                    continue;
                }

                if (cameras[eye].targetTexture.depthStencilFormat != GraphicsFormat.D16_UNorm)
                {
                    Debug.LogError("D16_UNorm is not supported on this platform!");
                    continue;
                }

                // make temporary RenderTexture for blitting D16_UNorm depth (Unity takes care of mem management)
                RenderTexture tmpDepthTexture = RenderTexture.GetTemporary(cameras[eye].targetTexture.width, cameras[eye].targetTexture.height, 0, RenderTextureFormat.R16);

                // Blit the depth buffer from the camera's targetTexture to the temporary texture
                Graphics.Blit(cameras[eye].targetTexture, tmpDepthTexture, depthCopyMaterial_);

                // flip it vertically
                SharedTexture.VerticallyFlipRenderTexture(tmpDepthTexture);

                // copy to the external depth texture
                Graphics.CopyTexture(tmpDepthTexture, depthTextures[eye]);

                // release temporary texture
                RenderTexture.ReleaseTemporary(tmpDepthTexture);
            }
        }
    }
}
