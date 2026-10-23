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

using UnityEngine;

namespace SynthEnvBuilder
{
    public class Utils
    {
        // Converts an OpenXR Vector3 to its Unity equivalent.
        public static UnityEngine.Vector3 FromOpenXRVector3f(Synthenv.Vector3 V)
        {
            // Flip the z-axis.
            return new UnityEngine.Vector3(V.X, V.Y, -V.Z);
        }

        public static Synthenv.Vector3 FromUnityVector3(UnityEngine.Vector3 V)
        {
            return new Synthenv.Vector3
            {
                X = V.x,
                Y = V.y,
                Z = -V.z
            };
        }

        // Converts an OpenXR Quaternion to its Unity equivalent.
        public static UnityEngine.Quaternion FromOpenXRQuaternion(Synthenv.Quaternion Q)
        {
            // Flip the z-axis.
            return new UnityEngine.Quaternion(-Q.X, -Q.Y, Q.Z, Q.W);
        }

        public static Synthenv.Quaternion FromUnityQuaternion(UnityEngine.Quaternion Q)
        {
            return new Synthenv.Quaternion
            {
                X = -Q.x,
                Y = -Q.y,
                Z = Q.z,
                W = Q.w
            };
        }

        public static Synthenv.Pose FromUnityPose(Vector3 position, Quaternion rotation)
        {
            return new Synthenv.Pose
            {
                // use the pose of the GameObject hosting this spatial entity
                Position = FromUnityVector3(position),
                Orientation = FromUnityQuaternion(rotation)
            };
        }

        public static UnityEngine.Pose FromOpenXRPose(Synthenv.Pose pose)
        {
            return new Pose()
            {
                position = FromOpenXRVector3f(pose.Position),
                rotation = FromOpenXRQuaternion(pose.Orientation)
            };
        }

        // Get the 3D bounding box size with respect to the SceneAPI reference frame
        // i.e., z is always pointing up, x and y are unrestricted.
        // cf. https://developer.oculus.com/documentation/unity/unity-scene-overview/ (bottom of the page)
        public static Vector3 Get3DBoundingBoxSize(Bounded3DEntity entity)
        {
            var sizeVectorRotation = Quaternion.Euler(
                entity.transform.rotation.eulerAngles.x, 0, entity.transform.rotation.eulerAngles.z);
            var transformedSize = sizeVectorRotation * entity.GlobalBoundingBoxSize;
            return new Vector3(
                Mathf.Abs(transformedSize.x), Mathf.Abs(transformedSize.y), Mathf.Abs(transformedSize.z));
        }

        // Get the pose of the SceneAPI reference frame in relation to the Unity reference frame
        // cf. https://developer.oculus.com/documentation/unity/unity-scene-overview/ (bottom of the page)
        public static Synthenv.Pose Get3DBoundingBoxPose(Bounded3DEntity entity)
        {
            var onFloorRotation = Quaternion.Euler(0, entity.transform.rotation.eulerAngles.y, 0);
            var boundingBoxSize = Get3DBoundingBoxSize(entity);

            // the anchor's position is the center of the top surface (so move up by half the height of the box)
            var anchorPosition = entity.Center +
                Vector3.Scale(boundingBoxSize, new Vector3(0, 0.5f, 0));
            // the anchor's rotation is always pointing up but unrestricted in the other two directions
            var anchorRotation = RotateToSceneApiCoordinates(onFloorRotation);

            return FromUnityPose(anchorPosition, anchorRotation);
        }

        // reference: https://developer.oculus.com/documentation/unity/unity-scene-overview/ (bottom of the page)
        public static Quaternion RotateToSceneApiCoordinates(Quaternion orientation)
        {
            return orientation * Quaternion.Euler(90, 0, 0);
        }

        // Source: https://docs.unity3d.com/ScriptReference/Camera-projectionMatrix.html
        public static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
        {
            float x = 2.0F * near / (right - left);
            float y = 2.0F * near / (top - bottom);
            float a = (right + left) / (right - left);
            float b = (top + bottom) / (top - bottom);
            float c = -(far + near) / (far - near);
            float d = -(2.0F * far * near) / (far - near);
            float e = -1.0F;
            Matrix4x4 m = new Matrix4x4();
            m[0, 0] = x;
            m[0, 1] = 0;
            m[0, 2] = a;
            m[0, 3] = 0;
            m[1, 0] = 0;
            m[1, 1] = y;
            m[1, 2] = b;
            m[1, 3] = 0;
            m[2, 0] = 0;
            m[2, 1] = 0;
            m[2, 2] = c;
            m[2, 3] = d;
            m[3, 0] = 0;
            m[3, 1] = 0;
            m[3, 2] = e;
            m[3, 3] = 0;
            return m;
        }
    }
}
