using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Orodje, ki iz uvoženih assetov samodejno zgradi celotno igro:
// materiale (URP), Animator, prefabe borcev, 3 arene, 10 levelov ter sceni MainMenu in Game.
// Meni v Unityju: Sword Duel > Build Everything
// POZOR: ponovna gradnja prepiše datoteke v Assets/SwordDuel (scene, prefabe, levele).
public static class SwordDuelBuilder
{
    const string Root = "Assets/SwordDuel";
    const string HeroAnims = "Assets/RPGHero/Animations/";
    const string SwordAnims = "Assets/SwordAnimation/FreeSwordAnimations/Animations/";
    const string ArenaBuilds = "Assets/Modular Arenas/ModularArena/Prefabs/builds/";
    const string ArenaWalls = "Assets/Modular Arenas/ModularArena/Prefabs/wall/";

    // Napadi: animacija iz paketa Sword Animation + trenutek udarca (izmerjen kot največja hitrost meča)
    static readonly (string clip, float hitTime)[] AttackClips =
    {
        ("slash1", 0.62f),
        ("slash4", 0.60f),
        ("slash2", 0.45f),
    };

    static readonly Color Gold = new Color(0.83f, 0.66f, 0.27f);
    static readonly Color TextColor = new Color(0.93f, 0.9f, 0.84f);
    static readonly Color Muted = new Color(0.62f, 0.58f, 0.5f);
    static readonly Color PanelColor = new Color(0.07f, 0.065f, 0.06f, 0.94f);

    static Font font;
    static Font titleFont;
    static Sprite roundSprite, frameSprite, whiteSprite, lockSprite, checkSprite, coinSprite, dotSprite, shadeSprite;
    static Sprite dividerSprite, vignetteSprite, buttonSprite, diamondSprite, topShadeSprite, shieldSprite;

    [MenuItem("Sword Duel/Build Everything", priority = 0)]
    public static void BuildEverything()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAssets();
        BuildMainMenuScene();
        BuildGameScene();
        SetBuildScenes();
        EditorSceneManager.OpenScene(Root + "/Scenes/MainMenu.unity");
        Debug.Log("Sword Duel: vse zgrajeno. Odpri sceno MainMenu in pritisni Play.");
    }

    // ------------------------------------------------------------------ ASSETI

    static void BuildAssets()
    {
        foreach (var f in new[] { "Materials", "Animation", "Prefabs", "Prefabs/Arenas", "Resources", "Levels", "UI", "Scenes", "Fonts" })
            EnsureFolder(Root + "/" + f);

        BuildSprites();
        LoadFonts();

        var playerMat = HeroMaterial("Assets/RPGHero/Materials/PBR.mat", "Hero_PBR_URP");
        var enemyMat = HeroMaterial("Assets/RPGHero/Materials/PolyArt.mat", "Hero_PolyArt_URP");
        HeroMaterial("Assets/RPGHero/Materials/HandPainted.mat", "Hero_HandPainted_URP");
        var particleMat = ParticleMaterial();

        var controller = BuildAnimator();
        var spark = BuildHitSpark(particleMat);
        var torch = BuildTorch(particleMat);

        BuildFighter("Player", "Assets/RPGHero/Prefabs/RPGHeroPBR.prefab", playerMat, controller, true);
        BuildFighter("Enemy", "Assets/RPGHero/Prefabs/RPGHeroPolyart.prefab", enemyMat, controller, false);

        var arenas = BuildArenas(torch);
        BuildLevels(arenas);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.Exception("Sword Duel builder: manjka asset " + path);
        return asset;
    }

    static T CreateOrReplace<T>(T asset, string path) where T : Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // Materiali RPG Hero uporabljajo shader "Standard", ki v URP ni podprt (roza barva).
    // Naredimo URP kopijo z istimi teksturami – originali ostanejo nespremenjeni.
    static Material HeroMaterial(string sourcePath, string name)
    {
        var src = Load<Material>(sourcePath);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture albedo = src.GetTexture("_MainTex") != null ? src.GetTexture("_MainTex") : src.GetTexture("_EmissionMap");
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.05f);
        m.SetFloat("_Smoothness", 0.25f);

        if (src.GetTexture("_BumpMap") != null)
        {
            m.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
            m.EnableKeyword("_NORMALMAP");
        }
        if (src.GetTexture("_MetallicGlossMap") != null)
        {
            m.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 0.6f);
        }
        if (src.GetTexture("_OcclusionMap") != null)
        {
            m.SetTexture("_OcclusionMap", src.GetTexture("_OcclusionMap"));
            m.EnableKeyword("_OCCLUSIONMAP");
        }

        // Emisija je vklopljena (črna), da lahko ob zadetku lik zasveti rdeče
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return CreateOrReplace(m, $"{Root}/Materials/{name}.mat");
    }

    static Material ParticleMaterial()
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", dotSprite.texture);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // additive
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return CreateOrReplace(m, $"{Root}/Materials/Particle_Additive.mat");
    }

    // Animator: Locomotion (stoj/hoja/tek), 3 napadi, odziv na udarec in smrt.
    // Prehode med stanji naredi koda (Fighter.cs) s CrossFade, zato v Animatorju ni puščic.
    static AnimatorController BuildAnimator()
    {
        string path = Root + "/Animation/Fighter.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
        var parameters = ctrl.parameters;
        parameters[1].defaultFloat = 1f;
        ctrl.parameters = parameters;

        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Load<AnimationClip>(HeroAnims + "Idle_SwordShield.anim"), 0f);
        tree.AddChild(Load<AnimationClip>(HeroAnims + "Walk_SwordShield.anim"), 0.4f);
        tree.AddChild(Load<AnimationClip>(HeroAnims + "Run_SwordShield.anim"), 1f);
        sm.defaultState = loco;
        SetStatePosition(sm, loco, new Vector3(300, 0));

        for (int i = 0; i < AttackClips.Length; i++)
        {
            var s = sm.AddState("Attack" + i, new Vector3(600, -100 + i * 70));
            s.motion = Load<AnimationClip>(SwordAnims + AttackClips[i].clip + ".anim");
            s.speedParameter = "AttackSpeed";
            s.speedParameterActive = true;
        }
        sm.AddState("Block", new Vector3(300, -100)).motion = Load<AnimationClip>(SwordAnims + "deffensive stance.anim");
        sm.AddState("Hit", new Vector3(300, 150)).motion = Load<AnimationClip>(HeroAnims + "GetHit_SwordShield.anim");
        sm.AddState("Die", new Vector3(300, 230)).motion = Load<AnimationClip>(HeroAnims + "Die_SwordShield.anim");
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void SetStatePosition(AnimatorStateMachine sm, AnimatorState state, Vector3 pos)
    {
        var states = sm.states;
        for (int i = 0; i < states.Length; i++)
            if (states[i].state == state) states[i].position = pos;
        sm.states = states;
    }

    static AttackMove[] AttackMoves()
    {
        var moves = new AttackMove[AttackClips.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var clip = Load<AnimationClip>(SwordAnims + AttackClips[i].clip + ".anim");
            moves[i] = new AttackMove { stateName = "Attack" + i, clipLength = clip.length, hitTime = AttackClips[i].hitTime };
        }
        return moves;
    }

    static GameObject BuildFighter(string name, string modelPath, Material material, AnimatorController ctrl, bool isPlayer)
    {
        var root = new GameObject(name);
        var model = SpawnModel(modelPath, material, ctrl, root.transform);

        var cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.95f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.skinWidth = 0.05f;
        cc.stepOffset = 0.3f;

        root.AddComponent<Health>();
        var fighter = root.AddComponent<Fighter>();
        fighter.animator = model.GetComponent<Animator>();
        fighter.attacks = AttackMoves();

        if (isPlayer)
        {
            root.tag = "Player";
            root.AddComponent<PlayerController>();
            fighter.canBlock = true;
        }
        else
        {
            root.AddComponent<EnemyAI>();
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Model junaka iz paketa RPG Hero z URP materialom in našim Animatorjem
    static GameObject SpawnModel(string modelPath, Material material, AnimatorController ctrl, Transform parent)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(modelPath), parent);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = material;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }
        model.AddComponent<CharacterVisual>();
        return model;
    }

    static GameObject BuildHitSpark(Material mat)
    {
        var go = new GameObject("HitSpark");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.55f, 0.15f));
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.05f;
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = g;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 1f;
        renderer.sharedMaterial = mat;
        main.playOnAwake = true;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/HitSpark.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Bakla: kamniti steber iz Modular Arenas + ogenj (delci) + utripajoča luč
    static GameObject BuildTorch(Material mat)
    {
        var go = new GameObject("Torch");
        var post = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ArenaWalls + "Wall_Post_B_2m.prefab"), go.transform);
        post.transform.localScale = new Vector3(0.7f, 0.85f, 0.7f);

        var fire = new GameObject("Fire");
        fire.transform.SetParent(go.transform, false);
        fire.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        var ps = fire.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.8f), new Color(1f, 0.3f, 0.05f, 0.6f));
        main.gravityModifier = -0.15f;
        var emission = ps.emission;
        emission.rateOverTime = 28f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = g;
        fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.range = 7f;
        light.intensity = 2.5f;
        light.shadows = LightShadows.None;
        lightGo.AddComponent<TorchFlicker>().baseIntensity = 2.5f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Torch.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ ARENE

    class ArenaSpec
    {
        public string name, build;
        public Vector3 center;
        public float radius;
        public Vector3 sunRotation;
        public Color sunColor;
        public float sunIntensity;
        public Color sky, equator, ground;
        public bool fog;
        public Color fogColor;
        public float fogDensity;
        public float skyExposure, skyThickness;
        public Color skyTint;
        public int torches;
        public float torchRadius;
        public bool fillLight;
    }

    static GameObject[] BuildArenas(GameObject torch)
    {
        var specs = new[]
        {
            new ArenaSpec
            {
                name = "Arena1_TrainingGrounds", build = "BuildTest_A", center = new Vector3(0f, 1f, 0f), radius = 6.8f,
                sunRotation = new Vector3(50f, -30f, 0f), sunColor = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.6f,
                sky = new Color(0.62f, 0.7f, 0.82f), equator = new Color(0.5f, 0.5f, 0.48f), ground = new Color(0.3f, 0.27f, 0.22f),
                skyExposure = 1.3f, skyThickness = 0.9f, skyTint = new Color(0.5f, 0.5f, 0.5f),
            },
            new ArenaSpec
            {
                name = "Arena2_WarriorsArena", build = "BuildTest_D", center = Vector3.zero, radius = 10.5f,
                sunRotation = new Vector3(24f, 60f, 0f), sunColor = new Color(1f, 0.78f, 0.55f), sunIntensity = 1.3f,
                sky = new Color(0.38f, 0.38f, 0.46f), equator = new Color(0.32f, 0.27f, 0.23f), ground = new Color(0.12f, 0.1f, 0.08f),
                fog = true, fogColor = new Color(0.5f, 0.42f, 0.36f), fogDensity = 0.006f,
                skyExposure = 1f, skyThickness = 1.6f, skyTint = new Color(0.55f, 0.45f, 0.4f),
                torches = 8, torchRadius = 11.3f,
            },
            new ArenaSpec
            {
                name = "Arena3_ChampionsArena", build = "BuildTest_E", center = Vector3.zero, radius = 7.4f,
                sunRotation = new Vector3(16f, -120f, 0f), sunColor = new Color(1f, 0.6f, 0.38f), sunIntensity = 1.5f,
                sky = new Color(0.28f, 0.3f, 0.45f), equator = new Color(0.25f, 0.2f, 0.22f), ground = new Color(0.08f, 0.07f, 0.08f),
                fog = true, fogColor = new Color(0.32f, 0.26f, 0.3f), fogDensity = 0.008f,
                skyExposure = 0.8f, skyThickness = 2.4f, skyTint = new Color(0.6f, 0.4f, 0.45f),
                torches = 8, torchRadius = 8.6f, fillLight = true,
            },
        };

        var prefabs = new GameObject[specs.Length];
        for (int i = 0; i < specs.Length; i++) prefabs[i] = BuildArena(specs[i], torch);
        return prefabs;
    }

    static GameObject BuildArena(ArenaSpec s, GameObject torchPrefab)
    {
        var root = new GameObject(s.name);
        var arena = root.AddComponent<Arena>();
        arena.arenaName = s.name;

        var structure = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ArenaBuilds + s.build + ".prefab"), root.transform);
        structure.name = "Structure (" + s.build + ")";

        var center = new GameObject("BoundsCenter").transform;
        center.SetParent(root.transform, false);
        center.localPosition = s.center;
        arena.boundsCenter = center;
        arena.boundsRadius = s.radius;

        float spawnOffset = Mathf.Min(4f, s.radius * 0.5f);
        arena.playerSpawn = MakePoint(root.transform, "PlayerSpawn", s.center + new Vector3(0f, 0.05f, -spawnOffset), 0f);
        arena.enemySpawn = MakePoint(root.transform, "EnemySpawn", s.center + new Vector3(0f, 0.05f, spawnOffset), 180f);

        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(root.transform, false);
        sunGo.transform.localRotation = Quaternion.Euler(s.sunRotation);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = s.sunColor;
        sun.intensity = s.sunIntensity;
        sun.shadows = LightShadows.Soft;
        arena.sun = sun;

        if (s.fillLight)
        {
            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localRotation = Quaternion.Euler(35f, s.sunRotation.y + 180f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.65f, 1f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
        }

        for (int i = 0; i < s.torches; i++)
        {
            float a = (i + 0.5f) / s.torches * Mathf.PI * 2f;
            var t = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, root.transform);
            t.transform.localPosition = s.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.torchRadius;
        }

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_Exposure", s.skyExposure);
        sky.SetFloat("_AtmosphereThickness", s.skyThickness);
        sky.SetColor("_SkyTint", s.skyTint);
        sky.SetFloat("_SunSize", 0.03f);
        arena.skybox = CreateOrReplace(sky, $"{Root}/Materials/Sky_{s.name}.mat");

        arena.skyColor = s.sky;
        arena.equatorColor = s.equator;
        arena.groundColor = s.ground;
        arena.fog = s.fog;
        arena.fogColor = s.fogColor;
        arena.fogDensity = s.fogDensity;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/Arenas/{s.name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Transform MakePoint(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // ------------------------------------------------------------------ LEVELI

    static void BuildLevels(GameObject[] arenas)
    {
        // level, ime, težavnost, HP, škoda, cooldown, hitrost, reakcija, nagrada, arena, barva, velikost
        var table = new (string name, Difficulty d, int hp, int dmg, float cd, float speed, float react, int coins, int arena, Color tint, float scale)[]
        {
            ("Training Dummy",     Difficulty.Easy,   100,  8, 2.6f,  2.6f, 0.90f,  50, 0, new Color(0.85f, 0.78f, 0.62f), 1f),
            ("Rookie Swordsman",   Difficulty.Easy,   120, 10, 2.4f,  2.8f, 0.80f,  75, 0, new Color(0.7f, 0.82f, 1f), 1f),
            ("Apprentice Warrior", Difficulty.Easy,   145, 12, 2.2f,  3.0f, 0.70f, 100, 0, new Color(0.72f, 1f, 0.7f), 1f),
            ("Arena Fighter",      Difficulty.Medium, 170, 14, 2.0f,  3.3f, 0.60f, 150, 1, new Color(1f, 0.75f, 0.5f), 1f),
            ("Skilled Swordsman",  Difficulty.Medium, 200, 16, 1.85f, 3.5f, 0.55f, 200, 1, new Color(0.65f, 0.7f, 1f), 1.02f),
            ("Veteran Warrior",    Difficulty.Medium, 235, 18, 1.7f,  3.7f, 0.50f, 250, 1, new Color(0.8f, 0.62f, 0.5f), 1.04f),
            ("Elite Knight",       Difficulty.Medium, 270, 20, 1.6f,  3.9f, 0.45f, 300, 1, new Color(0.88f, 0.9f, 1f), 1.05f),
            ("Royal Guardian",     Difficulty.Hard,   310, 22, 1.45f, 4.1f, 0.35f, 400, 2, new Color(0.78f, 0.58f, 1f), 1.06f),
            ("Master Duelist",     Difficulty.Hard,   350, 25, 1.35f, 4.3f, 0.30f, 500, 2, new Color(0.5f, 0.5f, 0.56f), 1.07f),
            ("Arena Champion",     Difficulty.Hard,   400, 28, 1.25f, 4.5f, 0.25f, 750, 2, new Color(1f, 0.85f, 0.4f), 1.12f),
        };

        var levels = new LevelData[table.Length];
        for (int i = 0; i < table.Length; i++)
        {
            var row = table[i];
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelNumber = i + 1;
            level.enemyName = row.name;
            level.difficulty = row.d;
            level.enemyHealth = row.hp;
            level.enemyDamage = row.dmg;
            level.enemyAttackCooldown = row.cd;
            level.enemyMoveSpeed = row.speed;
            level.enemyReactionTime = row.react;
            level.coinReward = row.coins;
            level.arenaPrefab = arenas[row.arena];
            level.enemyTint = row.tint;
            level.enemyScale = row.scale;
            levels[i] = CreateOrReplace(level, $"{Root}/Levels/Level{i + 1:00}.asset");
        }

        var db = ScriptableObject.CreateInstance<LevelDatabase>();
        db.levels = levels;
        CreateOrReplace(db, $"{Root}/Resources/LevelDatabase.asset");
    }

    // ------------------------------------------------------------------ SPRITI (narisani s kodo)

    delegate float Shape(float x, float y); // vrne "pokritost" 0..1 za piksel

    static void BuildSprites()
    {
        roundSprite = MakeSprite("RoundRect", 64, (x, y) => Cover(RoundRectSdf(x, y, 32, 32, 30, 30, 14)), 16);
        frameSprite = MakeSprite("Frame", 64, (x, y) => Cover(Mathf.Abs(RoundRectSdf(x, y, 32, 32, 30, 30, 14)) - 1f), 16);
        whiteSprite = MakeSprite("White", 8, (x, y) => 1f, 0);
        coinSprite = MakeSprite("Coin", 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
            return Cover(d - 28f) * (1f - 0.35f * Cover(Mathf.Abs(d - 20f) - 1.5f));
        }, 0);
        dotSprite = MakeSprite("SoftDot", 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f;
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
        }, 0);
        lockSprite = MakeSprite("Lock", 64, (x, y) =>
        {
            float body = Cover(RoundRectSdf(x, y, 32, 22, 19, 15, 4));
            float ring = Vector2.Distance(new Vector2(x, y), new Vector2(32, 38));
            float shackle = y > 34 ? Cover(Mathf.Abs(ring - 11f) - 3f) : (Mathf.Abs(x - 32) > 8 && Mathf.Abs(x - 32) < 14 && y > 30 ? 1f : 0f);
            float hole = Cover(Vector2.Distance(new Vector2(x, y), new Vector2(32, 24)) - 3.5f);
            return Mathf.Max(body, shackle) * (1f - hole);
        }, 0);
        checkSprite = MakeSprite("Check", 64, (x, y) =>
        {
            float d = Mathf.Min(SegmentDistance(new Vector2(x, y), new Vector2(14, 33), new Vector2(27, 19)),
                                SegmentDistance(new Vector2(x, y), new Vector2(27, 19), new Vector2(51, 46)));
            return Cover(d - 4.5f);
        }, 0);
        shadeSprite = MakeSprite("Shade", 256, (x, y) =>
        {
            float k = x / 255f;
            return Mathf.Clamp01(1f - k * k);
        }, 0, 8);
        // Okrasna ločilna črta: tanka črta, ki proti robovom izzveni, z diamanti na sredini
        dividerSprite = MakeSprite("Divider", 512, (x, y) =>
        {
            float fromCenter = Mathf.Abs(x - 256f);
            float line = Cover(Mathf.Abs(y - 16f) - 1f) * Mathf.Clamp01(1f - fromCenter / 250f);
            float bigDiamond = Cover((fromCenter + Mathf.Abs(y - 16f)) - 9f);
            float smallDiamonds = Cover((Mathf.Abs(fromCenter - 24f) + Mathf.Abs(y - 16f)) - 4.5f);
            float gap = fromCenter < 15f ? 0f : 1f;
            return Mathf.Max(line * gap, bigDiamond, smallDiamonds);
        }, 0, 32);
        vignetteSprite = MakeSprite("Vignette", 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 181f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.05f, d));
        }, 0);
        diamondSprite = MakeSprite("Diamond", 32, (x, y) => Cover(Mathf.Abs(x - 16f) + Mathf.Abs(y - 16f) - 12f), 0);
        topShadeSprite = MakeSprite("TopShade", 8, (x, y) => Mathf.SmoothStep(0f, 1f, y / 128f), 0, 128);
        // Ščit: raven vrh, stranice se proti dnu zožijo v konico; notranji rob je prosojnejši
        shieldSprite = MakeSprite("Shield", 64, (x, y) =>
        {
            float HalfWidth(float yy) => yy > 30f ? 24f : 24f * Mathf.Sqrt(Mathf.Clamp01((yy - 4f) / 26f));
            float outer = Cover(Mathf.Abs(x - 32f) - HalfWidth(y)) * Cover(y - 58f) * Cover(4f - y + 1f);
            float inner = Cover(Mathf.Abs(x - 32f) - (HalfWidth(y + 5f) - 6f)) * Cover(y - 52f) * Cover(12f - y);
            float boss = Cover(Vector2.Distance(new Vector2(x, y), new Vector2(32f, 36f)) - 6f);
            return Mathf.Max(outer * (1f - 0.45f * inner), boss);
        }, 0);
        buttonSprite = MakeGradientSprite("ButtonFill", 64, 16);
        AssetDatabase.Refresh();
    }

    // Zaobljen pravokotnik s svetlejšim vrhom (gumbi dobijo rahel 3D videz)
    static Sprite MakeGradientSprite(string name, int size, int border)
    {
        string path = $"{Root}/UI/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Cover(RoundRectSdf(x + 0.5f, y + 0.5f, size / 2f, size / 2f, size / 2f - 2f, size / 2f - 2f, 14));
                float shade = Mathf.Lerp(0.72f, 1f, y / (size - 1f));
                if (y > size - 5) shade = 1.25f; // tanek svetel rob na vrhu
                tex.SetPixel(x, y, new Color(Mathf.Min(1f, shade), Mathf.Min(1f, shade), Mathf.Min(1f, shade), a));
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float Cover(float sdf) => Mathf.Clamp01(0.5f - sdf);

    static float RoundRectSdf(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(x - cx) - (hw - r);
        float qy = Mathf.Abs(y - cy) - (hh - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static Sprite MakeSprite(string name, int size, Shape shape, int border, int height = -1)
    {
        if (height < 0) height = size;
        string path = $"{Root}/UI/{name}.png";
        var tex = new Texture2D(size, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, shape(x + 0.5f, y + 0.5f)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Če v Assets/SwordDuel/Fonts položiš svojo pisavo (.ttf/.otf), jo uporabimo za naslove
    static void LoadFonts()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleFont = font;
        foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { Root + "/Fonts" }))
        {
            titleFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            break;
        }
    }

    static void EnsureUiLoaded()
    {
        if (roundSprite != null && font != null) return;
        string ui = Root + "/UI/";
        roundSprite = Load<Sprite>(ui + "RoundRect.png");
        frameSprite = Load<Sprite>(ui + "Frame.png");
        whiteSprite = Load<Sprite>(ui + "White.png");
        lockSprite = Load<Sprite>(ui + "Lock.png");
        checkSprite = Load<Sprite>(ui + "Check.png");
        coinSprite = Load<Sprite>(ui + "Coin.png");
        dotSprite = Load<Sprite>(ui + "SoftDot.png");
        shadeSprite = Load<Sprite>(ui + "Shade.png");
        dividerSprite = Load<Sprite>(ui + "Divider.png");
        vignetteSprite = Load<Sprite>(ui + "Vignette.png");
        diamondSprite = Load<Sprite>(ui + "Diamond.png");
        buttonSprite = Load<Sprite>(ui + "ButtonFill.png");
        topShadeSprite = Load<Sprite>(ui + "TopShade.png");
        shieldSprite = Load<Sprite>(ui + "Shield.png");
        LoadFonts();
    }

    // ------------------------------------------------------------------ UI POMOČNIKI

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = true)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false, Font f = null)
    {
        var t = Rect(name, parent).gameObject.AddComponent<Text>();
        t.font = f ?? font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        return t;
    }

    static void AddShadow(Graphic g, float distance = 3f, float alpha = 0.6f)
    {
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, alpha);
        s.effectDistance = new Vector2(distance, -distance);
    }

    static void TextOutline(Graphic g)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.75f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
    }

    static Image Card(Transform parent, string name, Vector2 size)
    {
        var card = Img(parent, name, roundSprite, PanelColor);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var frame = Img(card.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.7f));
        Stretch(frame.rectTransform);
        // Dvojni okvir in zlati diamanti v kotih – srednjeveški videz
        var inner = Img(card.transform, "InnerFrame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.18f));
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(8, 8);
        inner.rectTransform.offsetMax = new Vector2(-8, -8);
        for (int i = 0; i < 4; i++)
        {
            var corner = new Vector2(i % 2, i / 2);
            var d = Img(card.transform, "Corner" + i, diamondSprite, Gold);
            Place(d.rectTransform, corner, new Vector2(corner.x == 0 ? 14 : -14, corner.y == 0 ? 14 : -14), new Vector2(14, 14), new Vector2(0.5f, 0.5f));
        }
        return card;
    }

    // Naslov s pisavo Cinzel in okrasno črto pod njim
    static Text Heading(Transform parent, string text, float y, int size = 56, Color? color = null)
    {
        var t = Txt(parent, "Title", text, size, color ?? Gold, TextAnchor.MiddleCenter, true, titleFont);
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(1000, size * 1.4f), new Vector2(0.5f, 0.5f));
        AddShadow(t, 3f, 0.7f);
        var divider = Img(parent, "TitleDivider", dividerSprite, new Color(Gold.r, Gold.g, Gold.b, 0.9f), false);
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y - size * 0.75f - 10f), new Vector2(460, 29), new Vector2(0.5f, 0.5f));
        return t;
    }

    static Button Btn(Transform parent, string name, string label, Vector2 size, int fontSize = 30)
    {
        var bg = Img(parent, name, buttonSprite, new Color(0.09f, 0.08f, 0.07f, 0.92f));
        bg.raycastTarget = true;
        bg.rectTransform.sizeDelta = size;
        var frame = Img(bg.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.8f));
        Stretch(frame.rectTransform);
        var text = Txt(bg.transform, "Label", label, fontSize, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Stretch(text.rectTransform);

        var button = bg.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        var fx = bg.gameObject.AddComponent<UIButtonFx>();
        fx.background = bg;
        fx.label = text;
        return button;
    }

    static CanvasGroup Panel(Transform parent, string name)
    {
        var rt = Stretch(Rect(name, parent));
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    static Slider MakeSlider(Transform parent, string name, float min, float max, Vector2 pos, float width)
    {
        var rt = Place(Rect(name, parent), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 30));
        var slider = rt.gameObject.AddComponent<Slider>();

        var bg = Img(rt, "Background", roundSprite, new Color(1f, 1f, 1f, 0.12f));
        Place(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 10));
        var fillArea = Rect("Fill Area", rt);
        Place(fillArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 10));
        var fill = Img(fillArea, "Fill", roundSprite, Gold);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = Vector2.zero;
        var handleArea = Rect("Handle Area", rt);
        Stretch(handleArea);
        var handle = Img(handleArea, "Handle", coinSprite, new Color(0.95f, 0.85f, 0.6f));
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(30, 0);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
        return slider;
    }

    static HealthBar MakeHealthBar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor, bool showValue)
    {
        var frame = Img(parent, name, roundSprite, new Color(0f, 0f, 0f, 0.7f));
        Place(frame.rectTransform, anchor, pos, size + new Vector2(8, 8));
        var border = Img(frame.transform, "Border", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.65f));
        Stretch(border.rectTransform);
        border.rectTransform.offsetMin = new Vector2(-3, -3);
        border.rectTransform.offsetMax = new Vector2(3, 3);
        var trail = Img(frame.transform, "Trail", whiteSprite, new Color(1f, 0.92f, 0.75f, 0.85f), false);
        Place(trail.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        trail.type = Image.Type.Filled;
        trail.fillMethod = Image.FillMethod.Horizontal;
        var fill = Img(frame.transform, "Fill", whiteSprite, fillColor, false);
        Place(fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        var shine = Img(frame.transform, "Shine", whiteSprite, new Color(1f, 1f, 1f, 0.12f), false);
        Place(shine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(size.x, size.y * 0.35f));

        var bar = frame.gameObject.AddComponent<HealthBar>();
        bar.fill = fill;
        bar.trail = trail;
        if (showValue)
        {
            var value = Txt(frame.transform, "Value", "100 / 100", 18, TextColor, TextAnchor.MiddleCenter, true);
            Stretch(value.rectTransform);
            AddShadow(value, 1.5f, 0.9f);
            bar.valueText = value;
        }
        return bar;
    }

    static Canvas MakeCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void MakeEventSystem()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static void AddVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        if (profile == null) return;
        var go = new GameObject("Global Volume");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    static Camera MakeCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;
        cam.fieldOfView = 55f;
        go.AddComponent<AudioListener>();
        return cam;
    }

    // ------------------------------------------------------------------ SCENA: GLAVNI MENI

    static void BuildMainMenuScene()
    {
        EnsureUiLoaded();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Ozadje: Warrior's Arena z junakom in nasprotnikom, kamera počasi kroži
        var arenaGo = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Root}/Prefabs/Arenas/Arena2_WarriorsArena.prefab"));
        arenaGo.GetComponent<Arena>().ApplyLighting();
        var ctrl = Load<AnimatorController>(Root + "/Animation/Fighter.controller");
        var heroRoot = new GameObject("MenuHero").transform;
        heroRoot.position = new Vector3(-1.2f, 0f, 0f);
        heroRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
        SpawnModel("Assets/RPGHero/Prefabs/RPGHeroPBR.prefab", Load<Material>($"{Root}/Materials/Hero_PBR_URP.mat"), ctrl, heroRoot);
        var rivalRoot = new GameObject("MenuRival").transform;
        rivalRoot.position = new Vector3(1.6f, 0f, 0f);
        rivalRoot.rotation = Quaternion.Euler(0f, -90f, 0f);
        var rival = SpawnModel("Assets/RPGHero/Prefabs/RPGHeroPolyart.prefab", Load<Material>($"{Root}/Materials/Hero_PolyArt_URP.mat"), ctrl, rivalRoot);
        rival.GetComponent<CharacterVisual>().tint = new Color(1f, 0.85f, 0.4f);

        var cam = MakeCamera();
        var orbit = cam.gameObject.AddComponent<MenuCameraOrbit>();
        orbit.target = new GameObject("MenuFocus").transform;
        orbit.target.position = new Vector3(0.2f, 0f, 0f);
        orbit.distance = 6f;
        orbit.height = 1.9f;
        AddVolume();
        MakeEventSystem();

        var canvas = MakeCanvas("MenuCanvas");
        var ui = canvas.gameObject.AddComponent<MainMenuUI>();
        var root = canvas.transform;

        var shade = Img(root, "LeftShade", shadeSprite, new Color(0.02f, 0.02f, 0.02f, 0.9f), false);
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.55f, 1f);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        var dim = Img(root, "BottomShade", whiteSprite, new Color(0f, 0f, 0f, 0.2f), false);
        Stretch(dim.rectTransform);
        var vignette = Img(root, "Vignette", vignetteSprite, new Color(0f, 0f, 0f, 0.85f), false);
        Stretch(vignette.rectTransform);

        // Kovanci (vedno vidni, zgoraj desno)
        var coinBox = Img(root, "CoinBox", roundSprite, PanelColor);
        Place(coinBox.rectTransform, new Vector2(1f, 1f), new Vector2(-50, -40), new Vector2(240, 64));
        Stretch(Img(coinBox.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.55f)).rectTransform);
        Place(Img(coinBox.transform, "Icon", coinSprite, Gold).rectTransform, new Vector2(0f, 0.5f), new Vector2(22, 0), new Vector2(36, 36), new Vector2(0f, 0.5f));
        ui.coinsText = Txt(coinBox.transform, "Coins", "0", 32, TextColor, TextAnchor.MiddleRight, true, titleFont);
        Place(ui.coinsText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(160, 50), new Vector2(1f, 0.5f));

        var backs = new List<Button>();
        BuildMainPanel(ui, root);
        BuildLevelPanel(ui, root, backs);
        BuildUpgradePanel(ui, root, backs);
        BuildSettingsPanel(ui, root, backs);
        ui.backButtons = backs.ToArray();
        coinBox.transform.SetAsLastSibling();

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/MainMenu.unity");
    }

    static void BuildMainPanel(MainMenuUI ui, Transform root)
    {
        var panel = Panel(root, "MainPanel");
        ui.mainPanel = panel;
        var p = panel.transform;

        var title = Txt(p, "Title", "SWORD DUEL", 118, Gold, TextAnchor.MiddleLeft, true, titleFont);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(132, -190), new Vector2(1000, 150), new Vector2(0f, 0.5f));
        var titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.25f, 0.16f, 0.04f, 0.9f);
        titleOutline.effectDistance = new Vector2(2f, -2f);
        AddShadow(title, 6f, 0.75f);
        var line = Img(p, "TitleDivider", dividerSprite, Gold, false);
        Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(120, -272), new Vector2(560, 32), new Vector2(0f, 0.5f));
        var subtitle = Txt(p, "Subtitle", "Defeat Champions   ·   Earn Gold   ·   Grow Stronger", 24, Muted, TextAnchor.MiddleLeft, false, titleFont);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(140, -312), new Vector2(900, 40), new Vector2(0f, 0.5f));

        string[] labels = { "PLAY", "UPGRADES", "SETTINGS", "QUIT" };
        var buttons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = Btn(p, labels[i] + "Button", labels[i], new Vector2(400, 72), 32);
            Place((RectTransform)buttons[i].transform, new Vector2(0f, 1f), new Vector2(140, -420 - i * 92), new Vector2(400, 72), new Vector2(0f, 0.5f));
        }
        ui.playButton = buttons[0];
        ui.upgradesButton = buttons[1];
        ui.settingsButton = buttons[2];
        ui.quitButton = buttons[3];

        var stats = Img(p, "StatsCard", roundSprite, new Color(0.05f, 0.045f, 0.04f, 0.75f));
        Place(stats.rectTransform, new Vector2(0f, 0f), new Vector2(140, 90), new Vector2(520, 130), Vector2.zero);
        Stretch(Img(stats.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.35f)).rectTransform);
        var accent = Img(stats.transform, "Accent", whiteSprite, Gold, false);
        Place(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(4, 90), new Vector2(0f, 0.5f));
        var statsTitle = Txt(stats.transform, "Header", "YOUR WARRIOR", 20, Gold, TextAnchor.UpperLeft, true, titleFont);
        Place(statsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(24, -16), new Vector2(460, 30), new Vector2(0f, 1f));
        ui.strengthText = Txt(stats.transform, "Strength", "Strength", 24, TextColor, TextAnchor.MiddleLeft);
        Place(ui.strengthText.rectTransform, new Vector2(0f, 1f), new Vector2(24, -64), new Vector2(460, 30), new Vector2(0f, 0.5f));
        ui.speedText = Txt(stats.transform, "Speed", "Attack Speed", 24, TextColor, TextAnchor.MiddleLeft);
        Place(ui.speedText.rectTransform, new Vector2(0f, 1f), new Vector2(24, -98), new Vector2(460, 30), new Vector2(0f, 0.5f));
    }

    static Button BackButton(Transform parent, List<Button> backs, Vector2 pos)
    {
        var b = Btn(parent, "BackButton", "BACK", new Vector2(200, 60), 26);
        Place((RectTransform)b.transform, new Vector2(0f, 0f), pos, new Vector2(200, 60), Vector2.zero);
        backs.Add(b);
        return b;
    }

    static void BuildLevelPanel(MainMenuUI ui, Transform root, List<Button> backs)
    {
        var panel = Panel(root, "LevelPanel");
        ui.levelPanel = panel;
        var card = Card(panel.transform, "Card", new Vector2(1320, 860));
        var c = card.transform;

        Heading(c, "SELECT LEVEL", -76);

        var groups = new (string label, Difficulty d, int first, int count)[]
        {
            ("EASY", Difficulty.Easy, 1, 3), ("MEDIUM", Difficulty.Medium, 4, 4), ("HARD", Difficulty.Hard, 8, 3),
        };
        var buttons = new List<LevelButton>();
        for (int g = 0; g < groups.Length; g++)
        {
            float y = -230 - g * 180;
            var label = Txt(c, groups[g].label + "Label", groups[g].label, 32, GameUI.DifficultyColor(groups[g].d), TextAnchor.MiddleLeft, true, titleFont);
            AddShadow(label, 2f, 0.6f);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(70, y), new Vector2(220, 50), new Vector2(0f, 0.5f));

            for (int i = 0; i < groups[g].count; i++)
            {
                int number = groups[g].first + i;
                var lb = MakeLevelButton(c, number, GameUI.DifficultyColor(groups[g].d));
                Place((RectTransform)lb.transform, new Vector2(0f, 1f), new Vector2(300 + i * 240, y), new Vector2(220, 160), new Vector2(0f, 0.5f));
                buttons.Add(lb);
            }
        }
        ui.levelButtons = buttons.ToArray();

        var soon = Txt(c, "ComingSoon", "More levels coming soon", 22, Muted, TextAnchor.MiddleCenter, true, titleFont);
        Place(soon.rectTransform, new Vector2(0f, 1f), new Vector2(300 + 2 * 240 + 110, -230 - 2 * 180 - 104), new Vector2(360, 30), new Vector2(0.5f, 0.5f));

        BackButton(c, backs, new Vector2(40, 36));
    }

    static LevelButton MakeLevelButton(Transform parent, int number, Color accent)
    {
        var button = Btn(parent, "Level" + number, "", new Vector2(220, 160));
        var lb = button.gameObject.AddComponent<LevelButton>();
        lb.levelNumber = number;
        lb.button = button;
        var fx = button.GetComponent<UIButtonFx>();

        Object.DestroyImmediate(fx.label.gameObject);
        lb.numberText = Txt(button.transform, "Number", number.ToString(), 64, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Place(lb.numberText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(200, 80));
        fx.label = lb.numberText;
        lb.nameText = Txt(button.transform, "Enemy", "Enemy", 18, Muted, TextAnchor.MiddleCenter);
        Place(lb.nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(210, 30));
        var strip = Img(button.transform, "Accent", whiteSprite, accent, false);
        Place(strip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(60, 4));

        lb.lockIcon = Img(button.transform, "Lock", lockSprite, new Color(0.75f, 0.7f, 0.6f, 0.9f));
        Place(lb.lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(56, 56));
        lb.checkIcon = Img(button.transform, "Check", checkSprite, new Color(0.45f, 0.85f, 0.45f));
        Place(lb.checkIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-14, -14), new Vector2(38, 38), new Vector2(1f, 1f));
        return lb;
    }

    static void BuildUpgradePanel(MainMenuUI ui, Transform root, List<Button> backs)
    {
        var panel = Panel(root, "UpgradePanel");
        ui.upgradePanel = panel;
        var card = Card(panel.transform, "Card", new Vector2(1200, 800));
        var c = card.transform;

        Heading(c, "UPGRADES", -76);
        Place(Img(c, "CoinIcon", coinSprite, Gold).rectTransform, new Vector2(0.5f, 1f), new Vector2(-40, -172), new Vector2(30, 30), new Vector2(0.5f, 0.5f));
        ui.upgradeCoinsText = Txt(c, "Coins", "0", 30, TextColor, TextAnchor.MiddleLeft, true);
        Place(ui.upgradeCoinsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-16, -172), new Vector2(300, 40), new Vector2(0f, 0.5f));

        ui.upgradeRows = new[]
        {
            MakeUpgradeRow(c, UpgradeRow.Kind.Strength, "STRENGTH", "Sword damage +5 per level", -208),
            MakeUpgradeRow(c, UpgradeRow.Kind.AttackSpeed, "ATTACK SPEED", "Time between attacks -0.08 s per level", -416),
        };
        BackButton(c, backs, new Vector2(40, 36));
    }

    static UpgradeRow MakeUpgradeRow(Transform parent, UpgradeRow.Kind kind, string name, string description, float y)
    {
        var bg = Img(parent, name + "Row", roundSprite, new Color(0f, 0f, 0f, 0.35f));
        Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(1080, 190));
        Stretch(Img(bg.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.25f)).rectTransform);
        var row = bg.gameObject.AddComponent<UpgradeRow>();
        row.kind = kind;
        var t = bg.transform;

        var nameText = Txt(t, "Name", name, 34, Gold, TextAnchor.MiddleLeft, true, titleFont);
        Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(36, -38), new Vector2(500, 44), new Vector2(0f, 0.5f));
        var desc = Txt(t, "Description", description, 20, Muted, TextAnchor.MiddleLeft);
        Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(36, -74), new Vector2(600, 30), new Vector2(0f, 0.5f));
        row.statText = Txt(t, "Stat", "", 26, TextColor, TextAnchor.MiddleLeft, true);
        Place(row.statText.rectTransform, new Vector2(0f, 0f), new Vector2(36, 34), new Vector2(600, 36), new Vector2(0f, 0.5f));
        row.levelText = Txt(t, "Level", "Level 0 / 10", 22, TextColor, TextAnchor.MiddleLeft);
        Place(row.levelText.rectTransform, new Vector2(0f, 1f), new Vector2(560, -38), new Vector2(200, 30), new Vector2(0f, 0.5f));

        var pips = new Image[10];
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i] = Img(t, "Pip" + i, roundSprite, Color.white);
            Place(pips[i].rectTransform, new Vector2(0f, 1f), new Vector2(560 + i * 22, -78), new Vector2(16, 16), new Vector2(0f, 0.5f));
        }
        row.pips = pips;

        row.costText = Txt(t, "Cost", "100 coins", 26, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Place(row.costText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-150, 40), new Vector2(240, 36));
        row.buyButton = Btn(t, "BuyButton", "UPGRADE", new Vector2(240, 66), 26);
        Place((RectTransform)row.buyButton.transform, new Vector2(1f, 0.5f), new Vector2(-150, -26), new Vector2(240, 66));
        row.buyLabel = row.buyButton.GetComponent<UIButtonFx>().label;
        return row;
    }

    static void BuildSettingsPanel(MainMenuUI ui, Transform root, List<Button> backs)
    {
        var panel = Panel(root, "SettingsPanel");
        ui.settingsPanel = panel;
        var card = Card(panel.transform, "Card", new Vector2(1000, 700));
        var c = card.transform;

        Heading(c, "SETTINGS", -76);

        var volLabel = Txt(c, "VolumeLabel", "Master Volume", 26, TextColor, TextAnchor.MiddleLeft, true, titleFont);
        Place(volLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-400, -190), new Vector2(300, 40), new Vector2(0f, 0.5f));
        ui.volumeSlider = MakeSlider(c, "VolumeSlider", 0f, 1f, Vector2.zero, 420);
        ui.volumeSlider.GetComponent<RectTransform>().anchorMin = ui.volumeSlider.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        ui.volumeSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, -190);
        ui.volumeValue = Txt(c, "VolumeValue", "80%", 24, Gold, TextAnchor.MiddleRight, true, titleFont);
        Place(ui.volumeValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(400, -190), new Vector2(100, 40), new Vector2(1f, 0.5f));

        var sensLabel = Txt(c, "SensitivityLabel", "Mouse Sensitivity", 26, TextColor, TextAnchor.MiddleLeft, true, titleFont);
        Place(sensLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(-400, -265), new Vector2(300, 40), new Vector2(0f, 0.5f));
        ui.sensitivitySlider = MakeSlider(c, "SensitivitySlider", 0.2f, 3f, Vector2.zero, 420);
        ui.sensitivitySlider.GetComponent<RectTransform>().anchorMin = ui.sensitivitySlider.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        ui.sensitivitySlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, -265);
        ui.sensitivityValue = Txt(c, "SensitivityValue", "1.0x", 24, Gold, TextAnchor.MiddleRight, true, titleFont);
        Place(ui.sensitivityValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(400, -265), new Vector2(100, 40), new Vector2(1f, 0.5f));

        var controls = Img(c, "Controls", roundSprite, new Color(0f, 0f, 0f, 0.35f));
        Place(controls.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(820, 190));
        Stretch(Img(controls.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.25f)).rectTransform);
        var controlsTitle = Txt(controls.transform, "Header", "CONTROLS", 22, Gold, TextAnchor.UpperCenter, true, titleFont);
        Place(controlsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(700, 30), new Vector2(0.5f, 1f));
        var controlsText = Txt(controls.transform, "Text",
            "WASD  –  Move          Mouse  –  Camera          Left Click  –  Attack\nHold Right Click  –  Block (2 hits, then 15 s cooldown)\nSpace  –  Dodge          Esc  –  Pause",
            22, TextColor, TextAnchor.MiddleCenter);
        controlsText.lineSpacing = 1.4f;
        Place(controlsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(800, 130));

        ui.resetButton = Btn(c, "ResetButton", "RESET PROGRESS", new Vector2(360, 60), 22);
        Place((RectTransform)ui.resetButton.transform, new Vector2(1f, 0f), new Vector2(-40, 36), new Vector2(360, 60), new Vector2(1f, 0f));
        var resetFx = ui.resetButton.GetComponent<UIButtonFx>();
        resetFx.hoverColor = new Color(0.75f, 0.2f, 0.18f);
        resetFx.hoverText = Color.white;
        ui.resetLabel = resetFx.label;
        BackButton(c, backs, new Vector2(40, 36));
    }

    // ------------------------------------------------------------------ SCENA: IGRA

    static void BuildGameScene()
    {
        EnsureUiLoaded();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = MakeCamera();
        var camController = cam.gameObject.AddComponent<CameraController>();
        AddVolume();
        MakeEventSystem();

        var canvas = MakeCanvas("HUD");
        var ui = canvas.gameObject.AddComponent<GameUI>();
        var root = canvas.transform;

        // Temen preliv na vrhu, da je HUD berljiv tudi v svetli areni
        var topShade = Img(root, "TopShade", topShadeSprite, new Color(0f, 0f, 0f, 0.85f), false);
        topShade.rectTransform.anchorMin = new Vector2(0f, 1f);
        topShade.rectTransform.anchorMax = new Vector2(1f, 1f);
        topShade.rectTransform.pivot = new Vector2(0.5f, 1f);
        topShade.rectTransform.anchoredPosition = Vector2.zero;
        topShade.rectTransform.sizeDelta = new Vector2(0f, 260f);

        // Sloj za številke škode – pod paneli za pavzo/zmago/poraz
        var damageLayer = Stretch(Rect("DamageNumbers", root));

        // Igralec (zgoraj levo)
        var youLabel = Txt(root, "PlayerLabel", "YOU", 26, Gold, TextAnchor.MiddleLeft, true, titleFont);
        Place(youLabel.rectTransform, new Vector2(0f, 1f), new Vector2(44, -40), new Vector2(200, 36), new Vector2(0f, 0.5f));
        AddShadow(youLabel, 2f, 0.8f);
        ui.playerBar = MakeHealthBar(root, "PlayerHealth", new Vector2(0f, 1f), new Vector2(40, -66), new Vector2(480, 26), new Color(0.42f, 0.78f, 0.35f), true);
        ui.playerBar.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

        // Ščit (pod zdravjem igralca): ikona, odštevanje cooldowna in 2 pikici za preostala bloka
        var shieldBox = Img(root, "ShieldStatus", roundSprite, new Color(0f, 0f, 0f, 0.65f));
        Place(shieldBox.rectTransform, new Vector2(0f, 1f), new Vector2(40, -112), new Vector2(64, 64), new Vector2(0f, 1f));
        Stretch(Img(shieldBox.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.65f)).rectTransform);
        ui.shieldIcon = Img(shieldBox.transform, "Icon", shieldSprite, Gold);
        Place(ui.shieldIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
        ui.shieldCooldownFill = Img(shieldBox.transform, "Cooldown", roundSprite, new Color(0f, 0f, 0f, 0.65f), false);
        Stretch(ui.shieldCooldownFill.rectTransform);
        ui.shieldCooldownFill.type = Image.Type.Filled;
        ui.shieldCooldownFill.fillMethod = Image.FillMethod.Radial360;
        ui.shieldCooldownFill.fillOrigin = (int)Image.Origin360.Top;
        ui.shieldCooldownFill.fillClockwise = false;
        ui.shieldCooldownText = Txt(shieldBox.transform, "Seconds", "15", 28, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Stretch(ui.shieldCooldownText.rectTransform);
        TextOutline(ui.shieldCooldownText);
        var shieldLabel = Txt(root, "ShieldLabel", "SHIELD", 20, Gold, TextAnchor.MiddleLeft, true, titleFont);
        Place(shieldLabel.rectTransform, new Vector2(0f, 1f), new Vector2(118, -128), new Vector2(200, 28), new Vector2(0f, 0.5f));
        TextOutline(shieldLabel);
        var shieldHint = Txt(root, "ShieldHint", "Hold Right Click", 18, new Color(0.9f, 0.87f, 0.8f), TextAnchor.MiddleLeft, true);
        Place(shieldHint.rectTransform, new Vector2(0f, 1f), new Vector2(118, -158), new Vector2(200, 24), new Vector2(0f, 0.5f));
        TextOutline(shieldHint);
        ui.shieldPips = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            ui.shieldPips[i] = Img(root, "ShieldPip" + i, diamondSprite, Gold);
            Place(ui.shieldPips[i].rectTransform, new Vector2(0f, 1f), new Vector2(214 + i * 22, -128), new Vector2(16, 16), new Vector2(0f, 0.5f));
        }

        // Nasprotnik (zgoraj na sredini)
        ui.enemyNameText = Txt(root, "EnemyName", "ENEMY", 34, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Place(ui.enemyNameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(800, 44));
        AddShadow(ui.enemyNameText, 2f, 0.8f);
        TextOutline(ui.enemyNameText);
        ui.enemyBar = MakeHealthBar(root, "EnemyHealth", new Vector2(0.5f, 1f), new Vector2(0, -84), new Vector2(680, 22), new Color(0.85f, 0.25f, 0.2f), true);
        ui.levelText = Txt(root, "LevelInfo", "LEVEL 1   EASY", 22, TextColor, TextAnchor.MiddleCenter, true, titleFont);
        Place(ui.levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(600, 30));
        AddShadow(ui.levelText, 2f, 0.8f);
        TextOutline(ui.levelText);
        TextOutline(youLabel);

        // Gumb za pavzo (zgoraj desno)
        ui.pauseButton = Btn(root, "PauseButton", "II", new Vector2(64, 64), 28);
        Place((RectTransform)ui.pauseButton.transform, new Vector2(1f, 1f), new Vector2(-40, -36), new Vector2(64, 64), new Vector2(1f, 1f));

        ui.countdownText = Txt(root, "Countdown", "3", 170, Gold, TextAnchor.MiddleCenter, true, titleFont);
        Place(ui.countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 220));
        AddShadow(ui.countdownText, 6f, 0.8f);

        var tutorial = Img(root, "Tutorial", roundSprite, PanelColor);
        Place(tutorial.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1400, 120), new Vector2(0.5f, 0f));
        Stretch(Img(tutorial.transform, "Frame", frameSprite, new Color(Gold.r, Gold.g, Gold.b, 0.55f)).rectTransform);
        var tutTitle = Txt(tutorial.transform, "Title", "HOW TO FIGHT", 22, Gold, TextAnchor.MiddleCenter, true, titleFont);
        Place(tutTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(1000, 30));
        var tutText = Txt(tutorial.transform, "Text",
            "<b>WASD</b> Move     <b>Mouse</b> Camera     <b>Left Click</b> Attack     <b>Hold Right Click</b> Block     <b>Space</b> Dodge     <b>Esc</b> Pause",
            24, TextColor, TextAnchor.MiddleCenter);
        Place(tutText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(1360, 40));
        ui.tutorialPanel = tutorial.gameObject;

        ui.damageNumberTemplate = Txt(damageLayer, "DamageNumber", "20", 48, Color.white, TextAnchor.MiddleCenter, true, titleFont);
        Place(ui.damageNumberTemplate.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(200, 60), new Vector2(0.5f, 0.5f));
        var outline = ui.damageNumberTemplate.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Pavza
        ui.pausePanel = OverlayPanel(root, "PausePanel", new Vector2(560, 470), "PAUSED", Gold, out var pauseCard);
        ui.resumeButton = StackButton(pauseCard, "RESUME", 0);
        ui.restartButton = StackButton(pauseCard, "RESTART", 1);
        ui.pauseMenuButton = StackButton(pauseCard, "MAIN MENU", 2);

        // Zmaga
        ui.victoryPanel = OverlayPanel(root, "VictoryPanel", new Vector2(640, 600), "VICTORY", Gold, out var winCard);
        Place(Img(winCard, "CoinIcon", coinSprite, Gold).rectTransform, new Vector2(0.5f, 1f), new Vector2(-62, -210), new Vector2(50, 50), new Vector2(0.5f, 0.5f));
        ui.victoryCoinsText = Txt(winCard, "Coins", "+50", 56, TextColor, TextAnchor.MiddleLeft, true, titleFont);
        Place(ui.victoryCoinsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-28, -210), new Vector2(300, 70), new Vector2(0f, 0.5f));
        ui.victoryTotalText = Txt(winCard, "Total", "Total: 0 coins", 22, Muted, TextAnchor.MiddleCenter);
        Place(ui.victoryTotalText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -262), new Vector2(500, 30), new Vector2(0.5f, 0.5f));
        ui.nextLevelButton = StackButton(winCard, "NEXT LEVEL", 1.4f);
        ui.replayButton = StackButton(winCard, "REPLAY", 2.4f);
        ui.victoryMenuButton = StackButton(winCard, "MAIN MENU", 3.4f);

        // Poraz
        ui.defeatPanel = OverlayPanel(root, "DefeatPanel", new Vector2(640, 520), "DEFEAT", new Color(0.9f, 0.3f, 0.26f), out var loseCard);
        var hint = Txt(loseCard, "Hint", "Buy upgrades with your coins and try again!", 22, Muted, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(560, 30), new Vector2(0.5f, 0.5f));
        ui.retryButton = StackButton(loseCard, "RETRY", 1.1f);
        ui.defeatMenuButton = StackButton(loseCard, "MAIN MENU", 2.1f);

        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        manager.playerPrefab = Load<GameObject>($"{Root}/Prefabs/Player.prefab");
        manager.enemyPrefab = Load<GameObject>($"{Root}/Prefabs/Enemy.prefab");
        manager.hitSparkPrefab = Load<GameObject>($"{Root}/Prefabs/HitSpark.prefab");
        manager.cameraController = camController;
        manager.ui = ui;

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Game.unity");
    }

    static CanvasGroup OverlayPanel(Transform root, string name, Vector2 size, string title, Color titleColor, out Transform card)
    {
        var panel = Panel(root, name);
        Stretch(Img(panel.transform, "Dim", whiteSprite, new Color(0f, 0f, 0f, 0.6f), false).rectTransform).GetComponent<Image>().raycastTarget = true;
        card = Card(panel.transform, "Card", size).transform;
        Heading(card, title, -86, 76, titleColor);
        return panel;
    }

    static Button StackButton(Transform card, string label, float index)
    {
        var b = Btn(card, label.Replace(" ", "") + "Button", label, new Vector2(360, 66), 28);
        Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0, -190 - index * 84), new Vector2(360, 66));
        return b;
    }

    static void SetBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Root + "/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene(Root + "/Scenes/Game.unity", true),
        };
    }
}
