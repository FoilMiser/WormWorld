using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WormWorld.Genome;
using WormWorld.Runtime;

namespace WormWorld.EditorTools
{
    public static class CellGridDemoSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/CellGridDemo.unity";
        private const string ConfigPath = "Assets/ScriptableObjects/CellPhysicsConfig.asset";
        private const string GenomeAssetPath = "Assets/Generated/Genomes/seed-genome.jsonl";

        [MenuItem("WormWorld/Scenes/Create Cell Grid Demo", priority = 30)]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CellGridDemo";

            EnsureFolders();
            var config = EnsurePhysicsConfig();
            var genomeAsset = EnsureGenomeAsset();

            CreateCamera();
            CreateLighting();
            CreateGround();
            CreateCreature(config, genomeAsset);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Cell Grid Demo", "Created CellGridDemo scene with a sample creature.", "OK");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Generated/Genomes"))
            {
                AssetDatabase.CreateFolder("Assets/Generated", "Genomes");
            }
        }

        private static CellPhysicsConfig EnsurePhysicsConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CellPhysicsConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<CellPhysicsConfig>();
            config.cellSize = 0.5f;
            config.baseMass = 1f;
            config.baseDamping = 0.3f;
            config.baseFriction = 0.6f;
            config.neighborJointFrequency = 6f;
            config.neighborJointDampingRatio = 0.5f;
            config.diagonalJoints = true;

            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static TextAsset EnsureGenomeAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GenomeAssetPath);
            if (asset != null)
            {
                return asset;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var csvPath = Path.Combine(projectRoot, "Data", "genomes", "genomes.csv");
            var genomes = GenomeIO.ReadFromCsv(csvPath);
            if (genomes.Count == 0)
            {
                throw new FileNotFoundException("No genomes found in Data/genomes/genomes.csv");
            }

            var canonical = GenomeIO.ToCanonicalJson(genomes[0]);
            var assetFullPath = Path.Combine(projectRoot, GenomeAssetPath);
            File.WriteAllText(assetFullPath, canonical + "\n");
            AssetDatabase.ImportAsset(GenomeAssetPath);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(GenomeAssetPath);
        }

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            cameraGo.transform.position = new Vector3(0f, 4f, -10f);
        }

        private static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, -1f, 0f);
            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(40f, 2f);
            var body = ground.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
        }

        private static void CreateCreature(CellPhysicsConfig config, TextAsset genomeAsset)
        {
            var creature = new GameObject("Demo Creature");
            creature.transform.position = Vector3.zero;
            creature.AddComponent<CreatureRuntime>();
            var spawner = creature.AddComponent<CreatureSpawner>();
            spawner.genomeJsonl = genomeAsset;
            spawner.cellConfig = config;
            spawner.controllerAmplitude = 0.6f;
            spawner.controllerFrequency = 1.2f;
            spawner.stepsPerEpisode = 600;
        }
    }
}
