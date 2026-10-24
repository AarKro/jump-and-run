using UnityEngine;

// Grundriss der gemeinsamen Stadt, dieselben Werte wie auf dem Stadtplan (Artefakt "Pizza City Map").
// Welt-Meter: X = Osten (rechts), Z = Norden (in den Bildschirm hinein). Gehweg-Oberkante y = 0.5, Straße y = 0.3.
// Level 1 liegt in der Südost-Ecke: Margherita Avenue ist der Südrand (Laufbahn bei z = 0), Pepperoni Parkway der Ostrand (x = 68).
public static class CityLayout
{
    // Blockkanten der Stadt (dort liegt bei den Ringstraßen die Laufbahn)
    public const float West = -250f;
    public const float East = 68f;
    public const float North = 318f;

    public const float Surface = 0.5f;
    public const float RoadTop = 0.3f;

    // Ringstraße: Straßenmitte 7 m außerhalb der Blockkante (Bordstein bei 2 m, Fahrbahn 10 m breit)
    public const float RingOffset = 7f;

    // Straßenarten. Ring = Außenring wie Level 1 (Kamera schaut von außen hinein),
    // Avenue = 20 m Fassade zu Fassade (die Kamera 8 m vor dem Spieler bleibt über der Fahrbahn),
    // Narrow = 10 m, nur zum Überqueren (Autos wie die Querstraße in Level 1)
    public enum Side { Ring, Avenue, Narrow }

    // Abstand Straßenmitte → Fassade bzw. → Gehweg-Kante (Bordstein). Beim Ring von der Blockkante aus gemessen
    public static float FacadeInset(Side side) => side == Side.Narrow ? 5f : side == Side.Avenue ? 10f : 3f;
    public static float SidewalkInset(Side side) => side == Side.Narrow ? 3f : side == Side.Avenue ? 6f : -2f;

    public enum BlockKind { Houses, Downtown, Piazza, Park, Ruins }

    public readonly struct Street
    {
        public readonly string Name;
        public readonly bool Vertical;   // true: verläuft nach Norden (konstantes X)
        public readonly float At;        // X (vertikal) bzw. Z (horizontal) der Straßenmitte
        public readonly float From, To;  // Ausdehnung entlang der Straße
        public readonly Side Type;

        public Street(string name, bool vertical, float at, float from, float to, Side type)
        {
            Name = name; Vertical = vertical; At = at; From = from; To = to; Type = type;
        }

        // Halbe Fahrbahnbreite
        public float HalfRoad => Type == Side.Narrow ? 3f : Type == Side.Avenue ? 6f : 5f;
    }

    private const float RW = West - RingOffset, RE = East + RingOffset, RS = -RingOffset, RN = North + RingOffset;

    // Strandpromenade (Level 2): Zwischen Basil Street und Mozzarella Blvd hat die Margherita Avenue keine Fahrbahn. Stattdessen
    // ein Plattenweg (Laufbahn bei z 0, bis zu den Fassaden bei z 3), davor eine Steinkante, ein Streifen Sand, ein flacher
    // Abhang mit Brandung ins Wasser. Die Kamera steht dort über dem Meer und schaut über den Strand auf die Häuser.
    // Die Oregano Street endet an der Promenade (Poller), Autos gibt es nur noch am Mozzarella Blvd
    public const float BeachFromX = -174f;      // Ostkante der Basil Street
    public const float BeachToX = -46f;         // Westkante des Mozzarella Blvd
    public const float PromenadeFrontZ = -2f;   // Steinkante zum Sand
    public const float PromenadeBackZ = 3f;     // Fassaden
    public const float SandTop = 0.2f;
    public const float SandFrontZ = -5.5f;      // hier beginnt der Abhang
    public const float SlopeEndZ = -9.5f;
    public const float SlopeEndY = -0.8f;
    public const float SeaLevel = -0.5f;
    // Spiel-Elemente von Level 2 auf dem Strand (Bachmündungen, Rettungsturm, Schirm): dort keine Strand-Deko
    public static readonly float[] BeachKeepClearX = { -58f, -110f, -126f, -144f };

    // Abhang vom Sand ins Wasser als Ebene: Mitte der Oberfläche (x = 0, vom Aufrufer gesetzt), Drehung (lokal Z = hangaufwärts,
    // lokal Y = Oberflächen-Normale), Länge entlang des Hangs, und wo darauf (lokal Z) die Wasserlinie liegt
    public static void BeachSlope(out Vector3 surfaceCenter, out Quaternion rotation, out float length, out float waterlineZ)
    {
        var top = new Vector3(0f, SandTop, SandFrontZ);
        var bottom = new Vector3(0f, SlopeEndY, SlopeEndZ);
        Vector3 uphill = top - bottom;
        length = uphill.magnitude;
        surfaceCenter = (top + bottom) / 2f;
        // Kreuzprodukt hangaufwärts × rechts = Normale, die nach oben (und leicht zum Meer) zeigt
        rotation = Quaternion.LookRotation(uphill.normalized, Vector3.Cross(uphill, Vector3.right).normalized);
        waterlineZ = -length / 2f + length * (SeaLevel - SlopeEndY) / (SandTop - SlopeEndY);
    }

    public static readonly Street[] Streets =
    {
        // Außenring
        // Unterbrochen von der Strandpromenade (siehe BeachFromX)
        new Street("Margherita Avenue", false, RS, RW, BeachFromX, Side.Ring),
        new Street("Margherita Avenue", false, RS, BeachToX, RE, Side.Ring),
        new Street("Pepperoni Parkway", true, RE, RS, RN, Side.Ring),
        new Street("Calzone Road", false, RN, RW, RE, Side.Ring),
        new Street("Marinara Promenade", true, RW, RS, RN, Side.Ring),
        // Nord-Süd
        new Street("Basil Street", true, -180f, RS, RN, Side.Avenue),
        new Street("Oregano Street", true, -110f, PromenadeBackZ, 130f, Side.Avenue),
        new Street("Oregano Street", true, -110f, 215f, RN, Side.Avenue),
        new Street("Mozzarella Blvd", true, -40f, RS, RN, Side.Avenue),
        new Street("Anchovy Alley", true, 50f, RS, RN, Side.Narrow),
        // Ost-West
        new Street("Crust Street", false, 130f, RW, -40f, Side.Avenue),
        new Street("Crust Street", false, 112f, -40f, RE, Side.Avenue),
        new Street("Olive Lane", false, 165f, -40f, RE, Side.Avenue),
        new Street("Four Cheese Avenue", false, 215f, RW, RE, Side.Avenue),
        new Street("Salami Street", false, 265f, RW, -110f, Side.Avenue),
    };

    public readonly struct Block
    {
        public readonly float X0, X1, Z0, Z1;               // begrenzende Straßenmitten bzw. Blockkanten am Ring
        public readonly Side Left, Right, Bottom, Top;      // Bottom = Süden, Top = Norden
        public readonly BlockKind Kind;

        public Block(float x0, float x1, float z0, float z1, string sides, BlockKind kind)
        {
            X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; Kind = kind;
            Left = Parse(sides[0]); Right = Parse(sides[1]); Bottom = Parse(sides[2]); Top = Parse(sides[3]);
        }

        private static Side Parse(char c) => c == 'r' ? Side.Ring : c == 'n' ? Side.Narrow : Side.Avenue;

        // Fläche, auf der Häuser stehen (bis zu den Fassaden)
        public Rect Lots => Inset(FacadeInset);

        // Gehweg-Platte: bis zum Bordstein der umgebenden Straßen
        public Rect Sidewalk => Inset(SidewalkInset);

        private Rect Inset(System.Func<Side, float> inset)
        {
            return Rect.MinMaxRect(X0 + inset(Left), Z0 + inset(Bottom), X1 - inset(Right), Z1 - inset(Top));
        }
    }

    // Seiten: links (West), rechts (Ost), unten (Süd), oben (Nord). r = Ring, a = Avenue, n = Narrow
    public static readonly Block[] Blocks =
    {
        new Block(West, -180f, 0f, 130f, "rara", BlockKind.Piazza),
        new Block(-180f, -110f, 0f, 130f, "aara", BlockKind.Houses),
        new Block(-110f, -40f, 0f, 130f, "aara", BlockKind.Houses),
        new Block(-40f, 50f, 0f, 112f, "anra", BlockKind.Houses),
        new Block(50f, East, 0f, 112f, "nrra", BlockKind.Houses),
        new Block(West, -180f, 130f, 215f, "raaa", BlockKind.Houses),
        new Block(-180f, -40f, 130f, 215f, "aaaa", BlockKind.Park),
        new Block(-40f, 50f, 112f, 165f, "anaa", BlockKind.Downtown),
        new Block(50f, East, 112f, 165f, "nraa", BlockKind.Houses),
        new Block(-40f, 50f, 165f, 215f, "anaa", BlockKind.Downtown),
        new Block(50f, East, 165f, 215f, "nraa", BlockKind.Houses),
        new Block(West, -180f, 215f, 265f, "raaa", BlockKind.Houses),
        new Block(West, -180f, 265f, North, "raar", BlockKind.Houses),
        new Block(-180f, -110f, 215f, 265f, "aaaa", BlockKind.Houses),
        new Block(-180f, -110f, 265f, North, "aaar", BlockKind.Houses),
        new Block(-110f, -40f, 215f, North, "aaar", BlockKind.Ruins),
        new Block(-40f, 50f, 215f, North, "anar", BlockKind.Ruins),
        new Block(50f, East, 215f, North, "nrar", BlockKind.Houses),
    };

    // Feste Gebäude (Prefabs mit Spiel-Funktion, bekommen eine CityLandmark). Stehen genau da, wo sie in Level 1 standen:
    // Position = Prefab-Wurzel, Drehung = Lane-Rahmen (nach der Kurve -90°)
    public static readonly (string prefab, Vector3 position, float yaw)[] Landmarks =
    {
        ("Pizzeria", new Vector3(-8f, 0f, 0f), 0f),
        ("Coffee Shop", new Vector3(61f, 0f, 0f), 0f),
        // Strandpromenade, kurz vor der Basil Street: Checkpoint von Level 2 (nach dem dritten Chili)
        ("Gelato Shop", new Vector3(-138f, 0f, 0f), 0f),
    };

    // Grundstücke, auf denen der Generator keine zufälligen Häuser baut: unter den festen Gebäuden, und ein Haus mit fester
    // Größe dort, wo in Level 1 das Kundenhaus steht (Level 1 schneidet genau dieses Haus aus und setzt sein Ziel hinein)
    public static readonly Rect[] LandmarkLots =
    {
        Rect.MinMaxRect(-13f, 3f, -3f, 11f),       // Pizzeria
        Rect.MinMaxRect(57f, 3f, 65f, 11f),        // Café
        Rect.MinMaxRect(-142f, 3f, -134f, 11f),    // Gelateria
    };

    // Piazza Pisa ist auch zur Margherita Avenue hin offen: ab hier bis zur Basil Street keine Häuser in der Südreihe
    public const float PiazzaSouthOpeningFrom = -236f;
    public static readonly Rect CustomerHouseLot = Rect.MinMaxRect(55f, 75.7f, 65f, 83.7f);

    // Pesto Park (Level 3): Rasen und Wege. Die Wege liegen 4 cm über dem Gehweg-Niveau, damit Prefabs (für 0.5 gebaut,
    // Wurzeln auf ganzen Metern) kaum sichtbar einsinken
    public const float ParkLawnTop = Surface + 0.02f;
    public const float ParkPathTop = Surface + 0.04f;
    public static Rect ParkArea => System.Array.Find(Blocks, block => block.Kind == BlockKind.Park).Lots;
    public static Vector2 ParkCenter => ParkArea.center;
    // Richtung vom Brunnen zur Südwest-Ecke (dort entlang läuft Level 3 zum Kunden) und senkrecht dazu nach Nordwesten
    public static Vector2 ParkDirSW => (new Vector2(ParkArea.xMin - 1f, ParkArea.yMin - 1f) - ParkCenter).normalized;
    public static Vector2 ParkNormalNW => new Vector2(ParkDirSW.y, -ParkDirSW.x);
    // Punkt auf dem Südwest-Weg im Abstand s von der Brunnenmitte
    public static Vector2 ParkPathSW(float s) => ParkCenter + ParkDirSW * s;

    // Ententeich: Oval mit der langen Achse entlang des Südwest-Wegs, nordwestlich davon. Südöstlich des Wegs (Kameraseite
    // von Level 3) ein kleiner Teich; zwei Bäche verbinden beide und kreuzen dabei den Weg (Fußgängerbrücken, die Level 3
    // ausschneidet: dort sind es Lücken). Alles als Abstand s entlang des Südwest-Wegs
    public const float PondAlongPath = 36f;
    public const float PondOffPath = 13f;
    public const float PondHalfLength = 12f;
    public const float PondHalfWidth = 7f;
    public static Vector2 PondCenter => ParkPathSW(PondAlongPath) + ParkNormalNW * PondOffPath;
    public static readonly float[] ParkStreamsAlongPath = { 27f, 37f };
    public const float ParkStreamWidth = 3f;
    // Hier stellt Level 3 etwas neben die Bahn (Abstand s vom Brunnen): keine Laternen, Bänke, Beete der Park-Deko.
    // Südost-Weg: das quer werfende Frisbee-Paar; Südwest-Weg: die zwei Bäche und das Picknick
    public static readonly float[] ParkDecorKeepClearSE = { 42.5f };
    public static readonly float[] ParkDecorKeepClearSW = { 27f, 37f, 50f };
    // Kleiner Teich: Mitte zwischen den Bächen, so weit südöstlich des Wegs (negativ entlang ParkNormalNW). Sein Steinrand
    // endet genau an der Wegkante: Die Kamera von Level 3 sieht nur ~3 m vor die Bahn, weiter weg läge er außerhalb des Bilds
    public const float SmallPondAlongPath = 32f;
    public const float SmallPondOffPath = 4.5f;
    public const float SmallPondHalfLength = 6.5f;
    public const float SmallPondHalfWidth = 2.5f;
    public static Vector2 SmallPondCenter => ParkPathSW(SmallPondAlongPath) - ParkNormalNW * SmallPondOffPath;
    // Halbe Kantenlänge des Ausschnitts, mit dem Level 3 eine Brücke entfernt. Alle Brückenteile liegen mit ihrer Mitte
    // innerhalb, Bachwasser und Ufer außerhalb
    public const float ParkBridgeCutoutHalf = 1.6f;

    // Sehenswürdigkeiten
    public static readonly Vector2 LeaningTower = new Vector2(-200f, 50f);
    public static readonly Vector2 FoundersCircle = new Vector2(-40f, 130f);
    public static readonly Vector2 KaijuFight = new Vector2(-40f, 270f);
}
