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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SynthEnvBuilder
{
    public class SceneSelector : MonoBehaviour
    {
        // map from command line names (more human readable) to internal names
        private readonly Dictionary<string, string> _sceneNames = new Dictionary<string, string>()
    {
        { "LivingRoom", "M_LivingRoom" },
        { "Bedroom", "M_BedRoom" },
        { "GameRoom", "GameRoom" }
    };
        private readonly string _defaultSceneName = "GameRoom";

#if UNITY_STANDALONE
        private void Awake()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string scene = _defaultSceneName;
            if (args.Length >= 2 && _sceneNames.ContainsKey(args[1]))
            {
                scene = args[1];
            }
            SceneManager.LoadScene(_sceneNames[scene], LoadSceneMode.Single);
        }
#endif
    }
}
