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
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Synthenv;
using SynthEnvBuilder.ThirdParty;
using Google.Protobuf.WellKnownTypes;
using System;
using Google.Protobuf;
using System.Collections.Concurrent;

namespace SynthEnvBuilder
{
    public class SyntheticEnvironmentServer : SyntheticEnvironment.SyntheticEnvironmentBase
    {
        public static int kNumEyes { get; } = 2;
        // maps from peer id to the latest request from that peer
        private ClientStateManager clientStateManager = GameObject.FindObjectOfType<ClientStateManager>();
        // TODO: Combine the different client fields into the same class
        private ConcurrentDictionary<string, UpdateEyeViewRequest> _latestRequests =
            new ConcurrentDictionary<string, UpdateEyeViewRequest>();
        private ConcurrentDictionary<string, ConnectPassthroughRequest> _passthroughConfigs =
            new ConcurrentDictionary<string, ConnectPassthroughRequest>();
        private ConcurrentDictionary<string, List<Texture2D[]>> _textures =
            new ConcurrentDictionary<string, List<Texture2D[]>>();
        private ConcurrentDictionary<string, List<Texture2D[]>> _depthTextures =
            new ConcurrentDictionary<string, List<Texture2D[]>>();

        public override async Task<Empty> SendEyeViews(IAsyncStreamReader<UpdateEyeViewRequest> requestStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                _latestRequests[context.Peer] = requestStream.Current;
            }
            return new Empty();
        }

        public override async Task<GetSpatialEntitiesResponse> GetSpatialEntities(Empty request, ServerCallContext context)
        {
            List<Synthenv.SpatialEntity> spatialEntities = null;
            Action getAllSpatialEntitiesAction = () => SpatialEntityManager.GetAllSpatialEntities(out spatialEntities);
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(getAllSpatialEntitiesAction);

            GetSpatialEntitiesResponse response = new GetSpatialEntitiesResponse();
            response.SpatialEntities.AddRange(spatialEntities);
            return response;
        }

        // Deprecated
        public override async Task GetPassthroughFrames(GetPassthroughFramesRequest request,
                                                                IServerStreamWriter<PassthroughFrame> responseStream,
                                                                ServerCallContext context)
        {
            if (request.DataType == PassthroughFramesDataType.GpuHandleRgba)
            {
                await GetPassthroughFrames_DX11Handle(request, responseStream, context);
            }
            else if (request.DataType == PassthroughFramesDataType.JpgRgb)
            {
                await GetPassthroughFrames_JPG(request, responseStream, context);
            }
            else
            {
                Debug.Log("Unsupported data format!");
            }
        }

        // Deprecated
        public async Task GetPassthroughFrames_DX11Handle(GetPassthroughFramesRequest request,
                                                                IServerStreamWriter<PassthroughFrame> responseStream,
                                                                ServerCallContext context)
        {
            string clientName = context.Peer;
            Camera[] cameras = new Camera[2];
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                CameraOperations.CreateCameraPair(request.Resolution, request.ColorMode, out cameras));
            List<Texture2D[]> sharedTextures = await SharedTexture.ConvertSharedTextureHandles(request.Resolution, request.LeftEyeTextures, request.RightEyeTextures);
            Task writeTask = Task.CompletedTask;
            // frameIndex and nextTexutreIndex need to be kept separate because they are used and updated in
            // two different delegates that are executed asynchronously.
            int frameIndex = 0, nextTextureIndex = 0;

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                clientStateManager.RegisterClient(clientName));

            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (!_latestRequests.ContainsKey(clientName))
                {
                    continue;
                }

                // capture the latest requested views to be used in the delegate below
                UpdateEyeViewRequest latestRequest;
                _latestRequests.TryRemove(clientName, out latestRequest);
                View[] views = new View[]
                {
                latestRequest.LeftEyeView,
                latestRequest.RightEyeView
                };

                Action sendEyeViewAction = () =>
                {
                    for (int eye = 0; eye < kNumEyes; eye++)
                    {
                        CameraOperations.UpdateCameraView(cameras[eye], views[eye]);
                    }
                    CameraOperations.RenderViewsIntoGpuHandles(cameras, sharedTextures[nextTextureIndex]);
                    nextTextureIndex = (nextTextureIndex + 1) % sharedTextures.Count;
                };

                Action sendResponseAction = () =>
                {
                    PassthroughFrame frame = new PassthroughFrame
                    {
                        FrameIndex = (ulong)frameIndex,
                        Timestamp = (ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                        LeftEyeBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedTextures.Count)),
                        RightEyeBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedTextures.Count)),
                    };
                    frameIndex++;

                    // use blocking wait() because this delegate isn't executed as an async delegate
                    writeTask.Wait();
                    if (!context.CancellationToken.IsCancellationRequested)
                    {
                        writeTask = responseStream.WriteAsync(frame);
                    }
                    clientStateManager.UpdateClientState(clientName, Utils.FromOpenXRPose(views[0].Pose));
                };

                UnityMainThreadDispatcher.Instance().Enqueue(sendEyeViewAction);
                // delay sending the response message by 2 frames to ensure rendering has really finished
                UnityMainThreadDispatcher.Instance().EnqueueDeferred(sendResponseAction, UnityMainThreadDispatcher.Instance().FrameCount);
            }
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => CameraOperations.DestroyCameraPair(cameras));
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                clientStateManager.UnregisterClient(clientName));
        }

        // Deprecated
        public async Task GetPassthroughFrames_JPG(GetPassthroughFramesRequest request,
                                                                IServerStreamWriter<PassthroughFrame> responseStream,
                                                                ServerCallContext context)
        {
            string clientName = context.Peer;
            Camera[] cameras = new Camera[2];
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                CameraOperations.CreateCameraPair(request.Resolution, request.ColorMode, out cameras));
            Texture2D jpgTexture = null;
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                SharedTexture.CreateTexture2D(request.Resolution, out jpgTexture, TextureFormat.RGBA32));

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                clientStateManager.RegisterClient(clientName));

            Task writeTask = Task.CompletedTask;
            int frameIndex = 0;

            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (!_latestRequests.ContainsKey(clientName))
                {
                    continue;
                }

                // capture the latest requested views to be used in the delegate below
                UpdateEyeViewRequest latestRequest;
                _latestRequests.TryRemove(clientName, out latestRequest);
                View[] views = new View[]
                {
                latestRequest.LeftEyeView,
                latestRequest.RightEyeView
                };

                Action processRequestAction = () =>
                {
                    for (int eye = 0; eye < kNumEyes; eye++)
                    {
                        CameraOperations.UpdateCameraView(cameras[eye], views[eye]);
                    }
                    ByteString[] renderedJPGs = CameraOperations.RenderViewsIntoImages(cameras, jpgTexture);
                    PassthroughFrame frame = new PassthroughFrame
                    {
                        FrameIndex = (ulong)frameIndex++,
                        Timestamp = (ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                        LeftEyeBuffer = renderedJPGs[0],
                        RightEyeBuffer = renderedJPGs[1]
                    };
                    // use blocking wait() because this delegate isn't executed as an async delegate
                    writeTask.Wait();
                    if (!context.CancellationToken.IsCancellationRequested)
                    {
                        writeTask = responseStream.WriteAsync(frame);
                    }
                    clientStateManager.UpdateClientState(clientName, Utils.FromOpenXRPose(views[0].Pose));
                };
                UnityMainThreadDispatcher.Instance().Enqueue(processRequestAction);
            }
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => CameraOperations.DestroyCameraPair(cameras));
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                clientStateManager.UnregisterClient(clientName));
            jpgTexture = null; // to be cleaned by garbage collector
        }

        public override async Task<ConnectPassthroughResponse> ConnectPassthrough(ConnectPassthroughRequest request, ServerCallContext context)
        {
            ConnectPassthroughResponse response = new ConnectPassthroughResponse();
            _passthroughConfigs.TryAdd(context.Peer, request);

            if (request.DataType == PassthroughFramesDataType.GpuHandleRgba)
            {
                List<Texture2D[]> textures = new List<Texture2D[]>();
                List<IntPtr[]> sharedHandles = new List<IntPtr[]>();
                Action createTexturesAndSharedHandlesAction =
                    () => SharedTexture.CreateTexturesAndSharedHandles(
                        request.Resolution, SharedTexture.kSwapchainSize, out textures, out sharedHandles);
                await UnityMainThreadDispatcher.Instance().EnqueueAsync(createTexturesAndSharedHandlesAction);
                _textures.TryAdd(context.Peer, textures);

                foreach (var handlePtrs in sharedHandles)
                {
                    TextureHandle leftEyeHandle = new TextureHandle();
                    TextureHandle rightEyeHandle = new TextureHandle();
                    leftEyeHandle.Value = (ulong)handlePtrs[0];
                    response.LeftEyeTextures.Add(leftEyeHandle);
                    rightEyeHandle.Value = (ulong)handlePtrs[1];
                    response.RightEyeTextures.Add(rightEyeHandle);
                }


                // Create Depth DXGI handles
                textures = new List<Texture2D[]>();
                sharedHandles = new List<IntPtr[]>();
                Action createDepthTexturesAndSharedHandlesAction =
                    () => SharedTexture.CreateDepthTexturesAndSharedHandles(
                        request.Resolution, SharedTexture.kSwapchainSize, out textures, out sharedHandles);
                await UnityMainThreadDispatcher.Instance().EnqueueAsync(createDepthTexturesAndSharedHandlesAction);
                await UnityMainThreadDispatcher.Instance().EnqueueAsync(createDepthTexturesAndSharedHandlesAction);

                _depthTextures.TryAdd(context.Peer, textures);
                foreach (var handlePtrs in sharedHandles)
                {
                    TextureHandle leftEyeDepthHandle = new TextureHandle();
                    TextureHandle rightEyeDepthHandle = new TextureHandle();
                    leftEyeDepthHandle.Value = (ulong)handlePtrs[0];
                    response.LeftEyeDepthTextures.Add(leftEyeDepthHandle);
                    rightEyeDepthHandle.Value = (ulong)handlePtrs[1];
                    response.RightEyeDepthTextures.Add(rightEyeDepthHandle);
                }
            }
            // else if request.DataType is JPG, return empty texture handles

            return response;
        }

        public override async Task StartPassthrough(Empty request, IServerStreamWriter<PassthroughFrame> responseStream, ServerCallContext context)
        {
            ConnectPassthroughRequest passthroughConfig = new ConnectPassthroughRequest();
            _passthroughConfigs.TryGetValue(context.Peer, out passthroughConfig);

            if (passthroughConfig.DataType == PassthroughFramesDataType.GpuHandleRgba)
            {
                await StartPassthrough_DX11Handle(passthroughConfig, responseStream, context);
            }
            else if (passthroughConfig.DataType == PassthroughFramesDataType.JpgRgb)
            {
                // only supports dx11 handles for now
                Debug.Log("JPG requested but is not current supported :(");
                await StartPassthrough_Images(passthroughConfig, responseStream, context);
            }
            else
            {
                Debug.Log("Unsupported data format!");
            }
        }

        public async Task StartPassthrough_DX11Handle(ConnectPassthroughRequest passthroughConfig,
                                                      IServerStreamWriter<PassthroughFrame> responseStream,
                                                      ServerCallContext context)
        {
            string clientName = context.Peer;
            Camera[] cameras = new Camera[2];
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                CameraOperations.CreateCameraPair(passthroughConfig.Resolution, passthroughConfig.ColorMode, out cameras));
            List<Texture2D[]> sharedTextures = new List<Texture2D[]>(); // TODO: Rename
            _textures.TryGetValue(clientName, out sharedTextures);
            //-------- Depth textures ------------
            List<Texture2D[]> sharedDepthTextures = new List<Texture2D[]>(); // TODO: Rename
            _depthTextures.TryGetValue(clientName, out sharedDepthTextures);

            Task writeTask = Task.CompletedTask;
            // frameIndex and nextTexutreIndex need to be kept separate because they are used and updated in
            // two different delegates that are executed asynchronously.
            int frameIndex = 0, nextTextureIndex = 0, nextDepthTextureIndex = 0;

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                clientStateManager.RegisterClient(clientName));

            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (!_latestRequests.ContainsKey(clientName))
                {
                    continue;
                }

                // capture the latest requested views to be used in the delegate below
                UpdateEyeViewRequest latestRequest;
                _latestRequests.TryRemove(clientName, out latestRequest);
                View[] views = new View[]
                {
                latestRequest.LeftEyeView,
                latestRequest.RightEyeView
                };

                Action sendEyeViewAction = () =>
                {
                    for (int eye = 0; eye < kNumEyes; eye++)
                    {
                        CameraOperations.UpdateCameraView(cameras[eye], views[eye]);
                    }
                    CameraOperations.RenderViewsIntoGpuHandles(cameras, sharedTextures[nextTextureIndex], sharedDepthTextures[nextDepthTextureIndex]);
                    nextTextureIndex = (nextTextureIndex + 1) % sharedTextures.Count;
                    nextDepthTextureIndex = (nextDepthTextureIndex + 1) % sharedDepthTextures.Count;
                };

                Action sendResponseAction = () =>
                {
                    PassthroughFrame frame = new PassthroughFrame
                    {
                        FrameIndex = (ulong)frameIndex,
                        Timestamp = (ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                        LeftEyeBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedTextures.Count)),
                        RightEyeBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedTextures.Count)),
                        LeftEyeDepthBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedDepthTextures.Count)),
                        RightEyeDepthBuffer = SharedTexture.EncodeTextureIndex((uint)(frameIndex % sharedDepthTextures.Count)),
                    };
                    frameIndex++;

                    // use blocking wait() because this delegate isn't executed as an async delegate
                    writeTask.Wait();
                    if (!context.CancellationToken.IsCancellationRequested)
                    {
                        writeTask = responseStream.WriteAsync(frame);
                    }
                    clientStateManager.UpdateClientState(clientName, Utils.FromOpenXRPose(views[0].Pose));
                };

                UnityMainThreadDispatcher.Instance().Enqueue(sendEyeViewAction);
                // delay sending the response message by 2 frames to ensure rendering has really finished
                UnityMainThreadDispatcher.Instance().EnqueueDeferred(sendResponseAction, UnityMainThreadDispatcher.Instance().FrameCount);
            }
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => CameraOperations.DestroyCameraPair(cameras));
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                clientStateManager.UnregisterClient(clientName));
        }

        public async Task StartPassthrough_Images(ConnectPassthroughRequest passthroughConfig,
                                               IServerStreamWriter<PassthroughFrame> responseStream,
                                               ServerCallContext context)
        {
            string clientName = context.Peer;
            Camera[] cameras = new Camera[2];
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                CameraOperations.CreateCameraPair(passthroughConfig.Resolution, passthroughConfig.ColorMode, out cameras));
            Texture2D jpgTexture = null;
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                SharedTexture.CreateTexture2D(passthroughConfig.Resolution, out jpgTexture, TextureFormat.RGBA32));
            Texture2D depthTexture = null;
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                SharedTexture.CreateTexture2D(passthroughConfig.Resolution, out depthTexture, TextureFormat.RFloat));

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                clientStateManager.RegisterClient(clientName));

            Task writeTask = Task.CompletedTask;
            int frameIndex = 0;

            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (!_latestRequests.ContainsKey(clientName))
                {
                    continue;
                }

                // capture the latest requested views to be used in the delegate below
                UpdateEyeViewRequest latestRequest;
                _latestRequests.TryRemove(clientName, out latestRequest);
                View[] views = new View[]
                {
                latestRequest.LeftEyeView,
                latestRequest.RightEyeView
                };

                Action processRequestAction = () =>
                {
                    for (int eye = 0; eye < kNumEyes; eye++)
                    {
                        CameraOperations.UpdateCameraView(cameras[eye], views[eye]);
                    }
                    ByteString[] renderedImgs = CameraOperations.RenderViewsIntoImages(cameras, jpgTexture, depthTexture);
                    PassthroughFrame frame = new PassthroughFrame
                    {
                        FrameIndex = (ulong)frameIndex++,
                        Timestamp = (ulong)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                        LeftEyeBuffer = renderedImgs[0],
                        RightEyeBuffer = renderedImgs[1],
                        LeftEyeDepthBuffer = renderedImgs[2],
                        RightEyeDepthBuffer = renderedImgs[3]
                    };
                    // use blocking wait() because this delegate isn't executed as an async delegate
                    writeTask.Wait();
                    if (!context.CancellationToken.IsCancellationRequested)
                    {
                        writeTask = responseStream.WriteAsync(frame);
                    }
                    clientStateManager.UpdateClientState(clientName, Utils.FromOpenXRPose(views[0].Pose));
                };
                UnityMainThreadDispatcher.Instance().Enqueue(processRequestAction);
            }
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => CameraOperations.DestroyCameraPair(cameras));
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                clientStateManager.UnregisterClient(clientName));
            jpgTexture = null; // to be cleaned by garbage collector
        }
    }
}
