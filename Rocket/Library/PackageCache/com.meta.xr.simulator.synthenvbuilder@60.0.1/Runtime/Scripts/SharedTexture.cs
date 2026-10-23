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
using SynthEnvBuilder.ThirdParty;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace SynthEnvBuilder
{
    public class SharedTexture
    {
        public static int kSwapchainSize = 6; // This number must be substantially larger than the number of frame delays
        public async static Task<List<Texture2D[]>> ConvertSharedTextureHandles(
            PassthroughFrameResolution resolution, IEnumerable<TextureHandle> leftHandles, IEnumerable<TextureHandle> rightHandles)
        {
            List<Texture2D[]> textures = new List<Texture2D[]>();
            var leftHandleEnumerator = leftHandles.GetEnumerator();
            var rightHandleEnumerator = rightHandles.GetEnumerator();
            while (leftHandleEnumerator.MoveNext() && rightHandleEnumerator.MoveNext())
            {
                var leftHandle = leftHandleEnumerator.Current;
                var rightHandle = rightHandleEnumerator.Current;
                textures.Add(new Texture2D[]
                {
                await ConvertSharedTextureHandle(resolution, leftHandle),
                await ConvertSharedTextureHandle(resolution, rightHandle)
                });
            }
            return textures;
        }

        private async static Task<Texture2D> ConvertSharedTextureHandle(PassthroughFrameResolution resolution, TextureHandle handle)
        {
            IntPtr texturePtr = new IntPtr();
            Action convertAction = () => GetSharedTextureFromHandle((IntPtr)handle.Value, out texturePtr);
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(convertAction);
            Texture2D externalTexture = null;
            Action createExternalTextureAction = () => ConvertTexturePtrToExternalTexture(resolution, texturePtr, TextureFormat.RGBA32, out externalTexture);
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(createExternalTextureAction);
            return externalTexture;
        }

        public static void CreateTexturesAndSharedHandles(
            PassthroughFrameResolution resolution, int swapchainSize, out List<Texture2D[]> textures, out List<IntPtr[]> handles)
        {
            textures = new List<Texture2D[]>();
            handles = new List<IntPtr[]>();

            for (int textureIdx = 0; textureIdx < swapchainSize; textureIdx++)
            {
                textures.Add(new Texture2D[2]);
                handles.Add(new IntPtr[2]);
                for (int eye = 0; eye < SyntheticEnvironmentServer.kNumEyes; eye++)
                {
                    IntPtr texturePtr = new IntPtr();
                    CreateTexture2D(resolution.Width, resolution.Height, out texturePtr);
                    ConvertTexturePtrToExternalTexture(resolution, texturePtr, TextureFormat.RGBA32, out textures[textureIdx][eye]);
                    CreateSharedTextureHandle(texturePtr, out handles[textureIdx][eye]);
                }
            }
        }

        public static void CreateDepthTexturesAndSharedHandles(
            PassthroughFrameResolution resolution, int swapchainSize, out List<Texture2D[]> textures, out List<IntPtr[]> handles)
        {
            textures = new List<Texture2D[]>();
            handles = new List<IntPtr[]>();

            for (int textureIdx = 0; textureIdx < swapchainSize; textureIdx++)
            {
                textures.Add(new Texture2D[2]);
                handles.Add(new IntPtr[2]);
                for (int eye = 0; eye < SyntheticEnvironmentServer.kNumEyes; eye++)
                {
                    IntPtr texturePtr = new IntPtr();
                    CreateDepthTexture2D(resolution.Width, resolution.Height, out texturePtr);
                    ConvertTexturePtrToExternalTexture(resolution, texturePtr, TextureFormat.R16, out textures[textureIdx][eye]);
                    CreateSharedTextureHandle(texturePtr, out handles[textureIdx][eye]);
                }
            }
        }

        // Deprecated
        [DllImport("SharedTexturePlugin")]
        public static extern UInt32 GetSharedTextureFromHandle(IntPtr handle, out IntPtr texture);

        [DllImport("SharedTexturePlugin")]
        public static extern UInt32 CreateTexture2D(uint width, uint height, out IntPtr texture);

        [DllImport("SharedTexturePlugin")]
        public static extern UInt32 CreateDepthTexture2D(uint width, uint height, out IntPtr texture);

        [DllImport("SharedTexturePlugin")]
        public static extern UInt32 CreateSharedTextureHandle(IntPtr texture, out IntPtr handle);

        // Source: https://gist.github.com/mminer/816ff2b8a9599a9dd342e553d189e03f
        public static void VerticallyFlipRenderTexture(RenderTexture target)
        {
            var temp = RenderTexture.GetTemporary(target.descriptor);
            Graphics.Blit(target, temp, new Vector2(1, -1), new Vector2(0, 1));
            Graphics.Blit(temp, target);
            RenderTexture.ReleaseTemporary(temp);
        }

        public static void ConvertTexturePtrToExternalTexture(PassthroughFrameResolution resolution, IntPtr texturePtr, TextureFormat format, out Texture2D texture)
        {
            texture = Texture2D.CreateExternalTexture(
                (int)resolution.Width, (int)resolution.Height, format, false, true, texturePtr);
        }

        public static ByteString EncodeTextureIndex(uint index)
        {
            Assert.IsTrue(index < 256);
            byte[] indexAsBytes = new byte[1];
            indexAsBytes[0] = (byte)index;
            return ByteString.CopyFrom(indexAsBytes);
        }

        public static void CreateTexture2D(PassthroughFrameResolution resolution, out Texture2D texture, in TextureFormat textureFormat)
        {
            texture = new Texture2D((int)resolution.Width, (int)resolution.Height, textureFormat, -1, false);
        }
    }
}
