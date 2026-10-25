using UnityEditor;
using UnityEngine;

// Kaiju-Viertel: zerstörte Blöcke, Schutt, Krater und die beiden ~80 m großen Monster (Riesenechse gegen Riesenroboter).
// Die beiden stehen sich auf dem Mozzarella Blvd gegenüber, die Füße links und rechts der Straße (x -40 ± 14),
// so dass eine Laufbahn auf dem Gehweg später zwischen den Beinen hindurchführt. Animiert von KaijuFight.
public static partial class CityBuilder
{
    // Abstand der Füße von der Körpermitte (Straßenmitte). Gehweg-Laufbahn liegt bei ~7 m, Kamera ~1 m daneben
    private const float FootSpread = 14f;

    // ---------- Ruinen ----------

    // 40 % der Grundstücke sind nur noch Schutthaufen, der Rest sind niedrigere Hausreste
    private static void BuildRuins(Transform parent, CityLayout.Block block, System.Random random)
    {
        foreach (Rect lot in Lots(block.Lots, random, false))
        {
            if (random.NextDouble() < 0.4)
            {
                RubblePile(parent, lot.center, Mathf.Min(lot.width, lot.height) * 0.5f, random);
            }
            else
            {
                House(parent, lot, Pick(random, 5f, 6f, 8f, 10f, 12f));
            }
        }
    }

    // Haufen aus schräg liegenden Brocken. Ein paar Brocken tragen die Fassade (mit Fenstern) als abgebrochene Wandstücke
    private static void RubblePile(Transform parent, Vector2 at, float radius, System.Random random)
    {
        Material concrete = Mat("Rubble", new Color(0.55f, 0.52f, 0.48f));
        Transform pile = new GameObject("Rubble").transform;
        pile.SetParent(parent, false);
        pile.position = new Vector3(at.x, Surface, at.y);
        int pieces = 4 + random.Next(4);
        for (int i = 0; i < pieces; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = (float)random.NextDouble() * radius;
            float size = 1.5f + (float)random.NextDouble() * 3.5f;
            var scale = new Vector3(size * (1f + (float)random.NextDouble()), size * 0.6f, size);
            Renderer chunk = PrimitiveKit.Box("Chunk", pile, new Vector3(Mathf.Sin(angle) * distance, scale.y * 0.3f, Mathf.Cos(angle) * distance), scale,
                random.NextDouble() < 0.35 ? facade : concrete);
            chunk.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 40f - 20f);
        }
    }

    private static void Crater(Transform parent, Vector2 at, float radius)
    {
        Material scorched = Mat("Scorched", new Color(0.2f, 0.18f, 0.16f));
        Cylinder("Crater", parent, new Vector3(at.x, RoadTop + 0.02f, at.y), radius, 0.44f, scorched);
        Cylinder("Crater Rim", parent, new Vector3(at.x, RoadTop + 0.1f, at.y), radius + 1.2f, 0.3f, Mat("Rubble"));
    }

    // ---------- Für Level 5 ----------

    // Teile der beiden Monster, damit Level 5 sie steuern kann (Wüten mit Feueratem, Ankunft des Roboters, Kampf, Sieg)
    public struct KaijuParts
    {
        public Transform root, lizard, lizardUpper, lizardHead, lizardArmLeft, lizardArmRight, robot, robotUpper, robotHead, robotArmLeft, robotArmRight;
        public Transform[] lizardTail;
        public Renderer robotVisor;
        public KaijuFight fight;
    }

    // Baut beide Monster wie früher in der Stadt (Füße bei x ± FootSpread um die Straßenmitte, Echse 16 m südlich, Roboter
    // 20 m nördlich von "at"), mit KaijuFight. Für ein Level: Die Wurzel kommt in dessen Szene, Materialien wie in der Stadt
    public static KaijuParts BuildKaijuForLevel(Transform parent, Vector2 at)
    {
        Transform fight = new GameObject("Kaiju").transform;
        fight.SetParent(parent, false);
        fight.position = new Vector3(at.x, Surface, at.y);
        var lizard = BuildLizard(fight, new Vector3(0f, 0f, -16f));
        var robot = BuildRobot(fight, new Vector3(0f, 0f, 20f));
        var fightScript = fight.gameObject.AddComponent<KaijuFight>();
        var serialized = new SerializedObject(fightScript);
        serialized.FindProperty("lizardUpper").objectReferenceValue = lizard.upper;
        serialized.FindProperty("lizardHead").objectReferenceValue = lizard.head;
        serialized.FindProperty("lizardArmLeft").objectReferenceValue = lizard.armLeft;
        serialized.FindProperty("lizardArmRight").objectReferenceValue = lizard.armRight;
        PrimitiveKit.SetReferences(serialized, "lizardTail", lizard.tail);
        serialized.FindProperty("robotUpper").objectReferenceValue = robot.upper;
        serialized.FindProperty("robotHead").objectReferenceValue = robot.head;
        serialized.FindProperty("robotArmLeft").objectReferenceValue = robot.armLeft;
        serialized.FindProperty("robotArmRight").objectReferenceValue = robot.armRight;
        serialized.FindProperty("robotVisor").objectReferenceValue = robot.visor;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return new KaijuParts
        {
            root = fight, lizard = lizard.upper.parent, lizardUpper = lizard.upper, lizardHead = lizard.head,
            lizardArmLeft = lizard.armLeft, lizardArmRight = lizard.armRight, lizardTail = lizard.tail,
            robot = robot.upper.parent, robotUpper = robot.upper, robotHead = robot.head, robotArmLeft = robot.armLeft,
            robotArmRight = robot.armRight, robotVisor = robot.visor, fight = fightScript,
        };
    }

    // Fassaden-Material der Stadthäuser (Fenster aus der Objekt-Skalierung): Umgestürzte Türme in Level 5 sehen damit aus
    // wie die stehenden in den anderen Leveln
    public static Material FacadeMaterialAsset => FacadeMaterial();

    // ---------- Kampf ----------

    private static void BuildKaiju(Transform parent)
    {
        Vector2 at = CityLayout.KaijuFight;
        Transform fight = new GameObject("Kaiju Fight").transform;
        fight.SetParent(parent, false);
        fight.position = new Vector3(at.x, Surface, at.y);

        // Spuren des Kampfes auf der Straße (nicht auf den Gehwegen, dort läuft später die Route)
        var random = new System.Random(4242);
        Crater(fight, at + new Vector2(0f, -38f), 5f);
        Crater(fight, at + new Vector2(2f, 34f), 4f);
        for (int i = 0; i < 6; i++)
        {
            var spot = new Vector2(at.x - 3f + (float)random.NextDouble() * 6f, 225f + (float)random.NextDouble() * (N - 235f));
            RubblePile(fight, spot, 2f, random);
        }

        var lizard = BuildLizard(fight, new Vector3(0f, 0f, -16f));
        var robot = BuildRobot(fight, new Vector3(0f, 0f, 20f));

        var fightScript = fight.gameObject.AddComponent<KaijuFight>();
        var serialized = new SerializedObject(fightScript);
        serialized.FindProperty("lizardUpper").objectReferenceValue = lizard.upper;
        serialized.FindProperty("lizardHead").objectReferenceValue = lizard.head;
        serialized.FindProperty("lizardArmLeft").objectReferenceValue = lizard.armLeft;
        serialized.FindProperty("lizardArmRight").objectReferenceValue = lizard.armRight;
        PrimitiveKit.SetReferences(serialized, "lizardTail", lizard.tail);
        serialized.FindProperty("robotUpper").objectReferenceValue = robot.upper;
        serialized.FindProperty("robotHead").objectReferenceValue = robot.head;
        serialized.FindProperty("robotArmLeft").objectReferenceValue = robot.armLeft;
        serialized.FindProperty("robotArmRight").objectReferenceValue = robot.armRight;
        serialized.FindProperty("robotVisor").objectReferenceValue = robot.visor;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Echse, schaut nach Norden (+Z) zum Roboter. Lokal: +Z = vorne, Wurzel am Boden zwischen den Füßen
    private static (Transform upper, Transform head, Transform armLeft, Transform armRight, Transform[] tail) BuildLizard(Transform parent, Vector3 position)
    {
        Material skin = Mat("Lizard Skin", new Color(0.25f, 0.42f, 0.22f), 0.3f);
        Material belly = Mat("Lizard Belly", new Color(0.55f, 0.6f, 0.4f), 0.2f);
        Material plates = Mat("Lizard Plates", new Color(0.82f, 0.84f, 0.78f), 0.3f);
        Material eye = Glow("Lizard Eye", new Color(1f, 0.7f, 0.15f));

        Transform lizard = PrimitiveKit.Pivot("Giant Lizard", parent, position);

        // Beine: Fuß, Unterschenkel, Oberschenkel. Bleiben fest stehen
        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "L" : "R";
            PrimitiveKit.Part("Foot " + s, PrimitiveType.Sphere, lizard, new Vector3(side * FootSpread, 2.5f, 3f), new Vector3(10f, 5f, 14f), skin);
            for (int toe = -1; toe <= 1; toe++)
            {
                PrimitiveKit.Part($"Toe {s}{toe + 2}", PrimitiveType.Sphere, lizard, new Vector3(side * FootSpread + toe * 3f, 1.5f, 10f), new Vector3(2.6f, 2.4f, 4f), plates);
            }
            PrimitiveKit.Part("Shin " + s, PrimitiveType.Capsule, lizard, new Vector3(side * (FootSpread - 0.5f), 11f, 0f), new Vector3(8f, 9f, 8f), skin);
            PrimitiveKit.Part("Thigh " + s, PrimitiveType.Sphere, lizard, new Vector3(side * (FootSpread - 3f), 27f, -1f), new Vector3(13f, 22f, 15f), skin);
        }

        // Oberkörper an einem Pivot in Hüfthöhe: KaijuFight lehnt ihn vor und zurück
        Transform upper = PrimitiveKit.Pivot("Upper Body", lizard, new Vector3(0f, 32f, 0f));
        PrimitiveKit.Part("Belly", PrimitiveType.Sphere, upper, new Vector3(0f, 10f, 2f), new Vector3(26f, 30f, 22f), skin);
        PrimitiveKit.Part("Belly Front", PrimitiveType.Sphere, upper, new Vector3(0f, 11f, 9f), new Vector3(17f, 26f, 9f), belly);
        PrimitiveKit.Part("Chest", PrimitiveType.Sphere, upper, new Vector3(0f, 26f, 5f), new Vector3(22f, 22f, 20f), skin);
        // Rückenplatten: um 45° gedrehte Würfel = Rauten im Profil, nach oben kleiner
        for (int i = 0; i < 7; i++)
        {
            float size = 9f - i * 0.8f;
            Renderer plate = PrimitiveKit.Box($"Back Plate {i + 1}", upper, new Vector3(0f, -2f + i * 6f, -9f - Mathf.Sin(i * 0.5f) * 2f), new Vector3(1.6f, size, size), plates);
            plate.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        }

        // Arme hängen im Pivot nach unten (-Y). Der Pivot dreht sie nach vorne zum Roboter
        Transform armLeft = LizardArm(upper, -1, skin, plates);
        Transform armRight = LizardArm(upper, 1, skin, plates);

        Transform head = PrimitiveKit.Pivot("Head", upper, new Vector3(0f, 36f, 8f));
        PrimitiveKit.Part("Neck", PrimitiveType.Sphere, head, new Vector3(0f, -1f, 0f), new Vector3(12f, 12f, 12f), skin);
        PrimitiveKit.Part("Skull", PrimitiveType.Sphere, head, new Vector3(0f, 4f, 5f), new Vector3(11f, 10f, 15f), skin);
        PrimitiveKit.Box("Snout", head, new Vector3(0f, 3f, 12f), new Vector3(8f, 5f, 9f), skin);
        PrimitiveKit.Box("Jaw", head, new Vector3(0f, 0f, 11f), new Vector3(7f, 2f, 8f), belly);
        PrimitiveKit.Part("Eye L", PrimitiveType.Sphere, head, new Vector3(-4f, 6.5f, 10f), Vector3.one * 1.6f, eye);
        PrimitiveKit.Part("Eye R", PrimitiveType.Sphere, head, new Vector3(4f, 6.5f, 10f), Vector3.one * 1.6f, eye);

        // Schwanz: Kette aus Pivots, jedes Glied kleiner und weiter nach unten/zur Seite gebogen, liegt hinten auf dem Schutt
        var tail = new Transform[7];
        // Neigung pro Glied (summiert sich entlang der Kette). Negativ um X = Spitze nach unten, weil das Glied nach -Z zeigt
        float[] droop = { -10f, -10f, -8f, 6f, 6f, 6f, 6f };
        Transform previous = lizard;
        Vector3 offset = new Vector3(0f, 24f, -8f);
        float scale = 1f;
        for (int i = 0; i < tail.Length; i++)
        {
            Transform joint = PrimitiveKit.Pivot($"Tail {i + 1}", previous, offset);
            joint.localRotation = Quaternion.Euler(droop[i], 9f, 0f);
            PrimitiveKit.Part("Segment", PrimitiveType.Sphere, joint, new Vector3(0f, 0f, -9f * scale), new Vector3(14f, 12f, 22f) * scale, skin);
            if (i < 5)
            {
                Renderer plate = PrimitiveKit.Box("Tail Plate", joint, new Vector3(0f, 5.5f * scale, -9f * scale), new Vector3(1.2f, 5f, 5f) * scale, plates);
                plate.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
            tail[i] = joint;
            previous = joint;
            offset = new Vector3(0f, 0f, -17f * scale);
            scale *= 0.82f;
        }

        return (upper, head, armLeft, armRight, tail);
    }

    private static Transform LizardArm(Transform upper, int side, Material skin, Material claws)
    {
        Transform arm = PrimitiveKit.Pivot(side < 0 ? "Arm L" : "Arm R", upper, new Vector3(side * 11f, 28f, 8f));
        arm.localRotation = Quaternion.Euler(-80f, side * -10f, 0f);
        PrimitiveKit.Part("Upper Arm", PrimitiveType.Capsule, arm, new Vector3(0f, -6f, 0f), new Vector3(4.5f, 6f, 4.5f), skin);
        PrimitiveKit.Part("Hand", PrimitiveType.Sphere, arm, new Vector3(0f, -12.5f, 0f), Vector3.one * 4.8f, skin);
        for (int claw = -1; claw <= 1; claw++)
        {
            PrimitiveKit.Part("Claw", PrimitiveType.Capsule, arm, new Vector3(claw * 1.4f, -15.5f, 0.8f), new Vector3(0.8f, 1.6f, 0.8f), claws);
        }
        return arm;
    }

    // Roboter, schaut nach Süden (-Z) zur Echse: gleiche lokale Bauweise (+Z = vorne), die Wurzel ist um 180° gedreht
    private static (Transform upper, Transform head, Transform armLeft, Transform armRight, Renderer visor) BuildRobot(Transform parent, Vector3 position)
    {
        Material steel = Mat("Robot Steel", new Color(0.62f, 0.66f, 0.72f), 0.5f);
        Material dark = Mat("Robot Dark", new Color(0.25f, 0.27f, 0.3f), 0.4f);
        Material accent = Mat("Robot Accent", new Color(0.85f, 0.2f, 0.15f), 0.4f);
        Material visorGlow = Glow("Robot Visor", new Color(0.4f, 0.95f, 1f));

        Transform robot = PrimitiveKit.Pivot("Giant Robot", parent, position);
        robot.localRotation = Quaternion.Euler(0f, 180f, 0f);

        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "L" : "R";
            PrimitiveKit.Box("Foot " + s, robot, new Vector3(side * FootSpread, 2.5f, 2f), new Vector3(10f, 5f, 16f), dark);
            PrimitiveKit.Box("Shin " + s, robot, new Vector3(side * (FootSpread - 0.5f), 14f, 0f), new Vector3(8f, 18f, 9f), steel);
            PrimitiveKit.Box("Knee " + s, robot, new Vector3(side * (FootSpread - 0.5f), 23.5f, 1f), new Vector3(9f, 4f, 10f), accent);
            PrimitiveKit.Box("Thigh " + s, robot, new Vector3(side * (FootSpread - 1.5f), 31f, 0f), new Vector3(9f, 14f, 10f), steel);
        }
        PrimitiveKit.Box("Pelvis", robot, new Vector3(0f, 38f, 0f), new Vector3(24f, 7f, 13f), dark);

        Transform upper = PrimitiveKit.Pivot("Upper Body", robot, new Vector3(0f, 41f, 0f));
        PrimitiveKit.Box("Waist", upper, new Vector3(0f, 1f, 0f), new Vector3(18f, 8f, 14f), dark);
        PrimitiveKit.Box("Torso", upper, new Vector3(0f, 14f, 0f), new Vector3(30f, 20f, 18f), steel);
        PrimitiveKit.Box("Chest Plate", upper, new Vector3(0f, 16f, 9.2f), new Vector3(16f, 8f, 1f), accent);
        PrimitiveKit.Box("Back Pack", upper, new Vector3(0f, 15f, -11f), new Vector3(18f, 14f, 5f), dark);

        Transform armLeft = RobotArm(upper, -1, steel, dark, accent);
        Transform armRight = RobotArm(upper, 1, steel, dark, accent);

        Transform head = PrimitiveKit.Pivot("Head", upper, new Vector3(0f, 24f, 1f));
        Cylinder("Neck", head, new Vector3(0f, 1f, 0f), 3f, 3f, dark);
        PrimitiveKit.Box("Helmet", head, new Vector3(0f, 6f, 0f), new Vector3(11f, 9f, 11f), steel);
        Renderer visor = PrimitiveKit.Box("Visor", head, new Vector3(0f, 7f, 5.6f), new Vector3(9f, 2f, 0.6f), visorGlow);
        for (int side = -1; side <= 1; side += 2)
        {
            Cylinder("Antenna", head, new Vector3(side * 4f, 12.5f, -1f), 0.35f, 4f, dark);
            PrimitiveKit.Box("Ear", head, new Vector3(side * 6f, 6f, 0f), new Vector3(1.5f, 4f, 4f), accent);
        }

        return (upper, head, armLeft, armRight, visor);
    }

    private static Transform RobotArm(Transform upper, int side, Material steel, Material dark, Material accent)
    {
        Transform arm = PrimitiveKit.Pivot(side < 0 ? "Arm L" : "Arm R", upper, new Vector3(side * 19f, 19f, 0f));
        arm.localRotation = Quaternion.Euler(-50f, 0f, side * 8f);
        PrimitiveKit.Box("Shoulder", arm, Vector3.zero, new Vector3(11f, 10f, 11f), accent);
        PrimitiveKit.Box("Upper Arm", arm, new Vector3(0f, -9f, 0f), new Vector3(6f, 12f, 6f), steel);
        Cylinder("Elbow", arm, new Vector3(0f, -15.5f, 0f), 3.5f, 3f, dark);
        PrimitiveKit.Box("Forearm", arm, new Vector3(0f, -22f, 0f), new Vector3(7.5f, 12f, 7.5f), steel);
        PrimitiveKit.Box("Fist", arm, new Vector3(0f, -30f, 0f), new Vector3(9f, 8f, 9f), dark);
        return arm;
    }
}
