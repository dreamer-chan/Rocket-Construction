/*
Copyright 2015 Pim de Witte All Rights Reserved.
NOTE: The file has been modified from its original form.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace SynthEnvBuilder.ThirdParty
{
    public class TimedAction
    {
        public Action action;
        public int enqueuedFrameCount;
    }

    /// Author: Pim de Witte (pimdewitte.com) and contributors, https://github.com/PimDeWitte/UnityMainThreadDispatcher
    /// <summary>
    /// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to make calls to the main thread for
    /// things such as UI Manipulation in Unity. It was developed for use in combination with the Firebase Unity plugin, which uses separate threads for event handling
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static readonly Queue<TimedAction> _deferredExecutionQueue = new Queue<TimedAction>();
        private int _deferByFrameCount = 2;
        // FrameCount is a proxy for SyntheticEnvironmentServer::GetPassthroughFrames to get the frame index
        // because that function does not run on the main thread (and thus cannot directly access this property).
        public int FrameCount { get; set; } = 0;

        public void Update()
        {
            FrameCount = Time.frameCount;
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }

            lock (_deferredExecutionQueue)
            {
                // Process all requests that were enqueued (_deferByFrameCount) ago.
                // Used < instead of <= because the time when render() and sendResponse()
                // are scheduled is at frame i, but render() might not get executed until frame i+1, and we want
                // sendResponse() to be executed 2 frames after render() is *executed* (as opposed to *scheduled*).
                // So the difference must be at least 3 for an action to get executed.
                while (_deferredExecutionQueue.Count > 0 &&
                    _deferredExecutionQueue.Peek().enqueuedFrameCount + _deferByFrameCount < Time.frameCount)
                {
                    _deferredExecutionQueue.Dequeue().action.Invoke();
                }
            }
        }

        /// <summary>
        /// Locks the queue and adds the IEnumerator to the queue
        /// </summary>
        /// <param name="action">IEnumerator function that will be executed from the main thread.</param>
        public void Enqueue(IEnumerator action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(() => { StartCoroutine(action); });
            }
        }

        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        public void Enqueue(Action action)
        {
            Enqueue(ActionWrapper(action));
        }

        /// <summary>
        /// Locks the queue and adds the Action to the queue, returning a Task which is completed when the action completes
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        /// <returns>A Task that can be awaited until the action completes</returns>
        public Task EnqueueAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            void WrappedAction()
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            Enqueue(ActionWrapper(WrappedAction));
            return tcs.Task;
        }

        /// <summary>
        /// Locks the queue, clears the queue, and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        public void ClearQueueAndEnqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Clear();
                Enqueue(action);
            }
        }

        public void EnqueueDeferred(Action action, int currentFrameCount)
        {
            lock (_deferredExecutionQueue)
            {
                _deferredExecutionQueue.Enqueue(new TimedAction
                {
                    action = action,
                    enqueuedFrameCount = currentFrameCount
                });
            }
        }

        IEnumerator ActionWrapper(Action a)
        {
            a();
            yield return null;
        }


        private static UnityMainThreadDispatcher _instance = null;

        public static bool Exists()
        {
            return _instance != null;
        }

        public static UnityMainThreadDispatcher Instance()
        {
            if (!Exists())
            {
                throw new Exception(
                    "UnityMainThreadDispatcher could not find the UnityMainThreadDispatcher object. Please ensure you have added the MainThreadExecutor Prefab to your scene.");
            }

            return _instance;
        }


        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }

        void OnDestroy()
        {
            _instance = null;
        }
    }
}
