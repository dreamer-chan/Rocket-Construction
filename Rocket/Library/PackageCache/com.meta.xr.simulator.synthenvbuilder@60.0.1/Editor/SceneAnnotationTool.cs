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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor.SceneManagement;
using UnityEditor.UIElements;

namespace SynthEnvBuilder
{
    public class SceneAnnotationTool : EditorWindow
    {
        // Fields for UI elements in the editor window.
        // 1. Top-level elements:
        private Button initializeSceneButton;
        private ObjectField selectedObjectField;
        private TextField entityTypeField;
        // 2. Elements displayed when a non-entity is selected:
        private GroupBox makeEntityButtonGroup;
        private Button make2DEntityButton;
        private Button make3DEntityButton;
        // 3. Elements displayed when a bounded 2D entity is selected:
        private GroupBox bounded2DEntityInformationGroup;
        private DropdownField bounded2DSemanticLabelField;
        private Button clear2DEntityInfoButton;
        // 4. Elements displayed when a bounded 3D entity is selected:
        private GroupBox bounded3DEntityInformationGroup;
        private DropdownField bounded3DSemanticLabelField;
        private Vector3Field bounded3DEntityCenterField;
        private Vector3Field bounded3DEntitySizeField;
        private Button resetCenterAndSizeButton;
        private Button clear3DEntityInfoButton;

        private Object sesPrefab;

        [MenuItem("SynthEnvServer/Scene Annotation Tool")]
        public static void ShowTool()
        {
            SceneAnnotationTool wnd = GetWindow<SceneAnnotationTool>();
            wnd.titleContent = new GUIContent("Scene Annotation Tool");
        }

        public void CreateGUI()
        {
            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.meta.xr.simulator.synthenvbuilder/Editor/SceneAnnotationTool.uxml");
            VisualElement labelFromUXML = visualTree.Instantiate();
            rootVisualElement.Add(labelFromUXML);

            // Query all UI Elements
            initializeSceneButton = rootVisualElement.Q<Button>("InitializeSceneButton");
            selectedObjectField = rootVisualElement.Q<ObjectField>("SelectedObjectField");
            entityTypeField = rootVisualElement.Q<TextField>("EntityTypeField");

            makeEntityButtonGroup = rootVisualElement.Q<GroupBox>("MakeEntityButtonGroup");
            make2DEntityButton = rootVisualElement.Q<Button>("Make2DEntityButton");
            make3DEntityButton = rootVisualElement.Q<Button>("Make3DEntityButton");

            bounded2DEntityInformationGroup = rootVisualElement.Q<GroupBox>("2DEntityInformationGroup");
            bounded2DSemanticLabelField = rootVisualElement.Q<DropdownField>("2DSemanticLabelField");
            clear2DEntityInfoButton = rootVisualElement.Q<Button>("2DClearEntityInfoButton");

            bounded3DEntityInformationGroup = rootVisualElement.Q<GroupBox>("3DEntityInformationGroup");
            bounded3DSemanticLabelField = rootVisualElement.Q<DropdownField>("3DSemanticLabelField");
            bounded3DEntityCenterField = rootVisualElement.Q<Vector3Field>("3DEntityCenterField");
            bounded3DEntitySizeField = rootVisualElement.Q<Vector3Field>("3DEntitySizeField");
            resetCenterAndSizeButton = rootVisualElement.Q<Button>("ResetCenterAndSizeButton");
            clear3DEntityInfoButton = rootVisualElement.Q<Button>("3DClearEntityInfoButton");

            // Register callback functions
            initializeSceneButton.clicked += InitializeScene;
            make2DEntityButton.clicked += MakeBounded2DEntity;
            make3DEntityButton.clicked += MakeBounded3DEntity;
            clear2DEntityInfoButton.clicked += Clear2DEntityInfo;
            clear3DEntityInfoButton.clicked += Clear3DEntityInfo;
            resetCenterAndSizeButton.clicked += ResetCenterAndSize;
            bounded2DSemanticLabelField.RegisterValueChangedCallback(evt => UpdateBounded2DEntity());
            bounded3DSemanticLabelField.RegisterValueChangedCallback(evt => UpdateBounded3DEntity());
            bounded3DEntityCenterField.RegisterValueChangedCallback(evt => UpdateBounded3DEntity());
            bounded3DEntitySizeField.RegisterValueChangedCallback(evt => UpdateBounded3DEntity());

            // Load SES prefab
            sesPrefab = AssetDatabase.LoadAssetAtPath("Packages/com.meta.xr.simulator.synthenvbuilder/Runtime/Server/SyntheticEnvironmentServer.prefab", typeof(GameObject));
        }

        private void Update()
        {
            // Skip the refresh if the editor is currently in play mode.
            if (Application.isPlaying)
            {
                return;
            }

            // 1. Disable the "initialize scene" button if an SES prefab instance exists in the scene.
            initializeSceneButton.SetEnabled(FindObjectOfType<ServerMain>() == null);

            // 2. Set the "Selected Object" field to the actively selected object in the scene.
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                makeEntityButtonGroup.style.display = DisplayStyle.None;
                bounded2DEntityInformationGroup.style.display = DisplayStyle.None;
                bounded3DEntityInformationGroup.style.display = DisplayStyle.None;
                return;
            }
            selectedObjectField.value = selectedObject;

            // 3. Display the entity type and the corresponding sub-menu.
            Bounded2DEntity bounded2DEntity = selectedObject.GetComponent<Bounded2DEntity>();
            Bounded3DEntity bounded3DEntity = selectedObject.GetComponent<Bounded3DEntity>();
            if (bounded2DEntity != null && bounded3DEntity != null)
            {
                Debug.LogWarning("Selected object cannot both be a 2D and a 3D entity. Restoring it to the initial state");
                DestroyImmediate(bounded2DEntity);
                DestroyImmediate(bounded3DEntity);
                bounded2DEntity = null;
                bounded3DEntity = null;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            if (bounded2DEntity == null && bounded3DEntity == null)
            {
                entityTypeField.value = "Not a scene entity";
                makeEntityButtonGroup.style.display = DisplayStyle.Flex;
                bounded2DEntityInformationGroup.style.display = DisplayStyle.None;
                bounded3DEntityInformationGroup.style.display = DisplayStyle.None;
            }
            else if (bounded2DEntity != null)
            {
                entityTypeField.value = "Bounded 2D Entity";
                makeEntityButtonGroup.style.display = DisplayStyle.None;
                bounded2DEntityInformationGroup.style.display = DisplayStyle.Flex;
                bounded3DEntityInformationGroup.style.display = DisplayStyle.None;

                string newLabel = bounded2DSemanticLabelField.choices.Contains(bounded2DEntity.SemanticLabel) ? bounded2DEntity.SemanticLabel : "";
                bounded2DSemanticLabelField.SetValueWithoutNotify(newLabel);
            }
            else // bounded3DEntity != null
            {
                entityTypeField.value = "Bounded 3D Entity";
                makeEntityButtonGroup.style.display = DisplayStyle.None;
                bounded2DEntityInformationGroup.style.display = DisplayStyle.None;
                bounded3DEntityInformationGroup.style.display = DisplayStyle.Flex;

                string newLabel = bounded3DSemanticLabelField.choices.Contains(bounded3DEntity.SemanticLabel) ? bounded3DEntity.SemanticLabel : "";
                bounded3DSemanticLabelField.SetValueWithoutNotify(newLabel);
                bounded3DEntityCenterField.SetValueWithoutNotify(bounded3DEntity.Center);
                bounded3DEntitySizeField.SetValueWithoutNotify(bounded3DEntity.Size);
            }
        }

        private void InitializeScene()
        {
            // Make sure that no instances of the SES prefab exists in the scene.
            // This should be guaranteed by the UI logic, hence checking with an assertion.
            Debug.Assert(FindObjectOfType<ServerMain>() == null);

            PrefabUtility.InstantiatePrefab(sesPrefab);
            // TODO: Use a fresh project to determine whether scene gizmos work out of the box
        }

        private void MakeBounded2DEntity()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null);
            // TODO: Warn (or block) the user if the selected object is not a plane

            // 1. Create a horizontal with the same x/z dimensions as the selected object.
            //    TODO: Also offer the options to create vertical planes (in both directions)
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = selectedObject.name + "_Bounded2DEntity";

            // Derive the plane's size from the hosting object's size.
            MeshRenderer parentMeshRenderer = selectedObject.GetComponent<MeshRenderer>();
            Vector3 parentSize = new Vector3(0.1f, 0.1f, 0.1f);
            if (parentMeshRenderer != null)
            {
                parentSize = parentMeshRenderer.localBounds.size;
            }
            plane.transform.SetParent(selectedObject.transform, true); // organize it under the host object
            plane.transform.localScale = Vector3.Scale(parentSize, new Vector3(0.1f, 0.1f, 0.1f)); // plane is a 10x10 mesh
            plane.transform.localPosition = Vector3.zero;

            // Remove the plane's mesh collider so that it is causally ineffective.
            MeshCollider planeMeshCollider = plane.GetComponent<MeshCollider>();
            DestroyImmediate(planeMeshCollider);
            // Remove the plane's mesh renderer so that it becomes invisible.
            MeshRenderer planeMeshRenderer = plane.GetComponent<MeshRenderer>();
            planeMeshRenderer.enabled = false;

            plane.AddComponent<Bounded2DEntity>();
            Selection.activeGameObject = plane;
        }

        private void MakeBounded3DEntity()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded3DEntity>() == null);
            selectedObject.AddComponent<Bounded3DEntity>();
        }

        private void UpdateBounded2DEntity()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded2DEntity>() != null);
            Bounded2DEntity bounded2DEntity = selectedObject.GetComponent<Bounded2DEntity>();
            bounded2DEntity.SemanticLabel = bounded2DSemanticLabelField.text;
            EditorApplication.QueuePlayerLoopUpdate(); // force updated scene gizmos to render
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void UpdateBounded3DEntity()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded3DEntity>() != null);
            Bounded3DEntity bounded3DEntity = selectedObject.GetComponent<Bounded3DEntity>();
            bounded3DEntity.SemanticLabel = bounded3DSemanticLabelField.text;
            bounded3DEntity.Center = bounded3DEntityCenterField.value;
            bounded3DEntity.Size = bounded3DEntitySizeField.value;
            EditorApplication.QueuePlayerLoopUpdate(); // force updated scene gizmos to render
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void ResetCenterAndSize()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded3DEntity>() != null);
            selectedObject.GetComponent<Bounded3DEntity>().CalculateBoundingBox();
            EditorApplication.QueuePlayerLoopUpdate(); // force updated scene gizmos to render
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void Clear2DEntityInfo()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded2DEntity>() != null);
            Selection.activeGameObject = selectedObject.transform.parent.gameObject;
            DestroyImmediate(selectedObject); // destroy the plane created by the "Make 2D Entity" button
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void Clear3DEntityInfo()
        {
            GameObject selectedObject = Selection.activeGameObject;
            Debug.Assert(selectedObject != null && selectedObject.GetComponent<Bounded3DEntity>() != null);
            DestroyImmediate(selectedObject.GetComponent<Bounded3DEntity>());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
