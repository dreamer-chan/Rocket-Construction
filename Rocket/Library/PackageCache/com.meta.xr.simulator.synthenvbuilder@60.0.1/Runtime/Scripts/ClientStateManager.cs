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

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace SynthEnvBuilder
{
    public class FPSCounter
    {
        public static int SampleWindowSize = 144;
        private long[] ticks = new long[SampleWindowSize]; // circular buffer
        private int headIndex = 0;
        public double FPS;

        public void Update(long tick)
        {
            ticks[headIndex] = tick;
            int tailIndex = (headIndex + 1) % SampleWindowSize;
            var timespan = new TimeSpan(ticks[headIndex] - ticks[tailIndex]);
            // Environment.TickCount is in milliseconds while timespan expects it to be in 100ns,
            // hence the division by 1e4.
            FPS = (SampleWindowSize / (timespan.TotalSeconds * 1e4));
            headIndex = tailIndex;
        }
    }

    public class ClientState
    {
        public Color Color { get; set; }
        public GameObject PositionMark { get; set; }
        public FPSCounter FPSCounter { get; set; }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(PositionMark);
        }
    }

    public class ClientStateManager : MonoBehaviour
    {
        static readonly Color[] clientColorCandidates = new Color[5]
        {
        Color.red,
        Color.green,
        Color.blue,
        Color.cyan,
        Color.magenta,
        };
        private ConcurrentDictionary<string, ClientState> _clientStates =
            new ConcurrentDictionary<string, ClientState>();

        public void RegisterClient(string name)
        {
            var clientColor = clientColorCandidates[_clientStates.Count % clientColorCandidates.Length];
            _clientStates[name] = new ClientState()
            {
                Color = clientColor,
                PositionMark = CreatePositionMark(clientColor),
                FPSCounter = new FPSCounter()
            };
        }

        public void UnregisterClient(string name)
        {
            ClientState removedClientState;
            _clientStates.TryRemove(name, out removedClientState);
            removedClientState.Destroy();
        }

        public void UpdateClientState(string name, Pose pose)
        {
            _clientStates[name].PositionMark.transform.position = pose.position;
            _clientStates[name].PositionMark.transform.rotation = pose.rotation;
            _clientStates[name].FPSCounter.Update(Environment.TickCount);
        }

        private GameObject CreatePositionMark(Color color)
        {
            // TODO: Use a better looking mesh.
            var positionMark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            positionMark.transform.localScale = new Vector3(0.1f, 0.1f, 0.2f);
            positionMark.layer = LayerMask.NameToLayer("Position Marks");
            positionMark.GetComponent<Renderer>().material.color = color;
            return positionMark;
        }

        private GUIContent GetStringContent()
        {
            string text = "";
            foreach (var entry in _clientStates)
            {
                text += entry.Key + ": " + (int)entry.Value.FPSCounter.FPS + "\n";
            }
            return new GUIContent(text);
        }

        private int nextGUICallTime = 0;
        private GUIContent guiContent = new GUIContent();
        private Vector2 textboxSize = new Vector2(0, 0);

        private void OnGUI()
        {
            if (Environment.TickCount > nextGUICallTime)
            {
                nextGUICallTime = (int)(Environment.TickCount + 1e3); // updates FPS information every second
                guiContent = GetStringContent();
                var style = GUI.skin.box;
                style.alignment = TextAnchor.UpperLeft;
                textboxSize = style.CalcSize(guiContent);
            }
            GUI.Box(new Rect(
                Screen.width - textboxSize.x, Screen.height - textboxSize.y, textboxSize.x, textboxSize.y), guiContent);
        }
    }
}
