#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neighbor.Main.EditorTools
{
    public static class InteractionPrefabVisualUpgradeUtility
    {
        private const string GeneratedRoot = "Assets/Main/Features/Interaction/Items/GameReadyVisuals";
        private const string MaterialsRoot = GeneratedRoot + "/Materials";
        private const string MeshesRoot = GeneratedRoot + "/Meshes";
        private const string VisualRootName = "GameReadyVisual";

        private static readonly Dictionary<string, Action<GameObject, Transform>> Builders =
            new Dictionary<string, Action<GameObject, Transform>>
            {
                ["Assets/Main/Features/Interaction/Items/AlarmClocks/Prefabs/PlaceholderAlarmClock.prefab"] = BuildAlarmClock,
                ["Assets/Main/Features/Interaction/Items/Beartraps/Prefabs/BeartrapPlaceholder.prefab"] = BuildBeartrap,
                ["Assets/Main/Features/Interaction/Items/Beds/Prefabs/PlaceholderHideableBed.prefab"] = BuildBed,
                ["Assets/Main/Features/Interaction/Items/Boards/Prefabs/PlaceholderWoodBoard.prefab"] = BuildWoodBoard,
                ["Assets/Main/Features/Interaction/Items/Books/Prefabs/PlaceholderNotebook.prefab"] = BuildNotebook,
                ["Assets/Main/Features/Interaction/Items/Books/Prefabs/PlaceholderReadableBook.prefab"] = BuildReadableBook,
                ["Assets/Main/Features/Interaction/Items/Boxes/CardboardBox/Prefabs/CardboardBox.prefab"] = BuildCardboardBox,
                ["Assets/Main/Features/Interaction/Items/Cameras/Prefabs/PlaceholderPhotoCamera.prefab"] = BuildPhotoCamera,
                ["Assets/Main/Features/Interaction/Items/Closets/Prefabs/PlaceholderCloset.prefab"] = BuildCloset,
                ["Assets/Main/Features/Interaction/Items/Cupboards/Prefabs/PlaceholderCupboard.prefab"] = BuildCupboard,
                ["Assets/Main/Features/Interaction/Items/Curtains/Prefabs/PlaceholderHideableCurtains.prefab"] = BuildCurtains,
                ["Assets/Main/Features/Interaction/Items/Doorbells/Prefabs/PlaceholderDoorbell.prefab"] = BuildDoorbell,
                ["Assets/Main/Features/Interaction/Items/Doors/Prefabs/Door.prefab"] = BuildDoor,
                ["Assets/Main/Features/Interaction/Items/Doors/Prefabs/DoorFrame.prefab"] = BuildDoorFrame,
                ["Assets/Main/Features/Interaction/Items/Doors/Prefabs/LatchedDoorPlaceholder.prefab"] = BuildLatchedDoor,
                ["Assets/Main/Features/Interaction/Items/Doors/Prefabs/LockedDoor.prefab"] = BuildLockedDoor,
                ["Assets/Main/Features/Interaction/Items/Flashlights/Prefabs/PlaceholderFlashlight.prefab"] = BuildFlashlight,
                ["Assets/Main/Features/Interaction/Items/Food/Tomato/Prefabs/PlaceholderTomato.prefab"] = BuildTomato,
                ["Assets/Main/Features/Interaction/Items/Glass/Prefabs/PlaceholderGlass.prefab"] = BuildGlass,
                ["Assets/Main/Features/Interaction/Items/Keys/Prefabs/TestKey.prefab"] = BuildKey,
                ["Assets/Main/Features/Interaction/Items/Lights/LightSwitches/Prefabs/PlaceholderLightSwitch.prefab"] = BuildLightSwitch,
                ["Assets/Main/Features/Interaction/Items/Mirrors/Prefabs/PlaceholderMirror.prefab"] = BuildMirror,
                ["Assets/Main/Features/Interaction/Items/RemoteControls/Prefabs/PlaceholderRemoteControl.prefab"] = BuildRemoteControl,
                ["Assets/Main/Features/Interaction/Items/Security/LaserGrid/Prefabs/PlaceholderLaserGrid.prefab"] = BuildLaserGrid,
                ["Assets/Main/Features/Interaction/Items/Security/LaserGrid/Prefabs 1/PlaceholderLaserGrid.prefab"] = BuildLaserGrid,
                ["Assets/Main/Features/Interaction/Items/SecurityCameras/Prefabs/PlaceholderSecurityCamera.prefab"] = BuildSecurityCamera,
                ["Assets/Main/Features/Interaction/Items/Sports/Basketball/Prefabs/PlaceholderBasketball.prefab"] = BuildBasketball,
                ["Assets/Main/Features/Interaction/Items/SprayCans/Prefabs/PlaceholderSprayCan.prefab"] = BuildSprayCan,
                ["Assets/Main/Features/Interaction/Items/SwingingAxes/Prefabs/HugeSwingingAxePlaceholder.prefab"] = BuildHugeSwingingAxe,
                ["Assets/Main/Features/Interaction/Items/SwingingAxes/Prefabs/HugeSwingingAxeTripWirePlaceholder.prefab"] = BuildHugeSwingingAxeTripWire,
                ["Assets/Main/Features/Interaction/Items/SwingingAxes/Prefabs/PhysicsSwingingObjectPlaceholder.prefab"] = BuildPhysicsSwingingObject,
                ["Assets/Main/Features/Interaction/Items/Tools/Screwdriver/Prefabs/PlaceholderScrewdriver.prefab"] = BuildScrewdriver,
                ["Assets/Main/Features/Interaction/Items/Toys/WindUpToy/Prefabs/PlaceholderWindUpToy.prefab"] = BuildWindUpToy,
                ["Assets/Main/Features/Interaction/Items/TrapDoors/Prefabs/FakeFloorTrapDoorPlaceholder.prefab"] = BuildTrapDoor,
                ["Assets/Main/Features/Interaction/Items/Traps/BoxingGloveTrap/Prefabs/PlaceholderSpringLoadedBoxingGloveTrap.prefab"] = BuildBoxingGloveTrap,
                ["Assets/Main/Features/Interaction/Items/Traps/SawBladeTrap/Prefabs/PlaceholderRaySawBladeTrap.prefab"] = BuildSawBladeTrap,
                ["Assets/Main/Features/Interaction/Items/TVs/Prefabs/PlaceholderTV.prefab"] = BuildTelevision,
                ["Assets/Main/Features/Interaction/Items/Umbrellas/Prefabs/PlaceholderUmbrella.prefab"] = BuildUmbrella,
                ["Assets/Main/Features/Interaction/Items/Vents/Prefabs/PlaceholderScrew.prefab"] = BuildScrew,
                ["Assets/Main/Features/Interaction/Items/Vents/Prefabs/PlaceholderVentCover.prefab"] = BuildVentCover,
                ["Assets/Main/Features/Interaction/Items/Windows/Blinds/Prefabs/PlaceholderWindowBlinds.prefab"] = BuildWindowBlinds
            };

        private static Palette palette;
        private static Mesh torusMesh;
        private static Mesh sawBladeMesh;
        private static Mesh starMesh;
        private static Mesh shardMeshA;
        private static Mesh shardMeshB;
        private static Mesh coneMesh;

        [MenuItem("Tools/Neighbor/Upgrade Interaction Blockout Visuals")]
        private static void UpgradeFromMenu()
        {
            UpgradeAllPrefabs(true);
        }

        public static void UpgradeFromCommandLine()
        {
            UpgradeAllPrefabs(true);
            EditorApplication.Exit(0);
        }

        public static int UpgradeAllPrefabs(bool logProgress = false)
        {
            EnsureGeneratedAssets();
            int changedCount = 0;
            foreach (KeyValuePair<string, Action<GameObject, Transform>> entry in Builders)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(entry.Key);
                if (root == null)
                {
                    Debug.LogError($"{entry.Key}: failed to load prefab contents.");
                    continue;
                }

                try
                {
                    Transform visualRoot = ResetVisualRoot(root);
                    DisableLegacyMeshRenderers(root, visualRoot);
                    entry.Value(root, visualRoot);
                    PrefabUtility.SaveAsPrefabAsset(root, entry.Key, out bool savedSuccessfully);
                    if (!savedSuccessfully)
                    {
                        Debug.LogError($"{entry.Key}: failed to save upgraded prefab.");
                        continue;
                    }

                    changedCount++;
                    if (logProgress)
                    {
                        Debug.Log($"{entry.Key}: upgraded blockout presentation.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (logProgress)
            {
                Debug.Log($"Interaction visual upgrade finished for {changedCount} prefab(s).");
            }

            return changedCount;
        }

        private static Transform ResetVisualRoot(GameObject root)
        {
            Transform existing = root.transform.Find(VisualRootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject visual = new GameObject(VisualRootName);
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            return visual.transform;
        }

        private static void DisableLegacyMeshRenderers(GameObject root, Transform visualRoot)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.transform.IsChildOf(visualRoot))
                {
                    continue;
                }

                renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void EnsureGeneratedAssets()
        {
            EnsureFolder("Assets/Main/Features/Interaction/Items", "GameReadyVisuals");
            EnsureFolder(GeneratedRoot, "Materials");
            EnsureFolder(GeneratedRoot, "Meshes");
            palette = Palette.LoadOrCreate(MaterialsRoot);
            torusMesh = EnsureMesh("GameReady_Torus", CreateTorusMesh);
            sawBladeMesh = EnsureMesh("GameReady_SawBlade", CreateSawBladeMesh);
            starMesh = EnsureMesh("GameReady_Star", CreateStarMesh);
            shardMeshA = EnsureMesh("GameReady_GlassShardA", () => CreateTriangleMesh("GlassShardA", new Vector2(-0.4f, -0.35f), new Vector2(0.42f, -0.22f), new Vector2(-0.08f, 0.45f)));
            shardMeshB = EnsureMesh("GameReady_GlassShardB", () => CreateTriangleMesh("GlassShardB", new Vector2(-0.32f, -0.42f), new Vector2(0.36f, 0.38f), new Vector2(0.08f, -0.28f)));
            coneMesh = EnsureMesh("GameReady_Cone", CreateConeMesh);
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Mesh EnsureMesh(string name, Func<Mesh> create)
        {
            string path = $"{MeshesRoot}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh mesh = create();
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void BuildAlarmClock(GameObject root, Transform visualRoot)
        {
            Renderer body = AddCube(visualRoot, "BrushedClockBody", new Vector3(0f, 0f, 0.02f), new Vector3(0.66f, 0.46f, 0.24f), palette.Brass).GetComponent<Renderer>();
            AddCube(visualRoot, "LowerMetalBezel", new Vector3(0f, -0.21f, -0.115f), new Vector3(0.58f, 0.035f, 0.035f), palette.WarmMetal);
            AddCustom(visualRoot, "FaceGlassRing", torusMesh, new Vector3(0f, 0.01f, -0.145f), new Vector3(0.56f, 0.56f, 0.035f), palette.WarmMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "CreamClockFace", new Vector3(0f, 0.01f, -0.165f), 0.24f, 0.018f, palette.Cream, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "CenterPin", new Vector3(0f, 0.01f, -0.188f), 0.025f, 0.012f, palette.BlackRubber, new Vector3(90f, 0f, 0f));

            Transform ticking = AddEmpty(visualRoot, "TickingHand_Final", new Vector3(0f, 0.01f, -0.192f), Quaternion.identity);
            AddCube(ticking, "SecondHandNeedle", new Vector3(0f, 0.09f, 0f), new Vector3(0.018f, 0.19f, 0.01f), palette.GlowRed);
            AddCube(visualRoot, "HourHand", new Vector3(-0.04f, 0.035f, -0.19f), new Vector3(0.026f, 0.115f, 0.012f), palette.BlackRubber, new Vector3(0f, 0f, 35f));
            AddCube(visualRoot, "MinuteHand", new Vector3(0.055f, 0.055f, -0.191f), new Vector3(0.018f, 0.17f, 0.012f), palette.BlackRubber, new Vector3(0f, 0f, -55f));

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(angle) * 0.19f, Mathf.Cos(angle) * 0.19f, -0.19f);
                AddCube(visualRoot, $"HourTick_{i:00}", pos, new Vector3(i % 3 == 0 ? 0.018f : 0.01f, 0.04f, 0.008f), palette.BlackRubber, new Vector3(0f, 0f, -i * 30f));
            }

            AddSphere(visualRoot, "LeftBellDome", new Vector3(-0.24f, 0.33f, 0f), new Vector3(0.25f, 0.15f, 0.25f), palette.WarmMetal);
            AddSphere(visualRoot, "RightBellDome", new Vector3(0.24f, 0.33f, 0f), new Vector3(0.25f, 0.15f, 0.25f), palette.WarmMetal);
            AddCylinder(visualRoot, "TopHandle", new Vector3(0f, 0.45f, 0f), 0.018f, 0.38f, palette.WarmMetal, new Vector3(0f, 0f, 90f));
            AddCylinder(visualRoot, "LeftFoot", new Vector3(-0.23f, -0.31f, 0.03f), 0.025f, 0.25f, palette.WarmMetal, new Vector3(0f, 0f, -16f));
            AddCylinder(visualRoot, "RightFoot", new Vector3(0.23f, -0.31f, 0.03f), 0.025f, 0.25f, palette.WarmMetal, new Vector3(0f, 0f, 16f));

            SetObjectReference(root, "feedbackRenderer", body);
            SetObjectReference(root, "tickingVisual", ticking);
        }

        private static void BuildBeartrap(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "HeavyBasePlate", Vector3.zero, new Vector3(0.95f, 0.08f, 0.58f), palette.DullMetal);
            AddCylinder(visualRoot, "CenterAxle", new Vector3(0f, 0.08f, 0f), 0.08f, 0.72f, palette.BrushedMetal, new Vector3(0f, 0f, 90f));
            Transform leftJaw = AddEmpty(visualRoot, "LeftJaw_Final", new Vector3(0f, 0.14f, 0.28f), Quaternion.identity);
            Transform rightJaw = AddEmpty(visualRoot, "RightJaw_Final", new Vector3(0f, 0.14f, -0.28f), Quaternion.identity);
            BuildTrapJaw(leftJaw, -1f);
            BuildTrapJaw(rightJaw, 1f);
            Transform plate = AddCylinder(visualRoot, "PressurePlate_Final", new Vector3(0f, 0.16f, 0f), 0.18f, 0.035f, palette.DarkMetal, Vector3.zero).transform;
            AddCustom(plate, "PressureStarGrip", starMesh, new Vector3(0f, 0.025f, 0f), new Vector3(0.24f, 0.015f, 0.24f), palette.WarmMetal);
            AddCube(visualRoot, "SafetyChain", new Vector3(-0.52f, 0.08f, 0f), new Vector3(0.26f, 0.035f, 0.035f), palette.DarkMetal, new Vector3(0f, 28f, 0f));
            AddCustom(visualRoot, "ChainRing", torusMesh, new Vector3(-0.68f, 0.08f, 0.04f), new Vector3(0.16f, 0.16f, 0.028f), palette.DarkMetal, new Vector3(0f, 0f, 90f));

            SetObjectReference(root, "leftJaw", leftJaw);
            SetObjectReference(root, "rightJaw", rightJaw);
            SetObjectReference(root, "pressurePlate", plate);
        }

        private static void BuildTrapJaw(Transform parent, float side)
        {
            AddCube(parent, "JawBackBar", new Vector3(0f, 0f, 0f), new Vector3(0.88f, 0.06f, 0.06f), palette.DarkMetal);
            for (int i = 0; i < 7; i++)
            {
                float x = -0.36f + i * 0.12f;
                AddCustom(parent, $"Tooth_{i:00}", coneMesh, new Vector3(x, 0.08f, -side * 0.03f), new Vector3(0.055f, 0.16f, 0.055f), palette.BrushedMetal, new Vector3(side > 0f ? 90f : -90f, 0f, 0f));
            }
        }

        private static void BuildBed(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "DarkWoodFrame", new Vector3(0f, 0.26f, 0f), new Vector3(1.75f, 0.16f, 2.25f), palette.DarkWood);
            AddCube(visualRoot, "SoftMattress", new Vector3(0f, 0.42f, 0f), new Vector3(1.6f, 0.22f, 2.05f), palette.Cream);
            AddCube(visualRoot, "FoldedBlanket", new Vector3(0f, 0.57f, -0.2f), new Vector3(1.48f, 0.08f, 1.15f), palette.FabricBlue);
            AddCube(visualRoot, "BlanketHem", new Vector3(0f, 0.62f, -0.8f), new Vector3(1.5f, 0.035f, 0.07f), palette.FabricRed);
            AddCube(visualRoot, "PillowLeft", new Vector3(-0.42f, 0.6f, 0.78f), new Vector3(0.62f, 0.14f, 0.36f), palette.WhitePlastic);
            AddCube(visualRoot, "PillowRight", new Vector3(0.42f, 0.6f, 0.78f), new Vector3(0.62f, 0.14f, 0.36f), palette.WhitePlastic);
            AddCube(visualRoot, "TallHeadboard", new Vector3(0f, 0.82f, 1.16f), new Vector3(1.86f, 0.95f, 0.16f), palette.Wood);
            for (int i = 0; i < 5; i++)
            {
                AddCube(visualRoot, $"HeadboardSlat_{i}", new Vector3(-0.72f + i * 0.36f, 0.86f, 1.06f), new Vector3(0.07f, 0.76f, 0.07f), palette.DarkWood);
            }

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddCube(visualRoot, $"TaperedLeg_{sx}_{sz}", new Vector3(sx * 0.78f, -0.08f, sz * 1.02f), new Vector3(0.16f, 0.54f, 0.16f), palette.DarkWood);
                }
            }
        }

        private static void BuildWoodBoard(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "WeatheredPlank", Vector3.zero, new Vector3(2.35f, 0.22f, 0.18f), palette.Wood);
            AddCube(visualRoot, "LeftChamferShadow", new Vector3(-1.19f, 0f, 0.005f), new Vector3(0.035f, 0.19f, 0.19f), palette.DarkWood, new Vector3(0f, 0f, 6f));
            AddCube(visualRoot, "RightChamferShadow", new Vector3(1.19f, 0f, 0.005f), new Vector3(0.035f, 0.19f, 0.19f), palette.DarkWood, new Vector3(0f, 0f, -6f));
            for (int i = 0; i < 6; i++)
            {
                AddCube(visualRoot, $"WoodGrain_{i}", new Vector3(-0.9f + i * 0.36f, 0.113f, -0.093f), new Vector3(0.26f, 0.012f, 0.012f), palette.DarkWood, new Vector3(0f, 0f, i % 2 == 0 ? 2f : -3f));
            }

            AddCylinder(visualRoot, "NailHeadLeft", new Vector3(-0.92f, 0.118f, -0.05f), 0.035f, 0.012f, palette.DarkMetal);
            AddCylinder(visualRoot, "NailHeadRight", new Vector3(0.92f, 0.118f, -0.05f), 0.035f, 0.012f, palette.DarkMetal);
            AddCube(visualRoot, "JaggedCrack", new Vector3(0.18f, 0.12f, -0.095f), new Vector3(0.46f, 0.014f, 0.014f), palette.DarkWood, new Vector3(0f, 0f, 13f));
        }

        private static void BuildNotebook(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "PageBlock", new Vector3(0.02f, -0.02f, 0f), new Vector3(0.72f, 0.08f, 0.98f), palette.Paper);
            Renderer cover = AddCube(visualRoot, "BlueNotebookCover", new Vector3(0f, 0.04f, 0f), new Vector3(0.78f, 0.045f, 1.04f), palette.FabricBlue).GetComponent<Renderer>();
            AddCube(visualRoot, "CreamLabel", new Vector3(0.12f, 0.07f, -0.18f), new Vector3(0.36f, 0.012f, 0.18f), palette.Cream);
            for (int i = 0; i < 7; i++)
            {
                AddCustom(visualRoot, $"SpiralRing_{i}", torusMesh, new Vector3(-0.43f, 0.08f, -0.38f + i * 0.13f), new Vector3(0.07f, 0.07f, 0.015f), palette.BrushedMetal, new Vector3(0f, 0f, 90f));
            }

            AddCube(visualRoot, "ElasticBand", new Vector3(0.31f, 0.085f, 0f), new Vector3(0.04f, 0.018f, 1.08f), palette.BlackRubber);
            SetObjectReference(root, "feedbackRenderer", cover);
        }

        private static void BuildReadableBook(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "CreamPageBlock", new Vector3(0.02f, -0.015f, 0f), new Vector3(0.8f, 0.1f, 1.05f), palette.Paper);
            AddCube(visualRoot, "RedHardcover", new Vector3(0f, 0.045f, 0f), new Vector3(0.88f, 0.055f, 1.12f), palette.FabricRed);
            AddCube(visualRoot, "GoldSpineBandTop", new Vector3(-0.46f, 0.085f, 0.32f), new Vector3(0.04f, 0.02f, 0.22f), palette.Brass);
            AddCube(visualRoot, "GoldSpineBandBottom", new Vector3(-0.46f, 0.085f, -0.32f), new Vector3(0.04f, 0.02f, 0.22f), palette.Brass);
            AddCube(visualRoot, "TitlePlate", new Vector3(0.12f, 0.085f, 0.05f), new Vector3(0.42f, 0.018f, 0.23f), palette.Brass);
            AddCube(visualRoot, "RibbonBookmark", new Vector3(0.32f, 0.09f, -0.08f), new Vector3(0.045f, 0.016f, 0.82f), palette.GlowRed);
            for (int i = 0; i < 5; i++)
            {
                AddCube(visualRoot, $"PageLine_{i}", new Vector3(0.02f, -0.075f, -0.42f + i * 0.18f), new Vector3(0.78f, 0.01f, 0.012f), palette.Cream);
            }
        }

        private static void BuildCardboardBox(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "BoxBody", Vector3.zero, new Vector3(1f, 0.86f, 1f), palette.Cardboard);
            AddCube(visualRoot, "FrontFaceInset", new Vector3(0f, 0.03f, -0.506f), new Vector3(0.88f, 0.66f, 0.018f), palette.CardboardDark);
            AddCube(visualRoot, "PackingTapeTop", new Vector3(0f, 0.445f, 0f), new Vector3(0.14f, 0.022f, 1.05f), palette.Tape);
            AddCube(visualRoot, "PackingTapeFront", new Vector3(0f, 0.03f, -0.525f), new Vector3(0.14f, 0.72f, 0.018f), palette.Tape);
            AddCube(visualRoot, "LeftOpenFlap", new Vector3(-0.32f, 0.58f, 0f), new Vector3(0.5f, 0.06f, 0.94f), palette.Cardboard, new Vector3(0f, 0f, 18f));
            AddCube(visualRoot, "RightOpenFlap", new Vector3(0.32f, 0.58f, 0f), new Vector3(0.5f, 0.06f, 0.94f), palette.Cardboard, new Vector3(0f, 0f, -18f));
            AddCube(visualRoot, "ShippingLabel", new Vector3(0.28f, 0.14f, -0.535f), new Vector3(0.28f, 0.18f, 0.012f), palette.Paper);
            for (int i = 0; i < 3; i++)
            {
                AddCube(visualRoot, $"LabelLine_{i}", new Vector3(0.28f, 0.18f - i * 0.055f, -0.545f), new Vector3(0.22f, 0.01f, 0.006f), palette.DarkMetal);
            }
        }

        private static void BuildPhotoCamera(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "CameraBody", Vector3.zero, new Vector3(0.7f, 0.44f, 0.25f), palette.BlackRubber);
            AddCube(visualRoot, "LeatherGrip", new Vector3(-0.29f, -0.01f, -0.145f), new Vector3(0.14f, 0.32f, 0.045f), palette.DarkPlastic);
            AddCylinder(visualRoot, "LensOuterRing", new Vector3(0.05f, 0f, -0.22f), 0.23f, 0.16f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "LensGlass", new Vector3(0.05f, 0f, -0.315f), 0.16f, 0.024f, palette.GlassBlue, new Vector3(90f, 0f, 0f));
            AddCustom(visualRoot, "LensHighlightRing", torusMesh, new Vector3(0.05f, 0f, -0.335f), new Vector3(0.31f, 0.31f, 0.018f), palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            AddCube(visualRoot, "ViewFinder", new Vector3(0.17f, 0.29f, -0.02f), new Vector3(0.26f, 0.14f, 0.14f), palette.DarkPlastic);
            AddCube(visualRoot, "FlashWindow", new Vector3(-0.18f, 0.24f, -0.145f), new Vector3(0.18f, 0.08f, 0.035f), palette.Cream);
            AddCylinder(visualRoot, "ShutterButton", new Vector3(-0.28f, 0.25f, 0.02f), 0.055f, 0.035f, palette.BrushedMetal);
            Light flash = AddLight(visualRoot, "CameraFlashLight", new Vector3(-0.18f, 0.24f, -0.18f), LightType.Point, 0f, 4f, Color.white);
            SetObjectReference(root, "flashLight", flash);
        }

        private static void BuildCloset(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "WardrobeCarcass", new Vector3(0f, 1.05f, 0f), new Vector3(1.55f, 2.1f, 0.58f), palette.DarkWood);
            Transform leftDoor = AddEmpty(visualRoot, "LeftDoor_Final", new Vector3(-0.39f, 1.05f, -0.31f), Quaternion.identity);
            Transform rightDoor = AddEmpty(visualRoot, "RightDoor_Final", new Vector3(0.39f, 1.05f, -0.31f), Quaternion.identity);
            BuildWardrobeDoor(leftDoor, -1f);
            BuildWardrobeDoor(rightDoor, 1f);
            AddCube(visualRoot, "TopCrown", new Vector3(0f, 2.16f, -0.01f), new Vector3(1.72f, 0.14f, 0.68f), palette.Wood);
            AddCube(visualRoot, "BottomToeKick", new Vector3(0f, -0.05f, -0.04f), new Vector3(1.66f, 0.14f, 0.58f), palette.DarkWood);
            AddCube(visualRoot, "LeftInteriorShadow", new Vector3(-0.36f, 1.05f, -0.34f), new Vector3(0.68f, 1.76f, 0.02f), palette.Black);
            AddCube(visualRoot, "RightInteriorShadow", new Vector3(0.36f, 1.05f, -0.34f), new Vector3(0.68f, 1.76f, 0.02f), palette.Black);
            SetObjectReference(root, "leftDoor", leftDoor);
            SetObjectReference(root, "rightDoor", rightDoor);
        }

        private static void BuildWardrobeDoor(Transform parent, float side)
        {
            AddCube(parent, "DoorPanel", Vector3.zero, new Vector3(0.72f, 1.92f, 0.07f), palette.Wood);
            AddCube(parent, "RaisedPanelTop", new Vector3(0f, 0.46f, -0.045f), new Vector3(0.5f, 0.66f, 0.035f), palette.DarkWood);
            AddCube(parent, "RaisedPanelBottom", new Vector3(0f, -0.46f, -0.045f), new Vector3(0.5f, 0.66f, 0.035f), palette.DarkWood);
            AddCylinder(parent, "PullHandle", new Vector3(side * -0.25f, 0f, -0.085f), 0.025f, 0.42f, palette.Brass, new Vector3(0f, 0f, 0f));
        }

        private static void BuildCupboard(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "CupboardOuterCase", new Vector3(0f, 0.74f, 0f), new Vector3(1.55f, 1.48f, 0.62f), palette.Wood);
            AddCube(visualRoot, "CounterTop", new Vector3(0f, 1.52f, 0f), new Vector3(1.7f, 0.12f, 0.72f), palette.DarkWood);
            Transform moving = AddEmpty(visualRoot, "MovingDrawer_Final", new Vector3(0f, 0.96f, -0.34f), Quaternion.identity);
            AddCube(moving, "WideDrawerFront", Vector3.zero, new Vector3(1.34f, 0.34f, 0.06f), palette.DarkWood);
            AddCylinder(moving, "DrawerHandle", new Vector3(0f, 0f, -0.055f), 0.025f, 0.48f, palette.Brass, new Vector3(0f, 0f, 90f));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                AddCube(visualRoot, $"LowerDoor_{sx}", new Vector3(sx * 0.36f, 0.38f, -0.34f), new Vector3(0.64f, 0.72f, 0.055f), palette.Wood);
                AddCylinder(visualRoot, $"LowerDoorHandle_{sx}", new Vector3(sx * 0.1f, 0.38f, -0.39f), 0.021f, 0.24f, palette.Brass);
            }

            AddCube(visualRoot, "ShelfLine", new Vector3(0f, 0.72f, -0.385f), new Vector3(1.42f, 0.035f, 0.035f), palette.DarkWood);
            SetObjectReference(root, "movingPart", moving);
        }

        private static void BuildCurtains(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "BlackCurtainRod", new Vector3(0f, 1.38f, -0.02f), 0.035f, 2.05f, palette.DarkMetal, new Vector3(0f, 0f, 90f));
            Transform moving = AddEmpty(visualRoot, "MovingCurtains_Final", Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 7; i++)
            {
                float x = -0.78f + i * 0.26f;
                AddCube(moving, $"LeftFold_{i}", new Vector3(x, 0.62f, -0.03f), new Vector3(0.16f, 1.42f, 0.055f), i % 2 == 0 ? palette.FabricRed : palette.FabricBlue, new Vector3(0f, 0f, i % 2 == 0 ? -2f : 2f));
                AddCustom(moving, $"Ring_{i}", torusMesh, new Vector3(x, 1.33f, -0.02f), new Vector3(0.09f, 0.09f, 0.018f), palette.Brass, new Vector3(90f, 0f, 0f));
            }

            SetObjectReference(root, "movingPart", moving);
        }

        private static void BuildDoorbell(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "DoorbellPlate", Vector3.zero, new Vector3(0.28f, 0.5f, 0.08f), palette.WhitePlastic);
            AddCube(visualRoot, "PlateInset", new Vector3(0f, 0f, -0.045f), new Vector3(0.21f, 0.4f, 0.018f), palette.Cream);
            Transform button = AddCylinder(visualRoot, "ButtonVisual_Final", new Vector3(0f, -0.03f, -0.08f), 0.09f, 0.045f, palette.GlowWarm, new Vector3(90f, 0f, 0f)).transform;
            AddCustom(visualRoot, "ButtonChromeRing", torusMesh, new Vector3(0f, -0.03f, -0.105f), new Vector3(0.19f, 0.19f, 0.016f), palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "TopScrew", new Vector3(0f, 0.2f, -0.055f), 0.025f, 0.01f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "BottomScrew", new Vector3(0f, -0.22f, -0.055f), 0.025f, 0.01f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            SetObjectReference(root, "buttonVisual", button);
        }

        private static void BuildDoor(GameObject root, Transform visualRoot)
        {
            Transform hinge = BuildDoorShared(visualRoot, false, false);
            SetObjectReference(root, "hinge", hinge);
        }

        private static void BuildLatchedDoor(GameObject root, Transform visualRoot)
        {
            Transform hinge = BuildDoorShared(visualRoot, true, false);
            Transform latch = visualRoot.Find("LatchHandle_Final");
            SetObjectReference(root, "hinge", hinge);
            SetObjectReference(root, "latchVisual", latch);
        }

        private static void BuildLockedDoor(GameObject root, Transform visualRoot)
        {
            Transform hinge = BuildDoorShared(visualRoot, true, true);
            SetObjectReference(root, "hinge", hinge);
        }

        private static Transform BuildDoorShared(Transform visualRoot, bool latched, bool locked)
        {
            AddCube(visualRoot, "PaintedDoorSlab", new Vector3(0f, 1.05f, 0f), new Vector3(1.16f, 2.1f, 0.12f), palette.Wood);
            AddCube(visualRoot, "TopRaisedPanel", new Vector3(0f, 1.45f, -0.075f), new Vector3(0.82f, 0.62f, 0.035f), palette.DarkWood);
            AddCube(visualRoot, "BottomRaisedPanel", new Vector3(0f, 0.58f, -0.075f), new Vector3(0.82f, 0.74f, 0.035f), palette.DarkWood);
            AddCube(visualRoot, "CenterRail", new Vector3(0f, 1.02f, -0.086f), new Vector3(1f, 0.08f, 0.03f), palette.DarkWood);
            Transform hinge = AddEmpty(visualRoot, "Hinge_Final", new Vector3(-0.62f, 1.05f, 0f), Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                AddCylinder(visualRoot, $"BrassHinge_{i}", new Vector3(-0.62f, 0.34f + i * 0.71f, -0.02f), 0.025f, 0.22f, palette.Brass);
            }

            AddCylinder(visualRoot, "RoundKnob", new Vector3(0.44f, 1.04f, -0.12f), 0.085f, 0.06f, palette.Brass, new Vector3(90f, 0f, 0f));
            AddCube(visualRoot, "LatchPlate", new Vector3(0.43f, 1.04f, -0.086f), new Vector3(0.22f, 0.36f, 0.018f), palette.Brass);
            if (latched)
            {
                Transform latch = AddCube(visualRoot, "LatchHandle_Final", new Vector3(0.55f, 1.22f, -0.13f), new Vector3(0.32f, 0.04f, 0.04f), palette.Brass, new Vector3(0f, 0f, -8f)).transform;
                AddCylinder(latch, "LatchPivot", new Vector3(-0.16f, 0f, 0f), 0.038f, 0.03f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            }

            if (locked)
            {
                AddCube(visualRoot, "DeadboltPlate", new Vector3(0.43f, 1.34f, -0.12f), new Vector3(0.2f, 0.18f, 0.035f), palette.DarkMetal);
                AddCube(visualRoot, "KeySlot", new Vector3(0.43f, 1.34f, -0.145f), new Vector3(0.035f, 0.11f, 0.015f), palette.Black);
            }

            return hinge;
        }

        private static void BuildDoorFrame(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "LeftJamb", new Vector3(-0.79f, 1.35f, 0f), new Vector3(0.18f, 2.72f, 0.22f), palette.DarkWood);
            AddCube(visualRoot, "RightJamb", new Vector3(0.79f, 1.35f, 0f), new Vector3(0.18f, 2.72f, 0.22f), palette.DarkWood);
            AddCube(visualRoot, "Header", new Vector3(0f, 2.69f, 0f), new Vector3(1.76f, 0.18f, 0.22f), palette.DarkWood);
            AddCube(visualRoot, "LeftTrim", new Vector3(-0.94f, 1.35f, -0.08f), new Vector3(0.08f, 2.82f, 0.08f), palette.Wood);
            AddCube(visualRoot, "RightTrim", new Vector3(0.94f, 1.35f, -0.08f), new Vector3(0.08f, 2.82f, 0.08f), palette.Wood);
            AddCube(visualRoot, "TopTrim", new Vector3(0f, 2.84f, -0.08f), new Vector3(1.96f, 0.08f, 0.08f), palette.Wood);
            AddCube(visualRoot, "DoorStopLeft", new Vector3(-0.64f, 1.35f, -0.12f), new Vector3(0.05f, 2.46f, 0.07f), palette.Wood);
            AddCube(visualRoot, "DoorStopRight", new Vector3(0.64f, 1.35f, -0.12f), new Vector3(0.05f, 2.46f, 0.07f), palette.Wood);
            AddCube(visualRoot, "DoorStopHeader", new Vector3(0f, 2.57f, -0.12f), new Vector3(1.26f, 0.05f, 0.07f), palette.Wood);
        }

        private static void BuildFlashlight(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "KnurledBody", new Vector3(0f, 0f, 0f), 0.12f, 0.74f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "WideHead", new Vector3(0f, 0f, -0.43f), 0.18f, 0.22f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            Renderer lens = AddCylinder(visualRoot, "GlassLens_Final", new Vector3(0f, 0f, -0.56f), 0.145f, 0.025f, palette.GlassBlue, new Vector3(90f, 0f, 0f)).GetComponent<Renderer>();
            for (int i = 0; i < 5; i++)
            {
                AddCylinder(visualRoot, $"GripRing_{i}", new Vector3(0f, 0f, -0.18f + i * 0.07f), 0.126f, 0.014f, palette.BlackRubber, new Vector3(90f, 0f, 0f));
            }

            AddCube(visualRoot, "SlideSwitch", new Vector3(0f, 0.12f, -0.09f), new Vector3(0.08f, 0.025f, 0.18f), palette.GlowWarm);
            Light light = AddLight(visualRoot, "FlashlightSpot_Final", new Vector3(0f, 0f, -0.59f), LightType.Spot, 0f, 9f, new Color(1f, 0.9f, 0.56f));
            light.spotAngle = 38f;
            SetObjectReference(root, "flashlightLight", light);
            SetObjectReference(root, "lensRenderer", lens);
        }

        private static void BuildTomato(GameObject root, Transform visualRoot)
        {
            Transform tomato = AddSphere(visualRoot, "TomatoVisual_Final", Vector3.zero, new Vector3(0.46f, 0.39f, 0.46f), palette.TomatoRed).transform;
            Renderer tomatoRenderer = tomato.GetComponent<Renderer>();
            AddSphere(visualRoot, "TomatoLobeLeft", new Vector3(-0.11f, 0.02f, 0f), new Vector3(0.28f, 0.32f, 0.34f), palette.TomatoRed);
            AddSphere(visualRoot, "TomatoLobeRight", new Vector3(0.11f, 0.02f, 0f), new Vector3(0.28f, 0.32f, 0.34f), palette.TomatoRed);
            Renderer stem = AddCylinder(visualRoot, "Stem_Final", new Vector3(0f, 0.24f, 0f), 0.035f, 0.16f, palette.TomatoGreen, new Vector3(12f, 0f, 24f)).GetComponent<Renderer>();
            for (int i = 0; i < 5; i++)
            {
                AddCube(visualRoot, $"Leaf_{i}", new Vector3(Mathf.Cos(i * 72f * Mathf.Deg2Rad) * 0.09f, 0.19f, Mathf.Sin(i * 72f * Mathf.Deg2Rad) * 0.09f), new Vector3(0.16f, 0.025f, 0.055f), palette.TomatoGreen, new Vector3(0f, -i * 72f, 16f));
            }

            SetObjectReference(root, "tomatoVisual", tomato);
            SetObjectReference(root, "tomatoRenderer", tomatoRenderer);
            SetObjectReference(root, "stemRenderer", stem);
        }

        private static void BuildGlass(GameObject root, Transform visualRoot)
        {
            Transform intact = AddEmpty(visualRoot, "IntactGlass_Final", Vector3.zero, Quaternion.identity);
            AddCube(intact, "GlassPane", Vector3.zero, new Vector3(0.08f, 1.5f, 2f), palette.GlassBlue);
            AddCube(intact, "LeftMetalEdge", new Vector3(0f, 0f, -1.04f), new Vector3(0.1f, 1.58f, 0.045f), palette.BrushedMetal);
            AddCube(intact, "RightMetalEdge", new Vector3(0f, 0f, 1.04f), new Vector3(0.1f, 1.58f, 0.045f), palette.BrushedMetal);
            AddCube(intact, "TopMetalEdge", new Vector3(0f, 0.79f, 0f), new Vector3(0.1f, 0.045f, 2.12f), palette.BrushedMetal);
            AddCube(intact, "BottomMetalEdge", new Vector3(0f, -0.79f, 0f), new Vector3(0.1f, 0.045f, 2.12f), palette.BrushedMetal);
            Transform shards = AddEmpty(visualRoot, "GlassShardRoot_Final", Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 7; i++)
            {
                Mesh mesh = i % 2 == 0 ? shardMeshA : shardMeshB;
                AddCustom(shards, $"Shard_{i:00}", mesh, new Vector3(0.01f, -0.42f + i * 0.14f, -0.76f + i * 0.24f), new Vector3(0.42f, 0.42f, 0.01f), palette.GlassBlue, new Vector3(0f, 90f, i * 23f));
            }

            SetObjectReference(root, "intactVisualRoot", intact.gameObject);
            SetObjectReference(root, "shardRoot", shards);
        }

        private static void BuildKey(GameObject root, Transform visualRoot)
        {
            AddCustom(visualRoot, "KeyBowRing", torusMesh, new Vector3(-0.34f, 0f, 0f), new Vector3(0.36f, 0.36f, 0.04f), palette.Brass, new Vector3(0f, 0f, 90f));
            AddCube(visualRoot, "KeyShaft", new Vector3(0.15f, 0f, 0f), new Vector3(0.7f, 0.08f, 0.05f), palette.Brass);
            AddCube(visualRoot, "KeyBitMain", new Vector3(0.56f, -0.09f, 0f), new Vector3(0.18f, 0.18f, 0.05f), palette.Brass);
            AddCube(visualRoot, "KeyBitTooth", new Vector3(0.72f, -0.02f, 0f), new Vector3(0.11f, 0.12f, 0.05f), palette.Brass);
            AddCube(visualRoot, "KeyGroove", new Vector3(0.15f, 0.04f, -0.035f), new Vector3(0.52f, 0.012f, 0.012f), palette.DarkMetal);
        }

        private static void BuildLightSwitch(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "WallPlate", Vector3.zero, new Vector3(0.34f, 0.55f, 0.06f), palette.WhitePlastic);
            AddCube(visualRoot, "InsetBevel", new Vector3(0f, 0f, -0.038f), new Vector3(0.24f, 0.42f, 0.018f), palette.Cream);
            Transform lever = AddCube(visualRoot, "SwitchLever_Final", new Vector3(0f, 0.04f, -0.07f), new Vector3(0.11f, 0.24f, 0.055f), palette.WhitePlastic, new Vector3(-12f, 0f, 0f)).transform;
            AddCylinder(visualRoot, "TopScrew", new Vector3(0f, 0.22f, -0.045f), 0.022f, 0.009f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "BottomScrew", new Vector3(0f, -0.22f, -0.045f), 0.022f, 0.009f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            SetObjectReference(root, "switchLever", lever);
        }

        private static void BuildMirror(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "DarkFrame", Vector3.zero, new Vector3(1.06f, 1.45f, 0.08f), palette.DarkWood);
            AddCube(visualRoot, "MirrorPane", new Vector3(0f, 0f, -0.055f), new Vector3(0.86f, 1.24f, 0.025f), palette.Mirror);
            AddCube(visualRoot, "InnerHighlight", new Vector3(-0.18f, 0.32f, -0.073f), new Vector3(0.36f, 0.04f, 0.012f), palette.GlassBlue, new Vector3(0f, 0f, 28f));
            AddCube(visualRoot, "BottomShelf", new Vector3(0f, -0.78f, -0.08f), new Vector3(1.14f, 0.09f, 0.16f), palette.Wood);
            AddCube(visualRoot, "LeftMiter", new Vector3(-0.55f, 0f, -0.08f), new Vector3(0.08f, 1.42f, 0.08f), palette.Wood);
            AddCube(visualRoot, "RightMiter", new Vector3(0.55f, 0f, -0.08f), new Vector3(0.08f, 1.42f, 0.08f), palette.Wood);
        }

        private static void BuildRemoteControl(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "RemoteBody", Vector3.zero, new Vector3(0.32f, 0.08f, 0.92f), palette.DarkPlastic);
            Renderer power = AddCylinder(visualRoot, "PowerButton_Final", new Vector3(0f, 0.06f, 0.31f), 0.045f, 0.018f, palette.GlowRed).GetComponent<Renderer>();
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    AddCylinder(visualRoot, $"RubberButton_{row}_{col}", new Vector3(-0.09f + col * 0.09f, 0.055f, 0.12f - row * 0.13f), 0.026f, 0.014f, palette.BlackRubber);
                }
            }

            AddCube(visualRoot, "InfraredWindow", new Vector3(0f, 0.035f, 0.48f), new Vector3(0.18f, 0.035f, 0.045f), palette.GlassBlue);
            Transform signalOrigin = AddEmpty(visualRoot, "SignalOrigin_Final", new Vector3(0f, 0.04f, 0.52f), Quaternion.identity);
            SetObjectReference(root, "signalOrigin", signalOrigin);
            SetObjectReference(root, "buttonRenderer", power);
        }

        private static void BuildLaserGrid(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "LeftEmitterPost", new Vector3(-1.62f, 0.9f, 0f), new Vector3(0.16f, 2.15f, 0.18f), palette.DarkMetal);
            AddCube(visualRoot, "RightReceiverPost", new Vector3(1.62f, 0.9f, 0f), new Vector3(0.16f, 2.15f, 0.18f), palette.DarkMetal);
            AddCube(visualRoot, "TopCableTray", new Vector3(0f, 1.98f, 0.02f), new Vector3(3.42f, 0.1f, 0.12f), palette.BlackRubber);
            Transform beamRoot = AddEmpty(visualRoot, "BeamVisualRoot_Final", Vector3.zero, Quaternion.identity);
            Renderer[] beamRenderers = new Renderer[5];
            for (int i = 0; i < beamRenderers.Length; i++)
            {
                float y = 0.22f + i * 0.34f;
                beamRenderers[i] = AddCylinder(beamRoot, $"LaserBeam_{i:00}", new Vector3(0f, y, 0f), 0.035f, 3.16f, palette.GlowRed, new Vector3(0f, 0f, 90f)).GetComponent<Renderer>();
                AddCylinder(visualRoot, $"EmitterLens_{i:00}", new Vector3(-1.62f, y, -0.105f), 0.055f, 0.02f, palette.GlowRed, new Vector3(90f, 0f, 0f));
                AddCylinder(visualRoot, $"ReceiverLens_{i:00}", new Vector3(1.62f, y, -0.105f), 0.055f, 0.02f, palette.GlowRed, new Vector3(90f, 0f, 0f));
            }

            Transform lever = AddCube(visualRoot, "PowerSwitchLever_Final", new Vector3(-1.82f, 1.72f, -0.15f), new Vector3(0.08f, 0.26f, 0.06f), palette.HazardYellow, new Vector3(-18f, 0f, 0f)).transform;
            AddCube(visualRoot, "WarningPanel", new Vector3(-1.82f, 1.32f, -0.12f), new Vector3(0.24f, 0.22f, 0.035f), palette.HazardYellow);
            SetObjectReference(root, "beamVisualRoot", beamRoot);
            SetObjectArrayReference(root, "beamRenderers", beamRenderers);
            SetObjectReference(root, "switchLever", lever);
        }

        private static void BuildSecurityCamera(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "WallMountPlate", new Vector3(0f, 0.05f, 0.24f), new Vector3(0.42f, 0.5f, 0.08f), palette.WhitePlastic);
            AddCylinder(visualRoot, "SwivelArm", new Vector3(0f, 0.02f, 0.02f), 0.055f, 0.5f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            Transform eye = AddEmpty(visualRoot, "CameraEye_Final", new Vector3(0f, 0f, -0.23f), Quaternion.identity);
            AddCube(eye, "CameraHousing", Vector3.zero, new Vector3(0.46f, 0.28f, 0.36f), palette.WhitePlastic);
            Renderer lens = AddCylinder(eye, "BlackLensBarrel", new Vector3(0f, 0f, -0.23f), 0.14f, 0.14f, palette.BlackRubber, new Vector3(90f, 0f, 0f)).GetComponent<Renderer>();
            AddCylinder(eye, "GlassLens", new Vector3(0f, 0f, -0.31f), 0.09f, 0.018f, palette.GlassBlue, new Vector3(90f, 0f, 0f));
            AddCube(eye, "StatusLed", new Vector3(0.18f, 0.09f, -0.18f), new Vector3(0.045f, 0.045f, 0.018f), palette.GlowRed);
            Transform sight = AddEmpty(eye, "SightBeam_Final", Vector3.zero, Quaternion.identity);
            AddCustom(sight, "SightConePreview", coneMesh, new Vector3(0f, 0f, 0.04f), new Vector3(0.75f, 0.75f, 1f), palette.GlassRed, Vector3.zero).SetActive(false);
            SetObjectReference(root, "eye", eye);
            SetObjectReference(root, "sightBeam", sight);
        }

        private static void BuildBasketball(GameObject root, Transform visualRoot)
        {
            AddSphere(visualRoot, "BasketballOrangeBody", Vector3.zero, new Vector3(0.52f, 0.52f, 0.52f), palette.BasketballOrange);
            AddCustom(visualRoot, "HorizontalSeam", torusMesh, Vector3.zero, new Vector3(0.535f, 0.535f, 0.024f), palette.BlackRubber);
            AddCustom(visualRoot, "VerticalSeamA", torusMesh, Vector3.zero, new Vector3(0.535f, 0.535f, 0.024f), palette.BlackRubber, new Vector3(90f, 0f, 0f));
            AddCustom(visualRoot, "VerticalSeamB", torusMesh, Vector3.zero, new Vector3(0.535f, 0.535f, 0.024f), palette.BlackRubber, new Vector3(0f, 0f, 90f));
            AddCube(visualRoot, "PebbledHighlight", new Vector3(0.11f, 0.19f, -0.21f), new Vector3(0.2f, 0.018f, 0.018f), palette.HazardYellow, new Vector3(0f, 28f, -24f));
        }

        private static void BuildSprayCan(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "MetalCanBody", Vector3.zero, 0.16f, 0.72f, palette.BrushedMetal);
            AddCylinder(visualRoot, "TopRim", new Vector3(0f, 0.38f, 0f), 0.17f, 0.035f, palette.DarkMetal);
            AddCylinder(visualRoot, "BottomRim", new Vector3(0f, -0.38f, 0f), 0.17f, 0.035f, palette.DarkMetal);
            AddCube(visualRoot, "ColorLabel", new Vector3(0f, 0f, -0.165f), new Vector3(0.24f, 0.42f, 0.018f), palette.FabricRed);
            AddCube(visualRoot, "WhiteLabelStripe", new Vector3(0f, 0.06f, -0.178f), new Vector3(0.22f, 0.05f, 0.012f), palette.Paper);
            AddCylinder(visualRoot, "SprayNozzle", new Vector3(0f, 0.47f, -0.04f), 0.06f, 0.08f, palette.WhitePlastic);
            AddCube(visualRoot, "NozzleSlot", new Vector3(0f, 0.47f, -0.105f), new Vector3(0.055f, 0.025f, 0.025f), palette.Black);
        }

        private static void BuildHugeSwingingAxe(GameObject root, Transform visualRoot)
        {
            Transform swing = AddEmpty(visualRoot, "SwingingAxe_Final", Vector3.zero, Quaternion.identity);
            AddCylinder(swing, "HeavyChain", new Vector3(0f, 2.4f, 0f), 0.045f, 4.25f, palette.DarkMetal);
            AddCylinder(swing, "CrossPin", new Vector3(0f, 0.18f, 0f), 0.06f, 0.56f, palette.DarkMetal, new Vector3(0f, 0f, 90f));
            AddCube(swing, "AxeHandle", new Vector3(0f, -0.82f, 0f), new Vector3(0.16f, 2f, 0.14f), palette.DarkWood);
            AddCustom(swing, "LeftBlade", sawBladeMesh, new Vector3(-0.42f, -1.48f, 0f), new Vector3(0.76f, 0.55f, 0.08f), palette.BrushedMetal, new Vector3(0f, 0f, 90f));
            AddCustom(swing, "RightBlade", sawBladeMesh, new Vector3(0.42f, -1.48f, 0f), new Vector3(0.76f, 0.55f, 0.08f), palette.BrushedMetal, new Vector3(0f, 0f, -90f));
            AddCube(swing, "RedDangerWrap", new Vector3(0f, -1.16f, -0.09f), new Vector3(0.2f, 0.18f, 0.045f), palette.GlowRed);
            SetObjectArrayReference(root, "swingingParts", new UnityEngine.Object[] { swing });
        }

        private static void BuildHugeSwingingAxeTripWire(GameObject root, Transform visualRoot)
        {
            BuildHugeSwingingAxe(root, visualRoot);
            Renderer wire = AddCylinder(visualRoot, "TripWire_Final", new Vector3(0f, 0.24f, -1.1f), 0.012f, 2.4f, palette.GlowRed, new Vector3(0f, 0f, 90f)).GetComponent<Renderer>();
            AddCube(visualRoot, "LeftTripAnchor", new Vector3(-1.2f, 0.24f, -1.1f), new Vector3(0.1f, 0.24f, 0.08f), palette.DarkMetal);
            AddCube(visualRoot, "RightTripAnchor", new Vector3(1.2f, 0.24f, -1.1f), new Vector3(0.1f, 0.24f, 0.08f), palette.DarkMetal);
            SetObjectReference(root, "wireRenderer", wire);
        }

        private static void BuildPhysicsSwingingObject(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "Rope", new Vector3(0f, 1.1f, 0f), 0.035f, 2.2f, palette.BlackRubber);
            AddSphere(visualRoot, "WeightedBall", new Vector3(0f, -0.18f, 0f), new Vector3(0.48f, 0.48f, 0.48f), palette.DarkMetal);
            AddCylinder(visualRoot, "EquatorBand", new Vector3(0f, -0.18f, 0f), 0.255f, 0.04f, palette.BrushedMetal);
            AddCustom(visualRoot, "BottomRing", torusMesh, new Vector3(0f, -0.47f, 0f), new Vector3(0.22f, 0.22f, 0.028f), palette.BrushedMetal);
        }

        private static void BuildScrewdriver(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "RedHandleGrip", new Vector3(0f, 0f, -0.18f), 0.12f, 0.5f, palette.FabricRed, new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                AddCylinder(visualRoot, $"GripGroove_{i}", new Vector3(0f, 0f, -0.36f + i * 0.1f), 0.124f, 0.018f, palette.BlackRubber, new Vector3(90f, 0f, 0f));
            }

            AddCylinder(visualRoot, "SteelShaft", new Vector3(0f, 0f, 0.28f), 0.035f, 0.68f, palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            AddCube(visualRoot, "FlatTip", new Vector3(0f, 0f, 0.65f), new Vector3(0.11f, 0.026f, 0.12f), palette.DarkMetal);
        }

        private static void BuildWindUpToy(GameObject root, Transform visualRoot)
        {
            Renderer body = AddCube(visualRoot, "ToyBody", new Vector3(0f, 0.12f, 0f), new Vector3(0.52f, 0.32f, 0.34f), palette.FabricBlue).GetComponent<Renderer>();
            AddSphere(visualRoot, "ToyHead", new Vector3(0f, 0.37f, -0.05f), new Vector3(0.28f, 0.26f, 0.28f), palette.FabricRed);
            Transform wheelA = AddCustom(visualRoot, "LeftWheel_Final", torusMesh, new Vector3(-0.28f, -0.06f, -0.14f), new Vector3(0.16f, 0.16f, 0.04f), palette.BlackRubber, new Vector3(0f, 0f, 90f)).transform;
            Transform wheelB = AddCustom(visualRoot, "RightWheel_Final", torusMesh, new Vector3(0.28f, -0.06f, -0.14f), new Vector3(0.16f, 0.16f, 0.04f), palette.BlackRubber, new Vector3(0f, 0f, 90f)).transform;
            Transform key = AddCustom(visualRoot, "WindUpKey_Final", torusMesh, new Vector3(0.33f, 0.17f, 0.02f), new Vector3(0.14f, 0.14f, 0.028f), palette.Brass, new Vector3(0f, 0f, 90f)).transform;
            AddCube(visualRoot, "ForwardArrowNose", new Vector3(0f, 0.16f, -0.23f), new Vector3(0.18f, 0.08f, 0.1f), palette.HazardYellow);
            Transform forward = AddEmpty(visualRoot, "ForwardReference_Final", new Vector3(0f, 0.16f, -0.42f), Quaternion.identity);
            SetObjectReference(root, "forwardReference", forward);
            SetObjectReference(root, "feedbackRenderer", body);
            SetObjectArrayReference(root, "spinningParts", new UnityEngine.Object[] { wheelA, wheelB, key });
        }

        private static void BuildTrapDoor(GameObject root, Transform visualRoot)
        {
            Transform left = AddEmpty(visualRoot, "LeftPanel_Final", new Vector3(-0.52f, 0f, 0f), Quaternion.identity);
            Transform right = AddEmpty(visualRoot, "RightPanel_Final", new Vector3(0.52f, 0f, 0f), Quaternion.identity);
            AddCube(left, "LeftFloorPanel", Vector3.zero, new Vector3(0.98f, 0.1f, 1.1f), palette.Wood);
            AddCube(right, "RightFloorPanel", Vector3.zero, new Vector3(0.98f, 0.1f, 1.1f), palette.Wood);
            AddCube(visualRoot, "FloorSeam", new Vector3(0f, 0.065f, 0f), new Vector3(0.035f, 0.025f, 1.12f), palette.DarkWood);
            AddCylinder(visualRoot, "LeftHingeRod", new Vector3(-1.02f, 0.02f, 0f), 0.035f, 1.08f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            AddCylinder(visualRoot, "RightHingeRod", new Vector3(1.02f, 0.02f, 0f), 0.035f, 1.08f, palette.DarkMetal, new Vector3(90f, 0f, 0f));
            AddCube(visualRoot, "CamouflageFloorGrain", new Vector3(0f, 0.08f, -0.28f), new Vector3(1.8f, 0.018f, 0.035f), palette.DarkWood);
            SetObjectReference(root, "leftPanel", left);
            SetObjectReference(root, "rightPanel", right);
        }

        private static void BuildBoxingGloveTrap(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "WallMountBase", Vector3.zero, new Vector3(0.72f, 0.72f, 0.14f), palette.DarkMetal);
            Transform spring = AddEmpty(visualRoot, "Spring_Final", new Vector3(0f, 0f, 0.42f), Quaternion.identity);
            for (int i = 0; i < 8; i++)
            {
                AddCustom(spring, $"SpringCoil_{i}", torusMesh, new Vector3(0f, 0f, -0.28f + i * 0.08f), new Vector3(0.24f, 0.24f, 0.026f), palette.BrushedMetal, new Vector3(90f, 0f, 0f));
            }

            Transform glove = AddEmpty(visualRoot, "Glove_Final", new Vector3(0f, 0f, 0.95f), Quaternion.identity);
            Renderer gloveRenderer = AddSphere(glove, "PaddedGlovePalm", Vector3.zero, new Vector3(0.46f, 0.34f, 0.34f), palette.RubberRed).GetComponent<Renderer>();
            AddSphere(glove, "ThumbPad", new Vector3(0.22f, -0.06f, -0.02f), new Vector3(0.18f, 0.14f, 0.2f), palette.RubberRed);
            AddCube(glove, "WhiteCuff", new Vector3(0f, -0.01f, -0.24f), new Vector3(0.4f, 0.26f, 0.14f), palette.WhitePlastic);
            AddCube(visualRoot, "YellowTriggerMarker", new Vector3(0f, -0.41f, -0.09f), new Vector3(0.36f, 0.08f, 0.035f), palette.HazardYellow);
            SetObjectReference(root, "glove", glove);
            SetObjectReference(root, "spring", spring);
            SetObjectReference(root, "gloveRenderer", gloveRenderer);
        }

        private static void BuildSawBladeTrap(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "RailBase", new Vector3(0f, 0f, 0f), new Vector3(0.72f, 0.18f, 1.1f), palette.DarkMetal);
            Transform sliding = AddEmpty(visualRoot, "SlidingCarriage_Final", new Vector3(0f, 0.18f, 0f), Quaternion.identity);
            AddCube(sliding, "CarriageBlock", Vector3.zero, new Vector3(0.48f, 0.18f, 0.3f), palette.BrushedMetal);
            Transform blade = AddCustom(sliding, "BladeVisual_Final", sawBladeMesh, new Vector3(0f, 0.19f, 0f), new Vector3(0.62f, 0.08f, 0.62f), palette.BrushedMetal).transform;
            AddCylinder(sliding, "BladeHub", new Vector3(0f, 0.19f, 0f), 0.08f, 0.08f, palette.DarkMetal);
            Renderer warning = AddCube(visualRoot, "WarningPanel_Final", new Vector3(0f, 0.16f, -0.58f), new Vector3(0.52f, 0.12f, 0.035f), palette.HazardYellow).GetComponent<Renderer>();
            LineRenderer line = AddLineRenderer(visualRoot, "DangerLine_Final", palette.GlowRed, 0.035f);
            SetObjectReference(root, "bladeVisual", blade);
            SetObjectReference(root, "slidingVisual", sliding);
            SetObjectReference(root, "dangerLine", line);
            SetObjectReference(root, "warningRenderer", warning);
        }

        private static void BuildTelevision(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "TVBody", new Vector3(0f, 0.55f, 0f), new Vector3(1.35f, 0.82f, 0.18f), palette.DarkPlastic);
            Renderer screen = AddCube(visualRoot, "Screen_Final", new Vector3(0f, 0.58f, -0.105f), new Vector3(1.16f, 0.64f, 0.026f), palette.ScreenOff).GetComponent<Renderer>();
            AddCube(visualRoot, "SpeakerGrillLeft", new Vector3(-0.56f, 0.1f, -0.11f), new Vector3(0.22f, 0.13f, 0.018f), palette.Black);
            AddCube(visualRoot, "SpeakerGrillRight", new Vector3(0.56f, 0.1f, -0.11f), new Vector3(0.22f, 0.13f, 0.018f), palette.Black);
            for (int i = 0; i < 4; i++)
            {
                AddCube(visualRoot, $"VentSlot_{i}", new Vector3(-0.56f + i * 0.37f, 0.94f, 0.105f), new Vector3(0.22f, 0.018f, 0.018f), palette.Black);
            }

            AddCube(visualRoot, "NeckStand", new Vector3(0f, 0.04f, 0.02f), new Vector3(0.22f, 0.22f, 0.12f), palette.DarkPlastic);
            AddCube(visualRoot, "WideStandFoot", new Vector3(0f, -0.09f, 0f), new Vector3(0.82f, 0.08f, 0.36f), palette.DarkPlastic);
            AddCylinder(visualRoot, "PowerButton", new Vector3(0.48f, 0.1f, -0.13f), 0.03f, 0.018f, palette.GlowRed, new Vector3(90f, 0f, 0f));
            SetObjectReference(root, "screenRenderer", screen);
        }

        private static void BuildUmbrella(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "BlackHandleShaft", new Vector3(0f, 0f, 0f), 0.035f, 1.2f, palette.BlackRubber);
            AddCustom(visualRoot, "CurvedHookHandle", torusMesh, new Vector3(0f, -0.66f, 0f), new Vector3(0.24f, 0.24f, 0.035f), palette.BlackRubber, new Vector3(0f, 0f, 90f));
            Transform open = AddEmpty(visualRoot, "OpenCanopy_Final", new Vector3(0f, 0.64f, 0f), Quaternion.identity);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                AddCube(open, $"CanopyPanel_{i:00}", new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.23f, 0f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.23f), new Vector3(0.38f, 0.045f, 0.72f), i % 2 == 0 ? palette.FabricRed : palette.FabricBlue, new Vector3(0f, angle, 14f));
                AddCylinder(open, $"CanopyRib_{i:00}", new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.32f, -0.02f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.32f), 0.012f, 0.72f, palette.BrushedMetal, new Vector3(90f, angle, 0f));
            }

            Transform closed = AddEmpty(visualRoot, "ClosedCanopy_Final", new Vector3(0f, 0.18f, 0f), Quaternion.identity);
            AddCylinder(closed, "WrappedFabric", Vector3.zero, 0.12f, 0.86f, palette.FabricRed);
            AddCube(closed, "TieBand", new Vector3(0f, 0.14f, -0.13f), new Vector3(0.22f, 0.07f, 0.035f), palette.HazardYellow);
            SetObjectReference(root, "openCanopy", open.gameObject);
            SetObjectReference(root, "closedCanopy", closed.gameObject);
        }

        private static void BuildScrew(GameObject root, Transform visualRoot)
        {
            AddCylinder(visualRoot, "ScrewHead", new Vector3(0f, 0.06f, 0f), 0.12f, 0.045f, palette.BrushedMetal);
            AddCylinder(visualRoot, "ThreadedShaft", new Vector3(0f, -0.1f, 0f), 0.045f, 0.34f, palette.BrushedMetal);
            AddCube(visualRoot, "PhillipsSlotA", new Vector3(0f, 0.09f, 0f), new Vector3(0.2f, 0.012f, 0.025f), palette.DarkMetal);
            AddCube(visualRoot, "PhillipsSlotB", new Vector3(0f, 0.091f, 0f), new Vector3(0.025f, 0.012f, 0.2f), palette.DarkMetal);
            for (int i = 0; i < 4; i++)
            {
                AddCustom(visualRoot, $"ThreadRidge_{i}", torusMesh, new Vector3(0f, -0.02f - i * 0.065f, 0f), new Vector3(0.095f, 0.095f, 0.012f), palette.DarkMetal);
            }
        }

        private static void BuildVentCover(GameObject root, Transform visualRoot)
        {
            Renderer cover = AddCube(visualRoot, "VentCoverPlate_Final", Vector3.zero, new Vector3(1.15f, 0.08f, 0.72f), palette.DullMetal).GetComponent<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                AddCube(visualRoot, $"AngledSlat_{i}", new Vector3(-0.42f + i * 0.17f, 0.06f, 0f), new Vector3(0.055f, 0.05f, 0.58f), palette.DarkMetal, new Vector3(0f, 0f, 16f));
            }

            GameObject[] screws = new GameObject[4];
            GameObject[] removed = new GameObject[4];
            Vector3[] positions =
            {
                new Vector3(-0.48f, 0.075f, -0.28f),
                new Vector3(0.48f, 0.075f, -0.28f),
                new Vector3(-0.48f, 0.075f, 0.28f),
                new Vector3(0.48f, 0.075f, 0.28f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                screws[i] = AddCylinder(visualRoot, $"InstalledScrew_{i}", positions[i], 0.045f, 0.016f, palette.BrushedMetal).gameObject;
                removed[i] = AddCylinder(visualRoot, $"RemovedScrewGhost_{i}", positions[i] + Vector3.up * 0.04f, 0.038f, 0.012f, palette.DarkMetal).gameObject;
                removed[i].SetActive(false);
            }

            SetObjectArrayReference(root, "screwVisuals", screws);
            SetObjectArrayReference(root, "removedScrewVisuals", removed);
            SetObjectReference(root, "coverRenderer", cover);
        }

        private static void BuildWindowBlinds(GameObject root, Transform visualRoot)
        {
            AddCube(visualRoot, "TopRail", new Vector3(0f, 0.9f, 0f), new Vector3(1.5f, 0.08f, 0.1f), palette.WhitePlastic);
            Transform moving = AddEmpty(visualRoot, "MovingBlinds_Final", Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 9; i++)
            {
                AddCube(moving, $"CurvedSlat_{i:00}", new Vector3(0f, 0.72f - i * 0.16f, 0f), new Vector3(1.42f, 0.035f, 0.075f), palette.Cream, new Vector3(8f, 0f, 0f));
            }

            AddCylinder(visualRoot, "LeftCord", new Vector3(-0.55f, 0.16f, -0.08f), 0.008f, 1.35f, palette.BlackRubber);
            AddCylinder(visualRoot, "RightCord", new Vector3(0.55f, 0.16f, -0.08f), 0.008f, 1.35f, palette.BlackRubber);
            AddCylinder(visualRoot, "PullCord", new Vector3(0.72f, 0.28f, -0.08f), 0.009f, 0.85f, palette.BlackRubber);
            AddCube(visualRoot, "PullWeight", new Vector3(0.72f, -0.18f, -0.08f), new Vector3(0.05f, 0.12f, 0.04f), palette.WhitePlastic);
            SetObjectReference(root, "movingPart", moving);
        }

        private static Transform AddEmpty(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            GameObject child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        private static GameObject AddCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return AddPrimitive(parent, PrimitiveType.Cube, name, position, scale, material, Quaternion.identity);
        }

        private static GameObject AddCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler)
        {
            return AddPrimitive(parent, PrimitiveType.Cube, name, position, scale, material, Quaternion.Euler(euler));
        }

        private static GameObject AddSphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return AddPrimitive(parent, PrimitiveType.Sphere, name, position, scale, material, Quaternion.identity);
        }

        private static GameObject AddCylinder(Transform parent, string name, Vector3 position, float radius, float height, Material material)
        {
            return AddCylinder(parent, name, position, radius, height, material, Vector3.zero);
        }

        private static GameObject AddCylinder(Transform parent, string name, Vector3 position, float radius, float height, Material material, Vector3 euler)
        {
            return AddPrimitive(parent, PrimitiveType.Cylinder, name, position, new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, Quaternion.Euler(euler));
        }

        private static GameObject AddPrimitive(Transform parent, PrimitiveType primitiveType, string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return child;
        }

        private static GameObject AddCustom(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            return AddCustom(parent, name, mesh, position, scale, material, Vector3.zero);
        }

        private static GameObject AddCustom(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, Vector3 euler)
        {
            GameObject child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = Quaternion.Euler(euler);
            child.transform.localScale = scale;
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return child;
        }

        private static Light AddLight(Transform parent, string name, Vector3 position, LightType lightType, float intensity, float range, Color color)
        {
            GameObject child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = Quaternion.identity;
            Light light = child.AddComponent<Light>();
            light.type = lightType;
            light.intensity = intensity;
            light.range = range;
            light.color = color;
            light.enabled = false;
            return light;
        }

        private static LineRenderer AddLineRenderer(Transform parent, string name, Material material, float width)
        {
            GameObject child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width * 0.3f;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            return line;
        }

        private static void SetObjectReference(GameObject root, string propertyName, UnityEngine.Object value)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(behaviour);
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static void SetObjectArrayReference(GameObject root, string propertyName, UnityEngine.Object[] values)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(behaviour);
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null || !property.isArray || property.propertyType == SerializedPropertyType.String)
                {
                    continue;
                }

                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }
        }

        private static Mesh CreateTorusMesh()
        {
            const int majorSegments = 48;
            const int tubeSegments = 8;
            const float majorRadius = 0.5f;
            const float tubeRadius = 0.065f;
            Vector3[] vertices = new Vector3[majorSegments * tubeSegments];
            int[] triangles = new int[majorSegments * tubeSegments * 6];

            for (int i = 0; i < majorSegments; i++)
            {
                float theta = i / (float)majorSegments * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                for (int j = 0; j < tubeSegments; j++)
                {
                    float phi = j / (float)tubeSegments * Mathf.PI * 2f;
                    vertices[i * tubeSegments + j] = radial * (majorRadius + Mathf.Cos(phi) * tubeRadius) + Vector3.up * (Mathf.Sin(phi) * tubeRadius);
                }
            }

            int index = 0;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * tubeSegments + j;
                    int b = ((i + 1) % majorSegments) * tubeSegments + j;
                    int c = ((i + 1) % majorSegments) * tubeSegments + (j + 1) % tubeSegments;
                    int d = i * tubeSegments + (j + 1) % tubeSegments;
                    triangles[index++] = a;
                    triangles[index++] = b;
                    triangles[index++] = c;
                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = d;
                }
            }

            return FinalizeMesh("GameReady_Torus", vertices, triangles);
        }

        private static Mesh CreateSawBladeMesh()
        {
            const int teeth = 24;
            const float innerRadius = 0.23f;
            const float outerRadius = 0.5f;
            const float thickness = 0.08f;
            int ringCount = teeth * 2;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            vertices.Add(new Vector3(0f, thickness * 0.5f, 0f));
            vertices.Add(new Vector3(0f, -thickness * 0.5f, 0f));
            for (int i = 0; i < ringCount; i++)
            {
                float angle = i / (float)ringCount * Mathf.PI * 2f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, thickness * 0.5f, Mathf.Sin(angle) * radius));
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -thickness * 0.5f, Mathf.Sin(angle) * radius));
            }

            for (int i = 0; i < ringCount; i++)
            {
                int next = (i + 1) % ringCount;
                int frontA = 2 + i * 2;
                int backA = frontA + 1;
                int frontB = 2 + next * 2;
                int backB = frontB + 1;
                triangles.Add(0);
                triangles.Add(frontA);
                triangles.Add(frontB);
                triangles.Add(1);
                triangles.Add(backB);
                triangles.Add(backA);
                triangles.Add(frontA);
                triangles.Add(backA);
                triangles.Add(backB);
                triangles.Add(frontA);
                triangles.Add(backB);
                triangles.Add(frontB);
            }

            Mesh mesh = FinalizeMesh("GameReady_SawBlade", vertices.ToArray(), triangles.ToArray());
            return mesh;
        }

        private static Mesh CreateStarMesh()
        {
            const int points = 8;
            Vector3[] vertices = new Vector3[points * 2 + 1];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < points * 2; i++)
            {
                float radius = i % 2 == 0 ? 0.5f : 0.24f;
                float angle = i / (float)(points * 2) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            int[] triangles = new int[points * 2 * 3];
            int index = 0;
            for (int i = 0; i < points * 2; i++)
            {
                triangles[index++] = 0;
                triangles[index++] = i + 1;
                triangles[index++] = i + 1 == points * 2 ? 1 : i + 2;
            }

            return FinalizeMesh("GameReady_Star", vertices, triangles);
        }

        private static Mesh CreateTriangleMesh(string name, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector3[] vertices =
            {
                new Vector3(a.x, a.y, 0f),
                new Vector3(b.x, b.y, 0f),
                new Vector3(c.x, c.y, 0f),
                new Vector3(a.x, a.y, -0.01f),
                new Vector3(b.x, b.y, -0.01f),
                new Vector3(c.x, c.y, -0.01f)
            };
            int[] triangles = { 0, 1, 2, 3, 5, 4, 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 3, 2, 3, 0 };
            return FinalizeMesh(name, vertices, triangles);
        }

        private static Mesh CreateConeMesh()
        {
            const int segments = 24;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = new Vector3(0f, 1f, 0f);
            vertices[1] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            }

            int index = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1 == segments ? 0 : i + 1;
                triangles[index++] = 0;
                triangles[index++] = i + 2;
                triangles[index++] = next + 2;
                triangles[index++] = 1;
                triangles[index++] = next + 2;
                triangles[index++] = i + 2;
            }

            return FinalizeMesh("GameReady_Cone", vertices, triangles);
        }

        private static Mesh FinalizeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class Palette
        {
            public Material Black;
            public Material BlackRubber;
            public Material DarkPlastic;
            public Material DarkMetal;
            public Material DullMetal;
            public Material BrushedMetal;
            public Material WarmMetal;
            public Material Brass;
            public Material Wood;
            public Material DarkWood;
            public Material Cream;
            public Material WhitePlastic;
            public Material Paper;
            public Material Cardboard;
            public Material CardboardDark;
            public Material Tape;
            public Material FabricBlue;
            public Material FabricRed;
            public Material HazardYellow;
            public Material GlowRed;
            public Material GlowWarm;
            public Material GlassBlue;
            public Material GlassRed;
            public Material Mirror;
            public Material ScreenOff;
            public Material BasketballOrange;
            public Material TomatoRed;
            public Material TomatoGreen;
            public Material RubberRed;

            public static Palette LoadOrCreate(string root)
            {
                Palette palette = new Palette
                {
                    Black = MaterialAsset(root, "GRV_Black", new Color(0.015f, 0.014f, 0.012f), 0f, 0.28f),
                    BlackRubber = MaterialAsset(root, "GRV_BlackRubber", new Color(0.025f, 0.024f, 0.022f), 0f, 0.18f),
                    DarkPlastic = MaterialAsset(root, "GRV_DarkPlastic", new Color(0.055f, 0.058f, 0.065f), 0f, 0.42f),
                    DarkMetal = MaterialAsset(root, "GRV_DarkMetal", new Color(0.11f, 0.115f, 0.12f), 0.7f, 0.35f),
                    DullMetal = MaterialAsset(root, "GRV_DullMetal", new Color(0.38f, 0.4f, 0.41f), 0.65f, 0.25f),
                    BrushedMetal = MaterialAsset(root, "GRV_BrushedMetal", new Color(0.68f, 0.69f, 0.67f), 0.85f, 0.48f),
                    WarmMetal = MaterialAsset(root, "GRV_WarmMetal", new Color(0.82f, 0.66f, 0.36f), 0.8f, 0.52f),
                    Brass = MaterialAsset(root, "GRV_Brass", new Color(0.92f, 0.68f, 0.22f), 0.8f, 0.5f),
                    Wood = MaterialAsset(root, "GRV_Wood", new Color(0.48f, 0.28f, 0.13f), 0f, 0.34f),
                    DarkWood = MaterialAsset(root, "GRV_DarkWood", new Color(0.23f, 0.12f, 0.065f), 0f, 0.28f),
                    Cream = MaterialAsset(root, "GRV_Cream", new Color(0.82f, 0.78f, 0.64f), 0f, 0.32f),
                    WhitePlastic = MaterialAsset(root, "GRV_WhitePlastic", new Color(0.88f, 0.88f, 0.82f), 0f, 0.45f),
                    Paper = MaterialAsset(root, "GRV_Paper", new Color(0.92f, 0.88f, 0.74f), 0f, 0.2f),
                    Cardboard = MaterialAsset(root, "GRV_Cardboard", new Color(0.58f, 0.39f, 0.21f), 0f, 0.24f),
                    CardboardDark = MaterialAsset(root, "GRV_CardboardDark", new Color(0.42f, 0.28f, 0.15f), 0f, 0.2f),
                    Tape = MaterialAsset(root, "GRV_Tape", new Color(0.77f, 0.62f, 0.36f), 0f, 0.58f),
                    FabricBlue = MaterialAsset(root, "GRV_FabricBlue", new Color(0.12f, 0.27f, 0.56f), 0f, 0.42f),
                    FabricRed = MaterialAsset(root, "GRV_FabricRed", new Color(0.68f, 0.09f, 0.065f), 0f, 0.4f),
                    HazardYellow = MaterialAsset(root, "GRV_HazardYellow", new Color(1f, 0.78f, 0.08f), 0f, 0.45f),
                    GlowRed = MaterialAsset(root, "GRV_GlowRed", new Color(1f, 0.05f, 0.03f), 0f, 0.35f, new Color(1f, 0.04f, 0.02f) * 1.8f),
                    GlowWarm = MaterialAsset(root, "GRV_GlowWarm", new Color(1f, 0.82f, 0.36f), 0f, 0.4f, new Color(1f, 0.62f, 0.18f) * 0.9f),
                    GlassBlue = MaterialAsset(root, "GRV_GlassBlue", new Color(0.42f, 0.72f, 0.9f, 0.55f), 0f, 0.82f, default, true),
                    GlassRed = MaterialAsset(root, "GRV_GlassRed", new Color(1f, 0.08f, 0.05f, 0.18f), 0f, 0.3f, new Color(1f, 0.04f, 0.02f) * 0.7f, true),
                    Mirror = MaterialAsset(root, "GRV_Mirror", new Color(0.62f, 0.74f, 0.78f), 0.2f, 0.9f),
                    ScreenOff = MaterialAsset(root, "GRV_ScreenOff", new Color(0.02f, 0.024f, 0.03f), 0f, 0.74f),
                    BasketballOrange = MaterialAsset(root, "GRV_BasketballOrange", new Color(0.88f, 0.34f, 0.08f), 0f, 0.42f),
                    TomatoRed = MaterialAsset(root, "GRV_TomatoRed", new Color(0.82f, 0.06f, 0.04f), 0f, 0.46f),
                    TomatoGreen = MaterialAsset(root, "GRV_TomatoGreen", new Color(0.1f, 0.32f, 0.08f), 0f, 0.3f),
                    RubberRed = MaterialAsset(root, "GRV_RubberRed", new Color(0.9f, 0.05f, 0.04f), 0f, 0.5f)
                };
                return palette;
            }

            private static Material MaterialAsset(string root, string name, Color color, float metallic, float smoothness, Color emission = default, bool transparent = false)
            {
                string path = $"{root}/{name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    material = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Glossiness", smoothness);
                if (emission.maxColorComponent > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
                else
                {
                    material.SetColor("_EmissionColor", Color.black);
                }

                if (transparent)
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetOverrideTag("RenderType", "Transparent");
                }
                else
                {
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1f);
                    material.renderQueue = -1;
                    material.SetOverrideTag("RenderType", "Opaque");
                }

                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
#endif
