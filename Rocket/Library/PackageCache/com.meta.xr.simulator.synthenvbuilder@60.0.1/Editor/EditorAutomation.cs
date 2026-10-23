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
using UnityEditor;
using System.IO;


namespace SynthEnvBuilder
{
    public class EditorAutomation
    {
        [MenuItem("SynthEnvServer/EditorAutomation/PerformBuild")]
        static void PerformBuild()
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = new[]
            {
            "Assets/Scenes/SceneSelector.unity",
            "Assets/Scenes/M_LivingRoom.unity",
            "Assets/Scenes/M_BedRoom.unity",
            "Assets/Scenes/Bubbles/GameRoom.unity",
        };
            buildPlayerOptions.locationPathName = Path.GetFullPath(Path.Combine(Application.dataPath, "../_output/synth_env_server.exe"));
            buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
            buildPlayerOptions.options = BuildOptions.None;
            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }
    }
}
