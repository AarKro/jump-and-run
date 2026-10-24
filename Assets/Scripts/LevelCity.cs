using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Verbindet eine Level-Szene mit der gemeinsamen Stadt-Szene:
// 1. Ist die Stadt noch nicht geladen, wird sie additiv dazugeladen (LoadSceneMode.Additive: beide Szenen gleichzeitig).
// 2. Die CityCutouts dieses Levels schneiden die Stadt dort aus, wo das Level eigenen Boden/eigene Gebäude hat.
// 3. Die festen Gebäude (Pizzeria, Café, Kiosk) werden für dieses Level eingerichtet, die Start-Tür geht an den GameManager.
// Wird das Level entladen, kommt die Stadt wieder in den Originalzustand (sie bleibt für das nächste Level geladen).
// Läuft vor allem anderen im Level, damit der GameManager weiß, dass er auf die Stadt warten muss.
[DefaultExecutionOrder(-200)]
public class LevelCity : MonoBehaviour
{
    public static LevelCity Instance { get; private set; }

    [Tooltip("Gebäude der Stadt, aus dem der Spieler beim Levelstart läuft")]
    [SerializeField] private string startLandmark = "Pizzeria";
    [Tooltip("Gebäude, deren Spiel-Funktion dieses Level nutzt (Café = Checkpoint, Kiosk = Mikrowelle). Alle anderen sind nur Kulisse")]
    [SerializeField] private string[] usedLandmarks = { "Coffee Shop" };

    // Stadt geladen und eingerichtet: Erst dann startet der GameManager das Level
    public bool Ready { get; private set; }

    private readonly List<GameObject> hiddenObjects = new List<GameObject>();
    private readonly List<GameObject> cutPieces = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        if (!SceneManager.GetSceneByName(LevelFlow.CitySceneName).IsValid())
        {
            SceneManager.LoadSceneAsync(LevelFlow.CitySceneName, LoadSceneMode.Additive);
        }
    }

    private IEnumerator Start()
    {
        // Additives Laden dauert mindestens einen Frame. Danach noch einen Frame warten, damit alle Start() der Stadt gelaufen sind
        while (CityRoot.Instance == null)
        {
            yield return null;
        }
        yield return null;

        ApplyCutouts(CityRoot.Instance.gameObject.scene);
        foreach (CityLandmark landmark in CityLandmark.All)
        {
            if (landmark != null)
            {
                landmark.SetUsed(System.Array.IndexOf(usedLandmarks, landmark.Id) >= 0);
            }
        }
        CityLandmark start = CityLandmark.Find(startLandmark);
        if (start != null && GameManager.Instance != null)
        {
            GameManager.Instance.SetStartDoor(start.Door);
        }
        Ready = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        RestoreCity();
    }

    // ---------- Ausschnitte ----------

    private void ApplyCutouts(Scene city)
    {
        var cutouts = new List<CityCutout>();
        foreach (CityCutout cutout in FindObjectsByType<CityCutout>())
        {
            if (cutout.gameObject.scene == gameObject.scene)
            {
                cutouts.Add(cutout);
            }
        }
        if (cutouts.Count == 0)
        {
            return;
        }

        foreach (GameObject root in city.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                // Feste Gebäude bleiben immer stehen
                if (renderer.GetComponentInParent<CityLandmark>() != null)
                {
                    continue;
                }
                if (renderer.TryGetComponent(out CityGround _))
                {
                    Carve(renderer, cutouts);
                }
                else if (cutouts.Exists(cutout => cutout.Contains(renderer.bounds.center)))
                {
                    Hide(renderer.gameObject);
                }
            }
        }
    }

    // Boden-Platte minus alle Ausschnitte = bis zu vier Rechtecke pro Ausschnitt drumherum. Die Original-Platte wird
    // ausgeblendet und durch diese Teilstücke ersetzt (gleiches Material, gleiche Höhe, Collider falls vorhanden)
    private void Carve(Renderer ground, List<CityCutout> cutouts)
    {
        Transform t = ground.transform;
        Vector3 size = t.lossyScale;
        var original = new Rect(t.position.x - size.x / 2f, t.position.z - size.z / 2f, size.x, size.z);

        var pieces = new List<Rect> { original };
        bool touched = false;
        foreach (CityCutout cutout in cutouts)
        {
            Rect hole = cutout.Area;
            var next = new List<Rect>();
            foreach (Rect piece in pieces)
            {
                touched |= Subtract(piece, hole, next);
            }
            pieces = next;
        }
        if (!touched)
        {
            return;
        }

        Hide(ground.gameObject);
        bool hasCollider = ground.TryGetComponent(out Collider _);
        foreach (Rect piece in pieces)
        {
            GameObject cut = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cut.name = ground.name + " (Cut)";
            // SetParent auf ein Objekt der Stadt verschiebt das Stück auch in die Stadt-Szene
            cut.transform.SetParent(t.parent, false);
            cut.transform.position = new Vector3(piece.center.x, t.position.y, piece.center.y);
            cut.transform.localScale = new Vector3(piece.width, size.y, piece.height);
            cut.layer = ground.gameObject.layer;
            cut.GetComponent<Renderer>().sharedMaterial = ground.sharedMaterial;
            if (!hasCollider)
            {
                Destroy(cut.GetComponent<Collider>());
            }
            cutPieces.Add(cut);
        }
    }

    // Rechteck minus Loch. Gibt true zurück, wenn sich beide überschneiden (dann landen nur die Reste in output)
    private static bool Subtract(Rect area, Rect hole, List<Rect> output)
    {
        if (!area.Overlaps(hole))
        {
            output.Add(area);
            return false;
        }
        float x0 = Mathf.Max(hole.xMin, area.xMin), x1 = Mathf.Min(hole.xMax, area.xMax);
        float z0 = Mathf.Max(hole.yMin, area.yMin), z1 = Mathf.Min(hole.yMax, area.yMax);
        AddIfVisible(output, Rect.MinMaxRect(area.xMin, area.yMin, x0, area.yMax));   // links
        AddIfVisible(output, Rect.MinMaxRect(x1, area.yMin, area.xMax, area.yMax));   // rechts
        AddIfVisible(output, Rect.MinMaxRect(x0, area.yMin, x1, z0));                 // vorne (Süden)
        AddIfVisible(output, Rect.MinMaxRect(x0, z1, x1, area.yMax));                 // hinten (Norden)
        return true;
    }

    private static void AddIfVisible(List<Rect> output, Rect piece)
    {
        if (piece.width > 0.01f && piece.height > 0.01f)
        {
            output.Add(piece);
        }
    }

    private void Hide(GameObject target)
    {
        if (target.activeSelf)
        {
            target.SetActive(false);
            hiddenObjects.Add(target);
        }
    }

    private void RestoreCity()
    {
        foreach (GameObject piece in cutPieces)
        {
            if (piece != null)
            {
                Destroy(piece);
            }
        }
        cutPieces.Clear();
        foreach (GameObject hidden in hiddenObjects)
        {
            if (hidden != null)
            {
                hidden.SetActive(true);
            }
        }
        hiddenObjects.Clear();
    }
}
