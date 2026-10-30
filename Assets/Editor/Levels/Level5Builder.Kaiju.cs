using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Level 5 (Teil von Level5Builder): Kaiju: Echse und Roboter, Intro-Exposé, Feuer-Hickser nach dem Sturz, Posen für Kampf und Sturz
public static partial class Level5Builder
{
    // ---------- Kaiju ----------

    // Beide Monster wie früher in der Stadt, aber: Die Echse schaut anfangs nach Süden (zum Spieler und zum Park, dorthin
    // speit sie Feuer), der Roboter ist noch nicht da (kommt später aus dem Himmel, eigener Arbeitsschritt). Der Ringkampf
    // (KaijuFight) ist darum erst mal aus
    private static KaijuBattle battleComponent;

    // Intro-Vorspann: Die Kamera gleitet von Südwesten auf die Echse zu, während sie das Hochhaus zerschlägt. Danach fliegt
    // die Kamera von dort zum Kunden
    private static void SetupIntroExpose()
    {
        GameObject mainCamera = GameObject.Find("Main Camera");
        IntroFlyover intro = mainCamera != null ? mainCamera.GetComponent<IntroFlyover>() : null;
        if (intro == null || battleComponent == null)
        {
            return;
        }
        var so = new SerializedObject(intro);
        so.FindProperty("exposeActor").objectReferenceValue = battleComponent;
        so.FindProperty("exposeCameraFrom").vector3Value = ExposeCameraFrom;
        so.FindProperty("exposeCameraTo").vector3Value = ExposeCameraTo;
        so.FindProperty("exposeLook").vector3Value = ExposeLook;
        so.FindProperty("exposeDuration").floatValue = ExposeDuration;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildKaiju()
    {
        CityBuilder.KaijuParts kaiju = CityBuilder.BuildKaijuForLevel(gameplayParent, KaijuCenter);
        kaiju.lizard.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // Der Schwanz zeigt jetzt nach Norden und lag quer über dem Kundenhaus (und im Flug des Intros zum Kunden): weiter nach
        // Osten über die Ruinen schwenken. KaijuFight merkt sich das als Ruhehaltung
        kaiju.lizardTail[0].localRotation *= Quaternion.Euler(0f, TailYaw, 0f);
        kaiju.robot.gameObject.SetActive(false);
        kaiju.fight.enabled = false;

        FireBreath breath = BuildFireBreath(kaiju);
        Material particles = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        // Düsen unter den Füßen des Roboters (Flammen nach unten) und ein Rauchschweif am Rücken, nur während er herabstürzt
        var thrusters = new List<ParticleSystem>();
        foreach (float x in new[] { -14f, 14f })
        {
            Transform foot = PrimitiveKit.Pivot("Thruster", kaiju.robot, new Vector3(x, 0f, 2f));
            ParticleSystem flame = Particles("Flame", foot, particles, new Color(1f, 0.5f, 0.1f, 0.95f), new Color(0.6f, 0.85f, 1f, 0.95f),
                lifetime: 0.5f, speed: 35f, size: new Vector2(3f, 6f), rate: 90f, radius: 2f, growth: 1f);
            flame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            thrusters.Add(Stopped(flame));
        }
        ParticleSystem trail = Particles("Smoke Trail", PrimitiveKit.Pivot("Trail", kaiju.robot, new Vector3(0f, 45f, -6f)), particles,
            new Color(0.4f, 0.4f, 0.42f, 0.8f), new Color(0.75f, 0.75f, 0.78f, 0.6f), lifetime: 4f, speed: 2f, size: new Vector2(8f, 16f), rate: 60f, radius: 4f, growth: 1.5f);
        thrusters.Add(Stopped(trail));

        // Staubwolken: beim Aufschlag des Roboters und wo die Echse hinfällt (nur Emit)
        Vector3 robotLanding = kaiju.robot.position;
        ParticleSystem landingDust = Dust(gameplayParent, "Landing Dust", robotLanding, 16f, particles);
        ParticleSystem fallDust = Dust(gameplayParent, "Fall Dust", new Vector3(KaijuCenter.x - 40f, CityLayout.Surface, KaijuCenter.y - 16f), 30f, particles);

        TowerCollapse tower = BuildIntroTower(particles);
        battleComponent = kaiju.root.gameObject.AddComponent<KaijuBattle>();
        var battle = new SerializedObject(battleComponent);
        battle.FindProperty("lizardRoot").objectReferenceValue = kaiju.lizard;
        battle.FindProperty("lizardUpper").objectReferenceValue = kaiju.lizardUpper;
        battle.FindProperty("lizardHead").objectReferenceValue = kaiju.lizardHead;
        battle.FindProperty("breath").objectReferenceValue = breath;
        // Die Echse ist um 180° gedreht (schaut nach Süden): ihr linker Arm (lokal -X) ist der östliche, zum Turm hin
        battle.FindProperty("lizardSmashArm").objectReferenceValue = kaiju.lizardArmLeft;
        battle.FindProperty("introTower").objectReferenceValue = tower;
        battle.FindProperty("robotRoot").objectReferenceValue = kaiju.robot;
        battle.FindProperty("robotUpper").objectReferenceValue = kaiju.robotUpper;
        battle.FindProperty("robotArmLeft").objectReferenceValue = kaiju.robotArmLeft;
        battle.FindProperty("robotArmRight").objectReferenceValue = kaiju.robotArmRight;
        battle.FindProperty("fight").objectReferenceValue = kaiju.fight;
        PrimitiveKit.SetReferences(battle, "thrusters", thrusters.ToArray());
        battle.FindProperty("landingDust").objectReferenceValue = landingDust;
        battle.FindProperty("fallDust").objectReferenceValue = fallDust;
        // Feueratem, sobald der Spieler an der Statue vorbei ist (bis zum alten Ende des Rasen-Stücks), Roboter, sobald er den
        // Park nach Norden verlässt, Finale an der Oregano Street
        battle.FindProperty("breathFromU").floatValue = LegU(BreathLegTopZ);
        battle.FindProperty("breathToU").floatValue = statueEndU;
        battle.FindProperty("vendor").objectReferenceValue = breathVendor;
        battle.FindProperty("breathCamera").objectReferenceValue = breathCameraZone;
        battle.FindProperty("chargeTime").floatValue = BreathChargeTime;
        // Der Strom braucht gut eine Sekunde bis zum Einschlag, dann rollt die Walze 1.7 s: etwas länger als früher (3 s)
        battle.FindProperty("breathTime").floatValue = BreathTime;
        battle.FindProperty("arrivalU").floatValue = LegU(ArrivalZ);
        battle.FindProperty("finaleU").floatValue = FourCheeseU(OreganoX);
        PrimitiveKit.SetReferences(battle, "lizardTail", kaiju.lizardTail);
        battle.FindProperty("fallAngle").floatValue = LizardFallAngle;
        SetLizardPoses(battle, kaiju);
        battle.ApplyModifiedPropertiesWithoutUndo();
        // Der Schwanz liegt im Kampf auf der Straße: nur leicht schwingen (8° pro Glied fegte die Spitze durch Park und Häuser)
        var fightData = new SerializedObject(kaiju.fight);
        fightData.FindProperty("tailSwing").floatValue = 1.5f;
        fightData.ApplyModifiedPropertiesWithoutUndo();

        // Bildschirm-Wackeln an der Kamera
        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null && mainCamera.GetComponent<CameraShake>() == null)
        {
            mainCamera.AddComponent<CameraShake>();
        }
        BuildHiccups(kaiju, particles);
    }

    // ---------- Nach dem Sturz: Feuer-Hickser die Salami Street hinunter ----------
    //   Die Echse liegt mit dem Maul zur Salami Street (KaijuBattle.headFallen). KaijuHiccups lässt im Takt Feuerbälle bis an
    //   die Häuser westlich der Basil Street rollen; die Bahn quert die Salami Street auf der Basil Street. Dazu Sterne über dem
    //   Kopf, Rauch aus den Nüstern, eine Brandspur auf der Straße, ein ausgeschlagener Zahn, Schutt und kleine Feuer
    private const float HiccupInterval = 2.4f, HiccupSpeed = 14f, FireballRadius = 1.2f;
    // Verpuffen auf dem Westgehweg der Basil Street, kurz vor den Fassaden (x -190); die Bahn liegt auf dem Ostgehweg (BasilX)
    private const float HiccupEndX = -187.5f;

    private static void BuildHiccups(CityBuilder.KaijuParts kaiju, Material particles)
    {
        Transform mouth = kaiju.lizardHead.Find("Mouth");
        Material fire = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Fire Additive.mat");
        Material core = PrimitiveKit.Material("Assets/Materials/City/Fireball Core.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.62f, 0.15f));
        Material hot = PrimitiveKit.Material("Assets/Materials/City/Fireball Hot.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.9f, 0.45f));
        Material star = PrimitiveKit.Material("Assets/Materials/City/Dizzy Star.mat", PrimitiveKit.UnlitShader, new Color(1f, 0.85f, 0.2f));
        Transform root = PrimitiveKit.Pivot("Lizard Hiccups", gameplayParent, Vector3.zero);

        // Feuerbälle: kleiner, heller Kern, darum ein flackernder Flammenkörper, der mitfliegt (lokale Simulation; ein einfarbige
        // Kugel sah wie ein Wasserball aus), dahinter ziehen Flammen und Rauch (Welt-Simulation)
        var balls = new List<Transform>();
        int count = Mathf.CeilToInt((mouth.position.x - HiccupEndX) / HiccupSpeed / HiccupInterval) + 1;
        for (int i = 0; i < count; i++)
        {
            Transform ball = PrimitiveKit.Pivot($"Fireball {i + 1}", root, mouth.position);
            PrimitiveKit.Part("Core", PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * FireballRadius * 1.1f, hot);
            ParticleSystem body = Particles("Flame Body", ball, fire, new Color(1f, 0.5f, 0.1f, 0.95f), new Color(1f, 0.85f, 0.35f, 0.95f),
                lifetime: 0.35f, speed: 1.2f, size: new Vector2(1.6f, 2.8f), rate: 110f, radius: FireballRadius * 0.7f, growth: 0.3f);
            ParticleSystem.MainModule bodyMain = body.main;
            bodyMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.ShapeModule bodyShape = body.shape;
            bodyShape.shapeType = ParticleSystemShapeType.Sphere;
            Particles("Flames", ball, fire, new Color(1f, 0.45f, 0.08f, 0.95f), new Color(1f, 0.82f, 0.3f, 0.95f),
                lifetime: 0.5f, speed: 2.5f, size: new Vector2(1.6f, 3.2f), rate: 110f, radius: FireballRadius, growth: 0.4f);
            Particles("Smoke", ball, particles, new Color(0.15f, 0.14f, 0.13f, 0.7f), new Color(0.3f, 0.28f, 0.27f, 0.5f),
                lifetime: 1.6f, speed: 1.5f, size: new Vector2(1.8f, 3.5f), rate: 14f, radius: FireballRadius, growth: 1.5f);
            ball.gameObject.SetActive(false);
            balls.Add(ball);
        }

        // Warnung im Maul: wachsendes Glühen, beim Hicks eine Rauchwolke; am Ende Funken an den Häusern
        Renderer glow = PrimitiveKit.Part("Hiccup Glow", PrimitiveType.Sphere, mouth, new Vector3(0f, 0f, -1.5f), Vector3.one * 4f, core);
        glow.gameObject.SetActive(false);
        ParticleSystem puff = Stopped(Particles("Hiccup Puff", mouth, particles, new Color(0.2f, 0.19f, 0.18f, 0.8f), new Color(0.4f, 0.38f, 0.36f, 0.6f),
            lifetime: 1.4f, speed: 6f, size: new Vector2(2.5f, 5f), rate: 0f, radius: 1.5f, growth: 1.5f));
        ParticleSystem fizzle = Stopped(Particles("Fireball Fizzle", root, fire, new Color(1f, 0.5f, 0.1f, 0.95f), new Color(1f, 0.85f, 0.35f, 0.9f),
            lifetime: 0.8f, speed: 7f, size: new Vector2(0.6f, 1.6f), rate: 0f, radius: 1f, growth: 0.5f));
        ParticleSystem.ShapeModule fizzleShape = fizzle.shape;
        fizzleShape.shapeType = ParticleSystemShapeType.Hemisphere;

        // Benommen: Sterne kreisen über dem Kopf (Kopf schaut nach Westen, lokal oben = Welt oben), Rauch aus den Nüstern
        Transform dazed = PrimitiveKit.Pivot("Dazed", kaiju.lizardHead, new Vector3(0f, 13f, 4f));
        Transform ring = PrimitiveKit.Pivot("Dizzy Stars", dazed, Vector3.zero);
        var spinner = new SerializedObject(ring.gameObject.AddComponent<Spinner>());
        spinner.FindProperty("axis").vector3Value = Vector3.up;
        spinner.FindProperty("degreesPerSecond").floatValue = 110f;
        spinner.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f * Mathf.Deg2Rad;
            Transform s = PrimitiveKit.Pivot($"Star {i + 1}", ring, new Vector3(Mathf.Cos(angle) * 6f, Mathf.Sin(i * 2.1f) * 0.8f, Mathf.Sin(angle) * 6f));
            s.localRotation = Quaternion.Euler(0f, -i * 72f, 0f);
            PrimitiveKit.Part("Point", PrimitiveType.Sphere, s, Vector3.zero, Vector3.one * 0.9f, star);
            foreach (float tilt in new[] { 0f, 72f, 144f, 216f, 288f })
            {
                Renderer ray = PrimitiveKit.Box("Ray", s, Vector3.zero, new Vector3(0.35f, 1.6f, 0.35f), star);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                ray.transform.localPosition = ray.transform.localRotation * new Vector3(0f, 0.6f, 0f);
            }
        }
        Particles("Nostril Smoke", PrimitiveKit.Pivot("Nostrils", dazed, new Vector3(0f, -9f, 11.5f)), particles,
            new Color(0.25f, 0.24f, 0.23f, 0.6f), new Color(0.45f, 0.43f, 0.41f, 0.45f), lifetime: 3f, speed: 2f, size: new Vector2(1.5f, 3f), rate: 7f, radius: 1.2f, growth: 2f);
        dazed.gameObject.SetActive(false);

        var hiccups = new SerializedObject(root.gameObject.AddComponent<KaijuHiccups>());
        hiccups.FindProperty("battle").objectReferenceValue = battleComponent;
        hiccups.FindProperty("mouth").objectReferenceValue = mouth;
        hiccups.FindProperty("head").objectReferenceValue = kaiju.lizardHead;
        hiccups.FindProperty("jaw").objectReferenceValue = kaiju.lizardJaw;
        hiccups.FindProperty("jawOpen").floatValue = HiccupJawRest;
        hiccups.FindProperty("interval").floatValue = HiccupInterval;
        hiccups.FindProperty("speed").floatValue = HiccupSpeed;
        hiccups.FindProperty("streetZ").floatValue = SalamiZ;
        hiccups.FindProperty("endX").floatValue = HiccupEndX;
        hiccups.FindProperty("ground").floatValue = CityLayout.RoadTop;
        hiccups.FindProperty("radius").floatValue = FireballRadius;
        PrimitiveKit.SetReferences(hiccups, "balls", balls.ToArray());
        hiccups.FindProperty("glow").objectReferenceValue = glow.transform;
        hiccups.FindProperty("puff").objectReferenceValue = puff;
        hiccups.FindProperty("fizzle").objectReferenceValue = fizzle;
        hiccups.FindProperty("dazedDecor").objectReferenceValue = dazed.gameObject;
        hiccups.ApplyModifiedPropertiesWithoutUndo();

        SalamiAftermath(mouth.position.x);
    }

    // Spuren der Hickser und des Sturzes: Brandspur auf der Salami Street (zeigt, wo die Bälle rollen), ein ausgeschlagener
    // Zahn, Schutt am Rand und auf der Basil Street, zwei kleine Feuer. Alles Kulisse ohne Collider, nicht auf der Bahn
    private static void SalamiAftermath(float mouthX)
    {
        Transform parent = PrimitiveKit.Pivot("Salami Aftermath", propParent, Vector3.zero);
        Material rubble = PrimitiveKit.Material("Assets/Materials/City/Rubble.mat", PrimitiveKit.LitShader, new Color(0.55f, 0.52f, 0.48f));
        Material scorched = PrimitiveKit.Material("Assets/Materials/City/Scorched.mat", PrimitiveKit.LitShader, new Color(0.2f, 0.18f, 0.16f));
        Material tooth = PrimitiveKit.Material("Assets/Materials/City/Lizard Tooth.mat", PrimitiveKit.LitShader, new Color(0.95f, 0.93f, 0.85f), 0.5f);
        Material facade = CityBuilder.FacadeMaterialAsset;
        var random = new System.Random(1313);

        float from = mouthX - 2f, to = HiccupEndX;
        PrimitiveKit.Box("Scorch Trail", parent, new Vector3((from + to) / 2f, CityLayout.RoadTop + 0.012f, SalamiZ), new Vector3(from - to, 0.02f, FireballRadius * 2.2f), scorched);
        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(from, to, (i + (float)random.NextDouble()) / 9f);
            PrimitiveKit.Cylinder("Scorch", parent, new Vector3(x, CityLayout.RoadTop + 0.014f, SalamiZ + ((float)random.NextDouble() - 0.5f) * 3f), 1.6f + (float)random.NextDouble() * 1.4f, 0.02f, scorched);
        }

        // Zahn: lag im Maul, liegt jetzt schräg auf der Straße neben der Brandspur
        Transform toothPivot = PrimitiveKit.Pivot("Knocked-Out Tooth", parent, new Vector3(-146f, CityLayout.RoadTop + 0.55f, SalamiZ + 4.2f));
        toothPivot.localRotation = Quaternion.Euler(0f, 35f, 78f);
        PrimitiveKit.Part("Crown", PrimitiveType.Capsule, toothPivot, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 1.2f, 1.1f), tooth);
        PrimitiveKit.Part("Tip", PrimitiveType.Sphere, toothPivot, new Vector3(0f, 1.9f, 0f), new Vector3(0.55f, 0.9f, 0.55f), tooth);

        // Schutt am Rand der Salami Street und auf der Basil Street neben der Bahn (Fahrbahn, Westgehweg)
        Vector3[] piles =
        {
            new Vector3(-133f, CityLayout.Surface, SalamiZ - 8f), new Vector3(-151f, CityLayout.Surface, SalamiZ + 8f),
            new Vector3(-163f, CityLayout.RoadTop, SalamiZ - 4.5f), new Vector3(-138f, CityLayout.RoadTop, SalamiZ + 5f),
            new Vector3(BasilX - 5f, CityLayout.RoadTop, 248f), new Vector3(BasilX - 4.5f, CityLayout.RoadTop, 283f),
            new Vector3(-188f, CityLayout.Surface, SalamiZ + 7.5f),
        };
        foreach (Vector3 pile in piles)
        {
            RubblePile(parent, pile, 2f, rubble, facade, random);
        }
        Smoke(parent, new Vector3(-157f, CityLayout.RoadTop, SalamiZ + 4.5f));
        Smoke(parent, new Vector3(-160f, CityLayout.Surface, SalamiZ + 8f));
    }

    // Das Hochhaus fürs Intro: steht als ganzer Quader (Fassaden-Shader wie die Stadt), darin steckt schon der Stumpf mit
    // Schutt, der nach dem Einsturz übrig bleibt. Dazu Brocken und zwei Staubwolken (Spitze, Fuß)
    // Rechnet die Haltungen für KaijuBattle aus, indem die Echse kurz in die jeweilige Lage gedreht wird: Schwanz im Kampf
    // (sie schaut nach Norden, Schwanz flach nach Süden auf dem Mozzarella Blvd), nach dem Sturz (gekippt, Kopf zur Salami
    // Street, Schwanz von der Hüfte schräg zurück auf den Mozzarella Blvd). Danach steht alles wieder wie gebaut
    private static void SetLizardPoses(SerializedObject battle, CityBuilder.KaijuParts kaiju)
    {
        Transform[] tail = kaiju.lizardTail;
        Quaternion lizardRest = kaiju.lizard.localRotation, headRest = kaiju.lizardHead.localRotation, upperRest = kaiju.lizardUpper.localRotation;
        var tailRest = new Quaternion[tail.Length];
        for (int i = 0; i < tail.Length; i++)
        {
            tailRest[i] = tail[i].localRotation;
        }

        Quaternion facingNorth = lizardRest * Quaternion.Euler(0f, 180f, 0f);
        kaiju.lizard.localRotation = facingNorth;
        Vector3 root = tail[0].position;
        SetQuaternions(battle, "tailFightPose", LayTail(tail, CityLayout.RoadTop,
            new Vector2(root.x, root.z), new Vector2(root.x, root.z - 200f)));

        kaiju.lizard.localRotation = facingNorth * Quaternion.Euler(0f, 0f, LizardFallAngle);
        kaiju.lizardUpper.localRotation = upperRest * Quaternion.Euler(0f, FallenBellyRoll, 0f);
        battle.FindProperty("upperFallen").quaternionValue = kaiju.lizardUpper.localRotation;
        Vector3 headRestPosition = kaiju.lizardHead.localPosition;
        PlaceFallenHead(kaiju.lizardHead, kaiju.lizardJaw);
        battle.FindProperty("headFallen").quaternionValue = kaiju.lizardHead.localRotation;
        battle.FindProperty("headFallenPosition").vector3Value = kaiju.lizardHead.localPosition;
        root = tail[0].position;
        SetQuaternions(battle, "tailFallenPose", LayTail(tail, CityLayout.Surface,
            new Vector2(root.x, root.z), new Vector2(-48f, 232f), new Vector2(-41f, 214f), new Vector2(-41f, 0f)));

        kaiju.lizard.localRotation = lizardRest;
        kaiju.lizardUpper.localRotation = upperRest;
        kaiju.lizardHead.localRotation = headRest;
        kaiju.lizardHead.localPosition = headRestPosition;
        for (int i = 0; i < tail.Length; i++)
        {
            tail[i].localRotation = tailRest[i];
        }
    }

    // Kopf nach dem Sturz (2026-10-29, er steckte ~6 m in der Straße: das Rollen auf den Bauch schwenkt den Hals-Pivot nach
    // unten): schaut nach Westen über die Salami Street (z SalamiZ), die Schnauze um HiccupJawRest gehoben, der um ebenso viel
    // geöffnete Unterkiefer liegt also waagrecht; so hoch gesetzt, dass er 5 cm über der Straße liegt. Der Hinterkopf darf
    // bis 1 m in den Schutt sinken (der Hals steckt sowieso im liegenden Körper)
    private static void PlaceFallenHead(Transform head, Transform jaw)
    {
        Quaternion jawRest = jaw.localRotation;
        Vector3 pivot = head.position;
        head.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-HiccupJawRest, Vector3.forward) * Vector3.left, Vector3.up);
        head.position = new Vector3(pivot.x, 0f, SalamiZ);
        jaw.localRotation = jawRest * Quaternion.Euler(HiccupJawRest, 0f, 0f);
        float lift = CityLayout.RoadTop + 0.05f - HeadBottom(head, true);
        lift = Mathf.Max(lift, CityLayout.RoadTop - 1f - HeadBottom(head, false));
        head.position += Vector3.up * lift;
        jaw.localRotation = jawRest;
    }

    // Unterkante von Schädel, Schnauze und Kiefer (oder nur des Kiefers) in Welt-Y
    private static float HeadBottom(Transform head, bool jawOnly)
    {
        float bottom = float.MaxValue;
        foreach (MeshRenderer part in head.GetComponentsInChildren<MeshRenderer>())
        {
            if (part.name == "Skull" && !jawOnly || part.name == "Snout" && !jawOnly || part.name == "Jaw")
            {
                bottom = Mathf.Min(bottom, part.bounds.min.y);
            }
        }
        return bottom;
    }

    // Legt die Schwanzkette entlang eines Pfades (Welt X/Z, beginnt an der Wurzel) auf den Boden: Jedes Glied zeigt (mit
    // lokal -Z) auf den nächsten Punkt, der höchstens 70° tiefer liegt und sonst auf Bodenhöhe plus Glied-Radius. Glieder
    // werden der Reihe nach gedreht, weil jedes Kind dort sitzt, wo sein Elternglied hinzeigt. Gibt die lokalen Drehungen zurück
    private static Quaternion[] LayTail(Transform[] tail, float ground, params Vector2[] path)
    {
        var pose = new Quaternion[tail.Length];
        float s = 0f, scale = 1f;
        for (int i = 0; i < tail.Length; i++)
        {
            Vector3 current = tail[i].position;
            float length = i + 1 < tail.Length ? tail[i + 1].localPosition.magnitude : 17f * scale;
            // Segment-Kugel: 12 * scale hoch, etwas eingesunken
            float targetY = ground + 6f * scale * 0.82f - 1f;
            float drop = Mathf.Clamp(current.y - targetY, -0.3f * length, Mathf.Sin(70f * Mathf.Deg2Rad) * length);
            s += Mathf.Sqrt(length * length - drop * drop);
            Vector2 flat = PathPoint(path, s);
            var next = new Vector3(flat.x, current.y - drop, flat.y);
            tail[i].rotation = Quaternion.LookRotation(current - next, Vector3.up);
            pose[i] = tail[i].localRotation;
            scale *= 0.82f;
        }
        return pose;
    }

    // Punkt auf einem Streckenzug nach der Länge s (über das Ende hinaus geradeaus weiter)
    private static Vector2 PathPoint(Vector2[] path, float s)
    {
        for (int i = 0; i + 1 < path.Length; i++)
        {
            float length = Vector2.Distance(path[i], path[i + 1]);
            if (s <= length || i + 2 == path.Length)
            {
                return path[i] + (path[i + 1] - path[i]).normalized * s;
            }
            s -= length;
        }
        return path[0];
    }

    private static void SetQuaternions(SerializedObject data, string name, Quaternion[] values)
    {
        SerializedProperty array = data.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).quaternionValue = values[i];
        }
    }

    private static TowerCollapse BuildIntroTower(Material particles)
    {
        Material facade = CityBuilder.FacadeMaterialAsset;
        Material rubble = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/Rubble.mat");
        Rect lot = IntroTowerLot;
        var root = new GameObject("Intro Tower");
        root.transform.SetParent(propParent, false);
        root.transform.position = new Vector3(lot.center.x, CityLayout.Surface, lot.center.y);
        Transform body = PrimitiveKit.Pivot("Body", root.transform, Vector3.zero);
        PrimitiveKit.Box("Tower", body, new Vector3(0f, IntroTowerHeight / 2f, 0f), new Vector3(lot.width - 1f, IntroTowerHeight, lot.height - 1f), facade);
        PrimitiveKit.Box("Crown", body, new Vector3(0f, IntroTowerHeight + 3f, 0f), new Vector3(lot.width - 5f, 6f, lot.height - 5f), facade);

        var random = new System.Random(808);
        Transform aftermath = PrimitiveKit.Pivot("Aftermath", root.transform, Vector3.zero);
        Stump(aftermath, Rect.MinMaxRect(lot.xMin + 1.5f, lot.yMin + 1.5f, lot.xMax - 1.5f, lot.yMax - 1.5f), 9f, facade, rubble, random);
        RubblePile(aftermath, root.transform.position, 7f, rubble, facade, random);

        Transform debrisParent = PrimitiveKit.Pivot("Debris", root.transform, Vector3.zero);
        var debris = new List<Transform>();
        for (int i = 0; i < 16; i++)
        {
            float size = 1.5f + (float)random.NextDouble() * 2.5f;
            Renderer piece = PrimitiveKit.Box("Chunk", debrisParent, Vector3.up * IntroTowerHeight, new Vector3(size, size * 0.7f, size * 1.2f), i % 2 == 0 ? rubble : facade);
            debris.Add(piece.transform);
        }

        ParticleSystem impactDust = Dust(root.transform, "Impact Dust", root.transform.position + Vector3.up * IntroTowerHeight, 8f, particles);
        ParticleSystem baseDust = Dust(root.transform, "Collapse Dust", root.transform.position, 11f, particles);

        var tower = root.AddComponent<TowerCollapse>();
        var serialized = new SerializedObject(tower);
        serialized.FindProperty("body").objectReferenceValue = body;
        serialized.FindProperty("height").floatValue = IntroTowerHeight + 6f;
        serialized.FindProperty("aftermath").objectReferenceValue = aftermath.gameObject;
        PrimitiveKit.SetReferences(serialized, "debris", debris.ToArray());
        serialized.FindProperty("impactDust").objectReferenceValue = impactDust;
        serialized.FindProperty("baseDust").objectReferenceValue = baseDust;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return tower;
    }

    // Partikelsystem, das erst auf Befehl startet
    private static ParticleSystem Stopped(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    // Staubwolke: graue Brocken quellen in einer Halbkugel auf und sinken langsam, nur per Emit
    private static ParticleSystem Dust(Transform parent, string name, Vector3 at, float radius, Material material)
    {
        ParticleSystem dust = Particles(name, parent, material, new Color(0.55f, 0.52f, 0.48f, 0.85f), new Color(0.75f, 0.72f, 0.68f, 0.7f),
            lifetime: 3.5f, speed: 14f, size: new Vector2(6f, 14f), rate: 0f, radius: radius, growth: 1.5f);
        dust.transform.position = at;
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius;
        ParticleSystem.MainModule main = dust.main;
        main.gravityModifier = 0.15f;
        main.maxParticles = 400;
        return Stopped(dust);
    }
}
