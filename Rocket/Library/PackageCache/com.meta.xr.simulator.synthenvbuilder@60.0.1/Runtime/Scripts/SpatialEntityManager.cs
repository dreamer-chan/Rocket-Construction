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

using Synthenv;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SynthEnvBuilder
{
    public class SpatialEntityManager
    {
        private static string ROOM_ENTITY_UUID = "78b8401c-4feb-0040-8ba5-65d9ac98d5ab"; // hardcoding for consistency across sessions
        private static string FLOOR_SEMANTIC_LABEL = "FLOOR";
        private static string CEILING_SEMANTIC_LABEL = "CEILING";
        private static string WALL_SEMANTIC_LABEL = "WALL_FACE";

        static public void GetAllSpatialEntities(out List<Synthenv.SpatialEntity> spatialEntities)
        {
            spatialEntities = new List<Synthenv.SpatialEntity>();

            AddAllBounded2DEntities(spatialEntities);
            AddAllBounded3DEntities(spatialEntities);
            AddRoomEntity(spatialEntities);
        }

        static private void AddAllBounded2DEntities(List<Synthenv.SpatialEntity> spatialEntities)
        {
            var allEntities = GameObject.FindObjectsOfType<Bounded2DEntity>();

            foreach (Bounded2DEntity entity in allEntities)
            {
                Synthenv.SpatialEntity returningSpatialEntity = new Synthenv.SpatialEntity
                {
                    Uuid = new Synthenv.Uuid { Value = entity.Uuid.ToString() },
                };

                // supported component: locatable
                UnityEngine.Vector3 boundingBoxSize = UnityEngine.Vector3.Scale(entity.transform.lossyScale, entity.LocalBounds.size);
                var pose = Utils.FromUnityPose(entity.Center, Utils.RotateToSceneApiCoordinates(entity.transform.rotation));
                returningSpatialEntity.Components.Add(new SpatialEntityComponent
                {
                    Locatable = pose
                });

                // supported component: bounded2D
                Synthenv.Rect2D rect2D = new Synthenv.Rect2D
                {
                    Extent = new Synthenv.Extent2
                    {
                        Width = boundingBoxSize.x,
                        Height = boundingBoxSize.z, // x and z are parallel to the plane, y is perpendicular
                    },
                    Offset = new Synthenv.Offset2
                    {
                        // the center of the plane is the same as the position of the scene anchor
                        X = -boundingBoxSize.x / 2,
                        Y = -boundingBoxSize.z / 2,
                    },
                };
                returningSpatialEntity.Components.Add(new SpatialEntityComponent
                {
                    Bounded2D = rect2D,
                });

                // supported component: semantic label
                if (entity.SemanticLabel.Length > 0)
                {
                    returningSpatialEntity.Components.Add(new SpatialEntityComponent
                    {
                        SemanticLabels = entity.SemanticLabel,
                    });
                }

                spatialEntities.Add(returningSpatialEntity);
            }
        }

        static private void AddAllBounded3DEntities(List<Synthenv.SpatialEntity> spatialEntities)
        {
            var allEntities = GameObject.FindObjectsOfType<Bounded3DEntity>();

            foreach (Bounded3DEntity entity in allEntities)
            {
                Synthenv.SpatialEntity returningSpatialEntity = new Synthenv.SpatialEntity
                {
                    Uuid = new Synthenv.Uuid { Value = entity.Uuid.ToString() },
                };

                // supported component: locatable
                returningSpatialEntity.Components.Add(new SpatialEntityComponent
                {
                    Locatable = Utils.Get3DBoundingBoxPose(entity),
                });

                // supported component: bounded3D
                var boundingBoxSize = Utils.Get3DBoundingBoxSize(entity);
                Synthenv.Rect3D rect3D = new Synthenv.Rect3D
                {
                    Extent = new Synthenv.Extent3
                    {
                        Width = boundingBoxSize.x,
                        Depth = boundingBoxSize.y,
                        Height = boundingBoxSize.z,
                    },
                    Offset = new Synthenv.Offset3
                    {
                        // the offset of a 3D box is its minimum corner within its own XrSpace
                        X = -boundingBoxSize.x / 2,
                        Y = -boundingBoxSize.z / 2,
                        Z = -boundingBoxSize.y,
                    },
                };
                returningSpatialEntity.Components.Add(new SpatialEntityComponent
                {
                    Bounded3D = rect3D,
                });

                // supported component: semantic label
                if (entity.SemanticLabel.Length > 0)
                {
                    returningSpatialEntity.Components.Add(new SpatialEntityComponent
                    {
                        SemanticLabels = entity.SemanticLabel,
                    });
                }

                spatialEntities.Add(returningSpatialEntity);
            }
        }

        static private void AddRoomEntity(List<Synthenv.SpatialEntity> spatialEntities)
        {
            Synthenv.SpatialEntity roomEntity = new Synthenv.SpatialEntity
            {
                Uuid = new Synthenv.Uuid { Value = ROOM_ENTITY_UUID }
            };

            // supported component: room layout
            Func<List<SpatialEntity>, List<Synthenv.Uuid>> GetEntityUuids =
                entities => entities.ConvertAll(e => new Synthenv.Uuid { Value = e.Uuid.ToString() });
            var floors = FilterSpatialEntities(e => e.SemanticLabel == FLOOR_SEMANTIC_LABEL);
            Debug.AssertFormat(floors.Count == 1, "The scene must contain exactly one floor; currently has {0}", floors.Count);
            var ceilings = FilterSpatialEntities(e => e.SemanticLabel == CEILING_SEMANTIC_LABEL);
            Debug.AssertFormat(ceilings.Count == 1, "The scene must contain exactly one ceiling; currently has {0}", ceilings.Count);
            var walls = FilterSpatialEntities(e => e.SemanticLabel == WALL_SEMANTIC_LABEL);
            Synthenv.SpatialEntityComponent roomLayoutComponent = new SpatialEntityComponent
            {
                RoomLayout = new RoomLayout
                {
                    FloorUuid = GetEntityUuids(floors)[0],
                    CeilingUuid = GetEntityUuids(ceilings)[0]
                }
            };
            roomLayoutComponent.RoomLayout.WallUuids.AddRange(GetEntityUuids(walls));
            roomEntity.Components.Add(roomLayoutComponent);

            // supported component: space container
            var allSpatialEntities = FilterSpatialEntities(e => true);
            Synthenv.SpatialEntityComponent spaceContainerComponent = new SpatialEntityComponent
            {
                SpaceContainer = new SpaceContainer()
            };
            spaceContainerComponent.SpaceContainer.Uuids.AddRange(GetEntityUuids(allSpatialEntities));
            roomEntity.Components.Add(spaceContainerComponent);

            spatialEntities.Add(roomEntity);
        }

        static private List<SpatialEntity> FilterSpatialEntities(Func<SpatialEntity, bool> predicate)
        {
            List<SpatialEntity> allSpatialEntities = new List<SpatialEntity>();
            allSpatialEntities.AddRange(GameObject.FindObjectsOfType<Bounded2DEntity>());
            allSpatialEntities.AddRange(GameObject.FindObjectsOfType<Bounded3DEntity>());

            List<SpatialEntity> result = new List<SpatialEntity>();
            foreach (var entity in allSpatialEntities)
            {
                if (predicate(entity))
                {
                    result.Add(entity);
                }
            }
            return result;
        }
    }
}
