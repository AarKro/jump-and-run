using System.Collections.Generic;
using UnityEngine;

// Die Laufbahn des Levels als Pfad aus geraden Stücken und Kreisbögen (in der XZ-Ebene).
// Spieler, Kamera, Hunde und der Level-Aufbau richten sich danach statt nach der festen X-Achse.
// Lauf-Koordinate "u": Strecke entlang des Pfads, beginnend bei startU. Auf dem ersten geraden Stück
// entlang +X entspricht u genau der Welt-X-Koordinate, so passen alte Positionen weiter.
//
// Lane-Rahmen an einer Stelle u: lokal +X = Laufrichtung, +Y = oben, +Z = "hinten" (Hausseite), -Z = Kameraseite.
// Prefabs sind in diesem Rahmen gebaut (Fassaden bei lokal Z = 3), also einfach mit Frame(u) drehen.
// Läuft vor allen normalen Skripten (Awake/OnEnable), damit z.B. GroundPatrol schon in Awake auf der Bahn rechnet.
// Sonst ist die Reihenfolge zufällig, und wer vor uns dran ist, bekommt die Ersatz-X-Achse
[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class LanePath : MonoBehaviour
{
    public enum PieceType { Straight, Arc }

    [System.Serializable]
    public struct Piece
    {
        public PieceType type;
        [Tooltip("Länge entlang des Pfads in Metern")]
        public float length;
        [Tooltip("Nur für Bögen: Drehung in Grad. Positiv = links (von +X Richtung +Z, also in die Tiefe)")]
        public float turnDegrees;
    }

    [SerializeField] private Vector3 startPoint = new Vector3(-40f, 0f, 0f);
    [Tooltip("Richtung am Start in Grad: 0 = +X, 90 = +Z")]
    [SerializeField] private float startHeading;
    [SerializeField] private float startU = -40f;
    [SerializeField] private List<Piece> pieces = new List<Piece>();

    // Der Pfad der aktiven Szene. Statisch, damit alle Skripte ohne Referenz drankommen (siehe Lane)
    public static LanePath Instance { get; private set; }

    // Vorberechnete Abschnitte: Startpunkt, Startrichtung, Kreismittelpunkt
    private struct Segment
    {
        public PieceType type;
        public float startS;
        public float length;
        public Vector3 start;
        public float heading;     // Bogenmaß, 0 = +X
        public float turnSign;    // +1 links, -1 rechts
        public float radius;
        public Vector3 center;
    }

    private readonly List<Segment> segments = new List<Segment>();
    private float totalLength;

    public float StartU => startU;
    public float EndU => startU + totalLength;

    private void OnEnable()
    {
        Instance = this;
        Rebuild();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        Rebuild();
    }

    // Für den Level-Aufbau aus dem Editor: Pfad festlegen und sofort neu berechnen
    public void Configure(Vector3 start, float heading, float u0, List<Piece> newPieces)
    {
        startPoint = start;
        startHeading = heading;
        startU = u0;
        pieces = newPieces;
        Rebuild();
    }

    public void Rebuild()
    {
        segments.Clear();
        Vector3 position = new Vector3(startPoint.x, 0f, startPoint.z);
        float heading = startHeading * Mathf.Deg2Rad;
        float s = 0f;

        foreach (Piece piece in pieces)
        {
            if (piece.length <= 0f)
            {
                continue;
            }
            var segment = new Segment { type = piece.type, startS = s, length = piece.length, start = position, heading = heading };

            if (piece.type == PieceType.Arc && Mathf.Abs(piece.turnDegrees) > 0.01f)
            {
                float turn = piece.turnDegrees * Mathf.Deg2Rad;
                segment.turnSign = Mathf.Sign(turn);
                segment.radius = piece.length / Mathf.Abs(turn);
                // Mittelpunkt liegt um den Radius zur Kurven-Innenseite versetzt
                segment.center = position + segment.turnSign * segment.radius * LeftOf(heading);
                segments.Add(segment);
                position = ArcPoint(segment, piece.length);
                heading += turn;
            }
            else
            {
                segment.type = PieceType.Straight;
                segments.Add(segment);
                position += Direction(heading) * piece.length;
            }
            s += piece.length;
        }
        totalLength = s;
    }

    // ---------- Abfragen (u = Lauf-Koordinate) ----------

    public Vector3 PointAt(float u)
    {
        float s = u - startU;
        if (segments.Count == 0)
        {
            return startPoint + Vector3.right * s;
        }
        Segment segment = SegmentAt(s, out float local);
        return segment.type == PieceType.Arc ? ArcPoint(segment, local) : segment.start + Direction(segment.heading) * local;
    }

    public Vector3 TangentAt(float u)
    {
        float s = u - startU;
        if (segments.Count == 0)
        {
            return Vector3.right;
        }
        Segment segment = SegmentAt(s, out float local);
        float heading = segment.type == PieceType.Arc ? segment.heading + segment.turnSign * local / segment.radius : segment.heading;
        return Direction(heading);
    }

    // Nächstgelegene Stelle u auf dem Pfad (Höhe wird ignoriert)
    public float Project(Vector3 worldPosition)
    {
        Vector3 p = new Vector3(worldPosition.x, 0f, worldPosition.z);
        if (segments.Count == 0)
        {
            return startU + Vector3.Dot(p - startPoint, Vector3.right);
        }

        float bestS = 0f;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            bool first = i == 0;
            bool last = i == segments.Count - 1;
            float local;
            Vector3 closest;

            if (segment.type == PieceType.Arc)
            {
                // Winkel des Punkts um den Mittelpunkt, relativ zum Bogenanfang
                Vector3 fromCenter = p - segment.center;
                Vector3 startRadial = segment.start - segment.center;
                float angle = Vector3.SignedAngle(startRadial, fromCenter, Vector3.up) * Mathf.Deg2Rad;
                // SignedAngle um +Y ist im Uhrzeigersinn von oben positiv, ein Linksbogen (+Z) läuft andersherum
                angle *= -segment.turnSign;
                local = Mathf.Clamp(angle * segment.radius, 0f, segment.length);
                closest = ArcPoint(segment, local);
            }
            else
            {
                local = Vector3.Dot(p - segment.start, Direction(segment.heading));
                // Vor dem ersten und hinter dem letzten Stück geradeaus weiter, sonst begrenzen
                float min = first ? float.MinValue : 0f;
                float max = last ? float.MaxValue : segment.length;
                local = Mathf.Clamp(local, min, max);
                closest = segment.start + Direction(segment.heading) * local;
            }

            float distance = (p - closest).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestS = segment.startS + local;
            }
        }
        return startU + bestS;
    }

    // ---------- Hilfen ----------

    private Segment SegmentAt(float s, out float local)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            bool last = i == segments.Count - 1;
            if (s < segment.startS + segment.length || last)
            {
                local = s - segment.startS;
                if (i == 0 && local < 0f && segment.type == PieceType.Straight)
                {
                    return segment;
                }
                if (segment.type == PieceType.Arc)
                {
                    local = Mathf.Clamp(local, 0f, segment.length);
                }
                return segment;
            }
        }
        local = 0f;
        return segments[0];
    }

    private static Vector3 ArcPoint(Segment segment, float local)
    {
        float angle = segment.heading + segment.turnSign * local / segment.radius;
        // Vom Mittelpunkt aus zeigt der Radius zur Kurven-Außenseite, also entgegen "links" der aktuellen Richtung
        return segment.center - segment.turnSign * segment.radius * LeftOf(angle);
    }

    private static Vector3 Direction(float heading) => new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));

    // Links von der Laufrichtung (bei +X ist das +Z, also in die Tiefe)
    private static Vector3 LeftOf(float heading) => new Vector3(-Mathf.Sin(heading), 0f, Mathf.Cos(heading));

    private void OnDrawGizmos()
    {
        if (segments.Count == 0)
        {
            return;
        }
        Gizmos.color = new Color(0.2f, 0.9f, 1f);
        Vector3 previous = PointAt(startU) + Vector3.up * 0.55f;
        for (float u = startU; u <= EndU; u += 1f)
        {
            Vector3 point = PointAt(u) + Vector3.up * 0.55f;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}

// Zugriff auf die Laufbahn von überall. Ohne LanePath in der Szene (z.B. Level 2) ist die Bahn einfach die X-Achse
// bei Z = 0, also genau das alte Verhalten.
public static class Lane
{
    private static LanePath Path => LanePath.Instance;

    public static float Project(Vector3 position) => Path != null ? Path.Project(position) : position.x;

    public static Vector3 Point(float u) => Path != null ? Path.PointAt(u) : new Vector3(u, 0f, 0f);

    public static Vector3 Tangent(float u) => Path != null ? Path.TangentAt(u) : Vector3.right;

    // "Hinten" (Hausseite), bei Laufrichtung +X ist das +Z
    public static Vector3 Back(float u) => Vector3.Cross(Tangent(u), Vector3.up);

    // Lane-Rahmen: lokal +X = Laufrichtung, +Z = hinten. Für Spieler, Hunde, Kamera und alle Prefabs
    public static Quaternion Frame(float u) => Quaternion.LookRotation(Back(u), Vector3.up);
}
